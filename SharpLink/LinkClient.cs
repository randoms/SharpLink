using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpTox.Core;
using System.Net;
using Skynet.Models;
using System.Threading;
using Skynet.Utils;

namespace SharpLink
{
    class LinkClient
    {
        public string targetToxId;
        public ToxId serverToxId;
        public IPAddress ip;
        public int port;
        private Skynet.Base.Skynet mSkynet;
        public string clientId;
        public string serverId;
        private Action<byte[]> msgHandler;
        private Action<Exception> errorHander;
        private Action closeHandler;

        public LinkClient(Skynet.Base.Skynet mSkynet, string targetToxId, IPAddress ip, int port)
        {
            this.targetToxId = targetToxId;
            this.ip = ip;
            this.port = port;
            serverToxId = new ToxId(targetToxId);
            clientId = Guid.NewGuid().ToString();
            this.mSkynet = mSkynet;
        }

        private async Task<bool> HandShake()
        {
            string reqid = Guid.NewGuid().ToString();
            Utils.Log("Event: Start Handshake, ReqId: " + reqid + ", ClientId: " + clientId);
            Console.WriteLine("Event: Start Handshake, ReqId: " + reqid + ", ClientId: " + clientId);
            bool status;
            var res = await mSkynet.sendRequest(serverToxId, new ToxRequest
            {
                url = "/handshake",
                method = "get",
                uuid = reqid,
                fromNodeId = clientId,
                fromToxId = mSkynet.tox.Id.ToString(),
                toToxId = serverToxId.ToString(),
                toNodeId = "",
                time = Utils.UnixTimeNow(),
            }, out status);

            if (res == null)
            {
                Utils.Log("Event: Handshake Failed, ReqId: " + reqid + ", ClientId: " + clientId);
                return false;
            }
            else
            {
                Utils.Log("Event: Handshake Success, ReqId: " + reqid + ", ClientId: " + clientId);
                Console.WriteLine("Event: Handshake Success, ReqId: " + reqid + ", ClientId: " + clientId);
                return true;
            }

        }

        private async Task<bool> Connect()
        {
            mSkynet.addNewReqListener(newReqListener);
            Utils.Log("Event: callback added ClientID: " + clientId);
            bool status;
            string requuid = Guid.NewGuid().ToString();
            Utils.Log("Event: Start connect, ReqId: " + requuid + ", ClientId: " + clientId);
            var res = await mSkynet.sendRequest(new ToxId(targetToxId), new ToxRequest
            {
                url = "/connect",
                method = "get",
                uuid = requuid,
                fromNodeId = clientId,
                fromToxId = mSkynet.tox.Id.ToString(),
                toToxId = targetToxId,
                toNodeId = "",
                content = Encoding.UTF8.GetBytes(ip.ToString() + "\n" + port),
                time = Utils.UnixTimeNow(),
            }, out status);
            if (res == null || Encoding.UTF8.GetString(res.content) == "failed")
            {
                mSkynet.removeNewReqListener(newReqListener);
                Utils.Log("Event: Connect failed, ReqId: " + requuid + ", ClientId: " + clientId);
                return false;
            }
            Utils.Log("Event: Connect success, ReqId: " + requuid + ", ClientId: " + clientId);
            serverId = res.fromNodeId;
            return true;
        }

        public static LinkClient Connect(Skynet.Base.Skynet mSkynet, string targetToxId, IPAddress ip, int port,
            Action<byte[]> msgHandler,
            Action closeHandler,
            Action<Exception> errorHander = null)
        {
            LinkClient mLinkClient = new LinkClient(mSkynet, targetToxId, ip, port);
            mLinkClient.msgHandler = msgHandler;
            mLinkClient.closeHandler = closeHandler;
            mLinkClient.errorHander = errorHander;
            var res = mLinkClient.HandShake().GetAwaiter().GetResult();
            if (!res)
            {
                // 链接tox失败
                return null;
            }
            var connectRes = mLinkClient.Connect().GetAwaiter().GetResult();

            if (!connectRes)
            {
                // 创建socket失败
                return null;
            }
            return mLinkClient;
        }

        public static LinkClient Connect(Skynet.Base.Skynet mSkynet, string targetToxId, string targetNodeID)
        {
            LinkClient mLinkClient = new LinkClient(mSkynet, targetToxId, null, 0);
            mLinkClient.serverId = targetNodeID;
            mLinkClient.serverToxId = new ToxId(targetToxId);
            mLinkClient.mSkynet.addNewReqListener(mLinkClient.newReqListener);
            return mLinkClient;
        }

        public bool Send(byte[] msg, int size)
        {
            string msgGuidStr = Guid.NewGuid().ToString();
            Utils.Log("Event: Send Message, ClientId: " + clientId + ", ReqId: " + msgGuidStr);

            bool status;
            mSkynet.sendRequestNoReplay(new ToxId(targetToxId), new ToxRequest
            {
                url = "/msg",
                method = "get",
                uuid = msgGuidStr,
                fromNodeId = clientId,
                fromToxId = mSkynet.tox.Id.ToString(),
                toToxId = targetToxId,
                toNodeId = serverId,
                time = Skynet.Utils.Utils.UnixTimeNow(),
                content = msg.Take(size).ToArray(),
            }, out status);
            if (!status && errorHander != null)
                errorHander(new Exception("send message failed"));
            return status;
        }

        public bool Send(byte[] msg, int size, int retryCount)
        {
            int count = 0;
            while (count < retryCount)
            {
                var res = Send(msg, size);
                if (res)
                    break;
                count++;
                Thread.Sleep(10);
            }
            if (count == retryCount)
                return false;
            else
                return true;
        }

        public void OnMessage(Action<byte[]> msgHandler)
        {
            this.msgHandler = msgHandler;
        }

        public void newReqListener(ToxRequest req)
        {
            Utils.Log("Event: newReqListener  MessageID: " + req.uuid);
            if (req.toNodeId == clientId && req.url == "/msg" && req.fromNodeId != serverId)
            {
                // message arrived before connection callback resolved
                serverId = req.fromNodeId;
            }

            if (req.toNodeId == clientId && req.fromNodeId == serverId && req.url == "/msg")
            {
                Utils.Log("Event: Received Message, ClientId: " + clientId + ", MessageId: " + req.uuid);
                msgHandler(req.content);
            }
            if (req.toNodeId == clientId && req.fromNodeId == serverId && req.url == "/close")
            {
                Utils.Log("Event: Received Close, ClientId: " + clientId + ", MessageId: " + req.uuid);
                closeHandler();
                Close();
            }
        }

        public void Close()
        {
            mSkynet.removeNewReqListener(newReqListener);
        }

        public void CloseRemote()
        {
            bool status;
            mSkynet.sendRequestNoReplay(serverToxId, new ToxRequest
            {
                url = "/close",
                method = "get",
                uuid = Guid.NewGuid().ToString(),
                fromNodeId = clientId,
                fromToxId = mSkynet.tox.Id.ToString(),
                toToxId = serverToxId.ToString(),
                toNodeId = serverId,
                time = Skynet.Utils.Utils.UnixTimeNow(),
            }, out status);
        }

        public void OnClose(Action closeHandler)
        {
            this.closeHandler = closeHandler;
        }

        public void OnError(Action<Exception> errorHandler)
        {
            this.errorHander = errorHandler;
        }
    }
}
