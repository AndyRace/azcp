using Microsoft.Azure.Storage.DataMovement;
using System.Diagnostics;

namespace AzCp
{
  public static class TransferStatusExtension
  {
    public static string ToUserString(this TransferStatus progress, long? intialBytesTransferred, Stopwatch stopwatch)
    {
      var result = $"{progress.NumberOfFilesTransferred} transferred, {progress.NumberOfFilesSkipped} skipped, {progress.NumberOfFilesFailed} failed, {progress.BytesTransferred.ToSizeSuffix(0)} ({progress.BytesTransferred:N0})";

      if (intialBytesTransferred.HasValue && (progress.BytesTransferred > intialBytesTransferred))
      {
        result += $" {(progress.BytesTransferred - intialBytesTransferred).ToBitsPerSecond(stopwatch.ElapsedMilliseconds)}";
      }

      return result;
    }
  }
}
