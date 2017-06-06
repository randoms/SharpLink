using System;

namespace Skynet
{
    public class ClientInfo
    {
        public string toxid;
        public string inetAddr;

        public ClientInfo() { }

        public ClientInfo(string toxid, string inetAddr)
        {
            this.toxid = toxid;
            this.inetAddr = inetAddr;
        }


    }
}

