using AzCp.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace AzCp
{
  internal class ConsoleFeedback : IFeedback
  {
    private readonly object _consoleLockObj = new object();
    private int _prevProgressLen = 0;

    private static readonly Dictionary<IFeedback.Colors, ConsoleColor> _colorMapping = new Dictionary<IFeedback.Colors, ConsoleColor>  {
        { IFeedback.Colors.ErrorForegroundColor, ConsoleColor.Red },
        { IFeedback.Colors.OkForegroundColor, ConsoleColor.Green },
        { IFeedback.Colors.ProgressForegroundColor, ConsoleColor.Yellow },
        { IFeedback.Colors.WarningForegroundColor, ConsoleColor.Magenta }
      };

    public ConsoleFeedback()
    {
    }

    public void WriteProgress(string msg, IFeedback.Colors color)
    {
      Write(msg, color, true);
    }

    public void WriteLine(string msg, IFeedback.Colors color)
    {
      Write(msg, color, false);
    }

    private void Write(string msg, IFeedback.Colors fgColor, bool progressLine)
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
            Console.ForegroundColor = _colorMapping[fgColor];

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
