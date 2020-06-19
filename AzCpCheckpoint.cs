using Microsoft.Azure.Storage.DataMovement;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace AzCp
{
  [DataContract]
  internal class AzCpCheckpoint
  {
    //public string TransferCheckpointJson { get; private set; }
    [DataMember]
    public TransferCheckpoint TransferCheckpoint { get; private set; }

    // Instantiate a Singleton of the Semaphore with a value of 1. This means that only 1 thread can be granted access at a time.
    //static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
    static readonly object _lock = new object();

    internal static void Write(string transferCheckpointFilename, TransferCheckpoint transferCheckpoint)
    {
      lock (_lock)
      {
        var persist = new AzCpCheckpoint
        {
          TransferCheckpoint = transferCheckpoint
        };

        // todo: Error checking
        using FileStream writer = new FileStream(transferCheckpointFilename, FileMode.Create);
        var ser = new DataContractJsonSerializer(persist.GetType());
        ser.WriteObject(writer, persist);
      }
    }

    internal static AzCpCheckpoint Read(string transferCheckpointFilename)
    {
      lock (_lock)
      {
        try
        {
          // todo: Error checking
          using FileStream reader = new FileStream(transferCheckpointFilename, FileMode.Open);
          var ser = new DataContractJsonSerializer(typeof(AzCpCheckpoint));
          return ser.ReadObject(reader) as AzCpCheckpoint;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex) when (ex is IOException | ex is SerializationException)
        {
          // ignore any IO exceptions (e.g. file not found) and return an empty checkpoint
          return new AzCpCheckpoint();
        }
#pragma warning restore CA1031 // Do not catch general exception types
      }
    }
  }
}