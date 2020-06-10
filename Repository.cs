namespace AzCp
{
  public class Repository
  {
    public string ContainerName { get; set; }
    public string UploadFolder { get; set; }
    public string ArchiveFolder { get; set; }
    // https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.storage.datamovement.transferconfigurations.paralleloperations?view=azure-dotnet
    public int? ParallelOperations { get; set; }
    // https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.storage.datamovement.transferconfigurations.blocksize?view=azure-dotnet#Microsoft_Azure_Storage_DataMovement_TransferConfigurations_BlockSize
    public int? BlockSize { get; set; }
    public bool Recursive { get; set; }
    public string TransferCheckpointFilename { get; set; }
  }
}