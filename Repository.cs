using Microsoft.Azure.Storage.DataMovement;
using System;
using System.IO;
using System.Net;

namespace AzCp
{
  public class Repository
  {
    public string ContainerName { get; set; }
    public string UploadFolder { get; set; } = "Upload";
    public string ArchiveFolder { get; set; } = "Archive";

    // https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.storage.datamovement.transferconfigurations.paralleloperations?view=azure-dotnet
    public int? ParallelOperations { get; set; } = TransferManager.Configurations.ParallelOperations;

    // https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.storage.datamovement.transferconfigurations.blocksize?view=azure-dotnet#Microsoft_Azure_Storage_DataMovement_TransferConfigurations_BlockSize
    public int? BlockSize { get; set; } = TransferManager.Configurations.BlockSize;

    public bool Recursive { get; set; } = true;
    public string TransferCheckpointFilename { get; set; } = Path.Combine(new string[] { ".azcp", "checkpoint.json" });
    public int? DefaultConnectionLimit { get; set; } = Environment.ProcessorCount * 8;
    public bool? Expect100Continue { get; set; } = ServicePointManager.Expect100Continue;
  }
}