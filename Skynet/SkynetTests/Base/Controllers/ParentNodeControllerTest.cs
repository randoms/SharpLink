using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Net.Http;
using Skynet.Models;
using System.Collections.Generic;
using Skynet.Base.Contollers;
using Newtonsoft.Json;
using Skynet.Base;
using System.Linq;

namespace SkynetTests.Base.Controllers
{
    [TestClass]
    public class ParentNodeControllerTest
    {

        Skynet.Base.Skynet mSkynet;
        string baseUrl;
        public ParentNodeControllerTest() {
            mSkynet = new Skynet.Base.Skynet();
            baseUrl = baseUrl = "http://localhost:" + mSkynet.httpPort + "/";
        }

        [TestMethod]
        public void GetParnentNode()
        {
            Task.Run(async () =>
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(200); // This process may take up to 2mins
                    // create a node
                    Node mNode = new Node(new List<NodeId>(), mSkynet);
                    // set http headers
                    client.DefaultRequestHeaders.Add("Uuid", Guid.NewGuid().ToString());
                    client.DefaultRequestHeaders.Add("From-Node-Id", mNode.selfNode.uuid);
                    client.DefaultRequestHeaders.Add("From-Tox-Id", mNode.selfNode.toxid);
                    client.DefaultRequestHeaders.Add("To-Node-Id", mNode.selfNode.uuid);
                    client.DefaultRequestHeaders.Add("To-Tox-Id", mSkynet.tox.Id.ToString());
                    string res = await client.GetStringAsync(baseUrl + "api/tox/" + ""); // the tox id does not exist
                    NodeResponse mRes = JsonConvert.DeserializeObject<NodeResponse>(res);
                    Assert.AreEqual(mRes.statusCode, NodeResponseCode.NotFound);
                }
            });
        }

        [TestMethod]
        public void setParentNode() {
            Task.Run(() => {

                Skynet.Base.Skynet sender = new Skynet.Base.Skynet();
                Node mNode = new Node(new List<NodeId>(), mSkynet);
                Task.Run(async () => {
                    // create a node

                    Node parentNode = new Node(new List<NodeId>(), sender);

                    long timeStamp = Skynet.Utils.Utils.UnixTimeNow();
                    ToxResponse res = await RequestProxy.sendRequest(mSkynet, new ToxRequest
                    {
                        uuid = Guid.NewGuid().ToString(),
                        url = "node/" + mNode.selfNode.uuid + "/parent",
                        method = "put",
                        content = JsonConvert.SerializeObject(parentNode.selfNode),
                        fromNodeId = parentNode.selfNode.uuid,
                        fromToxId = sender.tox.Id.ToString(),
                        toNodeId = mNode.selfNode.uuid,
                        toToxId = mSkynet.tox.Id.ToString(),
                        time = timeStamp,
                    });
                    NodeResponse mRes = JsonConvert.DeserializeObject<NodeResponse>(res.content);
                    Console.WriteLine("value: " + mRes.value);
                    Assert.AreEqual(mRes.statusCode, NodeResponseCode.OK);
                    Assert.AreEqual(timeStamp, mRes.time);
                    Assert.AreEqual(timeStamp, mNode.parentModifiedTime);
                }).GetAwaiter().GetResult();
            });
        }

        [TestMethod]
        public void autoSetGrandParnetsTest() {
            Skynet.Base.Skynet mSkynet = new Skynet.Base.Skynet();
            Node node1 = new Node(mSkynet);
            Node node2 = new Node(mSkynet);
            Node node3 = new Node(mSkynet);
            Task.Run(async () => {
                bool isConnect2 = await node2.joinNetByTargetParents(new List<NodeId> { node1.selfNode });
                bool isConnect3 = await node3.joinNetByTargetParents(new List<NodeId> { node2.selfNode });
                Assert.AreEqual(node1.childNodes.FirstOrDefault().uuid, node2.selfNode.uuid);
                Assert.AreEqual(node2.childNodes.FirstOrDefault().uuid, node3.selfNode.uuid);
                Assert.AreEqual(node3.parent.uuid, node2.selfNode.uuid);
                Assert.AreEqual(node2.parent.uuid, node1.selfNode.uuid);
                Assert.AreEqual(node3.grandParents.uuid, node1.selfNode.uuid);
            }).GetAwaiter().GetResult();
            
        }
    }
}
