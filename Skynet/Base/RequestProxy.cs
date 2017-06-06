using Newtonsoft.Json;
using SharpTox.Core;
using Skynet.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Skynet.Base
{
    /// <summary>
    /// switch between sky req and http req
    /// </summary>
    public class RequestProxy
    {

        public static ToxRequest toNodeRequest(HttpRequestMessage req)
        {
            return new ToxRequest
            {
                url = req.RequestUri.ToString(),
                method = req.Method.ToString(),
                content = Encoding.UTF8.GetBytes(req.Content.ToString()),
                uuid = req.Headers.GetValues("Uuid").FirstOrDefault(),
                fromNodeId = req.Headers.GetValues("From-Node-Id").FirstOrDefault(),
                fromToxId = req.Headers.GetValues("From-Tox-Id").FirstOrDefault(),
                toNodeId = req.Headers.GetValues("To-Node-Id").FirstOrDefault(),
                toToxId = req.Headers.GetValues("To-Tox-Id").FirstOrDefault(),
                time = long.Parse(req.Headers.GetValues("Skynet-Time").FirstOrDefault()),
            };
        }

        public static async Task<ToxResponse> sendRequest(Skynet host, ToxRequest req)
        {

            // if req is not send to local node
            if (host.tox.Id.ToString() != req.toToxId)
            {
                bool mResStatus = false;
                return await host.sendRequest(new ToxId(req.toToxId), req, out mResStatus);
            }

            string baseUrl = "http://localhost:" + host.httpPort + "/";
            var request = (HttpWebRequest)WebRequest.Create(baseUrl + "api/" + req.url);
            request.Headers.Add("Uuid", req.uuid);
            request.Headers.Add("From-Node-Id", req.fromNodeId);
            request.Headers.Add("From-Tox-Id", req.fromToxId);
            request.Headers.Add("To-Node-Id", req.toNodeId);
            request.Headers.Add("To-Tox-Id", req.toToxId);
            request.Headers.Add("Skynet-Time", req.time + "");
            request.Method = req.method.ToUpper();
            request.ContentType = "application/json";

            List<string> allowedMethods = new List<string> { "POST", "PUT", "PATCH" };
            if (allowedMethods.Any(x => x == req.method.ToUpper()))
            {
                // only the above methods are allowed to add body data
                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(req.content);
                }
            }
            var response = await request.GetResponseAsync();
            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            return req.createResponse(Encoding.UTF8.GetBytes(responseString));
        }
    }
}
