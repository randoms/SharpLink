using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Net.Http;
using Skynet.Models;
using Newtonsoft.Json;
using Skynet.Base;

namespace SkynetTests.Base.Controllers
{
    [TestClass]
    public class GrandParentsControllerTest
    {

        Skynet.Base.Skynet mSkynet;
        string baseUrl;

        public object ToxReponse { get; private set; }

        public GrandParentsControllerTest() {
            mSkynet = new Skynet.Base.Skynet();
            baseUrl = "http://localhost:" + mSkynet.httpPort + "/api/";
        }

        [TestMethod]
        public void GetTest()
        {
            Node node1 = new Node(mSkynet);
            Node node2 = new Node(mSkynet);
            node1.grandParents = node2.selfNode;
            node1.grandParentsModifiedTime = Skynet.Utils.Utils.UnixTimeNow();
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    string response = await client.GetStringAsync(baseUrl + "node/" + node1.selfNode.uuid
                        + "/grandParents");
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(response);
                    Assert.AreEqual(NodeResponseCode.OK, res.statusCode);
                    NodeId grandParents = JsonConvert.DeserializeObject<NodeId>(res.value);
                    Assert.AreEqual(node2.selfNode.uuid, grandParents.uuid);
                    Assert.AreEqual(node1.grandParentsModifiedTime, res.time);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void PutTest() {
            Node node1 = new Node(mSkynet);
            Node node2 = new Node(mSkynet);
            Node node3 = new Node(mSkynet);
            node2.parent = node1.selfNode;
            node2.parentModifiedTime = Skynet.Utils.Utils.UnixTimeNow();
            Task.Run(async () => {
                ToxResponse response = await RequestProxy.sendRequest(mSkynet, new ToxRequest {
                    uuid = Guid.NewGuid().ToString(),
                    url = "node/" + node3.selfNode.uuid + "/grandParents",
                    content = JsonConvert.SerializeObject(node1.selfNode),
                    method = "put",
                    fromNodeId = node2.selfNode.uuid,
                    fromToxId = node2.selfNode.toxid,
                    toNodeId = node3.selfNode.uuid,
                    toToxId = node3.selfNode.toxid,
                    time = node2.parentModifiedTime
                });

                NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(response.content);
                Assert.AreEqual(node1.selfNode.uuid, node3.grandParents.uuid);
                Assert.AreEqual(node2.parentModifiedTime, node3.grandParentsModifiedTime);
            }).GetAwaiter().GetResult();
        }
    }
}
