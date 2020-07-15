using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.DataMovement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;

namespace AzCp
{
  public class Repository
  {
    public const string AppSettingsJsonFilename = "appsettings.json";
    public const string AppSettingsSecretsJsonFilename = "appsettings.secrets.json";

    public Uri BlobContainerUri { get; set; }

    public string UploadFolder { get; set; } = "Upload";
    public string ArchiveFolder { get; set; } = "Archive";

    // https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.storage.datamovement.transferconfigurations.paralleloperations?view=azure-dotnet
    public int ParallelOperations { get; set; } = TransferManager.Configurations.ParallelOperations;

    // https://docs.microsoft.com/en-us/previous-versions/azure/reference/mt805217(v=azure.100)
    // https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.storage.datamovement.transferconfigurations.blocksize?view=azure-dotnet#Microsoft_Azure_Storage_DataMovement_TransferConfigurations_BlockSize
    public int BlockSize { get; set; } = TransferManager.Configurations.BlockSize;

    public bool Recursive { get; set; } = true;
    public string TransferCheckpointFilename { get; set; } = Path.Combine(new string[] { ".azcp", "checkpoint.json" });
    public int DefaultConnectionLimit { get; set; } = Environment.ProcessorCount * 8;
    public bool Expect100Continue { get; set; } = ServicePointManager.Expect100Continue;

    public static string ApplicationFullVersion
    {
      get
      {
        // Display the config info
        var entryAssembly = Assembly.GetEntryAssembly();
        var informationalVersion = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        var assemblyFileVersion = entryAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
        var product = entryAssembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
        return $@"{product} {informationalVersion} ({assemblyFileVersion})";
      }
    }

    public static string ApplicationDescription
    {
      get
      {
        var entryAssembly = Assembly.GetEntryAssembly();
        return entryAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
      }
    }

    public CloudBlobDirectory BlobDirectory
    {
      get
      {
        return BlobContainerUri == null ? null : new CloudBlobContainer(BlobContainerUri).GetDirectoryReference("");
      }
    }

    internal string ToFeedbackString()
    {
      var info = $@"{ApplicationFullVersion}
{ApplicationDescription}

";

      var repoDefaults = new Repository();

      var colInfo = new int[] { 0, 30, 65, 75 };
      var table = new List<string[]>()
         {
          new string[] { "Configuration entry",              "Description",                  "Value",                    "Default" },
          new string[] { "===================",              "===========",                  "=====",                    "=======" },
          Array.Empty<string>(),
          new string[] { nameof(UploadFolder),               "Upload Folder",                UploadFolder,               repoDefaults.UploadFolder },
          new string[] { nameof(ArchiveFolder),              "Archive Folder",               ArchiveFolder,              repoDefaults.ArchiveFolder },
          new string[] { nameof(TransferCheckpointFilename), "Transfer Checkpoint Filename", TransferCheckpointFilename, repoDefaults.TransferCheckpointFilename },
          new string[] { nameof(BlockSize),                  "Tx Block Size",                BlockSize.ToSizeSuffix(),   repoDefaults.BlockSize.ToSizeSuffix() },
          new string[] { nameof(ParallelOperations),         "Parallel Operations",          ParallelOperations.ToString(), repoDefaults.ParallelOperations.ToString() },
          new string[] { nameof(DefaultConnectionLimit),     "Default Connection Limit",     DefaultConnectionLimit.ToString(), repoDefaults.DefaultConnectionLimit.ToString() },
          new string[] { nameof(Expect100Continue),          "Wait for '100' response?",     Expect100Continue.ToString(), repoDefaults.Expect100Continue.ToString() },
          new string[] { nameof(Recursive),                  "Recurse the upload folder",    Recursive.ToString(),     repoDefaults.Recursive.ToString() },
          Array.Empty<string>(),
          new string[] { "For details of the configuration options see: https://github.com/Azure/azure-storage-net-data-movement/" },
          };

      table.ForEach(row =>
      {
        var line = "";
        for (int i = 0; i < row.Length; i++)
        {
          if (line.Length > colInfo[i])
          {
            info = info.TrimEnd(' ') + $"\n{line}";
            line = string.Empty;
          }
          line = $"{line.PadRight(colInfo[i])}{row[i]} ";
        }
        info = info.TrimEnd(' ') + $"\n{line}";
      });

      return info;
    }

    internal void UpdateEnvironmentFromSettings()
    {
      if (BlobContainerUri == null)
      {
        throw new Exception($"Please specify the URI to the BLOB container in the application settings file ({nameof(BlobContainerUri)})");
      }

      if (string.IsNullOrEmpty(UploadFolder))
      {
        throw new Exception("Please specify the upload folder in the application settings file");
      }

      if (!Directory.Exists(UploadFolder))
      {
        Directory.CreateDirectory(UploadFolder);
      }

      if (!Directory.Exists(ArchiveFolder))
      {
        Directory.CreateDirectory(ArchiveFolder);
      }

      if (!Directory.Exists(Path.GetDirectoryName(TransferCheckpointFilename)))
      {
        Directory.CreateDirectory(Path.GetDirectoryName(TransferCheckpointFilename));
      }

      TransferManager.Configurations.ParallelOperations = ParallelOperations;
      TransferManager.Configurations.BlockSize = BlockSize;
      ServicePointManager.DefaultConnectionLimit = DefaultConnectionLimit;
      ServicePointManager.Expect100Continue = Expect100Continue;
    }
  }
}