using Microsoft.Azure.Storage.DataMovement;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization.Json;

namespace AzCp
{
  internal static class AzCpCheckpoint
  {
    static readonly object _lock = new object();

    internal static void Write(TransferCheckpoint transferCheckpoint, string transferCheckpointFilename)
    {
      lock (_lock)
      {
        using var writer = new FileStream(transferCheckpointFilename, FileMode.Create, FileAccess.Write);

        if (transferCheckpoint.GetType().IsSerializable)
        {
          var formatter = new BinaryFormatter();
          formatter.Serialize(writer, transferCheckpoint);
        }
        else
        {
          var ser = new DataContractJsonSerializer(transferCheckpoint.GetType());
          ser.WriteObject(writer, transferCheckpoint);
        }

        writer.Flush();
        writer.Close();
      }
    }

    internal static TransferCheckpoint Read(string transferCheckpointFilename)
    {
      lock (_lock)
      {
        // todo: Error checking
        //        if (typeof(TransferCheckpoint).IsSerializable)
        //        {
        //          try
        //          {
        //            return JsonSerializer.Deserialize<TransferCheckpoint>(File.ReadAllText(transferCheckpointFilename));
        //          }
        //#pragma warning disable CA1031 // Do not catch general exception types
        //          catch (Exception ex) when (ex is IOException | ex is JsonException | ex is NotSupportedException)
        //          {
        //            return null;
        //          }
        //        }
        //        else
        {
          try
          {
            using var reader = new FileStream(transferCheckpointFilename, FileMode.Open, FileAccess.Read);

            if (typeof(TransferCheckpoint).IsSerializable)
            {
              var formatter = new BinaryFormatter();
              return formatter.Deserialize(reader) as TransferCheckpoint;
            }
            else
            {
              var ser = new DataContractJsonSerializer(typeof(TransferCheckpoint));
              return ser.ReadObject(reader) as TransferCheckpoint;
            }
          }
          catch (Exception ex) when (ex is IOException | ex is SerializationException)
          {
            // ignore any IO exceptions (e.g. file not found) and return an empty checkpoint
            return null;
          }
        }
      }
    }
  }
}