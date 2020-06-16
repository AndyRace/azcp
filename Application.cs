using AzCp.Interfaces;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.DataMovement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using System.Net;
using System.Collections.Generic;

namespace AzCp
{
  class Application : IHostedService
  {
    private readonly IConfiguration _configuration;
    private readonly IFeedback _feedback;
    private readonly ILogger _logger;
    private readonly Repository _repo;
    private long _changesInUploadFolder;

    public Application(IConfiguration configuration, IFeedback feedback, ILogger logger)
    {
      _configuration = configuration;
      _feedback = feedback;
      _logger = logger;
      _repo = _configuration.GetSection("Repository").Get<Repository>();

      if (_repo == null)
      {
        throw new Exception("Unable to find the 'Repository' section in the application settings!");
      }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
      if (_repo.ParallelOperations.HasValue)
      {
        TransferManager.Configurations.ParallelOperations = (int)_repo.ParallelOperations;
      }

      if (_repo.BlockSize.HasValue)
      {
        TransferManager.Configurations.BlockSize = (int)_repo.BlockSize;
      }

      if (_repo.DefaultConnectionLimit.HasValue)
      {
        ServicePointManager.DefaultConnectionLimit = (int)_repo.DefaultConnectionLimit;
      }
      else
      {
        ServicePointManager.DefaultConnectionLimit = Environment.ProcessorCount * 8;
      }

      if (_repo.Expect100Continue.HasValue)
      {
        ServicePointManager.Expect100Continue = (bool)_repo.Expect100Continue;
      }

      // Display the config info
      var entryAssembly = Assembly.GetEntryAssembly();
      var informationalVersion = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
      var assemblyFileVersion = entryAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
      var product = entryAssembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
      var description = entryAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;

      {
        var repoDefaults = new Repository();

        var info = $@"{product} {informationalVersion} ({assemblyFileVersion})
{description}
";
        var colInfo = new int[] { 0, 30, 65, 75 };
        var table = new List<string[]>()
         {
          new string[] { "Configuration entry",                     "Description",                    "Value",                        "Default" },
          new string[] { "===================",                     "===========",                    "=====",                        "=======" },
          Array.Empty<string>(),
          new string[] { nameof(_repo.UploadFolder),                "Upload Folder",                  _repo.UploadFolder },//,             repoDefaults.UploadFolder },
          new string[] { nameof(_repo.ArchiveFolder),               "Archive Folder",                 _repo.ArchiveFolder },//,            repoDefaults.ArchiveFolder },
          new string[] { nameof(_repo.ContainerName),               "Destination container",          _repo.ContainerName,            repoDefaults.ContainerName },
          new string[] { nameof(_repo.TransferCheckpointFilename),  "Transfer Checkpoint Filename",   _repo.TransferCheckpointFilename, repoDefaults.TransferCheckpointFilename },
          new string[] { nameof(_repo.BlockSize),                   "Tx Block Size",                  TransferManager.Configurations.BlockSize.ToSizeSuffix(), repoDefaults.BlockSize.ToString() },
          new string[] { nameof(_repo.ParallelOperations),          "Parallel Operations",            TransferManager.Configurations.ParallelOperations.ToString(), repoDefaults.ParallelOperations.ToString() },
          new string[] { nameof(_repo.DefaultConnectionLimit),      "Default Connection Limit",       ServicePointManager.DefaultConnectionLimit.ToString(), repoDefaults.DefaultConnectionLimit.ToString() },
          new string[] { nameof(_repo.Expect100Continue),           "Expect 100 Continue",            ServicePointManager.Expect100Continue.ToString(), repoDefaults.Expect100Continue.ToString() },
          new string[] { nameof(_repo.Recursive),                   "Recursive the upload folder",    _repo.Recursive.ToString(),     repoDefaults.Recursive.ToString() },
          Array.Empty<string>(),
          new string[] { "For details of the configuration options see: https://libraries.io/github/Azure/azure=storage-net-data-movement" },
          };

        table.ForEach(row =>
        {
          var line = "";
          for (int i = 0; i < row.Length; i++)
          {
            line = $"{line.PadRight(colInfo[i])}{row[i]} ";
          }
          info = info.TrimEnd(' ') + $"\n{line}";
        });

        _logger.Information(info);
      }

      if (_repo.UploadFolder == null)
      {
        throw new Exception("Please specify the upload folder in the application settings file!");
      }

      if (!Directory.Exists(_repo.UploadFolder))
      {
        throw new Exception("Missing upload folder.  Please create it and try again!");
      }

      if (!Directory.Exists(_repo.ArchiveFolder))
      {
        Directory.CreateDirectory(_repo.ArchiveFolder);
      }

      if (!Directory.Exists(Path.GetDirectoryName(_repo.TransferCheckpointFilename)))
      {
        Directory.CreateDirectory(Path.GetDirectoryName(_repo.TransferCheckpointFilename));
      }

      if (_repo.ContainerName == null)
      {
        throw new Exception("Please specify the container name in the application settings file!");
      }

      var connectionString = _configuration.GetConnectionString("StorageConnectionString");
      if (connectionString == null)
      {
        throw new Exception("Unable to find 'StorageConnectionString' details!");
      }

      CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);

      await TransferLocalDirectoryToAzureBlob(account, cancellationToken);
    }


    public async Task TransferLocalDirectoryToAzureBlob(CloudStorageAccount account, CancellationToken cancellationToken)
    {
      var blobDirectory = GetBlobDirectory(account, _repo.ContainerName);

      UploadDirectoryOptions options = new UploadDirectoryOptions()
      {
        Recursive = _repo.Recursive
        //SearchPattern = 
      };

      var internalTokenSource = new CancellationTokenSource();
      using var linkedCts =
             CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, internalTokenSource.Token);

      using var watcher = CreateFileSystemWatcher();

      try
      {
        var transferCheckpoint = AzCpCheckpoint.Read(_repo.TransferCheckpointFilename).TransferCheckpoint;
        if (transferCheckpoint != null)
        {
          _logger.Information("Resuming upload...");
        }

        while (true)
        {
          var context = GetDirectoryTransferContext(transferCheckpoint);
          transferCheckpoint = null;

          //var task = TransferManager.UploadDirectoryAsync(_repo.UploadFolder, blobDirectory, options, context, linkedCts.Token);
          //while (!task.IsCompleted)
          //{
          //  // back off
          //  Thread.Sleep(500);

          //  if (Console.KeyAvailable)
          //  {
          //    var keyinfo = Console.ReadKey(true);
          //    if (keyinfo.Key == ConsoleKey.C)
          //    {
          //      internalTokenSource.Cancel();
          //      break;
          //    }
          //  }
          //}

          _feedback.WriteProgress("Establishing connection...");

          Interlocked.Exchange(ref _changesInUploadFolder, 0);

          Stopwatch stopWatch = Stopwatch.StartNew();
          var transferStatus = await TransferManager.UploadDirectoryAsync(_repo.UploadFolder, blobDirectory, options, context, linkedCts.Token);
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

          _logger.Information($"{stopWatch.Elapsed}: {ToUserString(transferStatus)}");

          // wait until there are new files to upload
          // NOTE: Will also be triggered if files are renamed or deleted
          while (Interlocked.Read(ref _changesInUploadFolder) == 0)
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
#pragma warning disable CA1031 // Do not catch general exception types
      catch (Exception ex)
      {
        _logger.Error(ex, $"UNEXPECTED ERROR: {ex.Message}");
        throw;
      }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private FileSystemWatcher CreateFileSystemWatcher()
    {
      var watcher = new FileSystemWatcher
      {
        Path = _repo.UploadFolder,

        // Watch for changes in LastWrite times, and the renaming of files or directories.
        NotifyFilter = // NotifyFilters.LastAccess
                           NotifyFilters.LastWrite
                           | NotifyFilters.FileName
                           | NotifyFilters.DirectoryName,

        IncludeSubdirectories = true
      };

      //watcher.Filter = "*.txt";

      // Add event handlers.
      watcher.Changed += OnChanged;
      watcher.Created += OnChanged;
      //watcher.Deleted += OnChanged;
      watcher.Renamed += OnRenamed;

      // Begin watching.
      watcher.EnableRaisingEvents = true;

      return watcher;
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
      Interlocked.Increment(ref _changesInUploadFolder);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
      Interlocked.Increment(ref _changesInUploadFolder);
    }

    public DirectoryTransferContext GetDirectoryTransferContext(TransferCheckpoint checkpoint)
    {
      DirectoryTransferContext result;
      result = new DirectoryTransferContext(checkpoint);

      static string ToSourceDestination(TransferEventArgs e)
      {
        var result = $"'{e.Source}' => '{((CloudBlockBlob)e.Destination).Name}'";
        if (e.Exception != null)
        {
          result += $" ({e.Exception.Message})";
        }
        return result;
      }

      void ArchiveFile(TransferEventArgs e)
      {
        var relPath = Path.GetRelativePath(_repo.UploadFolder, (string)e.Source);
        var archivePath = Path.Combine(_repo.ArchiveFolder, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath));
        File.Move((string)e.Source, archivePath, true);
      }

      result.FileFailed += (sender, e) =>
      {
        _logger.Information($"FAILED: {ToSourceDestination(e)}", null, IFeedback.Colors.ErrorForegroundColor);
      };
      result.FileSkipped += (sender, e) =>
      {
        _logger.Information($"Skipped: {ToSourceDestination(e)}");
        ArchiveFile(e);
      };
      result.FileTransferred += (sender, e) =>
      {
        _logger.Information($"Transferred: {ToSourceDestination(e)}");
        ArchiveFile(e);
      };

      // todo: result.ShouldTransferCallbackAsync

      result.ProgressHandler = new Progress<TransferStatus>((progress) =>
        {
          _feedback.WriteProgress(ToUserString(progress), progress.NumberOfFilesFailed == 0 ? IFeedback.Colors.OkForegroundColor : IFeedback.Colors.WarningForegroundColor);

          AzCpCheckpoint.Write(_repo.TransferCheckpointFilename, result.LastCheckpoint);
        });

      return result;
    }

    private string ToUserString(TransferStatus progress)
    {
      return $"{progress.NumberOfFilesTransferred} transferred, {progress.NumberOfFilesSkipped} skipped, {progress.NumberOfFilesFailed} failed, {NumberFormatterExtension.ToSizeSuffix(progress.BytesTransferred, 0)} ({progress.BytesTransferred:N0})";
    }

    public CloudBlobDirectory GetBlobDirectory(CloudStorageAccount account, string containerName)
    {
      var container = account.CreateCloudBlobClient().GetContainerReference(containerName);

      //WriteProgress("Creating BLOB Container (if it doesn't exist already)");
      //await container.CreateIfNotExistsAsync();

      var blob = container.GetDirectoryReference("");

      //WriteLine("Got BLOB Container reference");

      return blob;
    }
  }
}
