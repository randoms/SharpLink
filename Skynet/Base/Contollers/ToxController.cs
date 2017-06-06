using Newtonsoft.Json;
using SharpTox.Core;
using Skynet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Skynet.Base.Contollers
{
    public class ToxController : ApiController
    {
        [Route("api/tox/{id}")]
        [HttpGet]
        public async Task<NodeResponse> Get(string id)
        {
            Skynet curHost = Skynet.allInstance.Where(x => x.httpPort == Request.RequestUri.Port).FirstOrDefault();
            if (!ToxId.IsValid(id))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "your tox id is invalid",
                };
            }

            // check if target tox client is local client
            if (curHost.tox.Id.ToString() == id)
            {
                // list all nodes on target tox client
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.OK,
                    description = "success",
                    value = JsonConvert.SerializeObject(new ToxClient
                    {
                        Id = id,
                        nodes = Node.AllLocalNodes.Select(x => x.getInfo()).ToList()
                    })
                };
            }
            // if not, send tox req to target tox client
            bool reqStatus = false;
            ToxResponse nodeResponse = await curHost.sendRequest(new ToxId(id), RequestProxy.toNodeRequest(Request), out reqStatus);
            if (reqStatus)
                return JsonConvert.DeserializeObject<NodeResponse>(Encoding.UTF8.GetString(nodeResponse.content));
            else
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target does not exist or target is current offline",
                };
        }

        [Route("api/tox")]
        [HttpGet]
        public NodeResponse GetAll()
        {
            List<string> toxList = Skynet.allInstance.Select(x => x.tox.Id.ToString()).ToList();
            return new NodeResponse
            {
                statusCode = NodeResponseCode.OK,
                description = "success",
                value = JsonConvert.SerializeObject(toxList)
            };
        }
    }
}
