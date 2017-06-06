using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skynet.Models
{
    public class ToxResponse
    {
        public string url { get; set; }
        public string uuid { get; set; }
        public byte[] content { get; set; }
        public string fromNodeId { get; set; }
        public string fromToxId { get; set; }
        public string toNodeId { get; set; }
        public string toToxId { get; set; }

        public byte[] getBytes()
        {
            MemoryStream ms = new MemoryStream();
            using (BsonWriter writer = new BsonWriter(ms))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(writer, this);
            }
            var res = ms.ToArray();
            ms.Close();
            return res;
        }

        public static ToxResponse fromBytes(byte[] data)
        {
            MemoryStream ms = new MemoryStream(data);
            ToxResponse res;
            using (BsonReader reader = new BsonReader(ms))
            {
                JsonSerializer serializer = new JsonSerializer();
                res = serializer.Deserialize<ToxResponse>(reader);
            }
            ms.Close();
            return res;
        }
    }
}
