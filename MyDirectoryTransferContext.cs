using AzCp.Interfaces;
using Microsoft.Azure.Storage.Blob;
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
      TransferCheckpoint checkpoint) : base(checkpoint)
    {
      if (checkpoint == null)
      {
        // if there's no previous checkpoint then set the initial tx to 0 instead of null (resuming)
        InitialBytesTransferred = 0;
      }

      static string ToSourceDestination(TransferEventArgs e)
      {
        //var result = $"'{e.Source}' => '{((CloudBlockBlob)e.Destination).Name}'";
        var result = $"'{e.Source}'";
        if (e.Exception != null)
        {
          result += $" ({e.Exception.Message})";
        }
        return result;
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
          logger.Information($"FAILED: {ToSourceDestination(e)}", null, IFeedback.Colors.ErrorForegroundColor);
        }
      };
      FileSkipped += (sender, e) =>
      {
        lock (feedback)
        {
          feedback.WriteProgress();
          logger.Information($"Skipped: {ToSourceDestination(e)}");
        }
        ArchiveFile(e);
      };
      FileTransferred += (sender, e) =>
      {
        lock (feedback)
        {
          feedback.WriteProgress();
          logger.Information($"Transferred: {ToSourceDestination(e)}");
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

        AzCpCheckpoint.Write(transferCheckpointFilename, LastCheckpoint);
      });
    }
  }
}