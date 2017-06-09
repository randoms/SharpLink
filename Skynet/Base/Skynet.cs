using SharpTox.Core;
using Skynet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace Skynet.Base
{
    public static class TaskExtension
    {
        public static void ForgetOrThrow(this Task task)
        {
            task.ContinueWith((t) =>
            {
                Console.WriteLine(t.Exception);
                Utils.Utils.Log(t.Exception.ToString());
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    public class Skynet
    {

        public Tox tox;
        object mPackageCacheLock = new object();
        private Dictionary<string, byte[]> mPackageCache = new Dictionary<string, byte[]>();
        object mPendingReqLock = new object();
        private Dictionary<string, Action<ToxResponse>> mPendingReqList = new Dictionary<string, Action<ToxResponse>>();
        public static int MAX_MSG_LENGTH = 1024;
        public int httpPort;
        private Dictionary<string, Action<ToxRequest>> reqCallbacks = new Dictionary<string, Action<ToxRequest>>();
        private object sendLock = new object();
        private object reqListnerLock = new object();
        private Queue<Package> reqQueue = new Queue<Package>();
        private object reqQueueLock = new object();


        public static List<Skynet> allInstance = new List<Skynet>();

        public Skynet(string filename = "")
        {
            // init tox client
            ToxOptions options = new ToxOptions(true, true);
            if (filename != "")
            {
                tox = new Tox(options, ToxData.FromDisk(filename));
            }
            else
            {
                tox = new Tox(options);
            }



            tox.OnFriendRequestReceived += tox_OnFriendRequestReceived;
            tox.OnFriendLosslessPacketReceived += tox_OnFriendLosslessPacketReceived;
            tox.OnFriendConnectionStatusChanged += tox_OnFriendConnectionStatusChanged;

            foreach (ToxNode node in Nodes)
                tox.Bootstrap(node);

            tox.Name = "Skynet";
            tox.StatusMessage = "Running Skynet";
            tox.Start();

            string id = tox.Id.ToString();
            Console.WriteLine("ID: {0}", id);
            Utils.Utils.Log("ID: " + id);

            // Log tox online status
            Task.Factory.StartNew(() =>
            {
                var offLineCount = 0;
                while (true)
                {
                    Thread.Sleep(2000);
                    if (tox.IsConnected)
                    {
                        Console.WriteLine("From Server " + httpPort + ":" + "tox is connected.");
                        Utils.Utils.Log("From Server " + httpPort + ":" + "tox is connected.", true);
                        offLineCount = 0;
                        break;
                    }
                    else
                    {
                        Utils.Utils.Log("Event: tox is offline", true);
                        offLineCount++;
                    }
                    if (offLineCount > 10)
                    {
                        // start a new tox node
                        offLineCount = 0;
                        tox.Stop();
                        options = new ToxOptions(true, true);
                        if (filename != "")
                        {
                            tox = new Tox(options, ToxData.FromDisk(filename));
                        }
                        else
                        {
                            tox = new Tox(options);
                        }

                        tox.OnFriendRequestReceived += tox_OnFriendRequestReceived;
                        tox.OnFriendLosslessPacketReceived += tox_OnFriendLosslessPacketReceived;
                        tox.OnFriendConnectionStatusChanged += tox_OnFriendConnectionStatusChanged;

                        foreach (ToxNode node in Nodes)
                            tox.Bootstrap(node);

                        tox.Name = "Skynet";
                        tox.StatusMessage = "Running Skynet";
                        tox.Start();

                        id = tox.Id.ToString();
                        Console.WriteLine("ID: {0}", id);
                        Console.WriteLine("Start a new Tox node");
                        Utils.Utils.Log("ID: " + id);
                    }
                }

                bool onlineStatus = true;
                while (true)
                {
                    // start queue process
                    while (tox.IsConnected)
                    {
                        if (!onlineStatus)
                        {
                            onlineStatus = true;
                            Utils.Utils.Log("Event: tox is online");
                        }
                        Package processPack = null;
                        lock (reqQueueLock)
                        {
                            if (reqQueue.Count > 0)
                            {
                                processPack = reqQueue.Dequeue();
                            }
                        }
                        if (processPack != null)
                        {
                            newReqReceived(processPack);
                        }
                        else
                            Thread.Sleep(1);
                    }
                    Utils.Utils.Log("Event: tox is offline", true);
                    onlineStatus = false;
                    Thread.Sleep(1000);
                }
            }, TaskCreationOptions.LongRunning).ForgetOrThrow();



            // start http server
            httpPort = Utils.Utils.FreeTcpPort();
            string baseUrl = "http://localhost:" + httpPort + "/";
            //WebApp.Start<StartUp> (url: baseUrl);
            Console.WriteLine("Server listening on " + httpPort);
            Utils.Utils.Log("Server listening on " + httpPort, true);
            allInstance.Add(this);
        }

        ~Skynet()
        {
            tox.Dispose();
        }

        public void Save(string filename)
        {
            tox.GetData().Save(filename);
        }

        public void addNewReqListener(string nodeid, Action<ToxRequest> cb)
        {
            lock (reqListnerLock)
            {
                reqCallbacks.Add(nodeid, cb);
            }
        }

        public void removeNewReqListener(string nodeid)
        {
            lock (reqListnerLock)
            {
                reqCallbacks.Remove(nodeid);
            }
        }

        static ToxNode[] Nodes = new ToxNode[] {
            new ToxNode ("119.23.239.31", 33445, new ToxKey (ToxKeyType.Public, "7F613A23C9EA5AC200264EB727429F39931A86C39B67FC14D9ECA4EBE0D37F25"))
        };

        void tox_OnFriendLosslessPacketReceived(object sender, ToxEventArgs.FriendPacketEventArgs e)
        {
            //get the name associated with the friendnumber
            byte[] receivedData = new byte[e.Data.Length - 1];
            for (int i = 0; i < receivedData.Length; i++)
            {
                receivedData[i] = e.Data[i + 1];
            }
            Package receivedPackage = Package.fromBytesStatic(receivedData);
            if (receivedPackage.currentCount == 0)
            {
                if (receivedPackage.totalCount == 1)
                {
                    lock (reqQueueLock)
                    {
                        reqQueue.Enqueue(Package.fromBytes(receivedData));
                    }
                    return;
                }
                byte[] fullSizeContent = new byte[receivedPackage.totalSize];
                receivedPackage.content.CopyTo(fullSizeContent, 0);
                lock (mPackageCacheLock)
                {
                    mPackageCache.Add(receivedPackage.uuid, fullSizeContent);
                }

            }
            else if (receivedPackage.currentCount != receivedPackage.totalCount - 1)
            {
                lock (mPackageCacheLock)
                {
                    receivedPackage.content.CopyTo(mPackageCache[receivedPackage.uuid], receivedPackage.startIndex);
                }
            }
            else if (receivedPackage.currentCount == receivedPackage.totalCount - 1)
            {
                lock (reqQueueLock)
                {
                    reqQueue.Enqueue(Package.fromBytes(receivedData));
                }
            }
        }

        void tox_OnFriendRequestReceived(object sender, ToxEventArgs.FriendRequestEventArgs e)
        {
            //automatically accept every friend request we receive
            tox.AddFriendNoRequest(e.PublicKey);
            Console.WriteLine("Received friend req: " + e.PublicKey);
            Utils.Utils.Log("From Server " + httpPort + " ");
            Utils.Utils.Log("Received friend req: " + e.PublicKey, true);
        }

        void tox_OnFriendConnectionStatusChanged(object sender, ToxEventArgs.FriendConnectionStatusEventArgs e)
        {
            Console.WriteLine("Friend connection status changed");
            if (e.Status == ToxConnectionStatus.None)
            {
                // find target friend in all nodes
                Node.AllLocalNodes.ForEach((mnode) =>
                {
                    List<NodeId> relatedNodes = mnode.childNodes.Concat(mnode.brotherNodes).ToList();
                    if (mnode.parent != null)
                        relatedNodes.Add(mnode.parent);
                    if (mnode.grandParents != null)
                        relatedNodes.Add(mnode.grandParents);
                    relatedNodes.
                    Where(x => x.toxid == tox.Id.ToString())
                    .ToList().ForEach(nodeToRemove =>
                    {
                        mnode.relatedNodesStatusChanged(nodeToRemove);
                    });
                });
            }
        }

        public bool sendResponse(ToxResponse res, ToxId toxid)
        {

            byte[] resContent = res.getBytes();
            int packageNum = resContent.Length / MAX_MSG_LENGTH + 1;
            bool result = false;
            for (int i = 0; i < packageNum; i++)
            {
                byte[] mcontent;
                if (i * MAX_MSG_LENGTH + MAX_MSG_LENGTH > resContent.Length)
                    mcontent = Utils.Utils.subArray(resContent, i * MAX_MSG_LENGTH);
                else
                    mcontent = Utils.Utils.subArray(resContent, i * MAX_MSG_LENGTH, MAX_MSG_LENGTH);
                result = sendMsg(toxid, new Package
                {
                    uuid = res.uuid,
                    totalCount = packageNum,
                    currentCount = i,
                    content = mcontent,
                    totalSize = (uint)resContent.Length,
                    startIndex = (uint)(i * MAX_MSG_LENGTH),
                }.toBytes());
            }
            return result;
        }

        public bool sendResponse(ToxResponse res, ToxKey toxkey)
        {
            return sendResponse(res, new ToxId(toxkey.GetBytes(), 100));
        }

        void newReqReceived(Package receivedPackage)
        {
            byte[] mcontentCache = new byte[receivedPackage.totalSize];
            lock (mPackageCacheLock)
            {
                if (mPackageCache.ContainsKey(receivedPackage.uuid))
                {
                    mcontentCache = mPackageCache[receivedPackage.uuid];
                    mPackageCache.Remove(receivedPackage.uuid);
                }
            }
            receivedPackage.content.CopyTo(mcontentCache, receivedPackage.startIndex);
            // check if this is a response
            lock (mPendingReqLock)
            {
                if (mPendingReqList.ContainsKey(receivedPackage.uuid))
                {
                    mPendingReqList[receivedPackage.uuid](ToxResponse.fromBytes(mcontentCache));
                    mPendingReqList.Remove(receivedPackage.uuid);
                    return;
                }
            }
            ToxRequest newReq = ToxRequest.fromBytes(mcontentCache);
            if (newReq.method == "")
            {
                Console.WriteLine("this happends, this is an ugly hack");
                return;
            }

            if (newReq == null)
            {
                Utils.Utils.Log("Event: Invalid Request Data: receivedPackage " + receivedPackage.uuid);
                return;
            }
            Utils.Utils.Log("Event: Start callbacks");
            Utils.Utils.Log("Event: Begin Process MessageID: " + newReq.uuid);
            if (newReq.url == "/msg")
                Utils.Utils.Log("Event: Message toNodeID: " + newReq.toNodeId + ", totoxid:" + newReq.toToxId);
            lock (reqListnerLock)
            {
                if (reqCallbacks.Keys.Contains(newReq.toNodeId))
                {
                    reqCallbacks[newReq.toNodeId](newReq);
                }
            }
            Utils.Utils.Log("Event: End callbacks");
        }

        public bool sendMsg(ToxKey toxkey, byte[] msg, int timeout = 20)
        {
            return sendMsg(new ToxId(toxkey.GetBytes(), 100), msg, timeout);
        }

        public bool sendMsg(ToxId toxid, byte[] msg, int timeout = 20)
        {
            try
            {
                lock (sendLock)
                {
                    // check if this message is send to itself
                    if (toxid.ToString() == tox.Id.ToString())
                    {
                        return false; // this is not allowed
                    }

                    // wait toxcore online
                    int maxOnlineWaitTime = 200000; // 200s
                    int onlineWaitCount = 0;
                    while (!tox.IsConnected)
                    {
                        Thread.Sleep(10);
                        onlineWaitCount += 10;
                        if (onlineWaitCount > maxOnlineWaitTime)
                            return false;
                    }

                    ToxKey toxkey = toxid.PublicKey;
                    int friendNum = tox.GetFriendByPublicKey(toxkey);
                    if (friendNum == -1)
                    {
                        int res = tox.AddFriend(toxid, "add friend");
                        if (res != (int)ToxErrorFriendAdd.Ok)
                            return false;
                        friendNum = tox.GetFriendByPublicKey(toxkey);
                    }

                    if (tox.GetFriendConnectionStatus(friendNum) == ToxConnectionStatus.None)
                        return false;

                    var mesError = new ToxErrorFriendCustomPacket();
                    // retry send message
                    int retryCount = 0;
                    while (retryCount < 60)
                    {
                        byte[] msgToSend = new byte[msg.Length + 1];
                        msgToSend[0] = 170; // The first byte must be in the range 160-191.
                        msg.CopyTo(msgToSend, 1);
                        bool msgRes = tox.FriendSendLosslessPacket(friendNum, msgToSend, out mesError);
                        if (msgRes)
                        {
                            break;
                        }

                        Utils.Utils.Log("Event: " + mesError, true);
                        Console.WriteLine(Utils.Utils.UnixTimeNow() + " Event: " + mesError);
                        if (mesError == ToxErrorFriendCustomPacket.SendQ)
                        {
                            Thread.Sleep(100);
                            continue;
                        }
                        retryCount++;
                        Thread.Sleep(100);

                    }
                    if (retryCount == 60)
                    {
                        tox.DeleteFriend(friendNum);
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                Utils.Utils.Log(e.StackTrace, true);
                return false;
            }

        }

        public void sendRequestNoReplay(ToxId toxid, ToxRequest req, out bool status)
        {
            status = true;

            try
            {
                if (toxid.ToString() == tox.Id.ToString())
                {
                    // request was sent to itself
                    status = true;
                }
            }
            catch (ObjectDisposedException)
            {
                status = false;
                return;
            }


            byte[] reqContent = req.getBytes();
            int packageNum = reqContent.Length / MAX_MSG_LENGTH + 1;
            bool res = false;
            for (int i = 0; i < packageNum; i++)
            {
                byte[] mcontent = null;
                if (i * MAX_MSG_LENGTH + MAX_MSG_LENGTH > reqContent.Length)
                    mcontent = Utils.Utils.subArray(reqContent, i * MAX_MSG_LENGTH);
                else
                    mcontent = Utils.Utils.subArray(reqContent, i * MAX_MSG_LENGTH, MAX_MSG_LENGTH);
                res = sendMsg(toxid, new Package
                {
                    uuid = req.uuid,
                    totalCount = packageNum,
                    currentCount = i,
                    content = mcontent,
                    totalSize = (uint)reqContent.Length,
                    startIndex = (uint)(i * MAX_MSG_LENGTH),
                }.toBytes());
                if (!res)
                {
                    status = false;
                }
            }
        }

        public Task<ToxResponse> sendRequest(ToxId toxid, ToxRequest req, out bool status, int timeout = 200)
        {
            try
            {
                if (toxid.ToString() == tox.Id.ToString())
                {
                    // request was sent to itself
                    status = true;
                    return RequestProxy.sendRequest(this, req);
                }
            }
            catch (ObjectDisposedException)
            {
                status = false;
                return Task.Factory.StartNew<ToxResponse>(() =>
                {
                    return null;
                }, TaskCreationOptions.LongRunning);
            }



            byte[] reqContent = req.getBytes();
            int packageNum = reqContent.Length / MAX_MSG_LENGTH + 1;
            bool res = false;

            ToxResponse mRes = null;
            object reslock = new object();
            bool resFlag = false;
            lock (mPendingReqLock)
            {
                mPendingReqList.Add(req.uuid, (response) =>
                {
                    mRes = response;
                    lock (reslock)
                    {
                        resFlag = true;
                        Utils.Utils.Log("Event: Callback called, ReqId: " + req.uuid);
                        Monitor.PulseAll(reslock);
                        Utils.Utils.Log("Event: Pulse Lock, ReqId: " + req.uuid);
                    }
                });
            }

            for (int i = 0; i < packageNum; i++)
            {
                byte[] mcontent;
                if (i * MAX_MSG_LENGTH + MAX_MSG_LENGTH > reqContent.Length)
                    mcontent = Utils.Utils.subArray(reqContent, i * MAX_MSG_LENGTH);
                else
                    mcontent = Utils.Utils.subArray(reqContent, i * MAX_MSG_LENGTH, MAX_MSG_LENGTH);
                res = sendMsg(toxid, new Package
                {
                    uuid = req.uuid,
                    totalCount = packageNum,
                    currentCount = i,
                    content = mcontent,
                    totalSize = (uint)reqContent.Length,
                    startIndex = (uint)(i * MAX_MSG_LENGTH),
                }.toBytes(), timeout);

                if (!res)
                {
                    status = false;
                    return Task.Factory.StartNew<ToxResponse>(() =>
                    {
                        lock (mPendingReqLock)
                        {
                            mPendingReqList.Remove(req.uuid);
                        }
                        return null;
                    }, TaskCreationOptions.LongRunning);
                }
            }
            status = res;

            Utils.Utils.Log("Event: return async, ReqId: " + req.uuid);

            return Task.Factory.StartNew(() =>
            {
                Task.Run(() =>
                {
                    // timeout count thread
                    int timeoutCount = 0;
                    while (timeoutCount < timeout * 1000)
                    {
                        Thread.Sleep(100);
                        timeoutCount += 100;
                        lock (mPendingReqLock)
                        {
                            if (!mPendingReqList.Keys.Contains(req.uuid))
                            {
                                // already received response
                                return;
                            }
                        }
                    }
                    Console.WriteLine(Utils.Utils.UnixTimeNow() + "Timeout Happends");
                    lock (mPendingReqLock)
                    {
                        if (mPendingReqList.Keys.Contains(req.uuid))
                        {
                            mRes = null;
                            mPendingReqList.Remove(req.uuid);
                            lock (reslock)
                            {
                                resFlag = true;
                                Utils.Utils.Log("Event: Callback Timeout, ReqId: " + req.uuid);
                                Monitor.PulseAll(reslock);
                                Utils.Utils.Log("Event: Pulse Lock, ReqId: " + req.uuid);
                            }
                        }
                    }
                });
                Utils.Utils.Log("Event: Response locked, ReqId: " + req.uuid);
                lock (reslock)
                {
                    while (!resFlag)
                        Monitor.Wait(reslock);
                }
                Utils.Utils.Log("Event: Response unlocked, ReqId: " + req.uuid);
                return mRes;
            }, TaskCreationOptions.LongRunning);
        }

        public async Task<bool> HandShake(ToxId target, int timeout = 20)
        {
            string reqid = Guid.NewGuid().ToString();
            Utils.Utils.Log("Event: Start Handshake , ReqId: " + reqid, true);
            Console.WriteLine("Event: Start Handshake , ReqId: " + reqid);
            bool status;
            var res = await sendRequest(target, new ToxRequest
            {
                url = "/handshake",
                method = "get",
                uuid = reqid,
                fromNodeId = reqid,
                fromToxId = tox.Id.ToString(),
                toToxId = target.ToString(),
                toNodeId = "",
                time = Utils.Utils.UnixTimeNow(),
            }, out status, timeout);

            if (res == null)
            {
                Utils.Utils.Log("Event: Handshake Failed, ReqId: " + reqid, true);
                Console.WriteLine("Event: Handshake failed, ReqId: " + reqid);
                return false;
            }
            else
            {
                Utils.Utils.Log("Event: Handshake Success, ReqId: " + reqid, true);
                Console.WriteLine("Event: Handshake Success, ReqId: " + reqid);
                return true;
            }
        }

        public void stop()
        {
            tox.Stop();
        }
    }
}
