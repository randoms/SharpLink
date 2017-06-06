using SharpTox.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skynet.Models
{


    class Package
    {
        public string uuid { get; set; } // 36 bytes string 
        public byte[] content { get; set; }
        public int totalCount { get; set; }
        public int currentCount { get; set; }
        public uint totalSize { get; set; }
        public uint startIndex { get; set; }
        private static Package mStaticPackage = null;


        public byte[] toBytes()
        {
            // uuid 16 bytes
            // content length 2 bytes
            // total count 2 bytes
            // current count 2 bytes
            byte[] packageBytes = new byte[16 + 2 + 2 + 2 + 4 + 4 + content.Length];

            byte[] uuidbytes = Guid.Parse(uuid).ToByteArray();
            for (int i = 0; i < 16; i++)
            {
                packageBytes[i] = uuidbytes[i];
            }
            packageBytes[16] = (byte)(content.Length / 256);
            packageBytes[17] = (byte)(content.Length % 256);
            packageBytes[18] = (byte)(totalCount / 256);
            packageBytes[19] = (byte)(totalCount % 256);
            packageBytes[20] = (byte)(currentCount / 256);
            packageBytes[21] = (byte)(currentCount % 256);
            byte[] totalSizeBytes = BitConverter.GetBytes(totalSize);
            totalSizeBytes.CopyTo(packageBytes, 22); // 22-25
            byte[] startIndexBytes = BitConverter.GetBytes(startIndex);
            startIndexBytes.CopyTo(packageBytes, 26); // 26-29
            for (int i = 30; i < packageBytes.Length; i++)
            {
                packageBytes[i] = content[i - 30];
            }
            return packageBytes;
        }

        public static Package fromBytes(byte[] data)
        {
            byte[] uuidbytes = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                uuidbytes[i] = data[i];
            }
            Package mPackage = new Package();
            mPackage.uuid = new Guid(uuidbytes).ToString();
            mPackage.totalCount = data[18] * 256 + data[19];
            mPackage.currentCount = data[20] * 256 + data[21];
            mPackage.totalSize = BitConverter.ToUInt32(data, 22);
            mPackage.startIndex = BitConverter.ToUInt32(data, 26);
            mPackage.content = new byte[data[16] * 256 + data[17]];
            for (int i = 0; i < mPackage.content.Length; i++)
            {
                mPackage.content[i] = data[30 + i];
            }
            return mPackage;
        }

        public static Package fromBytesStatic(byte[] data)
        {
            byte[] uuidbytes = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                uuidbytes[i] = data[i];
            }
            if (mStaticPackage == null)
                mStaticPackage = new Package();
            mStaticPackage.uuid = new Guid(uuidbytes).ToString();
            mStaticPackage.totalCount = data[18] * 256 + data[19];
            mStaticPackage.currentCount = data[20] * 256 + data[21];
            mStaticPackage.totalSize = BitConverter.ToUInt32(data, 22);
            mStaticPackage.startIndex = BitConverter.ToUInt32(data, 26);
            mStaticPackage.content = new byte[data[16] * 256 + data[17]];

            for (int i = 0; i < mStaticPackage.content.Length; i++)
            {
                mStaticPackage.content[i] = data[30 + i];
            }
            return mStaticPackage;
        }
    }


}
