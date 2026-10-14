using Decompress.Interface.Block;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KM.ConsoleDecode
{
    public record RollingPack
    {
        public string Tag;
        public int Index;
        public byte[] Data;
        public RollingPack(string tag, int index, byte[] data)
        {
            Tag = tag;
            Index = index;
            Data = data;
        }
    }
    public class RollingSaveBlock : ProcessBlock<RollingPack>
    {
        protected override string _Name => "文件存储模块";
        public Dictionary<string, RollingFileStreamV2> Storage = new Dictionary<string, RollingFileStreamV2>();
        public void AddStorage(string tag, RollingFileStreamV2 rollingFileStream)
        {
            if (!Storage.Keys.Contains(tag))
            {
                Storage.Add(tag, rollingFileStream);
            }
        }
        public void ResetName(string tag, string name)
        {
            Storage[tag].ResetName(name);
        }
        public void ResetTemplate(string tag, string template)
        {
            Storage[tag].ResetTemplate(template);
        }
        protected override void Action(RollingPack p)
        {
            if (p.Tag.Contains("RAW"))
            {
                Storage[p.Tag].Write(ForCam12ToRaw16(p.Data), p.Index);
            }
            else
            {
                Storage[p.Tag].Write(p.Data, p.Index);
            }
        }
        public void Save(string tag, int index, byte[] data)
        {
            Enqueue(new RollingPack(tag, index, data));
        }
        public string GetPath(string tag)
        {
            //if (Storage[tag].NewPathQueue.Count != 0)
            //{
            //    return Storage[tag].NewPathQueue.Dequeue();
            //}
            //else 
            //{
            //    return "无";
            //}
            return Storage[tag].NewPath;
        }
        private byte[] ForCam12ToRaw16(byte[] cam12)
        {
            byte[] raw16 = new byte[cam12.Length * 4 / 3];
            int batchcount = raw16.Length / 4;
            for (int i = 0; i < batchcount; i++)
            {
                raw16[i * 4] = ((byte)(((cam12[i * 3] & 0xf) << 4) + ((cam12[i * 3 + 1] & 0xf0) >> 4)));
                raw16[i * 4 + 1] = (byte)((cam12[i * 3] & 0xf0) >> 4);
                raw16[i * 4 + 2] = (byte)cam12[i * 3 + 2];
                raw16[i * 4 + 3] = (byte)(cam12[i * 3 + 1] & 0xf);
            }
            return raw16;
        }
    }

}
