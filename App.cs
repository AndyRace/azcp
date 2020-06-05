// see: https://docs.microsoft.com/en-us/azure/storage/common/storage-use-data-movement-library
// https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.storage.datamovement?view=azure-dotnet
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.DataMovement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AzCp
{
  public class App
  {
    private readonly IConfigurationRoot _config;
    private readonly ILogger<App> _logger;
    private Repository _repo;
    public string SourcePath => _repo.SourcePath;

    public App(IConfigurationRoot config, ILoggerFactory loggerFactory)
    {
      _logger = loggerFactory.CreateLogger<App>();
      _config = config;
    }

    public async Task Run()
    {
      // List<string> emailAddresses = _config.GetSection("EmailAddresses").Get<List<string>>();
      // foreach (string emailAddress in emailAddresses)
      // {
      //     _logger.LogInformation("Email address: {@EmailAddress}", emailAddress);
      // }

      _repo = _config.GetSection("Repository").Get<Repository>();

      // string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=" + accountName + ";AccountKey=" + accountKey;
      // CloudStorageAccount account = CloudStorageAccount.Parse(storageConnectionString);

      CloudStorageAccount account = CloudStorageAccount.Parse(_config.GetConnectionString("StorageConnectionString"));

      //await ExecuteChoice(account);
      TransferManager.Configurations.ParallelOperations = _repo.ParallelOperations;

      await TransferLocalFileToAzureBlob(account);
    }

    public async Task TransferLocalFileToAzureBlob1(CloudStorageAccount account)
    {
      string localFilePath = SourcePath;
      CloudBlockBlob blob = await GetBlob(account);
      TransferCheckpoint checkpoint = null;
      SingleTransferContext context = GetSingleTransferContext(checkpoint);
      WriteLine("\nTransfer started...\n");
      Stopwatch stopWatch = Stopwatch.StartNew();
      await TransferManager.UploadAsync(localFilePath, blob, null, context);
      stopWatch.Stop();
      WriteLine("\nTransfer operation completed in " + stopWatch.Elapsed.TotalSeconds + " seconds.");
    }

    public async Task TransferLocalFileToAzureBlob(CloudStorageAccount account)
    {
      string localFilePath = SourcePath;
      var blob = await GetBlob(account);
      var context = GetSingleTransferContext(null);
      var cancellationSource = new CancellationTokenSource();

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
      Task task;
      ConsoleKeyInfo keyinfo;
      try
      {
        task = TransferManager.UploadAsync(localFilePath, blob, null, context, cancellationSource.Token);
        while (!task.IsCompleted)
        {
          // back off
          Thread.Sleep(2000);

          if (Console.KeyAvailable)
          {
            keyinfo = Console.ReadKey(true);
            if (keyinfo.Key == ConsoleKey.C)
            {
              cancellationSource.Cancel();
              break;
            }
          }
        }
        await task;
      }
      catch (Exception e)
      {
        WriteLine();
        WriteLine("The transfer is cancelled: {0}", e.Message);
      }

      if (cancellationSource.IsCancellationRequested)
      {
        WriteLine();
        WriteLine("Transfer will resume in 3 seconds...");
        Thread.Sleep(3000);

        // Mimic persisting and resuming
        var json = SimpleJsonSerializer<TransferCheckpoint>.WriteFromObject(context.LastCheckpoint);
        TransferCheckpoint checkpointResume = SimpleJsonSerializer<TransferCheckpoint>.ReadToObject(json);
        var contextResume = GetSingleTransferContext(checkpointResume);

        WriteLine();
        WriteLine("Resuming transfer...");
        await TransferManager.UploadAsync(localFilePath, blob, null, contextResume);
      }

      stopWatch.Stop();
      WriteLine();
      WriteLine($"Transfer operation completed in {string.Format("HH:mm:ss", stopWatch.Elapsed)}");
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
          WriteProgress($"Bytes transferred: {NumberFormatter.SizeSuffix(progress.BytesTransferred, 0)}");
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