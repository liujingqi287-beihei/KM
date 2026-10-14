using System.Collections.Generic;
using Decompress;
using Decompress.Codec.L100.Const;

namespace KM.ConsoleDecode.Command
{
    public class FPGAStreamUnpackParser
    {
        //ILocator Head = new PatternLocator("head", "96 B5 2F A6 DA 70 B6 17 A5 E1 CB 5B 1A AA BB CC 96 B5 2F A6 DA 70 B6 17 A5 E1 CB 5B 1A 60 8E 97 96 B5 2F A6 DA 70 B6 17 A5 E1 CB 5B 1A 91 72 C8 96 B5 2F A6 DA 70 B6 17 A5 E1 CB 5B 1A 4E 49 6C **@64");
        ILocator Head = new PatternLocator("head", "96 B5 2F A6 DA 70 B6 17 A5 E1 CB 5B 1A AA BB CC **@64");
        ILocator Data_16 = new PatternLocator("data", "EB 90 57 16 16 57 90 EB **@262144 **@28 4E 53 5A 57");
        ILocator Data_20 = new PatternLocator("data", "EB 90 57 16 16 57 90 EB **@256000 **@28 4E 53 5A 57");

        ILocator ErrorData1 = new PatternLocator("error1", "AA BB ** 01 **@28");

        ILocator RightData = new PatternLocator("right", "AA BB ** 00 **@28");

        //ILocator Tail = new PatternLocator("tail", "**@28 4E 53 5A 57");
        int count = 0;

        //byte[] HeadData = Array.Empty<byte>();
        public InfoStructAnalysis_FPGA analysis = new InfoStructAnalysis_FPGA();
        List<byte> _Last = new List<byte>();
        public List<byte> temp = new List<byte>();
        bool IsSynced = false;
        public int Channel;
        int pack = 0;
        int ErrorCount = 0;
        int ErrorPackCount = 0;
        private byte[] HeadData = null;

        public FPGAStreamUnpackParser(int channel)
        {
            Channel = channel;
        }

        private List<byte> lastframe = new List<byte>();

        public IEnumerable<byte[]> Unpack(byte[] data)
        {
            foreach (var item in streamunpack(data))
            {
                if (item.IsNew)
                {
                    if (lastframe.Count != 0)
                    {
                        yield return lastframe.ToArray();
                    }

                    lastframe.Clear();
                }

                lastframe.AddRange(item.Data);
            }
        }

        public IEnumerable<(byte[] Data, bool IsNew)> streamunpack(byte[] data)
        {
            _Last.AddRange(data);

            while (true)
            {
                if (!_Last.AsSpan().EnoughForSearch(Head))
                {
                    //如果不足够的话
                    break;
                }

                var result = _Last.AsSpan().IndexOf(Head);
                if (result + Head.Length > _Last.Count)
                {
                    break;
                }

                if (result != -1)
                {
                    if (result != 0)
                    {
                        yield return (_Last[..result].ToArray(), false);
                    }

                    yield return (_Last[result..(result + Head.Length)].ToArray(), true);
                    _Last.RemoveRange(0, result + Head.Length);
                }

                if (_Last.Count > Head.Length)
                {
                    yield return (_Last[..^Head.Length].ToArray(), false);
                    _Last.RemoveRange(0,_Last.Count-Head.Length);
                }
                
                yield break;
            }

        }
    }


}