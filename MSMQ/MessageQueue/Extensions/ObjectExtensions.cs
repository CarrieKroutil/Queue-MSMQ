using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace MessageQueue
{
    /// <summary>
    /// Takes an object and serializes it to a json object
    /// </summary>
    public static class ObjectExtensions
    {
        public static string GetMessageType(this object obj)
        {
            return obj.GetType().AssemblyQualifiedName;
        }

        public static string ToJsonString(this object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public static Stream ToJsonStream(this object obj)
        {
            var json = obj.ToJsonString();
            return new MemoryStream(Encoding.Default.GetBytes(json));
        }
    }
}