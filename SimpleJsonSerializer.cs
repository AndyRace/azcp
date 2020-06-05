using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace AzCp
{
  class SimpleJsonSerializer<T> where T : class//, new()
  {
    // Create a User object and serialize it to a JSON stream.
    public static string WriteFromObject(T obj)
    {
      // Create a stream to serialize the object to.
      var ms = new MemoryStream();

      // Serializer the User object to the stream.
      var ser = new DataContractJsonSerializer(typeof(T));
      ser.WriteObject(ms, obj);
      byte[] json = ms.ToArray();
      ms.Close();
      return Encoding.UTF8.GetString(json, 0, json.Length);
    }

    // Deserialize a JSON stream to a User object.
    public static T ReadToObject(string json)
    {
      var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
      var ser = new DataContractJsonSerializer(typeof(T));
      T deserializedUser = ser.ReadObject(ms) as T;
      ms.Close();
      return deserializedUser;
    }
  }
}
