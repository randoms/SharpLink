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
    public class ToxRequest
    {
        public string url { get; set; }
        public string method { get; set; }
        public string uuid { get; set; }
        public byte[] content { get; set; }
        public string fromNodeId { get; set; }
        public string fromToxId { get; set; }
        public string toNodeId { get; set; }
        public string toToxId { get; set; }
        public long time { get; set; }

        public ToxRequest()
        {
            time = 0;
        }

        public ToxResponse createResponse(byte[] content = null)
        {
            return new ToxResponse
            {
                url = this.url,
                uuid = this.uuid,
                fromNodeId = this.toNodeId,
                fromToxId = this.toToxId,
                toToxId = this.fromToxId,
                toNodeId = this.fromNodeId,
                content = content,
            };
        }

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

        public static ToxRequest fromBytes(byte[] data)
        {
            MemoryStream ms = new MemoryStream(data);
            ToxRequest res;
            using (BsonReader reader = new BsonReader(ms))
            {
                JsonSerializer serializer = new JsonSerializer();
                try
                {
                    res = serializer.Deserialize<ToxRequest>(reader);
                }
                catch
                {
                    res = null;
                }

            }
            ms.Close();
            return res;
        }


    }
}
