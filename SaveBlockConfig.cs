using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KM.Codec.L100.Const;

namespace KM.ConsoleDecode
{
    public class SaveBlockConfig
    {
        public int Offset { get; set; } = 0;
        public int End { get; set; } = 0;
        public Func<byte[], byte[]> ToRAW { get; set; }

        public static SaveBlockConfig KM_WIN = new SaveBlockConfig()
        {
            Offset = Parameter.Windows.CameraHeadLength,
            End = Parameter.Windows.CameraTailLength,
            ToRAW = (cam16) =>
            {
                byte[] raw16 = new byte[cam16.Length];
                int batchcount = raw16.Length / 2;
                for (int i = 0; i < batchcount; i++)
                {
                    raw16[i * 2] = (byte)cam16[i * 2 + 1];
                    raw16[i * 2 + 1] = (byte)cam16[i * 2];
                }
                return raw16;
            }
        };

        public static SaveBlockConfig KM_MID = new SaveBlockConfig()
        {
            Offset = Parameter.ShortMid.CameraHeadLength,
            End = Parameter.ShortMid.CameraTailLength,
            ToRAW = (cam16) =>
            {
                byte[] raw16 = new byte[cam16.Length];
                int batchcount = raw16.Length / 2;
                for (int i = 0; i < batchcount; i++)
                {
                    raw16[i * 2] = (byte)cam16[i * 2 + 1];
                    raw16[i * 2 + 1] = (byte)cam16[i * 2];
                }
                return raw16;
            }
        };
        public static SaveBlockConfig KM_SHORT = new SaveBlockConfig()
        {
            Offset = Parameter.ShortMid.CameraHeadLength,
            End = Parameter.ShortMid.CameraTailLength,
            ToRAW = (cam16) =>
            {
                byte[] raw16 = new byte[cam16.Length];
                int batchcount = raw16.Length / 2;
                for (int i = 0; i < batchcount; i++)
                {
                    raw16[i * 2] = (byte)cam16[i * 2 + 1];
                    raw16[i * 2 + 1] = (byte)cam16[i * 2];
                }
                return raw16;
            }
        };
       
    }
}
