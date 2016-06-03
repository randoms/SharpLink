using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skynet.Models;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Linq;

namespace SkynetTests.Base.Controllers
{
    [TestClass]
    public class BrotherNodeTest
    {

        Skynet.Base.Skynet mSkynet;
        string baseUrl;
        Node targetNode;
        Node brotherNode;

        public BrotherNodeTest() {
            mSkynet = new Skynet.Base.Skynet();
            baseUrl = "http://localhost:" + mSkynet.httpPort + "/api/";
            targetNode = new Node(mSkynet);
            brotherNode = new Node(mSkynet);
        }

        [TestMethod]
        public void GetAllTest()
        {
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    // add brothers
                    targetNode.brotherNodes.Add(brotherNode.selfNode);
                    string response = await client.GetStringAsync(baseUrl + "node/" + targetNode.selfNode.uuid
                        + "/brotherNodes");
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(response);
                    Assert.AreEqual(NodeResponseCode.OK, res.statusCode);
                    List<NodeId> resBros = JsonConvert.DeserializeObject<List<NodeId>>(res.value);
                    Assert.AreEqual(brotherNode.selfNode.uuid, resBros[0].uuid);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void GetTest() {
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    // test not found
                    targetNode.brotherNodes = new List<NodeId>();
                    string responseNotFound = await client.GetStringAsync(baseUrl + "node/" + targetNode.selfNode.uuid
                        + "/brotherNodes/" + brotherNode.selfNode.uuid);
                    NodeResponse resNotFound = JsonConvert.DeserializeObject<NodeResponse>(responseNotFound);
                    Assert.AreEqual(NodeResponseCode.NotFound, resNotFound.statusCode);

                    // test get
                    targetNode.brotherNodes.Add(brotherNode.selfNode);
                    string response = await client.GetStringAsync(baseUrl + "node/" + targetNode.selfNode.uuid
                        + "/brotherNodes/" + brotherNode.selfNode.uuid);
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(response);
                    Assert.AreEqual(NodeResponseCode.OK, res.statusCode);
                    NodeId resBro = JsonConvert.DeserializeObject<NodeId>(res.value);
                    Assert.AreEqual(brotherNode.selfNode.uuid, resBro.uuid);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void PostTest() {
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    var data = new Dictionary<string, string> {
                        { "uuid", brotherNode.selfNode.uuid},
                        { "toxid", brotherNode.selfNode.toxid},
                    };
                    targetNode.parent = targetNode.selfNode;
                    client.DefaultRequestHeaders.Add("Uuid", Guid.NewGuid().ToString());
                    client.DefaultRequestHeaders.Add("From-Node-Id", targetNode.selfNode.uuid);
                    client.DefaultRequestHeaders.Add("From-Tox-Id", targetNode.selfNode.toxid);
                    client.DefaultRequestHeaders.Add("To-Node-Id", targetNode.selfNode.uuid);
                    client.DefaultRequestHeaders.Add("To-Tox-Id", targetNode.selfNode.toxid);
                    long timeStamp = Skynet.Utils.Utils.UnixTimeNow();
                    client.DefaultRequestHeaders.Add("Skynet-Time", timeStamp + "");
                    var postResponse = await client.PostAsync(baseUrl + "node/" + targetNode.selfNode.uuid
                        + "/brotherNodes", new FormUrlEncodedContent(data));
                    string responseString = await postResponse.Content.ReadAsStringAsync();
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(responseString);
                    Assert.AreEqual(NodeResponseCode.OK, res.statusCode);
                    List<NodeId> newBro = JsonConvert.DeserializeObject<List<NodeId>>(res.value);
                    Assert.AreEqual(brotherNode.selfNode.uuid, newBro[0].uuid);
                    Assert.AreEqual(timeStamp,targetNode.brotherModifiedTime);
                    Assert.AreEqual(timeStamp, res.time);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void TestAlreadyExist() {
            Task.Run(async () => { 
                using (var client = new HttpClient())
                {
                    targetNode.brotherNodes.Add(brotherNode.selfNode);
                    var data = new Dictionary<string, string> {
                            { "uuid", brotherNode.selfNode.uuid},
                            { "toxid", brotherNode.selfNode.toxid},
                        };
                    targetNode.parent = targetNode.selfNode;
                    client.DefaultRequestHeaders.Add("Uuid", Guid.NewGuid().ToString());
                    client.DefaultRequestHeaders.Add("From-Node-Id", targetNode.selfNode.uuid);
                    client.DefaultRequestHeaders.Add("From-Tox-Id", targetNode.selfNode.toxid);
                    client.DefaultRequestHeaders.Add("To-Node-Id", brotherNode.selfNode.uuid);
                    client.DefaultRequestHeaders.Add("To-Tox-Id", brotherNode.selfNode.toxid);
                    client.DefaultRequestHeaders.Add("Skynet-Time", Skynet.Utils.Utils.UnixTimeNow() + "");
                    var postResponse = await client.PostAsync(baseUrl + "node/" + targetNode.selfNode.uuid
                        + "/brotherNodes", new FormUrlEncodedContent(data));
                    string responseString = await postResponse.Content.ReadAsStringAsync();
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(responseString);
                    Assert.AreEqual(NodeResponseCode.AlreadyExist, res.statusCode);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void TestPut() {
            Node node3 = new Node(mSkynet);
            node3.parent = targetNode.selfNode;
            brotherNode.parent = targetNode.selfNode;
            targetNode.childNodes.Add(node3.selfNode);
            targetNode.childNodes.Add(brotherNode.selfNode);
            Task.Run(() => {
                var request = WebRequest.Create(baseUrl + "node/" + brotherNode.selfNode.uuid
                    + "/brotherNodes");
                request.Method = "PUT";
                request.Headers.Add("Uuid", Guid.NewGuid().ToString());
                request.Headers.Add("From-Node-Id", targetNode.selfNode.uuid);
                request.Headers.Add("From-Tox-Id", targetNode.selfNode.toxid);
                request.Headers.Add("To-Node-Id", brotherNode.selfNode.uuid);
                request.Headers.Add("To-Tox-Id", brotherNode.selfNode.toxid);
                long timestamp = Skynet.Utils.Utils.UnixTimeNow();
                request.Headers.Add("Skynet-Time", timestamp + "");
                request.ContentType = "application/json";
                using (StreamWriter writer = new StreamWriter(request.GetRequestStream())) {
                    writer.Write(
                        JsonConvert.SerializeObject(
                            targetNode.childNodes
                            .Where(x => x.uuid != brotherNode.selfNode.uuid).ToList()));
                }
                var response = request.GetResponse();
                string resString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(resString);
                Assert.AreEqual(NodeResponseCode.OK, res.statusCode);
                Assert.AreEqual(timestamp, res.time);
                Assert.AreEqual(timestamp, brotherNode.brotherModifiedTime);
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void TestDelete() {
            Node node3 = new Node(mSkynet);
            node3.parent = targetNode.selfNode;
            brotherNode.parent = targetNode.selfNode;

            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    client.DefaultRequestHeaders.Add("Uuid", Guid.NewGuid().ToString());
                    client.DefaultRequestHeaders.Add("From-Node-Id", targetNode.selfNode.uuid);
                    client.DefaultRequestHeaders.Add("From-Tox-Id", targetNode.selfNode.toxid);
                    client.DefaultRequestHeaders.Add("To-Node-Id", brotherNode.selfNode.uuid);
                    client.DefaultRequestHeaders.Add("To-Tox-Id", brotherNode.selfNode.toxid);
                    long timeStamp = Skynet.Utils.Utils.UnixTimeNow();
                    client.DefaultRequestHeaders.Add("Skynet-Time", timeStamp + "");
                    // test not found
                    var responseNotFound = await client.DeleteAsync(baseUrl + "node/" + brotherNode.selfNode.uuid
                        + "/brotherNodes/" + node3.selfNode.uuid);
                    string resNotFound = await responseNotFound.Content.ReadAsStringAsync();
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(resNotFound);
                    Assert.AreEqual(NodeResponseCode.NotFound, res.statusCode);
                    // test delete
                    brotherNode.brotherNodes.Add(node3.selfNode);
                    var response = await client.DeleteAsync(baseUrl + "node/" + brotherNode.selfNode.uuid
                        + "/brotherNodes/" + node3.selfNode.uuid);
                    string resString = await response.Content.ReadAsStringAsync();
                    NodeResponse res1 = JsonConvert.DeserializeObject<NodeResponse>(resString);
                    Assert.AreEqual(NodeResponseCode.OK, res1.statusCode);
                    Assert.AreEqual(timeStamp, res1.time);
                    Assert.AreEqual(timeStamp, brotherNode.brotherModifiedTime);
                }
            }).GetAwaiter().GetResult();
        }
    }
}
