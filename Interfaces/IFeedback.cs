namespace AzCp.Interfaces
{
  public interface IFeedback
  {
    void WriteLine(string format = "", object arg0 = null);
    void WriteProgress(string format = "", object arg0 = null);
  }
}