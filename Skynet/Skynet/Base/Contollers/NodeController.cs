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
    public class NodeController:ApiController
    {
        [Route("api/node")]
        [HttpGet]
        public NodeResponse GetAll() {
            Skynet mSkynet = Skynet.allInstance.Where(x => x.httpPort == Request.RequestUri.Port).FirstOrDefault();
            List<NodeId> nodeList = Node.AllLocalNodes.Where(x => x.mSkynet.httpPort == mSkynet.httpPort)
                .Select(x => x.selfNode).ToList();
            return new NodeResponse {
                statusCode = NodeResponseCode.OK,
                description = "success",
                value = JsonConvert.SerializeObject(nodeList)
            };
        }

        [Route("api/node/{nodeId}")]
        [HttpGet]
        public NodeResponse Get(string nodeId) {
            // check if a valid nodeid
            if (!Utils.Utils.isValidGuid(nodeId)) {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "not a valid node id",
                };
            }

            Node targetNode = Node.AllLocalNodes.Where(x => x.selfNode.uuid == nodeId).DefaultIfEmpty(null)
                .FirstOrDefault();
            if (targetNode == null) {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "can not find target node on this client"
                };
            }
            return new NodeResponse {
                statusCode = NodeResponseCode.OK,
                description = "success",
                value = JsonConvert.SerializeObject(targetNode.getInfo()),
            };
        }

        [Route("api/node")]
        [HttpPost]
        public NodeResponse Post([FromBody]List<NodeId> parents) {
            Skynet mSkynet = Skynet.allInstance.Where(x => x.httpPort == Request.RequestUri.Port).FirstOrDefault();
            Node newNode = new Node(parents, mSkynet);
            return new NodeResponse {
                statusCode = NodeResponseCode.OK,
                description = "success",
                value = JsonConvert.SerializeObject(newNode.getInfo()),
            };
        }

        [Route("api/node/{nodeId}")]
        [HttpDelete]
        public NodeResponse Delete(string nodeId) {

            // check if a valid nodeid
            if (!Utils.Utils.isValidGuid(nodeId))
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.InvalidRequest,
                    description = "not a valid node id",
                };
            }

            Node targetNode = Node.AllLocalNodes.Where(x => x.selfNode.uuid == nodeId).DefaultIfEmpty(null)
                .FirstOrDefault();
            if (targetNode == null)
            {
                return new NodeResponse
                {
                    statusCode = NodeResponseCode.NotFound,
                    description = "can not find target node on this client"
                };
            }
            Node.AllLocalNodes.Remove(targetNode);
            return new NodeResponse {
                statusCode = NodeResponseCode.OK,
                description = "success",
            };
        }
    }
}
