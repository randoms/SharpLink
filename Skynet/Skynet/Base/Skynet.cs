using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using SharpTox.Core;
using Skynet.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Skynet.Base
{
    public class Skynet
    {

        public Tox tox;
        private Dictionary<string, byte[]> mPackageCache = new Dictionary<string, byte[]>();
        object mPendingReqLock = new object();
        private Dictionary<string, Action<ToxResponse>> mPendingReqList = new Dictionary<string, Action<ToxResponse>>();
        public static int MAX_MSG_LENGTH = 1024;
        private List<string> connectedList = new List<string>();
        public int httpPort;
        private List<Action<ToxRequest>> reqCallbacks = new List<Action<ToxRequest>>();
        private object sendLock = new object();
        private object reqListnerLock = new object();
        private Queue<Package> reqQueue = new Queue<Package>();
        private object reqQueueLock = new object();

        
        public static List<Skynet> allInstance = new List<Skynet>();

        public Skynet()
        {
            // init tox client
            ToxOptions options = new ToxOptions(true, true);
            
            tox = new Tox(options);
            tox.OnFriendRequestReceived += tox_OnFriendRequestReceived;
            //tox.OnFriendMessageReceived += tox_OnFriendMessageReceived;
            tox.OnFriendLosslessPacketReceived += tox_OnFriendLosslessPacketReceived;
            tox.OnFriendConnectionStatusChanged += tox_OnFriendConnectionStatusChanged;

            foreach (ToxNode node in Nodes)
                tox.Bootstrap(node);

            tox.Name = "Skynet";
            tox.StatusMessage = "Running Skynet";
            tox.Start();

            string id = tox.Id.ToString();
            Console.WriteLine("ID: {0}", id);

            // Log tox online status
            Task.Run(() => {
                while (true) {
                    Thread.Sleep(200);
                    if (tox.IsConnected)
                    {
                        Console.WriteLine("From Server " + httpPort + ":" + "tox is connected.");
                        break;
                    }
                }

                while (true) {
                    // start queue process
                    while (tox.IsConnected)
                    {
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
                    Console.WriteLine("tox is offline");
                    Thread.Sleep(1000);
                }
            });

            

            // start http server
            httpPort = Utils.Utils.FreeTcpPort();
            string baseUrl = "http://localhost:" + httpPort + "/";
            WebApp.Start<StartUp>(url: baseUrl);
            Console.WriteLine("Server listening on " + httpPort);
            allInstance.Add(this);
        }

        ~Skynet() {
            tox.Dispose();
        }

        public void addNewReqListener(Action<ToxRequest> cb) {
            Task.Run(() => {
                lock (reqListnerLock)
                {
                    reqCallbacks.Add(cb);
                }
            });
        }

        public void removeNewReqListener(Action<ToxRequest> cb) {
            Task.Run(() =>
            {
                lock (reqListnerLock)
                {
                    reqCallbacks.Remove(cb);
                }
            });
        }

        static ToxNode[] Nodes = new ToxNode[]
        {
            new ToxNode("198.98.51.198", 33445, new ToxKey(ToxKeyType.Public, "1D5A5F2F5D6233058BF0259B09622FB40B482E4FA0931EB8FD3AB8E7BF7DAF6F"))
        };

        void tox_OnFriendLosslessPacketReceived(object sender, ToxEventArgs.FriendPacketEventArgs e)
        {
            //get the name associated with the friendnumber
            string name = tox.GetFriendName(e.FriendNumber);
            byte[] receivedData = new byte[e.Data.Length - 1];
            for (int i = 0; i < receivedData.Length; i++) {
                receivedData[i] = e.Data[i+1];
            }
            Package receivedPackage = Package.fromBytesStatic(receivedData);
            Console.WriteLine("Received package: " + receivedPackage.uuid);
            if (receivedPackage.currentCount == 0)
            {
                if(receivedPackage.totalCount == 1)
                {
                    lock (reqQueueLock) {
                        reqQueue.Enqueue(Package.fromBytes(receivedData));
                    }
                    return;
                }
                byte[] fullSizeContent = new byte[receivedPackage.totalSize];
                receivedPackage.content.CopyTo(fullSizeContent, 0);
                mPackageCache.Add(receivedPackage.uuid, fullSizeContent);
            }else if(receivedPackage.currentCount != receivedPackage.totalCount - 1)
            {
                receivedPackage.content.CopyTo(mPackageCache[receivedPackage.uuid], receivedPackage.startIndex);
            }
            else if(receivedPackage.currentCount == receivedPackage.totalCount - 1)
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
            Console.WriteLine("From Server " + httpPort + " ");
            Console.WriteLine("Received friend req: " + e.PublicKey);
        }

        void tox_OnFriendConnectionStatusChanged(object sender, ToxEventArgs.FriendConnectionStatusEventArgs e) {
            if (e.Status == ToxConnectionStatus.None) {
                // find target friend in all nodes
                Node.AllLocalNodes.ForEach((mnode) => {
                    List<NodeId> relatedNodes = mnode.childNodes.Concat(mnode.brotherNodes).ToList();
                    if(mnode.parent != null)
                        relatedNodes.Add(mnode.parent);
                    if(mnode.grandParents != null)
                        relatedNodes.Add(mnode.grandParents);
                    relatedNodes.
                    Where(x => x.toxid == tox.Id.ToString())
                    .ToList().ForEach(nodeToRemove => {
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
                    startIndex = (uint)(i*MAX_MSG_LENGTH),
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
            if (mPackageCache.ContainsKey(receivedPackage.uuid)) {
                mcontentCache = mPackageCache[receivedPackage.uuid];
                mPackageCache.Remove(receivedPackage.uuid);
            }
            receivedPackage.content.CopyTo(mcontentCache, receivedPackage.startIndex);
            // check if this is a response
            lock (mPendingReqLock) {
                if (mPendingReqList.ContainsKey(receivedPackage.uuid))
                {
                    mPendingReqList[receivedPackage.uuid](ToxResponse.fromBytes(mcontentCache));
                    mPendingReqList.Remove(receivedPackage.uuid);
                    return;
                }
            }
            ToxRequest newReq = ToxRequest.fromBytes(mcontentCache);
            List<Action<ToxRequest>> tempReqList;
            lock (reqListnerLock)
            {
                tempReqList = new List<Action<ToxRequest>>(reqCallbacks);
            }
            Console.WriteLine("Start callbacks");
            foreach (var cb in tempReqList)
            {
                cb(newReq);
            }
            Console.WriteLine("End callbacks");
        }

        public bool sendMsg(ToxKey toxkey, byte[] msg)
        {
            return sendMsg(new ToxId(toxkey.GetBytes(), 100), msg);
        }

        public bool sendMsg(ToxId toxid, byte[] msg)
        {
            lock (sendLock) {

                // check if this message is send to itself
                if (toxid.ToString() == tox.Id.ToString())
                {
                    return false; // this is not allowed
                }
                
                // wait toxcore online
                int maxOnlineWaitTime = 20000; // 20s
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
                
                int waitCount = 0;
                int maxCount = 500;
                if (connectedList.IndexOf(toxkey.GetString()) == -1)
                    maxCount = 200 * 1000; // first time wait for 200s
                while (tox.GetFriendConnectionStatus(friendNum) == ToxConnectionStatus.None && waitCount < maxCount)
                {
                    if (waitCount % 1000 == 0)
                        Console.WriteLine("target is offline." + waitCount / 1000);
                    waitCount += 10;
                    Thread.Sleep(10);
                }
                if (waitCount == maxCount)
                {
                    Console.WriteLine("Connect Failed");
                    connectedList.Remove(toxkey.GetString());
                    return false;
                }
                if (connectedList.IndexOf(toxkey.GetString()) == -1) {
                    connectedList.Add(toxkey.GetString());
                }
                
                var mesError = new ToxErrorFriendCustomPacket();
                // retry send message
                int retryCount = 0;
                while (retryCount < 10) {
                    byte[] msgToSend = new byte[msg.Length + 1];
                    msgToSend[0] = 170; // The first byte must be in the range 160-191.
                    msg.CopyTo(msgToSend, 1);
                    bool msgRes = tox.FriendSendLosslessPacket(friendNum, msgToSend, out mesError);
                    if (msgRes)
                    {
                        break;
                    }
                        
                    Console.WriteLine(mesError + " " + Utils.Utils.UnixTimeNow());
                    if (mesError == ToxErrorFriendCustomPacket.SendQ) {
                        Thread.Sleep(10);
                        continue;
                    }
                    retryCount++;
                    Thread.Sleep(100);

                }
                if (retryCount == 10)
                    return false;
                return true;
            }
        }

        public void sendRequestNoReplay(ToxId toxid, ToxRequest req, out bool status) {
            status = true;
            if (toxid.ToString() == tox.Id.ToString())
            {
                // request was sent to itself
                status = true;
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
                    startIndex = (uint)(i*MAX_MSG_LENGTH),
                }.toBytes());
                if (!res)
                {
                    status = false;
                }
            }
        }

        public Task<ToxResponse> sendRequest(ToxId toxid, ToxRequest req, out bool status) {

            if (toxid.ToString() == tox.Id.ToString()) {
                // request was sent to itself
                status = true;
                return RequestProxy.sendRequest(this, req);
            }

            byte[] reqContent = req.getBytes();
            int packageNum = reqContent.Length / MAX_MSG_LENGTH + 1;
            bool res = false;

            ToxResponse mRes = null;
            object reslock = new object();
            bool resFlag = false;
            lock (mPendingReqLock)
            {
                mPendingReqList.Add(req.uuid, (response) => {
                    mRes = response;
                    lock (reslock)
                    {
                        resFlag = true;
                        Console.WriteLine("Callback called: " + req.uuid);
                        Monitor.PulseAll(reslock);
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
                }.toBytes());
                if (!res) {
                    status = false;
                    return Task.Factory.StartNew<ToxResponse>(()=> {
                        mPendingReqList.Remove(req.uuid);
                        return null;
                    });
                }
            }
            status = res;
            
            return Task.Run(() => {
                lock (reslock) {
                    if(!resFlag)
                        Monitor.Wait(reslock);
                }
                Console.WriteLine("Response unlocked: " + req.uuid);
                return mRes;
            });
        }

        public void stop() {
            tox.Stop();
        }
    }
}
