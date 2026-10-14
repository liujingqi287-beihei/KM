using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;

namespace KM.ConsoleDecode
{
    [MessagePackObject]
    public class StatusInfoPack
    {
        [Key(0)]
        public string 数据类型 { get; set; }
        [Key(1)]
        public uint 帧计数 { get; set; }
        [Key(2)]
        public string 压缩比 { get; set; }
        [Key(3)]
        public string 数据状态 { get; set; }
        [Key(4)]
        public string 耗时 { get; set; }
        [IgnoreMember]
        public string 时延 { get; set; }
        [Key(5)]
        public int total { get; set; }
        [Key(6)]
        public int error { get; set; }
        [Key(7)]
        public double 输入帧频 { get; set; }
        [Key(8)]
        public double 输出帧频 { get; set; }
        [Key(9)]
        public int 输入字节数 { get; set; }
        [Key(10)]
        public bool 解码状态 { get; set; }

    }
}
