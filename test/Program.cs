using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace test
{
    class Program
    {
        static void Main(string[] args)
        {
            IPAddress targetIp = IPAddress.Parse("127.0.0.1");
            Socket mClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            mClientSocket.Connect(new IPEndPoint(targetIp, 9999));
            byte[] data = new byte[256];
            for (int i = 0; i < 256; i++) {
                data[i] = (byte)i;
            }
            mClientSocket.Send(data, SocketFlags.None);
        }
    }
}
