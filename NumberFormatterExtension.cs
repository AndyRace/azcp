using System;
using System.Drawing;
using System.Text.RegularExpressions;

namespace AzCp
{
  public static class NumberFormatterExtension
  {
    private static readonly string[] SizeSuffixes = { " bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

    public static string ToSizeSuffix(this int? value, int decimalPlaces = 0)
    {
      return ToSizeSuffix(value, decimalPlaces, SizeSuffixes);
    }

    public static string ToSizeSuffix(this long value, int decimalPlaces = 0)
    {
      return ToSizeSuffix(value, decimalPlaces, SizeSuffixes);
    }


    public static string ToSizeSuffix(this long? value, int decimalPlaces = 0)
    {
      return ToSizeSuffix(value, decimalPlaces, SizeSuffixes);
    }

    private static string ToSizeSuffix(this long? value, int decimalPlaces, string[] suffixes)
    {
      if (!value.HasValue) return string.Empty;

      if (value < 0) { return "-" + ToSizeSuffix(-value, decimalPlaces); }

      int i = 0;
      decimal dValue = (long)value;
      while (Math.Round(dValue, decimalPlaces) >= 1024)
      {
        dValue /= 1024;
        i++;
      }

      var strValue = Regex.Replace(string.Format($"{{0:n{decimalPlaces}}}", dValue), @"\.0+$", "");
      return $"{strValue}{(suffixes == null ? SizeSuffixes[i] : suffixes[i])}";
    }

    private static readonly string[] BitsPerSecSuffixes = { "b", "Kb", "Mb", "Gb", "Tb", "Pb", "Eb", "Zb", "Yb" };

    public static string ToTxRate(this long numBytes, long elapsedMilliseconds)
    {
      return ((long?)numBytes).ToBitsPerSecond(elapsedMilliseconds);
    }

    public static string ToBitsPerSecond(this long? numBytes, long elapsedMilliseconds)
    {
      if (!numBytes.HasValue) return string.Empty;

      try
      {
        return ((numBytes * 8) / (elapsedMilliseconds / 1000)).ToSizeSuffix(1, BitsPerSecSuffixes) + "/s";
      }
#pragma warning disable CA1031 // Do not catch general exception types
      catch (OverflowException)
      {
        return string.Empty;
      }
#pragma warning restore CA1031 // Do not catch general exception types
    }
  }
}
