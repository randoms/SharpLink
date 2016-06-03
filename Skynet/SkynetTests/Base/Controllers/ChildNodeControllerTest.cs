using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skynet.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Skynet.Base.Contollers;
using Newtonsoft.Json;

namespace SkynetTests.Base.Controllers
{
    [TestClass]
    public class ChildNodeControllerTest
    {
        Skynet.Base.Skynet mSkynet;
        Node testNode;
        Node childNode;
        string baseUrl;

        public ChildNodeControllerTest() {
            mSkynet = new Skynet.Base.Skynet();
            testNode = new Node(mSkynet);
            baseUrl = "http://localhost:" + mSkynet.httpPort + "/api/";

            childNode = new Node(mSkynet);
        }

        [TestMethod]
        public void GetAllTest()
        {
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    string response = await client.GetStringAsync(baseUrl + "node/" + testNode.selfNode.uuid
                         + "/childNodes");
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(response);
                    Assert.AreEqual(res.statusCode, NodeResponseCode.OK);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void GetTest() {
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    testNode.childNodes.Add(childNode.selfNode);
                    string response = await client.GetStringAsync(baseUrl + "node/" + testNode.selfNode.uuid
                        + "/childNodes/" + childNode.selfNode.uuid);
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(response);
                    Assert.AreEqual(res.statusCode, NodeResponseCode.OK);
                    NodeId value = JsonConvert.DeserializeObject<NodeId>(res.value);
                    Assert.AreEqual(value.uuid, childNode.selfNode.uuid);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void GetNotFoundTest() {
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    string response = await client.GetStringAsync(baseUrl + "node/" + testNode.selfNode.uuid
                        + "/childNodes/" + Guid.NewGuid().ToString());
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(response);
                    Assert.AreEqual(res.statusCode, NodeResponseCode.NotFound);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void PostTest() {
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    var values = new Dictionary<string, string> {
                        { "uuid", childNode.selfNode.uuid},
                        { "toxid", childNode.selfNode.toxid},
                    };
                    long timeStamp = Skynet.Utils.Utils.UnixTimeNow();
                    client.DefaultRequestHeaders.Add("Skynet-Time", timeStamp + "");
                    var response = await client.PostAsync(baseUrl + "node/" + testNode.selfNode.uuid
                        + "/childNodes", new FormUrlEncodedContent(values));
                    string responseString = await response.Content.ReadAsStringAsync();
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(responseString);
                    Assert.AreEqual(res.statusCode, NodeResponseCode.OK);
                    NodeId newNode = JsonConvert.DeserializeObject<NodeId>(res.value);
                    Assert.AreEqual(newNode.uuid, childNode.selfNode.uuid);
                    Assert.AreEqual(timeStamp, res.time);
                    Assert.AreEqual(timeStamp, testNode.childNodesModifiedTime);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void DeleteTest() {
            Task.Run(async () => {
                using (var client = new HttpClient()) {
                    // add childnode first
                    var values = new Dictionary<string, string> {
                        { "uuid", childNode.selfNode.uuid},
                        { "toxid", childNode.selfNode.toxid},
                    };
                    long timeStamp = Skynet.Utils.Utils.UnixTimeNow();
                    client.DefaultRequestHeaders.Add("Skynet-Time", timeStamp + "");
                    var response = await client.PostAsync(baseUrl + "node/" + testNode.selfNode.uuid
                        + "/childNodes", new FormUrlEncodedContent(values));
                    string responseString = await response.Content.ReadAsStringAsync();

                    var deleteResponse = await client.DeleteAsync(baseUrl + "node/" + testNode.selfNode.uuid
                        + "/childNodes/" + childNode.selfNode.uuid);
                    string deleteResponseString = await deleteResponse.Content.ReadAsStringAsync();
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(deleteResponseString);
                    Assert.AreEqual(res.statusCode, NodeResponseCode.OK);
                    Assert.AreEqual(timeStamp, res.time);
                    Assert.AreEqual(timeStamp, testNode.childNodesModifiedTime);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void PutTest() {
            Task.Run(async() => {
                using (var client = new HttpClient()) {

                    testNode.childNodes.Add(childNode.selfNode);
                    long timeStamp = Skynet.Utils.Utils.UnixTimeNow();
                    client.DefaultRequestHeaders.Add("Skynet-Time", timeStamp + "");
                    var values = new Dictionary<string, string> {
                        { "uuid", childNode.selfNode.uuid },
                        { "toxid", childNode.selfNode.toxid },
                    };

                    var response = await client.PutAsync(baseUrl + "node/" + testNode.selfNode.uuid
                        + "/childNodes/" + childNode.selfNode.uuid, new FormUrlEncodedContent(values));
                    string putResponseString = await response.Content.ReadAsStringAsync();
                    NodeResponse res = JsonConvert.DeserializeObject<NodeResponse>(putResponseString);
                    Assert.AreEqual(res.statusCode, NodeResponseCode.OK);
                    Assert.AreEqual(timeStamp, res.time);
                    Assert.AreEqual(timeStamp, testNode.childNodesModifiedTime);
                }
            }).GetAwaiter().GetResult();
        }
    }
}
