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
        private Queue<ToxRequest> messageQueue;
        private object messageQueueLock = new object();
        private bool runningFlag = true;
        private DateTime lastActiveTime;

        public LinkClient(Skynet.Base.Skynet mSkynet, string targetToxId, IPAddress ip, int port)
        {
            this.targetToxId = targetToxId;
            this.ip = ip;
            this.port = port;
            serverToxId = new ToxId(targetToxId);
            clientId = Guid.NewGuid().ToString();
            this.mSkynet = mSkynet;
            messageQueue = new Queue<ToxRequest>();
            lastActiveTime = DateTime.UtcNow;

            // send message to local loop
            Task.Run(() =>
            {
                ToxRequest mReq;
                // if idle for 600s, shutdown
                while (runningFlag && (long)(DateTime.UtcNow - lastActiveTime).TotalMilliseconds < 600 * 1000)
                {
                    mReq = getRequestToSend();
                    if (mReq != null && msgHandler != null)
                    {
                        lastActiveTime = DateTime.UtcNow;
                        msgHandler(mReq.content);
                        Utils.Log("Event: Received Message Complete, ClientId: " + clientId + ", MessageId: " + mReq.uuid);
                        var response = mReq.createResponse(Encoding.UTF8.GetBytes("OK"));
                        mSkynet.sendResponse(response, new ToxId(response.toToxId));
                    }
                    else
                        Thread.Sleep(1);
                }
            });
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
            mSkynet.addNewReqListener(clientId, newReqListener);
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
                mSkynet.removeNewReqListener(clientId);
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
                mLinkClient.Close();
                return null;
            }
            var connectRes = mLinkClient.Connect().GetAwaiter().GetResult();

            if (!connectRes)
            {
                // 创建socket失败
                mLinkClient.Close();
                return null;
            }
            return mLinkClient;
        }

        public static LinkClient Connect(Skynet.Base.Skynet mSkynet, string targetToxId, string targetNodeID)
        {
            LinkClient mLinkClient = new LinkClient(mSkynet, targetToxId, null, 0);
            mLinkClient.serverId = targetNodeID;
            mLinkClient.serverToxId = new ToxId(targetToxId);
            mLinkClient.mSkynet.addNewReqListener(mLinkClient.clientId, mLinkClient.newReqListener);
            return mLinkClient;
        }

        public bool Send(byte[] msg, int size)
        {
            lastActiveTime = DateTime.UtcNow;
            string msgGuidStr = Guid.NewGuid().ToString();
            Utils.Log("Event: Send Message, ClientId: " + clientId + ", ReqId: " + msgGuidStr);

            bool status;
            var res = mSkynet.sendRequest(new ToxId(targetToxId), new ToxRequest
            {
                url = "/msg",
                method = "get",
                uuid = msgGuidStr,
                fromNodeId = clientId,
                fromToxId = mSkynet.tox.Id.ToString(),
                toToxId = targetToxId,
                toNodeId = serverId,
                time = Utils.UnixTimeNow(),
                content = msg.Take(size).ToArray(),
            }, out status, 1000).GetAwaiter().GetResult();
            if (!status && errorHander != null)
                errorHander(new Exception("send message failed"));
            if (res == null)
                return false;
            else
                return true;
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
                sendRequestLocal(req);
            }
            if (req.toNodeId == clientId && req.fromNodeId == serverId && req.url == "/close")
            {
                Utils.Log("Event: Received Close, ClientId: " + clientId + ", MessageId: " + req.uuid);
                closeHandler();
                Close();
            }
        }

        private void sendRequestLocal(ToxRequest req)
        {
            // 把变量保存至本地
            lock (messageQueueLock)
            {
                messageQueue.Enqueue(req);
            }
        }

        private ToxRequest getRequestToSend()
        {
            lock (messageQueue)
            {
                if (messageQueue.Count > 0)
                    return messageQueue.Dequeue();
            }
            return null;
        }

        public void Close()
        {
            mSkynet.removeNewReqListener(clientId);
            runningFlag = false;
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
                time = Utils.UnixTimeNow(),
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
