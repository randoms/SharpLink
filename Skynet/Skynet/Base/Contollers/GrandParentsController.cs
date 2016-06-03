using Newtonsoft.Json;
using Skynet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Skynet.Base.Contollers
{
    public class GrandParentsController : ApiController
    {
        [Route("api/node/{nodeId}/grandParents")]
        [HttpGet]
        public NodeResponse Get(string nodeId) {
            if (!Utils.Utils.isValidGuid(nodeId))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "your node id is invalid",
                };
            }
            Node targetNode = Node.AllLocalNodes.Where(x => x.selfNode.uuid == nodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (targetNode == null)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target node cannot be found on the client",
                };
            }
            
            return new NodeResponse {
                statusCode = NodeResponseCode.OK,
                description = "success",
                value = JsonConvert.SerializeObject(targetNode.grandParents),
                time = targetNode.grandParentsModifiedTime
            };
        }

        [Route("api/node/{nodeId}/grandParents")]
        [HttpPut]
        public NodeResponse Put(string nodeId, [FromBody] NodeId values) {
            IEnumerable<string> requestTime = new List<string>();
            if (!Request.Headers.TryGetValues("Skynet-Time", out requestTime))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "you need to add some http headers"
                };
            }

            if (!Utils.Utils.isValidGuid(nodeId))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "your node id is invalid",
                };
            }
            Node targetNode = Node.AllLocalNodes.Where(x => x.selfNode.uuid == nodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (targetNode == null)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target node cannot be found on the client",
                };
            }
            // check lock
            if (targetNode.nodeChangeLock.isLocked == true)
            {
                // target is locked, cannot be changed at this time
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.TargetLocked,
                    description = "target is locked",
                };
            }

            long reqTime = long.Parse(requestTime.DefaultIfEmpty("0").FirstOrDefault());
            if (reqTime < targetNode.brotherModifiedTime)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.OutOfDate,
                    description = "Your data is out of date",
                };
            }
            targetNode.grandParentsModifiedTime = reqTime;
            targetNode.grandParents = values;

            return new NodeResponse
            {
                statusCode = NodeResponseCode.OK,
                description = "set parent success",
                value = JsonConvert.SerializeObject(values),
                time = reqTime,
            };   
        }
    }
}
