using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Skynet.Utils
{
    public class Utils
    {
        public static bool isValidGuid(string guid) {
            Guid mGuid;
            return Guid.TryParse(guid, out mGuid);
        }

        public static int FreeTcpPort()
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static string GetString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }

        public static long UnixTimeNow()
        {
            var timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
            return (long)timeSpan.TotalMilliseconds;
        }

        public static byte[] joinBytes(byte[] one, byte[] two) {
            if (one == null)
                return (byte[])two.Clone();
            if (two == null)
                return (byte[])one.Clone();
            byte[] res = new byte[one.Length + two.Length];
            one.CopyTo(res, 0);
            two.CopyTo(res, one.Length);
            return res;
        }

        public static byte[] subArray(byte[] array, int startIndex) {
            byte[] res = new byte[array.Length - startIndex];
            for (int i = 0; i < res.Length; i++) {
                res[i] = array[startIndex + i];
            }
            return res;
        }

        public static byte[] subArray(byte[] array,int startIndex, int length)
        {
            byte[] res = new byte[length];
            for (int i = startIndex; i < startIndex + length; i++)
            {
                res[i - startIndex] = array[i];
            }
            return res;
        }

		#if(DEBUG)
		private static StreamWriter streamwriter = null;
		private static object loglock = new object();
		private static string logFilename = "log.txt";
		#endif

		public static void setLogFile(string filename){
			#if(DEBUG)
			logFilename = filename;
			#endif
		}

		public static void Log(string detail) {
			#if(DEBUG)
			lock (loglock) {
				if (streamwriter == null)
				{
					FileStream fs = new FileStream(logFilename, FileMode.Create);
					streamwriter = new StreamWriter(fs);
					streamwriter.AutoFlush = true;
				}
				streamwriter.WriteLine("Time: " + UnixTimeNow() + ", " + detail);
				streamwriter.Flush();
			}
			#endif
		}

		public static string GetLocalIPAddress()
		{
			var host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (var ip in host.AddressList)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					return ip.ToString();
				}
			}
			return "";
		}
    }
}
