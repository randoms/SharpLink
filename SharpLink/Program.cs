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
                        Socket clientSocket = serverSocket.Accept();
                        var mlink = LinkClient.Connect(mSkynet, targetToxId, IPAddress.Parse(targetIP), Convert.ToInt32(targetPort));
                        if (mlink == null) {
                            // connected failed
                            clientSocket.Shutdown(SocketShutdown.Both);
                            clientSocket.Close();
                            continue;
                        }
                        mlink.OnMessage((msg) => {
                            Console.WriteLine("write socket " + msg.Length);
                            clientSocket.Send(msg, SocketFlags.None);
                        });
                        mlink.OnClose(() => {
                            clientSocket.Shutdown(SocketShutdown.Both);
                            clientSocket.Close();
                        });
                        Task.Factory.StartNew(() =>
                        {
                            while (true) {
                                byte[] buf = new byte[1024];
                                try
                                {
                                    if (clientSocket.Available < 0)
                                        break;
                                    int size = clientSocket.Receive(buf);
                                    if (size == 0) {
                                        // socket closed
                                        Console.WriteLine("Close Connection");
                                        mlink.CloseRemote();
                                        clientSocket.Shutdown(SocketShutdown.Both);
                                        clientSocket.Close();
                                    }
                                    Console.WriteLine("read from socket: " + size);
                                    mlink.Send(buf, size);
                                }
                                catch (Exception e){
                                    Console.WriteLine("ERROR: " + e.Message);
                                    Console.WriteLine(e.StackTrace);
                                    mlink.CloseRemote();
                                    clientSocket.Shutdown(SocketShutdown.Both);
                                    clientSocket.Close();
                                    break;
                                }
                                
                            }
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
                            string ipstr = req.content.Split('\n')[0];
                            string port = req.content.Split('\n')[1];
                            Console.WriteLine("Connect to " + ipstr + " " + port);
                            IPAddress targetIp = IPAddress.Parse(ipstr);
                            Socket mClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            mClientSocket.Connect(new IPEndPoint(targetIp, Convert.ToInt32(port)));

                            var mlink = LinkClient.Connect(mSkynet, req.fromToxId, req.fromNodeId);
                            mlink.OnMessage((msg) =>
                            {
                                try
                                {
                                    Console.WriteLine("write socket " + msg.Length);
                                    mClientSocket.Send(msg, SocketFlags.None);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("ERROR: " + e.Message);
                                    Console.WriteLine(e.StackTrace);
                                    mlink.CloseRemote();
                                    mClientSocket.Shutdown(SocketShutdown.Both);
                                    mClientSocket.Close();
                                }

                            });
                            mlink.OnClose(() => {
                                mClientSocket.Shutdown(SocketShutdown.Both);
                                mClientSocket.Close();
                            });
                            Task.Factory.StartNew(() =>
                            {
                                while (true)
                                {
                                    byte[] buf = new byte[1024];
                                    try
                                    {
                                        if (mClientSocket.Available < 0)
                                            break;
                                        int size = mClientSocket.Receive(buf);
                                        if (size == 0)
                                        {
                                            mlink.CloseRemote();
                                            mClientSocket.Shutdown(SocketShutdown.Both);
                                            mClientSocket.Close();
                                            Console.WriteLine("Close Connection");
                                        }
                                        Console.WriteLine("read from socket: " + size);
                                        mlink.Send(buf, size);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine("ERROR: " + e.Message);
                                        Console.WriteLine(e.StackTrace);
                                        mlink.CloseRemote();
                                        mClientSocket.Shutdown(SocketShutdown.Both);
                                        mClientSocket.Close();
                                        break;
                                    }
                                }
                            });
                            req.toNodeId = mlink.clientId;
                            mSkynet.sendResponse(req.createResponse("OK"), new ToxId(req.fromToxId));

                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("ERROR: " + e.Message);
                            Console.WriteLine(e.StackTrace);
                            // connected failed
                            string ipstr = req.content.Split('\n')[0];
                            string port = req.content.Split('\n')[1];
                            Console.WriteLine("Connect to " + ipstr + " " + port + " failed");
                            var response = req.createResponse("failed");
                            mSkynet.sendResponse(response, new ToxId(response.toToxId));
                        }
                    });
                }
                else if (req.toNodeId == "" && req.url == "/handshake") {
                    var response = req.createResponse("OK");
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
