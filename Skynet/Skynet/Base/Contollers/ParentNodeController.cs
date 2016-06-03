using Newtonsoft.Json;
using SharpTox.Core;
using Skynet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;

namespace Skynet.Base.Contollers
{
    public class ParentNodeController : ApiController
    {
        [Route("api/node/{nodeId}/parent")]
        [HttpGet]
        public NodeResponse Get(string nodeId)
        {
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
            return new NodeResponse
            {
                statusCode = NodeResponseCode.OK,
                description = "get target node info success",
                value = JsonConvert.SerializeObject(targetNode.parent),
                time = targetNode.grandParentsModifiedTime,
            };

        }

        [Route("api/node/{nodeId}/parent")]
        [HttpPut, HttpPatch]
        public NodeResponse Put(string nodeId, [FromBody] NodeId values)
        {

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
            else
            {
                long reqTime = long.Parse(requestTime.DefaultIfEmpty("0").FirstOrDefault());
                if (reqTime < targetNode.brotherModifiedTime)
                {
                    return new NodeResponse
                    {
                        statusCode = NodeResponseCode.OutOfDate,
                        description = "Your data is outofdate",
                    };
                }
                targetNode.parentModifiedTime = reqTime;
                targetNode.parent = values;

                Task.Run(async () =>
                {
                    // get parent node info, set grandparents
                    Skynet host = Skynet.allInstance.Where(x => x.httpPort == Request.RequestUri.Port).FirstOrDefault();
                    bool status = false;
                    ToxResponse getParentInfo = await host.sendRequest(new ToxId(values.toxid), new ToxRequest
                    {
                        url = "node/" + values.uuid,
                        method = "get",
                        content = null,
                        fromNodeId = targetNode.selfNode.uuid,
                        fromToxId = targetNode.selfNode.toxid,
                        toNodeId = values.uuid,
                        toToxId = values.toxid,
                        time = Utils.Utils.UnixTimeNow(),
                        uuid = Guid.NewGuid().ToString()
                    }, out status);
                    NodeResponse getParentInfoRes = JsonConvert.DeserializeObject<NodeResponse>(Encoding.UTF8.GetString(getParentInfo.content));
                    NodeInfo parentInfo = JsonConvert.DeserializeObject<NodeInfo>(getParentInfoRes.value);
                    targetNode.grandParents = parentInfo.parent;
                    targetNode.grandParentsModifiedTime = parentInfo.parentModifiedTime;
                    await BoardCastChanges(targetNode);
                });

                return new NodeResponse
                {
                    statusCode = NodeResponseCode.OK,
                    description = "set parent success",
                    value = JsonConvert.SerializeObject(values),
                    time = reqTime,
                };
            }
        }

        public async Task BoardCastChanges(Node targetNode)
        {
            await Task.Run(() =>
            {
                targetNode.childNodes.ForEach((nodeId) =>
                {
                    bool status = false;
                    targetNode.mSkynet.sendRequest(new ToxId(nodeId.toxid), new ToxRequest
                    {
                        url = "node/" + nodeId.uuid + "/grandParents",
                        uuid = Guid.NewGuid().ToString(),
                        content = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(targetNode.parent)),
                        method = "put",
                        fromNodeId = targetNode.selfNode.uuid,
                        fromToxId = targetNode.selfNode.toxid,
                        toNodeId = nodeId.toxid,
                        toToxId = nodeId.toxid,
                        time = targetNode.parentModifiedTime,
                    }, out status);
                });
            });
        }
    }
}
