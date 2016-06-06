using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            FileStream fs = new FileStream("testlog.txt", FileMode.Create);
            var streamwriter = new StreamWriter(fs);
            streamwriter.AutoFlush = true;
            Console.SetOut(streamwriter);
            Console.SetError(streamwriter);

            IPAddress targetIp = IPAddress.Parse("127.0.0.1");
            Socket mClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            mClientSocket.Connect(new IPEndPoint(targetIp, 9999));
            byte[] data = new byte[1024*1024];
            for (int i = 0; i < data.Length; i++) {
                data[i] = (byte)(i % 256);
            }
            mClientSocket.Send(data);
            byte[] recData = new byte[1024*1024];
            mClientSocket.Receive(recData);
            foreach(byte one in recData) {
                Console.WriteLine(one);
            }
            //Console.ReadKey();
            mClientSocket.Shutdown(SocketShutdown.Both);
            mClientSocket.Close();
        }
    }
}
