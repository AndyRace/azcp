using System;

namespace AzCp.Interfaces
{
  public interface IFeedback
  {
    enum Colors
    {
      OkForegroundColor = ConsoleColor.Green,
      WarningForegroundColor = ConsoleColor.Magenta,
      ErrorForegroundColor = ConsoleColor.Red,
      ProgressForegroundColor = ConsoleColor.Yellow,
    }

    void WriteLine(string format = "", object arg0 = null, Colors color = Colors.OkForegroundColor);
    void WriteProgress(string format = "", object arg0 = null, Colors color = Colors.ProgressForegroundColor);
  }
}