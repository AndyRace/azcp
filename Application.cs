using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.DataMovement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AzCp
{
  class Application : IHostedService
  {
    private readonly IConfiguration _configuration;
    private readonly Repository _repo;

    public string SourcePath => _repo.SourcePath;

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

      TransferManager.Configurations.ParallelOperations = _repo.ParallelOperations;

      await TransferLocalFileToAzureBlob(account, cancellationToken);
    }

    public async Task TransferLocalFileToAzureBlob(CloudStorageAccount account, CancellationToken cancellationToken)
    {
      string localFilePath = SourcePath;
      var blob = await GetBlob(account);
      var context = GetSingleTransferContext(null);

      //Console.CancelKeyPress += delegate {
      //  DoQuit = true;
      //  cancellationSource.Cancel();
      //};

      // Display the config info
      var started = DateTime.Now;
      WriteLine($"Started:     {started}");
      WriteLine($"Source");
      WriteLine($"  Path:      {localFilePath}");
      WriteLine($"Destination");
      WriteLine($"  Container: {blob.Container.Name}");
      WriteLine($"  Name:      {blob.Name}");
      WriteLine();
      WriteLine($"Configuration");
      WriteLine($"  Parallel Operations:");
      WriteLine($"             {_repo.ParallelOperations}");
      WriteLine();
      WriteLine("Press 'c' to temporarily cancel your transfer...");
      WriteLine();

      Stopwatch stopWatch = Stopwatch.StartNew();

      var internalTokenSource = new CancellationTokenSource();
      using (CancellationTokenSource linkedCts =
             CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, internalTokenSource.Token))
      {
        try
        {
          var task = TransferManager.UploadAsync(localFilePath, blob, null, context, linkedCts.Token);
          while (!task.IsCompleted)
          {
            // back off
            Thread.Sleep(500);

            if (Console.KeyAvailable)
            {
              var keyinfo = Console.ReadKey(true);
              if (keyinfo.Key == ConsoleKey.C)
              {
                internalTokenSource.Cancel();
                break;
              }
            }
          }
          await task;
        }
        catch (TaskCanceledException e)
        {
          WriteLine("The transfer is cancelled: {0}", e.Message);
        }
      }

      if (internalTokenSource.IsCancellationRequested)
      {
        var json = SimpleJsonSerializer<TransferCheckpoint>.WriteFromObject(context.LastCheckpoint);

        WriteLine("Transfer will resume in 3 seconds...");
        Thread.Sleep(3000);

        // Mimic persisting and resuming
        TransferCheckpoint checkpointResume = SimpleJsonSerializer<TransferCheckpoint>.ReadToObject(json);
        var contextResume = GetSingleTransferContext(checkpointResume);

        WriteLine("Resuming transfer...");
        await TransferManager.UploadAsync(localFilePath, blob, null, contextResume, cancellationToken);
      }

      stopWatch.Stop();
      WriteLine($"Transfer operation completed in {stopWatch.Elapsed}");
    }

    static readonly object _consoleLockObj = new object();

    private static void WriteLine(string format = "", object arg0 = null)
    {
      lock (_consoleLockObj)
      {
        var bg = Console.BackgroundColor;
        var fg = Console.ForegroundColor;
        try
        {
          // clear any previous 'progress' line
          WriteProgress();

          Console.BackgroundColor = ConsoleColor.Black;
          Console.ForegroundColor = ConsoleColor.Green;
          Console.WriteLine(format, arg0 ?? "{0}");
        }
        finally
        {
          Console.BackgroundColor = bg;
          Console.ForegroundColor = fg;
        }
      }
    }

    static int _prevProgressLen = 0;
    private static void WriteProgress(string format = "", object arg0 = null)
    {
      lock (_consoleLockObj)
      {
        if (string.IsNullOrEmpty(format))
        {
          Console.Write($"{new String(' ', _prevProgressLen)}\r");
          _prevProgressLen = 0;
        }
        else
        {
          var bg = Console.BackgroundColor;
          var fg = Console.ForegroundColor;
          try
          {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Yellow;

            var msg = $"{DateTime.Now}: {string.Format(format, arg0)}";
            Console.Write($"{msg.PadRight(_prevProgressLen, ' ')}\r");
            _prevProgressLen = msg.Length;
          }
          finally
          {
            Console.BackgroundColor = bg;
            Console.ForegroundColor = fg;
          }
        }
      }
    }

    public static SingleTransferContext GetSingleTransferContext(TransferCheckpoint checkpoint)
    {
      return new SingleTransferContext(checkpoint)
      {
        ProgressHandler = new Progress<TransferStatus>((progress) =>
        {
          WriteProgress($"Bytes transferred: {NumberFormatter.SizeSuffix(progress.BytesTransferred, 0)} ({progress.BytesTransferred:N0})");
        })
      };
    }

    public async Task<CloudBlockBlob> GetBlob(CloudStorageAccount account)
    {
      CloudBlobClient blobClient = account.CreateCloudBlobClient();

      CloudBlobContainer container = blobClient.GetContainerReference(_repo.ContainerName);

      WriteProgress("Creating BLOB Container (if it doesn't exist already)");
      await container.CreateIfNotExistsAsync();

      CloudBlockBlob blob = container.GetBlockBlobReference(_repo.BlobName);
      WriteLine("Created BLOB Container");

      return blob;
    }
  }
}
