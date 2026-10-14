using CommandDotNet;
using Decompress;
using Decompress.Codec.L100.Const;
using Decompress.Codec.L200.Decoder.L210.Unpack;
using Decompress.Codec.L200.Decoder.L220.ECC;
using KM.Codec.L100.Const;
using KM.Codec.L200.Encoder.L210.Shared;
using KM.Codec.L200.Encoder.L210.Shared.Step5.FrameBuild;
using KM.Codec.L300.Decode.L310.Shared;
using KM.Codec.L300.Decode.L310.Shared.Step2.ECC;
using KM.Codec.L300.Decode.L310.Shared.Step3.EDC;
using KM.Codec.L300.Decode.L310.Shared.Step4.PatchDecoder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using KM.Codec.L100.Const.ShortMid;
using RollingFileStream = Decompress.Codec.L100.Const.RollingFileStream;

namespace KM.ConsoleDecode
{
    [Command("test")]
    public class TestCommand
    {
        [Command("rs")]
        public void RS(string path)
        {
            ECCCode eCC = new ECCCode();
            eCC.SetRSCode(ECCType.RSCode251);
            var data = File.ReadAllBytes(path);
            List<byte> result = new List<byte>();
            result.AddRange(eCC.Encode(data));
            result.AddRange(eCC.EncodeFlush());
            File.WriteAllBytes("结果数据/rs.src", result.ToArray());
        }

        [Command("ECC")]
        public void ECCDecode(string file, int times, int runpath)
        {
            RollingFileStream ECCwrite = new RollingFileStream("D:\\测试数据\\ECC", "ECC图", "ECC");
            UnpackParser UnpackParser = new UnpackParser(ShortMidParameter.Unpack);
            ECCDecoderBlock ECCParser = new ECCDecoderBlock();
            Stopwatch unpacktime = new Stopwatch();
            List<DataPack> unpack = new List<DataPack>();
            foreach (var data in file.Load(times))
            {
                unpacktime.Start();
                foreach (var pack in UnpackParser.Parse(data))
                {

                    unpack.Add(pack);
                }

                unpacktime.Stop();
            }

            Stopwatch ECCtime = new Stopwatch();
            List<long> ECCtimeList = new List<long>();
            List<DataPack> ECC = new List<DataPack>();
            //List<byte> ECCdata = new List<byte>();

            //block测试

            int ECCcount = 0;
            ECCParser.PackArrived += (data) =>
            {
                ECCwrite.Write(data.Data, ECCcount);
                //ECCdata.AddRange(data.Data);
                //if (data.Type == DataPackType.SyncHead)
                //{
                //    ECCtime.Restart();
                //}
                if (data.Type == DataPackType.FrameTail)
                {
                    if (ECCcount == times - 1)
                    {
                        ECCtime.Stop();
                    }

                    //ECCtimeList.Add(ECCtime.ElapsedMilliseconds);
                    Console.WriteLine($"完成第{ECCcount + 1}帧,共{times}帧");
                    //ECCwrite.Write(ECCdata.ToArray(),ECCcount);
                    //ECCdata.Clear();
                    ECCcount++;
                }
            };
            Stopwatch listime = new Stopwatch();
            listime.Start();
            foreach (var data in unpack)
            {
                ECCtime.Start();
                ECCParser.Enqueue(data);
            }

            listime.Stop();

            while (ECCParser.IsRunning)
            {
            }



            Thread.Sleep(3000);
            //ECCwrite.Flush();
            Console.WriteLine(
                $"各模块时延{(double)unpacktime.ElapsedMilliseconds / (1000 * times)}s,{(double)ECCtime.ElapsedMilliseconds / (1000 * times)}s,ECC输入帧率{(double)times / ((double)listime.ElapsedMilliseconds / 1000)},输出帧率{(double)times / ((double)ECCtime.ElapsedMilliseconds / 1000)}");

        }

        [Command("ECCparse")]
        public void ECCParser(string file, int times, int runpath)
        {
            //RollingFileStream ECCwrite = new RollingFileStream("C:\\测试数据\\ECC", "ECC图", "ECC");
            UnpackParser UnpackParser = new UnpackParser(ShortMidParameter.Unpack);
            //ECCDecoderBlock ECCParser = new ECCDecoderBlock(runpath);
            ECCDecoder ECCParser = new ECCDecoder();
            Stopwatch unpacktime = new Stopwatch();
            List<DataPack> unpack = new List<DataPack>();
            foreach (var data in file.Load(times))
            {
                unpacktime.Start();
                foreach (var pack in UnpackParser.Parse(data))
                {

                    unpack.Add(pack);
                }

                unpacktime.Stop();
            }

            Stopwatch ECCtime = new Stopwatch();
            List<long> ECCtimeList = new List<long>();
            List<DataPack> ECC = new List<DataPack>();
            //List<byte> ECCdata = new List<byte>();



            //parser测试

            int ECCcount = 0;
            foreach (var data in unpack)
            {
                ECCtime.Start();
                foreach (var pack in ECCParser.Parse(data))
                {
                    ECC.Add(pack);
                    if (pack.Type == DataPackType.FrameTail)
                    {
                        Console.WriteLine($"{ECCcount + 1},{times}");
                        ECCcount++;
                    }
                }

                ECCtime.Stop();
            }

            Thread.Sleep(3000);
            //ECCwrite.Flush();
            Console.WriteLine(
                $"各模块时延{(double)unpacktime.ElapsedMilliseconds / (1000 * times)}s,{(double)ECCtime.ElapsedMilliseconds / (1000 * times)}s,ECC输入帧率{100 /*(double)times / ((double)listime.ElapsedMilliseconds / 1000)*/},输出帧率{(double)times / ((double)ECCtime.ElapsedMilliseconds / 1000)}");

        }
        
    }
}
