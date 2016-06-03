using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;
using Skynet.Base.Contollers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Skynet.Models;
using System.Collections.Generic;

namespace SkynetTests
{
    [TestClass]
    public class ToxControllerTest
    {
        Skynet.Base.Skynet mSkynet;
        string baseUrl;

        public ToxControllerTest()
        {
            mSkynet = new Skynet.Base.Skynet();
            baseUrl = "http://localhost:" + mSkynet.httpPort + "/";
        }

        [TestMethod]
        public void GetLocalToxInfo()
        {
            Task.Run(async () =>
            {
                using (var client = new HttpClient())
                {
                    string res = await client.GetStringAsync(baseUrl + "api/tox/" + mSkynet.tox.Id.ToString());
                    NodeResponse mRes = JsonConvert.DeserializeObject<NodeResponse>(res);
                    Assert.AreEqual(mRes.statusCode, NodeResponseCode.OK);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void GetToxInfoNotFound() {
            Task.Run(async () =>
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(300); // This process may take up to 2mins
                    // create a node
                    Node mNode = new Node(new List<NodeId>(), mSkynet);
                    // set http headers
                    client.DefaultRequestHeaders.Add("Uuid", Guid.NewGuid().ToString());
                    client.DefaultRequestHeaders.Add("From-Node-Id", mNode.selfNode.uuid);
                    client.DefaultRequestHeaders.Add("From-Tox-Id", mNode.selfNode.toxid);
                    client.DefaultRequestHeaders.Add("To-Node-Id", mNode.selfNode.uuid);
                    client.DefaultRequestHeaders.Add("To-Tox-Id", "062AA695A9F3E8C6A6667E5BCD24B16ABF96F4775EE3E59764FDFC2453C4027A74FA2E4D26BC");
                    string res = await client.GetStringAsync(baseUrl + "api/tox/" + "062AA695A9F3E8C6A6667E5BCD24B16ABF96F4775EE3E59764FDFC2453C4027A74FA2E4D26BC"); // the tox id does not exist
                    NodeResponse mRes = JsonConvert.DeserializeObject<NodeResponse>(res);
                    Assert.AreEqual(mRes.statusCode, NodeResponseCode.NotFound);
                }
            }).GetAwaiter().GetResult();
            
        }

        [TestMethod]
        public void GetToxInfoInvalidId() {
            Task.Run(async () =>
            {
                using (var client = new HttpClient()) {
                    string res = await client.GetStringAsync(baseUrl + "api/tox/aaaaa");
                    NodeResponse mRes = JsonConvert.DeserializeObject<NodeResponse>(res);
                    Assert.AreEqual(mRes.statusCode, NodeResponseCode.InvalidRequest);
                }
            }).GetAwaiter().GetResult();
        }

        [TestMethod]
        public void GetToxInfoWithoutId() {
            Task.Run(async () => {
                using (var client = new HttpClient())
                {
                    string res = await client.GetStringAsync(baseUrl + "api/tox");
                    NodeResponse mRes = JsonConvert.DeserializeObject<NodeResponse>(res);
                    Assert.AreEqual(mRes.statusCode, NodeResponseCode.InvalidRequest);
                }
            });
        }
    }
}
