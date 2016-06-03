using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SharpTox.Core;
using Skynet.Base.Contollers;
using Skynet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SkynetTests.Base
{
    [TestClass]
    public class SkynetTest
    {
        Skynet.Base.Skynet mSkynet;

        public SkynetTest() {
            mSkynet = new Skynet.Base.Skynet();
        }

        [TestMethod]
        public void SendRequest() {
            bool status = false;
            Task.Run(async () => {
                Node mNode = new Node(new List<NodeId>(), mSkynet);
                var res = await mSkynet.sendRequest(new ToxId("062AA695A9F3E8C6A6667E5BCD24B16ABF96F4775EE3E59764FDFC2453C4027A74FA2E4D26BC"), new ToxRequest {
                    url = "",
                    uuid = Guid.NewGuid().ToString(),
                    content = "test",
                    fromNodeId = mNode.selfNode.uuid,
                    fromToxId = mSkynet.tox.Id.ToString(),
                    toNodeId = mNode.selfNode.uuid,
                    toToxId = "062AA695A9F3E8C6A6667E5BCD24B16ABF96F4775EE3E59764FDFC2453C4027A74FA2E4D26BC",
                }, out status);
                Assert.AreEqual(status, false);
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void TestHandShake() {
            while (!mSkynet.tox.IsConnected)
            {
                Thread.Sleep(10);
            }
            Skynet.Base.Skynet mSkynet2 = new Skynet.Base.Skynet();
            Node mNode1 = new Node(new List<NodeId>(), mSkynet);
            Node mNode2 = new Node(new List<NodeId>(), mSkynet2);

            bool status = false;
            Task.Run(async () =>
            {
                
                ToxResponse res = await mSkynet2.sendRequest(mSkynet.tox.Id, new ToxRequest
                {
                    url = "tox/" + mSkynet.tox.Id.ToString(),
                    uuid = Guid.NewGuid().ToString(),
                    method = "get",
                    content = "",
                    fromNodeId = mNode2.selfNode.uuid,
                    fromToxId = mNode2.selfNode.toxid,
                    toNodeId = mNode1.selfNode.uuid,
                    toToxId = mNode1.selfNode.toxid,
                }, out status);
                Console.WriteLine("status " + status);
                Assert.AreEqual(status, true);
                if (status) {
                    NodeResponse nodeRes = JsonConvert.DeserializeObject<NodeResponse>(res.content);
                    Assert.AreEqual(nodeRes.statusCode, NodeResponseCode.OK);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void TestSendMsgToSelf() {
            Node Node1 = new Node(mSkynet);
            Node Node2 = new Node(mSkynet);
            bool status = false;

            Task.Run(async () => {
                ToxResponse res = await mSkynet.sendRequest(mSkynet.tox.Id, new ToxRequest
                {
                    url = "tox/" + mSkynet.tox.Id.ToString(),
                    uuid = Guid.NewGuid().ToString(),
                    method = "get",
                    content = "",
                    fromNodeId = Node1.selfNode.uuid,
                    fromToxId = mSkynet.tox.Id.ToString(),
                    toNodeId = Node2.selfNode.uuid,
                    toToxId = mSkynet.tox.Id.ToString(),
                }, out status);
                Console.WriteLine("status: " + status);
                Assert.AreEqual(true, status);
                if (status) {
                    NodeResponse nodeRes = JsonConvert.DeserializeObject<NodeResponse>(res.content);
                    Assert.AreEqual(nodeRes.statusCode, NodeResponseCode.OK);
                }
            }).GetAwaiter().GetResult();
        }
    }

}
