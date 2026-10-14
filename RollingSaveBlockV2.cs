using Decompress.Codec.L100.Const;
using Decompress.Interface.Block;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KM.ConsoleDecode
{
    public record RollingPackV2
    {
        public string Tag;
        public int Index;
        public byte[] Data;
        public RollingPackV2(string tag, int index, byte[] data)
        {
            Tag = tag;
            Index = index;
            Data = data;
        }
    }
    public class InnerRollingSaveBlockV2 : ProcessBlock<RollingPackV2>
    {
        
        protected override string _Name => "文件存储模块";
        public Dictionary<string, RollingFileStreamV2> Storage = new Dictionary<string, RollingFileStreamV2>();
        private SaveBlockConfig Config;
        public InnerRollingSaveBlockV2(SaveBlockConfig config,ulong cpuIndex = 0) : base(cpuIndex)
        {
            Config = config;
        }
        public void AddStorage(string tag, string root, string taskID, string name, string template = "{root}/{TaskID}/{name}/{index_prex}.KM.{datatype}.temp")
        {
            Storage[tag] = new RollingFileStreamV2(root, taskID, name, name, template);
        }
        public void AddStorage(string tag, string root, string taskID, string Type, string ID, string template = "{root}/{TaskID}/{name}/{index_prex}.KM.{datatype}.temp")
        {
            Storage[tag] = new RollingFileStreamV2(root, taskID, Type, ID, template);
        }
        protected override void Action(RollingPackV2 p)
        {
            if (p.Tag.Contains("RAW"))
            {
                Storage[p.Tag].Write(Config.ToRAW(p.Data), p.Index);
            }
            else
            {
                Storage[p.Tag].Write(p.Data, p.Index);
            }
        }
        //public void Save(string tag, int index, byte[] data)
        //{
        //    Enqueue(new RollingPackV2(tag, index, data));
        //}
    }
    public class RollingSaveBlockV2
    {
        public int QueueCount => Storage.Values.Select(p => p.QueueCount).Sum();
        public Dictionary<string, InnerRollingSaveBlockV2> Storage = new Dictionary<string, InnerRollingSaveBlockV2>();
        private SaveBlockConfig Config;
        public RollingSaveBlockV2(SaveBlockConfig config, ulong cpuIndex = 0)
        {
            Config = config;
        }
        public void AddStorage(string tag, string root, string taskID, string name, string template = "{root}/{TaskID}/{name}/{index_prex}.KM.{datatype}.temp")
        {
            Storage[tag] = new InnerRollingSaveBlockV2(Config);
            Storage[tag].AddStorage(tag, root, taskID, name, name, template);
            
        }
        public void AddStorage(string tag, string root, string taskID, string Type, string ID, string template = "{root}/{TaskID}/{name}/{index_prex}.KM.{datatype}.temp")
        {
            Storage[tag] = new InnerRollingSaveBlockV2(Config);
            Storage[tag].AddStorage(tag, root, taskID, Type, ID, template);
        }
        //protected override void Action(RollingPackV2 p)
        //{
        //    if (p.Tag.Contains("RAW"))
        //    {
        //        Storage[p.Tag].Write(Config.ToRAW(p.Data[Config.Offset..^Config.End]), p.Index);
        //    }
        //    else
        //    {
        //        Storage[p.Tag].Write(p.Data, p.Index);
        //    }
        //}
        public void Save(string tag, int index, byte[] data)
        {
            Storage[tag].Enqueue(new RollingPackV2(tag, index, data));
        }
    }
}
