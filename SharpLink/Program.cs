using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skynet.Base;
using System.Net;
using System.Net.Sockets;
using SharpTox.Core;
using System.Threading;
using Newtonsoft.Json;
using System.IO;

namespace SharpLink
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 4 && args.Length != 0) {
                Console.WriteLine("usage: SharpLink [local_port] [target_tox_id] [target_ip] [target_port]");
            }

            Skynet.Base.Skynet mSkynet = new Skynet.Base.Skynet();
            if (args.Length == 4) {
                string localPort = args[0];
                string targetToxId = args[1];
                string targetIP = args[2];
                int targetPort = Convert.ToInt32(args[3]);

                // create local socket server
                IPAddress ip = IPAddress.Parse("127.0.0.1");
                var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverSocket.Bind(new IPEndPoint(ip, Convert.ToInt32(localPort)));
                serverSocket.Listen(1000);
                Task.Factory.StartNew(() => {
                    while (true)
                    {
                        Console.WriteLine("Waiting socket");
                        List<byte> tempData = new List<byte>();
                        Socket clientSocket = serverSocket.Accept();
                        Task.Factory.StartNew(() => {
                            bool closeFlag = false;
                            LinkClient mlink = null;
                            Task.Factory.StartNew(() =>
                            {
                                while (true)
                                {
                                    byte[] buf = new byte[1024];
                                    try
                                    {
                                        int size = clientSocket.Receive(buf);
                                        if (mlink == null)
                                        {
                                            tempData.AddRange(buf.Take(size));
                                        }
                                        if (size == 0)
                                        {
                                            // socket closed
                                            Console.WriteLine("Close Connection");
                                            if (mlink != null)
                                                mlink.CloseRemote();
                                            if (!closeFlag)
                                            {
                                                closeFlag = true;
                                                clientSocket.Shutdown(SocketShutdown.Both);
                                                clientSocket.Close();
                                            }
                                            break;
                                        }
                                        if (mlink != null)
                                            mlink.Send(buf, size);
                                    }
                                    catch (SocketException e)
                                    {
                                        if (e.ErrorCode != 10004)
                                        {
                                            Console.WriteLine("ERROR: " + e.Message);
                                            Console.WriteLine(e.StackTrace);
                                        }
                                        if (mlink != null)
                                            mlink.CloseRemote();
                                        if (!closeFlag)
                                        {
                                            closeFlag = true;
                                            clientSocket.Shutdown(SocketShutdown.Both);
                                            clientSocket.Close();
                                        }
                                        break;
                                    }

                                }
                            });
                            mlink = LinkClient.Connect(mSkynet, targetToxId, IPAddress.Parse(targetIP), Convert.ToInt32(targetPort));
                            if (mlink == null)
                            {
                                // connected failed
                                Console.WriteLine("Connected failed");
                                if (!closeFlag)
                                {
                                    closeFlag = true;
                                    clientSocket.Shutdown(SocketShutdown.Both);
                                    clientSocket.Close();
                                }
                                return;
                            }
                            if (tempData.Count != 0)
                                mlink.Send(tempData.ToArray(), tempData.Count);
                            // check if socket has closed
                            if (closeFlag)
                            {
                                // socket has closed
                                Console.WriteLine("Close remote");
                                mlink.CloseRemote();
                            }
                            mlink.OnMessage((msg) => {
                                try
                                {
                                    clientSocket.Send(msg, SocketFlags.None);
                                }
                                catch (Exception e){
                                    Console.WriteLine("ERROR: " + e.Message);
                                    Console.WriteLine(e.StackTrace);
                                    mlink.CloseRemote();
                                    if (!closeFlag)
                                    {
                                        closeFlag = true;
                                        clientSocket.Shutdown(SocketShutdown.Both);
                                        clientSocket.Close();
                                    }
                                }
                            });
                            mlink.OnClose(() => {
                                if (!closeFlag)
                                {
                                    closeFlag = true;
                                    clientSocket.Shutdown(SocketShutdown.Both);
                                    clientSocket.Close();
                                }
                            });
                        });
                    }
                });
            }

            mSkynet.addNewReqListener((req) => {
                // handle 
                if (req.toNodeId == "" && req.url == "/connect")
                {
                    Task.Factory.StartNew(() => {
                        // connect to server received, create sockets
                        try
                        {
                            string reqStr = Encoding.UTF8.GetString(req.content);
                            string ipstr = reqStr.Split('\n')[0];
                            string port = reqStr.Split('\n')[1];
                            Console.WriteLine("Connect to " + ipstr + " " + port);
                            IPAddress targetIp = IPAddress.Parse(ipstr);
                            Socket mClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            bool closeFlag = false;
                            mClientSocket.Connect(new IPEndPoint(targetIp, Convert.ToInt32(port)));

                            var mlink = LinkClient.Connect(mSkynet, req.fromToxId, req.fromNodeId);
                            req.toNodeId = mlink.clientId;

                            mSkynet.sendResponse(req.createResponse(Encoding.UTF8.GetBytes("OK")), new ToxId(req.fromToxId));
                            mlink.OnMessage((msg) =>
                            {
                                try
                                {
                                    mClientSocket.Send(msg, SocketFlags.None);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("ERROR: " + e.Message);
                                    Console.WriteLine(e.StackTrace);
                                    mlink.CloseRemote();
                                    if (!closeFlag) {
                                        closeFlag = true;
                                        mClientSocket.Shutdown(SocketShutdown.Both);
                                        mClientSocket.Close();
                                    }
                                    
                                }

                            });
                            mlink.OnClose(() => {
                                if (!closeFlag) {
                                    closeFlag = true;
                                    mClientSocket.Shutdown(SocketShutdown.Both);
                                    mClientSocket.Close();
                                }
                            });
                            Task.Factory.StartNew(() =>
                            {
                                while (true)
                                {
                                    byte[] buf = new byte[1024];
                                    try
                                    {
                                        int size = mClientSocket.Receive(buf);
                                        if (size == 0)
                                        {
                                            mlink.CloseRemote();
                                            if (!closeFlag) {
                                                closeFlag = true;
                                                mClientSocket.Shutdown(SocketShutdown.Both);
                                                mClientSocket.Close();
                                            }
                                            Console.WriteLine("Close Connection");
                                            break;
                                        }
                                        mlink.Send(buf, size);
                                    }
                                    catch (SocketException e)
                                    {
                                        if (e.ErrorCode != 10004) // this is not an error
                                        {
                                            Console.WriteLine("ERROR: " + e.Message);
                                            Console.WriteLine(e.StackTrace);
                                        }
                                        mlink.CloseRemote();
                                        if (!closeFlag) {
                                            closeFlag = true;
                                            mClientSocket.Shutdown(SocketShutdown.Both);
                                            mClientSocket.Close();
                                        }
                                        break;
                                    }
                                }
                            });

                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("ERROR: " + e.Message);
                            Console.WriteLine(e.StackTrace);
                            // connected failed
                            string reqStr = Encoding.UTF8.GetString(req.content);
                            string ipstr = reqStr.Split('\n')[0];
                            string port = reqStr.Split('\n')[1];
                            Console.WriteLine("Connect to " + ipstr + " " + port + " failed");
                            var response = req.createResponse(Encoding.UTF8.GetBytes("failed"));
                            mSkynet.sendResponse(response, new ToxId(response.toToxId));
                        }
                    });
                }
                else if (req.toNodeId == "" && req.url == "/handshake") {
                    var response = req.createResponse(Encoding.UTF8.GetBytes("OK"));
                    Console.WriteLine("HandShake from: " + response.toToxId);
                    mSkynet.sendResponse(response, new ToxId(response.toToxId));
                }
            });

            while (true) {
                Thread.Sleep(10);
            }
        }
    }
}
