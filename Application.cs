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

namespace AzCp
{
  class Application : IHostedService
  {
    private readonly IConfiguration _configuration;
    private readonly Repository _repo;

    public Application(IConfiguration configuration)
    {
      _configuration = configuration;
      _repo = _configuration.GetSection("Repository").Get<Repository>();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
      // CloudStorageAccount account = CloudStorageAccount.Parse(storageConnectionString);
      var connectionString = _configuration.GetConnectionString("StorageConnectionString");
      CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);

      if (_repo.ParallelOperations.HasValue)
      {
        TransferManager.Configurations.ParallelOperations = (int)_repo.ParallelOperations;
      }

      if (_repo.BlockSize.HasValue)
      {
        TransferManager.Configurations.BlockSize = (int)_repo.BlockSize;
      }

      await TransferLocalDirectoryToAzureBlob(account, cancellationToken);
    }

    public async Task TransferLocalDirectoryToAzureBlob(CloudStorageAccount account, CancellationToken cancellationToken)
    {
      var blobDirectory = GetBlobDirectory(account, _repo.ContainerName);

      // Display the config info
      var entryAssembly = Assembly.GetEntryAssembly();
      var informationalVersion = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
      var assemblyFileVersion = entryAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
      var product = entryAssembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
      var description = entryAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;

      WriteLine($@"{product} {informationalVersion} ({assemblyFileVersion})
{description}

Upload Folder:         {_repo.UploadFolder}
Destination container: {_repo.ContainerName}
  
Transfer Configuration
  Block Size:          {NumberFormatter.SizeSuffix(TransferManager.Configurations.BlockSize)}
  Parallel Operations: {TransferManager.Configurations.ParallelOperations}
  Recursive:           {_repo.Recursive}
");
      //WriteLine("Press 'c' to temporarily cancel your transfer...");
      //WriteLine();

      UploadDirectoryOptions options = new UploadDirectoryOptions()
      {
        Recursive = _repo.Recursive
        //SearchPattern = 
      };

      //var context = GetDirectoryTransferContext(null);

      var internalTokenSource = new CancellationTokenSource();
      using CancellationTokenSource linkedCts =
             CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, internalTokenSource.Token);
      try
      {
        var transferCheckpoint = AzCpCheckpoint.Read(_repo.TransferCheckpointFilename).TransferCheckpoint;
        if (transferCheckpoint != null)
        {
          WriteLine("Resuming upload...");
        }

        while (true)
        {
          var context = GetDirectoryTransferContext(transferCheckpoint);
          transferCheckpoint = null;

          var uploadFolderLastWriteTime = new DirectoryInfo(_repo.UploadFolder).LastWriteTimeUtc;

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

          WriteProgress("Establishing connection...");

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

          WriteLine($"{stopWatch.Elapsed}: {ToUserString(transferStatus)}");

          // wait until there are new files to upload
          // NOTE: Will also be triggered if files are renamed or deleted
          while (new DirectoryInfo(_repo.UploadFolder).LastWriteTimeUtc == uploadFolderLastWriteTime)
          {
            WriteProgress("Waiting for new files to upload...");
            if (linkedCts.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(2)))
            {
              linkedCts.Token.ThrowIfCancellationRequested();
            }
          }
        }
      }
      catch (TaskCanceledException e)
      {
        WriteLine("The transfer was cancelled: {0}", e.Message);
      }
    }

    static readonly object _consoleLockObj = new object();

    static int _prevProgressLen = 0;
    private static void WriteProgress(string format = "", object arg0 = null)
    {
      Write(format, arg0, ConsoleColor.Yellow, true);
    }
    private static void WriteLine(string format = "", object arg0 = null)
    {
      Write(format, arg0, ConsoleColor.Green, false);
    }

    private static void Write(string format, object arg0, ConsoleColor fgColor, bool progressLine)
    {
      lock (_consoleLockObj)
      {
        static void ClearProgressLine()
        {
          if (_prevProgressLen != 0)
          {
            Console.Write($"{new string(' ', _prevProgressLen)}\r");
            _prevProgressLen = 0;
          }
        }

        var msg = string.Format(format, arg0);
        if (string.IsNullOrEmpty(msg))
        {
          ClearProgressLine();
          if (!progressLine)
          {
            Console.WriteLine();
          }
        }
        else
        {
          msg = $"{DateTime.Now}: {msg}";

          var bg = Console.BackgroundColor;
          var fg = Console.ForegroundColor;
          try
          {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = fgColor;

            // clear any previous 'progress' line
            if (progressLine)
            {
              Console.Write($"{msg.PadRight(_prevProgressLen, ' ')}\r");
              _prevProgressLen = msg.Length;
            }
            else
            {
              ClearProgressLine();
              Console.WriteLine(msg);
            }
          }
          finally
          {
            Console.BackgroundColor = bg;
            Console.ForegroundColor = fg;
          }
        }
      }
    }

    public DirectoryTransferContext GetDirectoryTransferContext(TransferCheckpoint checkpoint)
    {
      DirectoryTransferContext result;
      result = new DirectoryTransferContext(checkpoint);

      result.ProgressHandler = new Progress<TransferStatus>((progress) =>
        {
          WriteProgress(ToUserString(progress));

          AzCpCheckpoint.Write(_repo.TransferCheckpointFilename, result.LastCheckpoint);
        });

      return result;
    }

    private string ToUserString(TransferStatus progress)
    {
      return $"{progress.NumberOfFilesTransferred} transferred, {progress.NumberOfFilesSkipped} skipped, {progress.NumberOfFilesFailed} failed, {NumberFormatter.SizeSuffix(progress.BytesTransferred, 0)} ({progress.BytesTransferred:N0})";
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
