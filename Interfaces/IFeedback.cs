using System;

namespace AzCp.Interfaces
{
  public interface IFeedback
  {
    public enum Colors
    {
      OkForegroundColor,
      WarningForegroundColor,
      ErrorForegroundColor,
      ProgressForegroundColor
    }

    void WriteLine(string line = "", Colors color = Colors.OkForegroundColor);
    void WriteProgress(string line = "", Colors color = Colors.ProgressForegroundColor);
  }
}