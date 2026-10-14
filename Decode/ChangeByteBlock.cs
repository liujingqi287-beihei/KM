using Decompress;
using Decompress.Codec.L100.Const;
using Decompress.Interface.Block;
using System;
using System.Collections.Generic;

namespace KM.ConsoleDecode.Decode
{
    public class ChangeByteBlock : ProcessBlock<byte[]>
    {
        private List<byte> Last = new List<byte>();
        private byte[] Head;
        private byte[] Data;
        private ILocator DataPackHead = new PatternLocator("DataPackHead", "20 00 03 23 **@16");

        //ProtocolParser protocolParser = new ProtocolParser();
        protected override void Action(byte[] data)
        {
            //foreach (var item in protocolParser.Decode(data))
            //{
            //    PackArrived?.Invoke(item);
            //}
            Last.AddRange(data);
            while (true)
            {
                if (!Last.AsSpan().EnoughForSearch(DataPackHead))
                {
                    break;
                }
                var index = Last.AsSpan().IndexOf(DataPackHead);
                if (index == -1)
                {
                    break;
                }
                if (index == 0)
                {
                    Head = Last.AsSpan()[index..(index + DataPackHead.Length)].ToArray();
                    Last.RemoveRange(0, index + DataPackHead.Length);
                    continue;
                }
                Data = Last.AsSpan()[..index].ToArray();
                if (Head == null)
                {
                    break;
                }
                //TODO 修改combine的写法，改为尽量少拷贝内存的形式
                PackArrived?.Invoke(Head.Combine(Data).ToDataPack());
                Last.RemoveRange(0, index);
                //    continue;
                //if (index + DataPackHead.Length > Last.Count())
                //{
                //    break;
                //}

                //Head = Last.AsSpan()[index..(index + DataPackHead.Length)].ToArray();
                //Last.RemoveRange(0, index + DataPackHead.Length);

            }

        }
        public Action<DataPack> PackArrived { get; set; }
    }
}
