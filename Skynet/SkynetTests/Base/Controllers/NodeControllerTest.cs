using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skynet.Models;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace SkynetTests.Base.Controllers
{
    [TestClass]
    public class NodeControllerTest
    {
        Skynet.Base.Skynet mSkynet;
        Node Node1;
        Node Node2;
        string baseUrl;

        public NodeControllerTest() {
            mSkynet = new Skynet.Base.Skynet();
            Node1 = new Node(mSkynet);
            Node2 = new Node(mSkynet);
            baseUrl = "http://localhost:" + mSkynet.httpPort + "/api/";
        }

        [TestMethod]
        public void GetAllTest()
        {
            Task.Run( async () => {
                using (var client = new HttpClient()) {
                    string response = await client.GetStringAsync(baseUrl + "node");
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(response);
                    Assert.AreEqual(NodeResponseCode.OK, res.statusCode);
                    List<NodeId> nodeList = JsonConvert.DeserializeObject<List<NodeId>>(res.value);
                    Assert.AreEqual(2, nodeList.Count);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void GetTest() {
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    string response = await client.GetStringAsync(baseUrl + "node/" + Node1.selfNode.uuid);
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(response);
                    Assert.AreEqual(NodeResponseCode.OK, res.statusCode);
                    NodeInfo nodeInfo = JsonConvert.DeserializeObject<NodeInfo>(res.value);
                    Assert.AreEqual(Node1.selfNode.uuid, nodeInfo.selfNode.uuid);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void PostTest() {
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    int currentNodeCount = Node.AllLocalNodes.Count;
                    var response = await client.PostAsync(baseUrl + "node", null);
                    string responseString = await response.Content.ReadAsStringAsync();
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(responseString);
                    Assert.AreEqual(NodeResponseCode.OK, res.statusCode);
                    Assert.AreEqual(currentNodeCount + 1, Node.AllLocalNodes.Count);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void DeleteTest() {
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    int currentNodeCount = Node.AllLocalNodes.Count;
                    var response = await client.DeleteAsync(baseUrl + "node/" + Node1.selfNode.uuid);
                    string responseString = await response.Content.ReadAsStringAsync();
                    Assert.AreEqual(currentNodeCount - 1, Node.AllLocalNodes.Count);
                    Assert.AreEqual(null, Node.AllLocalNodes.Where(x => x.selfNode.uuid == Node1.selfNode.uuid).DefaultIfEmpty(null).FirstOrDefault());
                }
            }).GetAwaiter().GetResult();
        }
    }
}
