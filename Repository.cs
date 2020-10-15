using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.DataMovement;
using Microsoft.Azure.Storage.File;
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
    public Uri FileContainerUri { get; set; }

    public string UploadFolder { get; set; } = "Upload";
    public string ArchiveFolder { get; set; } = "Archive";

    // https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.storage.datamovement.transferconfigurations.paralleloperations?view=azure-dotnet
    // NOTE: Impirically we found that TransferManager.Configurations.ParallelOperations was too high and could be one of the factors causing thread locking
    public int ParallelOperations { get; set; } = TransferManager.Configurations.ParallelOperations / 2;

    // https://docs.microsoft.com/en-us/previous-versions/azure/reference/mt805217(v=azure.100)
    // https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.storage.datamovement.transferconfigurations.blocksize?view=azure-dotnet#Microsoft_Azure_Storage_DataMovement_TransferConfigurations_BlockSize
    public int BlockSize { get; set; } = TransferManager.Configurations.BlockSize;

    public bool Recursive { get; set; } = true;

    public string TransferCheckpointFilename { get; set; } = Path.Combine(new string[] { ".azcp", "checkpoint.bin" });

    // NOTE: Impirically we found that the suggested default of Environment.ProcessorCount * 8 was too high and could be one of the factors causing thread locking
    public int DefaultConnectionLimit { get; set; } = Environment.ProcessorCount * 4;

    // NOTE: Impirically we found that in our scenarios we were getting much more reliable upload results if we didn't wait for the 100 response
    public bool Expect100Continue { get; set; } = false; // ServicePointManager.Expect100Continue;

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

    internal CloudBlobDirectory BlobDirectory
    {
      get
      {
        try
        {
          return BlobContainerUri == null ? null : new CloudBlobContainer(BlobContainerUri).GetDirectoryReference("");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch
        {
          return null;
        }
#pragma warning restore CA1031 // Do not catch general exception types
      }
    }

    internal CloudFileDirectory FileDirectory
    {
      get
      {
        try
        {
          return FileContainerUri == null ? null : new CloudFileDirectory(FileContainerUri);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch
        {
          return null;
        }
#pragma warning restore CA1031 // Do not catch general exception types
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
          new string[] { nameof(BlobContainerUri),           "Azure Blob Container",        BlobDirectory?.Uri.ToString(),     repoDefaults.BlobDirectory?.Uri.ToString() },
          new string[] { nameof(FileContainerUri),           "Azure File Directory",        FileDirectory?.Uri.ToString(),     repoDefaults.FileDirectory?.Uri.ToString() },
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
      if (BlobContainerUri == null && FileContainerUri == null)
      {
        throw new Exception($"Please specify the URI to the Azure BLOB or Azure File container in the application settings file ({nameof(BlobContainerUri)} or {nameof(FileContainerUri)}");
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