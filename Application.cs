using AzCp.Interfaces;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.File;
using Microsoft.Azure.Storage.DataMovement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace AzCp
{
  class Application : IHostedService
  {
    private readonly IConfiguration _configuration;
    private readonly IFeedback _feedback;
    private readonly ILogger _logger;
    private readonly Repository _repo;

    public Application(IConfiguration configuration, IFeedback feedback, ILogger logger)
    {
      const string RepositorySection = "Repository";

      _configuration = configuration;
      _feedback = feedback;
      _logger = logger;

      _repo = _configuration.GetSection(RepositorySection).Get<Repository>();

      if (_repo == null)
      {
        throw new Exception($@"Unable to find the '{RepositorySection}' section in the application settings files '{Repository.AppSettingsJsonFilename}' and '{Repository.AppSettingsSecretsJsonFilename}'
Please check that the JSON settings files exists and contains the relevant '{RepositorySection}' section");
      }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
      _logger.Information(_repo.ToFeedbackString());

      _repo.UpdateEnvironmentFromSettings();

      if (_repo.BlobDirectory != null)
      {
        await TransferLocalDirectoryToAzure(_repo.BlobDirectory, cancellationToken);
      }
      else if (_repo.FileDirectory != null)
      {
        await TransferLocalDirectoryToAzure(_repo.FileDirectory, cancellationToken);
      }
      else
      {
        throw new Exception("Please specify the Azure BLOB or Azure File URI in the configuration settings");
      }
    }

    private async Task TransferLocalDirectoryToAzure(CloudFileDirectory fileDirectory, CancellationToken cancellationToken)
    {
      await TransferLocalDirectoryToAzureStorage(
        async (uploadFolder, options, context, linkedCancellationToken) =>
        {
          return await TransferManager.UploadDirectoryAsync(uploadFolder, fileDirectory, options, context, linkedCancellationToken);
        },
        (e) => { 
          return $"'{e.Source}' => '{((CloudFile)e.Destination).Name}'"; },
        cancellationToken);
    }


    private async Task TransferLocalDirectoryToAzure(CloudBlobDirectory blobDirectory, CancellationToken cancellationToken)
    {
      await TransferLocalDirectoryToAzureStorage(
        async (uploadFolder, options, context, linkedCancellationToken) =>
        {
          return await TransferManager.UploadDirectoryAsync(uploadFolder, blobDirectory, options, context, linkedCancellationToken);
        },
        (e) => { 
          return $"'{e.Source}' => '{((CloudBlobContainer)e.Destination).Name}'"; },
        cancellationToken);
    }

    public async Task TransferLocalDirectoryToAzureStorage(Func<string, UploadDirectoryOptions, DirectoryTransferContext, CancellationToken, Task<TransferStatus>> UploadDirectoryAsync,
      Func<TransferEventArgs, string> getSourceToDestinationInfo,

      CancellationToken cancellationToken)
    {
      UploadDirectoryOptions options = new UploadDirectoryOptions()
      {
        Recursive = _repo.Recursive,
        BlobType = BlobType.BlockBlob
        //SearchPattern = 
      };

      var internalTokenSource = new CancellationTokenSource();
      using var linkedCts =
             CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, internalTokenSource.Token);

      using var watcher = new UploadFileSystemWatcher(_repo.UploadFolder);

      try
      {
        var transferCheckpoint = AzCpCheckpoint.Read(_repo.TransferCheckpointFilename);
        if (transferCheckpoint != null)
        {
          _logger.Information("Resuming upload...");
        }

        while (true)
        {
          Stopwatch stopWatch = new Stopwatch();// Stopwatch.StartNew();

          var context = new MyDirectoryTransferContext(_logger, _feedback, _repo.UploadFolder, _repo.ArchiveFolder, _repo.TransferCheckpointFilename, stopWatch, transferCheckpoint, getSourceToDestinationInfo);

          transferCheckpoint = null;

          _feedback.WriteProgress("Establishing connection...");

          watcher.ResetChangeCount();

          stopWatch.Start();
          // var transferStatus = await TransferManager.UploadDirectoryAsync(_repo.UploadFolder, blobDirectory, options, context, linkedCts.Token);
          var transferStatus = await UploadDirectoryAsync(_repo.UploadFolder, options, context, linkedCts.Token);
          stopWatch.Stop();

          linkedCts.Token.ThrowIfCancellationRequested();

          try
          {
            if (File.Exists(_repo.TransferCheckpointFilename))
            {
              File.Delete(_repo.TransferCheckpointFilename);
            }
          }
#pragma warning disable CA1031 // Do not catch general exception types
          catch (IOException)
          {
            // ignore any issues deleting the checkpoint file
            // e.g. doesn't exist
          }
#pragma warning restore CA1031 // Do not catch general exception types

          _logger.Information($"{stopWatch.Elapsed}: {transferStatus.ToUserString(context.InitialBytesTransferred, stopWatch)}");

          // wait until there are new files to upload
          // NOTE: Will also be triggered if files are renamed or deleted
          while (!watcher.AnyChangesInUploadFolder)
          {
            _feedback.WriteProgress("Waiting for new files to upload...");

            if (linkedCts.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(1500 - DateTime.UtcNow.Millisecond)))
            {
              linkedCts.Token.ThrowIfCancellationRequested();
            }
          }
        }
      }
      catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
      {
        _logger.Information($"The transfer was cancelled");
        throw;
      }
      catch (Exception)
      {
        //_logger.Error(ex, $"UNEXPECTED ERROR: {ex.Message}");
        throw;
      }
    }
  }
}
