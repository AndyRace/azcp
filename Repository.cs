namespace AzCp
{
  public class Repository
  {
    public string SourcePath { get; set; }
    public string ContainerName { get; set; }
    public string BlobName { get; set; }
    public int ParallelOperations { get; set; }
  }
}