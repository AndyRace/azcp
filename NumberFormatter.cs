using System;

namespace AzCp
{
  class NumberFormatter
  {
    private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

    public static string SizeSuffix(long value, int decimalPlaces = 0)
    {
      if (value < 0) { return "-" + SizeSuffix(-value); }

      int i = 0;
      decimal dValue = value;
      while (Math.Round(dValue, decimalPlaces) >= 1024)
      {
        dValue /= 1024;
        i++;
      }

      return string.Format("{0:n" + decimalPlaces + "} {1}", dValue, SizeSuffixes[i]);
    }
  }
}
