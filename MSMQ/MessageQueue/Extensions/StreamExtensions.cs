using Newtonsoft.Json;
using System;
using System.IO;

namespace MessageQueue
{
    /// <summary>
    /// Reads in content as json and then deserializes content into object of specified type
    /// </summary>
    public static class StreamExtensions
    {
        public static string ReadToEnd(this Stream stream)
        {
            var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static T ReadFromJson<T>(this Stream stream)
        {
            var json = stream.ReadToEnd();
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        /// This is used for getting a label off of the MSMQ message
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="messageType"></param>
        /// <returns></returns>
        public static object ReadFromJson(this Stream stream, string messageType)
        {
            var type = Type.GetType(messageType);
            var json = stream.ReadToEnd();
            return JsonConvert.DeserializeObject(json, type);
        }
    }
}