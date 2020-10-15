using AzCp.Interfaces;
using Microsoft.Azure.Storage.DataMovement;
using System;
using System.IO;
using Serilog;
using System.Diagnostics;

namespace AzCp
{
  public class MyDirectoryTransferContext : DirectoryTransferContext
  {
    public long? InitialBytesTransferred = null;

    public MyDirectoryTransferContext(ILogger logger, IFeedback feedback,
      string uploadFolder, string archiveFolder, string transferCheckpointFilename,
      Stopwatch stopwatch,
      TransferCheckpoint checkpoint,
      Func<TransferEventArgs, string> getSourceToDestinationInfo) : base(checkpoint)
    {
      if (checkpoint == null)
      {
        // if there's no previous checkpoint then set the initial tx to 0 instead of null (resuming)
        InitialBytesTransferred = 0;
      }

      void ArchiveFile(TransferEventArgs e)
      {
        var relPath = Path.GetRelativePath(uploadFolder, (string)e.Source);
        var archivePath = Path.Combine(archiveFolder, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath));
        File.Move((string)e.Source, archivePath, true);
      }

      FileFailed += (sender, e) =>
      {
        lock (feedback)
        {
          feedback.WriteProgress();
          logger.Warning($"FAILED: {getSourceToDestinationInfo(e)}", null, IFeedback.Colors.ErrorForegroundColor);
          e.Exception?.LogIt();
        }
      };
      FileSkipped += (sender, e) =>
      {
        lock (feedback)
        {
          feedback.WriteProgress();
          logger.Information($"Skipped: {getSourceToDestinationInfo(e)}");
          e.Exception?.LogIt();
        }
        ArchiveFile(e);
      };
      FileTransferred += (sender, e) =>
      {
        lock (feedback)
        {
          feedback.WriteProgress();
          logger.Information($"Transferred: {getSourceToDestinationInfo(e)}");
          e.Exception?.LogIt();
        }
        ArchiveFile(e);
      };

      // todo: result.ShouldTransferCallbackAsync

      ProgressHandler = new Progress<TransferStatus>((progress) =>
      {
        if (!InitialBytesTransferred.HasValue)
        {
          InitialBytesTransferred = progress.BytesTransferred;
        }

        lock (feedback)
        {
          feedback.WriteProgress(progress.ToUserString((long)InitialBytesTransferred, stopwatch), progress.NumberOfFilesFailed == 0 ? IFeedback.Colors.OkForegroundColor : IFeedback.Colors.WarningForegroundColor);
        }

        AzCpCheckpoint.Write(LastCheckpoint, transferCheckpointFilename);
      });
    }
  }
}