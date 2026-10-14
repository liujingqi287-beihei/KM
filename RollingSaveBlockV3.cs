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
    public record RollingPackV3
    {
        public string Tag;
        public int Index;
        public byte[] Data;
        public RollingPackV3(string tag, int index, byte[] data)
        {
            Tag = tag;
            Index = index;
            Data = data;
        }
    }
    public class InnerRollingSaveBlockV3 : ProcessBlock<RollingPackV3>
    {

        protected override string _Name => "文件存储模块";
        public Dictionary<string, RollingFileStreamV3> Storage = new Dictionary<string, RollingFileStreamV3>();
        private SaveBlockConfig Config;
        private string PathName = "";
        public InnerRollingSaveBlockV3(SaveBlockConfig config, ulong cpuIndex = 0,string pathname="") : base(cpuIndex)
        {
            Config = config;
            PathName = pathname;
        }
        public void AddStorage(string tag, string root, string taskID, string name, string template = "{root}/{TaskID}/{name}/{index_prex}.N01.{datatype}.temp")
        {
            Storage[tag] = new RollingFileStreamV3(root, taskID, name, name, template);
        }
        public void AddStorage(string tag, string root, string taskID, string Type, string ID, string template = "{root}/{TaskID}/{name}/{index_prex}.N01.{datatype}.temp")
        {
            Storage[tag] = new RollingFileStreamV3(root, taskID, Type, ID, template,PathName);
        }
        protected override void Action(RollingPackV3 p)
        {
            if (p.Tag.Contains("RAW"))
            {
                Storage[p.Tag].Write(Config.ToRAW(p.Data[Config.Offset..^Config.End]), p.Index);
            }
            else
            {
                Storage[p.Tag].Write(p.Data, p.Index);
            }
        }
        public void Save(string tag, int index, byte[] data)
        {
            Enqueue(new RollingPackV3(tag, index, data));
        }
    }
    public class RollingSaveBlockV3
    {
        public int Cache => Storage.Values.Select(p => p.QueueCount).Count();
        public Dictionary<string, InnerRollingSaveBlockV3> Storage = new Dictionary<string, InnerRollingSaveBlockV3>();
        private SaveBlockConfig Config;
        private string PathName = "";
        public RollingSaveBlockV3(SaveBlockConfig config, ulong cpuIndex = 0,string pathname="")
        {
            Config = config;
            PathName = pathname;
        }
        public void AddStorage(string tag, string root, string taskID, string name, string template = "{root}/{TaskID}/{name}/{index_prex}.N01.{datatype}.temp")
        {
            if (Storage.ContainsKey(tag))
                return;
            Storage[tag] = new InnerRollingSaveBlockV3(Config,pathname:PathName);
            Storage[tag].AddStorage(tag, root, taskID, name, name, template);
        }
        public void AddStorage(string tag, string root, string taskID, string Type, string ID, string template = "{root}/{TaskID}/{name}/{index_prex}.N01.{datatype}.temp")
        {
            Storage[tag] = new InnerRollingSaveBlockV3(Config);
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
            Storage[tag].Enqueue(new RollingPackV3(tag, index, data));
        }
    }
}
