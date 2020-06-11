using AzCp.Interfaces;
using System;

namespace AzCp
{
  internal class ConsoleFeedback : IFeedback
  {
    private readonly object _consoleLockObj = new object();

    private int _prevProgressLen = 0;

    public void WriteProgress(string format = "", object arg0 = null)
    {
      Write(format, arg0, ConsoleColor.Yellow, true);
    }
    public void WriteLine(string format = "", object arg0 = null)
    {
      Write(format, arg0, ConsoleColor.Green, false);
    }

    private void Write(string format, object arg0, ConsoleColor fgColor, bool progressLine)
    {
      lock (_consoleLockObj)
      {
        void ClearProgressLine()
        {
          if (_prevProgressLen != 0)
          {
            Console.Write($"{new string(' ', _prevProgressLen)}\r");
            _prevProgressLen = 0;
          }
        }

        var msg = string.Format(format, arg0);
        if (string.IsNullOrEmpty(msg))
        {
          ClearProgressLine();
          if (!progressLine)
          {
            Console.WriteLine();
          }
        }
        else
        {
          msg = $"{DateTime.Now}: {msg}";

          var bg = Console.BackgroundColor;
          var fg = Console.ForegroundColor;
          try
          {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = fgColor;

            // clear any previous 'progress' line
            if (progressLine)
            {
              Console.Write($"{msg.PadRight(_prevProgressLen, ' ')}\r");
              _prevProgressLen = msg.Length;
            }
            else
            {
              ClearProgressLine();
              Console.WriteLine(msg);
            }
          }
          finally
          {
            Console.BackgroundColor = bg;
            Console.ForegroundColor = fg;
          }
        }
      }
    }
  }
}
