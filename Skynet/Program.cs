using System;
using System.Threading.Tasks;
using Skynet.Models;
using System.Collections.Generic;
using SharpTox.Core;
using Skynet.Base.Contollers;
using Newtonsoft.Json;
using System.Threading;
using System.Text;
using System.Net;
using System.IO;
using Skynet.Base;

namespace Skynet
{
    class Program
    {

        static void Main(string[] args)
        {
            Base.Skynet mSkynet = new Base.Skynet();
            Base.Skynet mSkynet2 = new Base.Skynet();
            Node node1 = new Node(mSkynet);
            Node node3 = new Node(mSkynet);
            Node node2 = new Node(mSkynet2);

            Task.Run(async () => {
                // add node1 to node2's childnodes
                NodeResponse res = await node1.sendRequest(node2.selfNode,
                    JsonConvert.SerializeObject(node1.selfNode), "post",
                    "node/" + node2.selfNode.uuid + "/childNodes", Utils.Utils.UnixTimeNow());
                NodeResponse setParentRes = await node1.sendRequest(node1.selfNode,
                    JsonConvert.SerializeObject(node2.selfNode), "put",
                    "node/" + node1.selfNode.uuid + "/parent", Utils.Utils.UnixTimeNow());
                NodeResponse addChildToNode3 = await node2.sendRequest(node3.selfNode,
                    JsonConvert.SerializeObject(node2.selfNode), "post",
                    "node/" + node3.selfNode.uuid + "/childNodes", Utils.Utils.UnixTimeNow());
                NodeResponse setParentNode2 = await node2.sendRequest(node2.selfNode,
                    JsonConvert.SerializeObject(node3.selfNode), "put",
                    "node/" + node2.selfNode.uuid + "/parent", Utils.Utils.UnixTimeNow());
                // stop mskynet2
                mSkynet2.stop();
                Console.ReadLine();
            }).GetAwaiter().GetResult();
        }
    }
}