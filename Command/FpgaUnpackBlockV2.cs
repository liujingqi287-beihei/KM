using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Decompress;
using Decompress.Codec.L100.Const;
using Decompress.Interface.Block;
using KM.Codec.L300.Decode.L310.Shared.Step3._5.RefimageMatch;
using KM.Codec.L300.Decode.L310.Shared.Step3._9.DecodeAdaptor;

namespace KM.ConsoleDecode.Command;

public class FpgaUnpackParserBlock : ProcessBlock<byte[]>
{
    private FPGAStreamUnpackParser _parser;

    public FpgaUnpackParserBlock(int channel)
    {
        _parser=new FPGAStreamUnpackParser(channel);
    }
    protected override void Action(byte[] pack)
    {
        foreach (var data in _parser.Unpack(pack[8..]))
        {
            DataArrived?.Invoke(data);
                    
        }
    }
    public Action<byte[]> DataArrived { get; set; }
}

   public class FpgaUnpackBlockV2 : ProcessBlock<byte[]>
    {
        public Dictionary<byte, FpgaUnpackParserBlock> Unpack = new Dictionary<byte, FpgaUnpackParserBlock>();
        public int Cache => Unpack.Sum(p => p.Value.QueueCount);
        public uint totalpackindex = 0;
        public uint SinglePackOrder = 0;
        public Dictionary<uint, UnMatchPack_FPGA> SendQueue { get; set; } = new Dictionary<uint, UnMatchPack_FPGA>();
        private static int compressPeriod = 6;
        ///public List<UnMatchPack_FPGA> [] SendQueueList = new List<UnMatchPack_FPGA>[compressPeriod];
        private uint expectindex { get; set; } = 0;
        private uint newestindex { get; set; } = 0;
        private int count = 0;
        public InnerProcessBlock<byte[]> Frameparser;
        public InnerProcessBlock<(uint, uint, UnMatchPack_FPGA)> SortProcess;
        public SpeedMetric InputMetric2 = SpeedMetricManage.GetSpeedMetric();
        public FpgaUnpackBlockV2() : base(0, 0)
        {
            Frameparser = new InnerProcessBlock<byte[]>(pack =>
            {
            
                FpgaUnpackParserBlock parser;

                if (!Unpack.TryGetValue(pack[5],out  parser))
                {
                    parser= new FpgaUnpackParserBlock(pack[5]);
                    parser.DataArrived += (data) =>
                    {
                        DataArrived?.Invoke(data);
                    };

                    Unpack[pack[5]] = parser;

                }
               
                parser.Enqueue(pack);
               
            });
            
        }
        //List<DataPack> datalist = new List<DataPack>();
        protected override void Action(byte[] p)
        {
            InputMetric.Now(p.Length);
            var unmixpacks = UnpackMix(p);
            foreach (var pack in unmixpacks)
            {
                if (pack.Length < 5)
                {
                    continue;
                }
                Frameparser.Post(pack);
            }
        }

        List<byte> Last = new List<byte>();
        FPGAMixConfigurationV2 Configuration = new FPGAMixConfigurationV2();
        private List<byte[]> result = new List<byte[]>();

        private List<byte[]> UnpackMix(byte[] data)
        {
            result.Clear();
            if (data == null)
            {
                return result;
            }

            Last.AddRange(data);
            int CutLength = 0;
            while (Last.Count >= Configuration.packLength)
            {
                var temp = Last.AsSpan()[CutLength..];
                var index = temp.IndexOf(Configuration.TogetherPack);
                if (index != -1)
                {
                    var oneLength = index + Configuration.packLength;
                    CutLength += oneLength;
                    var sign = temp[index..(index + Configuration.packLength)].ToArray();
                    result.Add(sign);
                }
                else
                {
                    if (CutLength == 0)
                    {
                        Last.RemainTail(Configuration.packLength);
                    }

                    break;
                }
            }

            Last.RemoveRange(0, CutLength);

            return result;
        }

        private UnMatchPack_FPGA ToUnMatchPack_FPGA(DataPack data)
        {
            var outputpack = new UnMatchPack_FPGA(data.Type, data.CurrentReference, (ushort) data.Index, null, 128, null,
                data.Info, true, false, data.Data);
            outputpack.Info = data.Info;
            outputpack.Channel = data.Channel;
            var key = (outputpack.RefInfo[0] << 16) + (outputpack.RefInfo[1]<<8)+outputpack.RefInfo[2];
            var totalindex = BinaryPrimitives.ReadUInt32BigEndian(outputpack.RefInfo[3..7].AsSpan());
            outputpack.refkey = (key, outputpack.CurrentReference, outputpack.Index, totalindex);
            return outputpack;
        }
        

        public Action<byte[]> DataArrived { get; set; }
        public Action<DataPack> ErrorPackArrived { get; set; }
    }