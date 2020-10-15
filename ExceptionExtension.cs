using Serilog;
using System;

namespace AzCp
{
  public static class ExceptionExtension
  {
    public static bool IsDevelopment { get; set; }

    public static void LogIt(this Exception exception, string prefix = null)
    {
      //if (IsDevelopment)
      //{
      //  Log.Error(exception, Repository.ApplicationFullVersion);
      //}
      //else
      //{
      var msg = prefix;// $@"{Repository.ApplicationFullVersion}: ";

      var tmpE = exception;
      while (tmpE != null)
      {
        msg += $"{tmpE.Message}\n";
        tmpE = tmpE.InnerException;
      }

      Log.Error(msg.Trim());
      //}
    }
  }
}
