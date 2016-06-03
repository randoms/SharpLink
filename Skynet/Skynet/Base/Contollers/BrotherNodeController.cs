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
    public class BrotherNodeController: ApiController
    {
        [Route("api/node/{nodeId}/brotherNodes")]
        [HttpGet]
        public NodeResponse GetAll(string nodeId) {
            Skynet host = Skynet.allInstance.Where(x => x.httpPort == Request.RequestUri.Port).FirstOrDefault();
            // check if a valid nodeid
            if (!Utils.Utils.isValidGuid(nodeId)) {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "not a valid nodeid",
                };
            }
            Node targetNode = Node.AllLocalNodes.Where(x => x.selfNode.uuid == nodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (targetNode == null) {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target node can not be found on the client",
                };
            }
            return new NodeResponse {
                statusCode = NodeResponseCode.OK,
                description = "success",
                value = JsonConvert.SerializeObject(targetNode.brotherNodes),
                time = targetNode.brotherModifiedTime,
            };
        }

        [Route("api/node/{nodeId}/brotherNodes/{brotherNodeId}")]
        [HttpGet]
        public NodeResponse Get(string nodeId, string brotherNodeId) {
            Skynet host = Skynet.allInstance.Where(x => x.httpPort == Request.RequestUri.Port).FirstOrDefault();
            // check if a valid nodeid
            if (!Utils.Utils.isValidGuid(nodeId) || !Utils.Utils.isValidGuid(brotherNodeId))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "not a valid nodeid",
                };
            }
            Node targetNode = Node.AllLocalNodes.Where(x => x.selfNode.uuid == nodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (targetNode == null)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target node can not be found on the client",
                };
            }
            NodeId brotherNode = targetNode.brotherNodes.Where(x => x.uuid == brotherNodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (brotherNode == null) {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target node do not have this brother",
                };
            }

            return new NodeResponse {
                statusCode = NodeResponseCode.OK,
                description = "success",
                value = JsonConvert.SerializeObject(brotherNode),
                time = targetNode.brotherModifiedTime,
            };
        }

        [Route("api/node/{nodeId}/brotherNodes")]
        [HttpPost]
        public NodeResponse Post(string nodeId, [FromBody] NodeId newBrother) {
            IEnumerable<string> headerValues = new List<string>();
            IEnumerable<string> requestTime = new List<string>();
            if (!Request.Headers.TryGetValues("From-Node-Id", out headerValues) 
                || !Request.Headers.TryGetValues("Skynet-Time", out requestTime)) {
                // can not found from headers
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "you need to add some http headers"
                };
            }
            Skynet host = Skynet.allInstance.Where(x => x.httpPort == Request.RequestUri.Port).FirstOrDefault();
            // check if a valid nodeid
            if (!Utils.Utils.isValidGuid(nodeId))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "not a valid nodeid",
                };
            }
            Node targetNode = Node.AllLocalNodes.Where(x => x.selfNode.uuid == nodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (targetNode == null)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target node can not be found on the client",
                };
            }

            if (targetNode.parent == null || headerValues.FirstOrDefault() != targetNode.parent.uuid) {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NoPermission,
                    description = "you do not have permission to access change brother node",
                };
            }

            if (targetNode.brotherNodes.Count >= 10) {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.TargetIsFull,
                    description = "target brother nodes is full"
                };
            }

            if (targetNode.brotherNodes.Where(x => x.uuid == newBrother.uuid).Count() != 0)
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.AlreadyExist,
                    description = "target already existed"
                };

            if (targetNode.nodeChangeLock.isLocked) {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.TargetLocked,
                    description = "target is locked",
                };
            }

            if (long.Parse(requestTime.DefaultIfEmpty("0").FirstOrDefault()) 
                < targetNode.brotherModifiedTime) {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.OutOfDate,
                    description = "Your data is outofdate",
                };
            }

            targetNode.brotherNodes.Add(newBrother);
            return new NodeResponse {
                statusCode = NodeResponseCode.OK,
                description = "success",
                value = JsonConvert.SerializeObject(targetNode.brotherNodes),
            };
        }

        [Route("api/node/{nodeId}/brotherNodes")]
        [HttpPut]
        public async Task<NodeResponse> Put(string nodeId) {
            List<NodeId> newBrothers = JsonConvert.DeserializeObject<List<NodeId>>(await Request.Content.ReadAsStringAsync());
            IEnumerable<string> headerValues = new List<string>();
            IEnumerable<string> requestTime = new List<string>();
            if (!Request.Headers.TryGetValues("From-Node-Id", out headerValues)
                || !Request.Headers.TryGetValues("Skynet-Time", out requestTime))
            {
                // can not found from headers
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "you need to add some http headers"
                };
            }
            Skynet host = Skynet.allInstance.Where(x => x.httpPort == Request.RequestUri.Port).FirstOrDefault();
            // check if a valid nodeid
            if (!Utils.Utils.isValidGuid(nodeId))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "not a valid nodeid",
                };
            }
            Node targetNode = Node.AllLocalNodes.Where(x => x.selfNode.uuid == nodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (targetNode == null)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target node can not be found on the client",
                };
            }

            if (targetNode.parent == null || headerValues.FirstOrDefault() != targetNode.parent.uuid)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NoPermission,
                    description = "you do not have permission to access change brother node",
                };
            }

            if (targetNode.brotherNodes.Count >= 10)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.TargetIsFull,
                    description = "target brother nodes is full"
                };
            }
            if (targetNode.nodeChangeLock.isLocked) {
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
                    description = "Your data is outofdate",
                };
            }
            targetNode.brotherModifiedTime = reqTime;
            targetNode.brotherNodes = newBrothers;
            return new NodeResponse {
                statusCode = NodeResponseCode.OK,
                description = "success",
                value = JsonConvert.SerializeObject(targetNode.brotherNodes),
                time = reqTime,
            };
        }

        [Route("api/node/{nodeId}/brotherNodes/{brotherNodeId}")]
        [HttpDelete]
        public NodeResponse Delete(string nodeId, string brotherNodeId) {
            IEnumerable<string> headerValues = new List<string>();
            IEnumerable<string> requestTime = new List<string>();
            if (!Request.Headers.TryGetValues("From-Node-Id", out headerValues)
                || !Request.Headers.TryGetValues("Skynet-Time", out requestTime))
            {
                // can not found from headers
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "you need to add some http headers"
                };
            }
            Skynet host = Skynet.allInstance.Where(x => x.httpPort == Request.RequestUri.Port).FirstOrDefault();
            // check if a valid nodeid
            if (!Utils.Utils.isValidGuid(nodeId))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "not a valid nodeid",
                };
            }
            Node targetNode = Node.AllLocalNodes.Where(x => x.selfNode.uuid == nodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (targetNode == null)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target node can not be found on the client",
                };
            }

            if (headerValues.FirstOrDefault() != targetNode.parent.uuid)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NoPermission,
                    description = "you do not have permission to access change brother node",
                };
            }
            
            if (targetNode.nodeChangeLock.isLocked)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.TargetLocked,
                    description = "target is locked",
                };
            }

            NodeId brotherNode = targetNode.brotherNodes.Where(x => x.uuid == brotherNodeId).DefaultIfEmpty(null).FirstOrDefault();
            if (brotherNode == null) {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "target do not have a request brother node",
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

            targetNode.brotherModifiedTime = reqTime;
            targetNode.brotherNodes.Remove(brotherNode);
            return new NodeResponse {
                statusCode = NodeResponseCode.OK,
                description = "success",
                value = JsonConvert.SerializeObject(targetNode.brotherNodes),
                time = reqTime,
            };
        }
    }
}
