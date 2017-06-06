using Newtonsoft.Json;
using SharpTox.Core;
using Skynet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Skynet.Base.Contollers
{
    public class ChildNodeController : ApiController
    {
        [Route("api/node/{nodeId}/childNodes")]
        [HttpGet]
        public NodeResponse GetAll(string nodeId)
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
                description = "success",
                value = JsonConvert.SerializeObject(targetNode.childNodes),
                time = targetNode.childNodesModifiedTime
            };
        }

        [Route("api/node/{nodeId}/childNodes/{childNodeId}")]
        [HttpGet]
        public NodeResponse Get(string nodeId, string childNodeId)
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

            if (!Utils.Utils.isValidGuid(childNodeId))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "your child node id is invalid",
                };
            }

            NodeId childNode = targetNode.childNodes.Where(x => x.uuid == childNodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (childNode == null)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target child node cannot be found on the client",
                };
            }

            return new NodeResponse
            {
                statusCode = NodeResponseCode.OK,
                description = "success",
                value = JsonConvert.SerializeObject(childNode),
                time = targetNode.childNodesModifiedTime
            };
        }

        [Route("api/node/{nodeId}/childNodes")]
        [HttpPost]
        public NodeResponse Post(string nodeId, [FromBody]NodeId newNode)
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

            // checklock
            if (targetNode.nodeChangeLock.isLocked)
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.TargetLocked,
                    description = "target node is currently locked",
                    value = JsonConvert.SerializeObject(targetNode.childNodes),
                };
            if (targetNode.childNodes.Count >= 10)
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.TargetIsFull,
                    description = "target node childnode is full",
                    value = JsonConvert.SerializeObject(targetNode.childNodes),
                };
            // check if already a child node
            if (targetNode.childNodes.Where(x => x.uuid == newNode.uuid).Count() != 0)
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.AlreadyExist,
                    description = "target is already a child node"
                };
            long reqTime = long.Parse(requestTime.DefaultIfEmpty("0").FirstOrDefault());
            if (reqTime < targetNode.brotherModifiedTime)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.OutOfDate,
                    description = "Your data is outofdate",
                };
            }

            // add new child node
            targetNode.childNodes.Add(newNode);
            targetNode.childNodesModifiedTime = reqTime;
            Task.Run(async () =>
            {
                await BoardCastNodeChanges(targetNode);
            });
            return new NodeResponse
            {
                statusCode = NodeResponseCode.OK,
                description = "add child node success",
                value = JsonConvert.SerializeObject(newNode),
                time = targetNode.childNodesModifiedTime
            };
        }

        [Route("api/node/{nodeId}/childNodes/{childNodeId}")]
        [HttpDelete]
        public NodeResponse Delete(string nodeId, string childNodeId)
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

            if (!Utils.Utils.isValidGuid(childNodeId))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "your child node id is invalid",
                };
            }

            NodeId childNode = targetNode.childNodes.Where(x => x.uuid == childNodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (childNode == null)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target child node cannot be found on the client",
                };
            }

            long reqTime = long.Parse(requestTime.DefaultIfEmpty("0").FirstOrDefault());
            if (reqTime < targetNode.brotherModifiedTime)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.OutOfDate,
                    description = "Your data is outofdate",
                };
            }

            targetNode.childNodesModifiedTime = reqTime;
            targetNode.childNodes.Remove(childNode);

            Task.Run(async () =>
            {
                await BoardCastNodeChanges(targetNode);
            });

            return new NodeResponse
            {
                statusCode = NodeResponseCode.OK,
                description = "child node has been removed.",
                time = reqTime,
            };
        }

        [Route("api/node/{nodeId}/childNodes/{childNodeId}")]
        [HttpPut]
        public NodeResponse Put(string nodeId, string childNodeId, [FromBody] NodeId newNode)
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

            if (!Utils.Utils.isValidGuid(childNodeId))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "your child node id is invalid",
                };
            }

            NodeId childNode = targetNode.childNodes.Where(x => x.uuid == childNodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (childNode == null)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target child node cannot be found on the client",
                };
            }

            long reqTime = long.Parse(requestTime.DefaultIfEmpty("0").FirstOrDefault());
            if (reqTime < targetNode.brotherModifiedTime)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.OutOfDate,
                    description = "Your data is outofdate",
                };
            }

            targetNode.childNodesModifiedTime = reqTime;
            targetNode.childNodes.Remove(childNode);
            targetNode.childNodes.Add(newNode);

            Task.Run(async () =>
            {
                await BoardCastNodeChanges(targetNode);
            });

            return new NodeResponse
            {
                statusCode = NodeResponseCode.OK,
                description = "success changed target childnode",
                time = reqTime,
            };
        }

        public async Task BoardCastNodeChanges(Node targetNode)
        {
            // we need to boardcast node changes to its childNodes
            await Task.Run(() =>
            {
                targetNode.childNodes.ForEach((nodeId) =>
                {
                    bool status = false;
                    targetNode.mSkynet.sendRequest(new ToxId(nodeId.toxid), new ToxRequest
                    {
                        url = "node/" + nodeId.uuid + "/brotherNodes",
                        method = "put",
                        content = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(targetNode.childNodes.Where(x => x.uuid != nodeId.uuid).ToList())),
                        fromNodeId = targetNode.selfNode.uuid,
                        fromToxId = targetNode.selfNode.toxid,
                        toNodeId = targetNode.selfNode.uuid,
                        toToxId = targetNode.selfNode.toxid,
                        time = targetNode.childNodesModifiedTime,
                        uuid = Guid.NewGuid().ToString(),
                    }, out status);
                });
            });
        }

    }
}
