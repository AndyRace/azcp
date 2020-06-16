using System;

namespace AzCp
{
  public static class NumberFormatterExtension
  {
    private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

    public static string ToSizeSuffix(this int value, int decimalPlaces = 0)
    {
      return ((long)value).ToSizeSuffix(decimalPlaces);
    }

    public static string ToSizeSuffix(this long value, int decimalPlaces = 0)
    {
      if (value < 0) { return "-" + ToSizeSuffix(-value, decimalPlaces); }

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
