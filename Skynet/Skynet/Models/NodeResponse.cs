using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skynet.Models
{
    public class NodeResponse
    {
        public NodeResponseCode statusCode;
        public string description;
        public string value;
        public long time;
    }

    public enum NodeResponseCode
    {
        NotFound,
        OK,
        InvalidRequest,
        InvalidRequestMethod,
        TargetLocked,
        TargetIsFull,
        AlreadyExist,
        NoPermission,
        OutOfDate
    }
}
