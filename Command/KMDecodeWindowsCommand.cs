using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandDotNet;
using Decompress;
using Decompress.Codec.L100.Const;
using KM.Codec.L100.Const;
using KM.Codec.L100.SharedBlocks.Step5.Build;
using KM.Codec.L300.Decode.L310.Shared.Step0.TogetherPack;
using KM.Codec.L300.Decode.L320.ShortMidDecoder_FPGA;
using Lab404.Link;
using MessagePack;
using Serilog;
using Serilog.Events;
using XdmaEngine.Direct;
using LinkNode = Lab404.Link.LinkNode;

namespace KM.ConsoleDecode.Command;

[Command("XDMATest")]
public class XDMATestCommand
{
    [Command("XDMA")]
    public void XDMATest(string inputPath,
        string comparePath = @"D:\yj\data\XDMA_Test_Template", string outputPath = @"D:\yj\data\XDMATest", int channel = 0,
        [Option("times")]int times = 100,[Option("saveraw")]bool saveraw=false,[Option("saveframe")]bool saveframe=false,[Option("saveerror")]bool saveerror=true)

    {
        var xdma = new XDMA_Direct_Win(channel);
        xdma.Start();
        var files = Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories).ToArray();
        var templateFiles = Directory.EnumerateFiles(comparePath, "*", SearchOption.TopDirectoryOnly).ToArray();
        var inputMetric = SpeedMetricManage.GetSpeedMetric();
        var outputMetric = SpeedMetricManage.GetSpeedMetric();

        var saveCount = 0;
        var templateHash = new HashSet<string>();
        var unpackBlockV2 = new FpgaUnpackBlockV2();

        var errorCount = 0;
        var successCount = 0;
        var totalCount = 0;
        var frameoutput = Path.Combine(outputPath, "frame");
        Directory.CreateDirectory(frameoutput);
        var rawoutput = Path.Combine(outputPath, "raw");
        Directory.CreateDirectory(rawoutput);
        var erroroutput = Path.Combine(outputPath, "error");
        Directory.CreateDirectory(erroroutput);
        foreach (var data in templateFiles.Load(1)) templateHash.Add(Convert.ToHexString(MD5.HashData(data)));


        unpackBlockV2.DataArrived += pack =>
        {
            if (saveframe)
            {
                File.WriteAllBytes(Path.Combine(frameoutput, $"{totalCount}.src"), pack);
            }

            if (pack.Length < 88+952)
            {
             
                if (saveerror)
                {
                    File.WriteAllBytes(Path.Combine(erroroutput,$"{errorCount}_长度不够.src"),pack);
                }
                errorCount++;
                return;
            }
            
            var data = pack[88..^952];
            
            if (!templateHash.Contains(Convert.ToHexString(MD5.HashData(data))))
            {
                if (saveerror)
                {
                    File.WriteAllBytes(Path.Combine(erroroutput,$"{errorCount}_无对比源.src"),data);
                }
              
                errorCount++;
            }
            else
            {
                successCount++;
            }

            totalCount++;
        };

        int i = 0;
        Task.Run(() =>
        {
            while (true)
                if (xdma.OutputDMA.TryDequeue(out var dataOut))
                {
 
                    
                    if (saveraw)
                    {
                        File.AppendAllBytes(Path.Combine(rawoutput, $"output_{i++}.src"), dataOut);  
                    }
                   
                    outputMetric.Now(dataOut.Length);
                    unpackBlockV2.Enqueue(dataOut);
                }
        });

        Task.Run(() =>
        {
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine($"{DateTime.Now.ToLocalTime()}=============================================");
                Console.WriteLine("输入速率/输出速率：" +
                                  xdma.InputMetric.Last.BytesSpeed(1000) + " , " +
                                  xdma.OutputMetric.Last.BytesSpeed(1000));
                Console.WriteLine(
                    $"缓冲区：{xdma.InputDMA.Count}/{xdma.OutputDMA.Count}/{unpackBlockV2.QueueCount}/{unpackBlockV2.Frameparser.InputCount}/{string.Join(",",unpackBlockV2.Unpack.Select(p=>p.Value.QueueCount))}");
                Console.WriteLine($"错误数/正确/总量：{errorCount}/{successCount}/{totalCount}");
                Console.WriteLine();
                Thread.Sleep(1000);
            }
        });
        List<byte> buffer = new List<byte>();
        foreach (var tmp in files.Load(times))
        {
            buffer.AddRange(tmp);

            while (buffer.Count>512*1024)
            {
                var data = buffer.AsSpan()[..(512 * 1024)].ToArray();
                
                inputMetric.Now(data.Length);
                while (xdma.InputDMA.Count > 1000)
                {
                    //如果输入队列积压数据过多会导致内存耗光拖慢整体运行速度，需要防止
                    Thread.Sleep(10000);
                }
                //Thread.Sleep(50);
                while (unpackBlockV2.Cache>10000)
                {
                    Thread.Sleep(1000);
                }
            
                xdma.Enqueue(data);
                buffer.RemoveRange(0,512*1024);
            }
            
         
       
        }

        Console.WriteLine("加载结束");

        Console.ReadKey();
    }
}

[Command("KMWindows")]
public class KMDecodeWindowsCommand
{
    private readonly ulong CPUmask_FPGA = 00;
    private ulong CPUmask_CPU = 00;
    private bool isRead = true;

    private readonly LinkNode BoardNode = new()
    {
        Name = "板卡通道"
    };

    private readonly LinkNode[] Source_Targets = new LinkNode[4]
    {
        new()
        {
            Name = "短波上下游连接通道"
        },
        new()
        {
            Name = "中波上下游连接通道"
        },
        new()
        {
            Name = "星上开窗上下游连接通道"
        },
        new()
        {
            Name = "数据开窗上下游连接通道"
        }
    };

    private LinkNode[] boardNodes = new LinkNode[4]
    {
        new()
        {
            Name = "板卡1",
            Status = new Dictionary<string, object>
            {
                { "输入", 0 },
                { "输出", 0 },
                { "待处理队列", 0 }
            }
        },
        new()
        {
            Name = "板卡2",
            Status = new Dictionary<string, object>
            {
                { "输入", 0 },
                { "输出", 0 },
                { "待处理队列", 0 }
            }
        },
        new()
        {
            Name = "板卡3",
            Status = new Dictionary<string, object>
            {
                { "输入", 0 },
                { "输出", 0 },
                { "待处理队列", 0 }
            }
        },
        new()
        {
            Name = "板卡4",
            Status = new Dictionary<string, object>
            {
                { "输入", 0 },
                { "输出", 0 },
                { "待处理队列", 0 }
            }
        }
    };

    private readonly LinkNode ShortNode = new()
    {
        Name = "短波通道"
    };

    private LinkNode MidNode = new()
    {
        Name = "中波通道"
    };

    private LinkNode StarWinNode = new()
    {
        Name = "星上开窗通道"
    };

    private LinkNode DataWinNode = new()
    {
        Name = "数据开窗通道"
    };

    [Command("Short")]
    public void Short(string filePath = "D:\\code", string SavePath = @"D:\data", int channel = 0, int listenPort = 0,
        string dev = "", bool saveSrc = true, bool saveBoard = true, bool savecam = true, int sendtimes = 1,
        int sendspeed = 20, bool IsEcc = false)
    {
        LinkApiBuilder.Create(5500).AddLink(() => new LinkNode(
            BoardNode, Source_Targets[0], ShortNode
        )).Run();

        var pathName = "短波通道";
        var savePath = SavePath;
        var taskID = DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒") + pathName;
        var useFPGA = true;
        var speed = sendspeed;
        var sendTimes = sendtimes;
        var Channel = channel;
        var files = Directory.EnumerateFiles(filePath, "*.src", SearchOption.TopDirectoryOnly).ToArray();
        //files = files.Append(files[0]).ToArray(); // 清空缓存读出最后一帧

        SetLog($"{SavePath}/logs/{taskID}");
        ShortMidDecoder_FPGA decodeBlock;
        var saveBlock = new RollingSaveBlockV3(SaveBlockConfig.KM_SHORT, CPUmask_FPGA, pathName);

        var utf8 = new UTF8Encoding();
        var saveCount = new Dictionary<string, int>();
        var GetInfo = new GetInfoBlock($"{savePath}/数据报表/{pathName}");

        //XR将CAM转为RAW使用
        var imageConvert = new ImageConvert();
        var avgrates = new Dictionary<string, List<double>>();

        decodeBlock = new ShortMidDecoder_FPGA(new int[]
            { }, new int[] { }, IsEcc, Channel);
        var BoardDataSavePath = $"{savePath}/板卡数据/{DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒")}{pathName}";
        var ErrorPath = $"{BoardDataSavePath}/错误包收集";
        if (!Directory.Exists(ErrorPath)) Directory.CreateDirectory(ErrorPath);
        var Outputpath = $"{BoardDataSavePath}/板卡包输出";
        if (!Directory.Exists(Outputpath)) Directory.CreateDirectory(Outputpath);
        var Inputputpath = $"{BoardDataSavePath}/板卡包输入";
        if (!Directory.Exists(Inputputpath)) Directory.CreateDirectory(Inputputpath);

        decodeBlock.BoardPackArrived += (channel, isoutput, datapack) =>
        {
            if (saveBoard)
            {
                if (isoutput)
                    File.WriteAllBytes($"{Outputpath}/{datapack.Info.ToKey()}", datapack.Data);
                else
                    File.WriteAllBytes($"{Inputputpath}/{datapack.Info.ToKey()}", datapack.Data);
            }
        };
        var BoardRead = File.OpenWrite($"{BoardDataSavePath}//板卡{Channel}原始输出.src");
        decodeBlock.BoardReadArrived += (channel, data) =>
        {
            if (saveBoard) BoardRead.Write(data);
        };
        var BoardWrite = File.OpenWrite($"{BoardDataSavePath}//板卡{Channel}原始写入.src");
        decodeBlock.BoardWriteArrived += (channel, data) =>
        {
            if (saveBoard) BoardWrite.Write(data);
        };
        decodeBlock.ErrorBoardPackArrived += datapack =>
        {
            File.WriteAllBytes($"{ErrorPath}/{datapack.Type}{datapack.Info.ToKey()}", datapack.Data);
        };

        for (var i = 0; i < 4; i++)
        {
            var name = $"短波_探测器{i.ToString().PadLeft(3, '0')}";
            saveBlock.AddStorage($"{i + 10}", savePath, taskID, name,
                "{root}/{TaskID}/{name}/{index_prex}.km.{datatype}.cam");
            saveCount[$"{i + 10}"] = 0;
        }

        saveBlock.AddStorage("error", savePath, taskID, "错误收集",
            "{root}/{TaskID}/{name}/{index_prex}.n01.{datatype}.cam");
        saveCount["error"] = 0;
        decodeBlock.ErrorArrived += pack =>
        {
            var ErrorPack = MessagePackSerializer.Deserialize<ErrorInfoPack>(pack);
            saveBlock.Save("error", saveCount["error"], pack);
        };
        decodeBlock.PackArrived += data =>
        {
            var Key = (data as FramePack).Image.Channel.ToString();
            ++saveCount[Key];
            Console.WriteLine($"短波探测器{int.Parse(Key) - 10}第{saveCount[Key]}帧已输出");
            saveBlock.Save(Key, saveCount[Key], (data as FramePack).Image.Data);
        };
        var StarCamreaAssitCount = 0;
        var CompressCamreaAssitCount = 0;
        var unpack = new TogetherUnpackBlock(3488);
        var currentCount = new Dictionary<string, int>();
        //----------------------------------------//
        //var saveDir = @"D:\CJH\togetherUnpack";
        //if (!Directory.Exists(saveDir))
        //{
        //    Directory.CreateDirectory(saveDir);
        //}
        //FileStream[] saveStreams = new FileStream[100];
        //---------------------------------------//
        unpack.PackArrived += (data, packCount, detectorID, windetectorID, typeinfo) =>
        {
            //------------
            //if (saveStreams[detectorID] is null)
            //{
            //    saveStreams[detectorID] = new FileStream(Path.Combine(saveDir, $"unpacked_data_{detectorID}.src"), FileMode.Append, FileAccess.Write);
            //}
            //saveStreams[detectorID].Write(data, 0, data.Length);
            //saveStreams[detectorID].Flush();
            //-------------
            switch (typeinfo)
            {
                case "short_A":
                    detectorID += 10;
                    decodeBlock.Enqueue(detectorID, data);
                    break;
                case "short_B":
                    detectorID += 14;
                    decodeBlock.Enqueue(detectorID, data);
                    break;
                case "short_C":
                    detectorID += 20;
                    decodeBlock.Enqueue(detectorID, data);
                    break;
                case "short_D":
                    detectorID += 24;
                    decodeBlock.Enqueue(detectorID, data);
                    break;
                case "mid_A":
                    detectorID += 30;
                    decodeBlock.Enqueue(detectorID, data);
                    break;
                case "mid_B":
                    detectorID += 34;
                    decodeBlock.Enqueue(detectorID, data);
                    break;
                case "mid_C":
                    detectorID += 40;
                    decodeBlock.Enqueue(detectorID, data);
                    break;
                case "mid_D":
                    detectorID += 44;
                    decodeBlock.Enqueue(detectorID, data);
                    break;
            }
        };

        Thread.Sleep(1000);
        var count = 0;
        if (saveSrc)
            saveBlock.AddStorage("SRC", savePath, taskID, "",
                "{root}/{TaskID}/aa/{index_prex}.src".Replace("aa", pathName + "原始码流"));

        Task.Run(() =>
        {
            while (true)
            {
                Console.WriteLine(decodeBlock.GetStatus());
                Thread.Sleep(1000);
            }
        });


        foreach (var data in files.Load(1))
        {
            unpack.Enqueue(data);
            // decodeBlock.Enqueue(10, data);
            count++;
        }

        //------------
        //foreach (var saveStream in saveStreams)
        //{
        //    saveStream?.Dispose();
        //}
        //------------
        while (true) Thread.Sleep(1000);
    }


    public void SetLog(string home)
    {
        var path = string.IsNullOrEmpty(home)
            ? Path.Combine("logs", "service")
            : Path.Combine(home, "logs", "service");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        var builder = new LoggerConfiguration().MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
            .Enrich.WithProperty("app name", "decompress").WriteTo.Console()
            .Enrich.WithProperty("app name", "decompress")
            .WriteTo.File(Path.Combine(path, "log.txt"), rollingInterval: RollingInterval.Hour, outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Name} {Status} {Message}{NewLine}{Exception}",
                retainedFileCountLimit: 24 * 90);
        Log.Logger = builder.CreateLogger();
    }

    private string SwitchName(int channel)
    {
        if (channel >= 10 && channel < 14) return $"短波_探测器A{(channel - 10).ToString().PadLeft(3, '0')}";
        if (channel >= 14 && channel < 20) return $"短波_探测器B{(channel - 14).ToString().PadLeft(3, '0')}";
        if (channel >= 20 && channel < 24) return $"短波_探测器C{(channel - 20).ToString().PadLeft(3, '0')}";
        if (channel >= 24 && channel < 30) return $"短波_探测器D{(channel - 24).ToString().PadLeft(3, '0')}";
        if (channel >= 30 && channel < 34) return $"中波_探测器A{(channel - 30).ToString().PadLeft(3, '0')}";
        if (channel >= 34 && channel < 40) return $"中波_探测器B{(channel - 34).ToString().PadLeft(3, '0')}";
        if (channel >= 40 && channel < 44) return $"中波_探测器C{(channel - 40).ToString().PadLeft(3, '0')}";
        if (channel >= 44 && channel < 50) return $"中波_探测器D{(channel - 44).ToString().PadLeft(3, '0')}";
        if (channel >= 50 && channel < 150) return $"数据开窗短波FPGA1_开窗号{(channel - 50).ToString().PadLeft(3, '0')}";
        if (channel >= 150 && channel < 250) return $"数据开窗短波FPGA2_开窗号{(channel - 150).ToString().PadLeft(3, '0')}";
        if (channel >= 250 && channel < 350) return $"数据开窗中波FPGA1_开窗号{(channel - 250).ToString().PadLeft(3, '0')}";
        if (channel >= 350 && channel < 450) return $"数据开窗中波FPGA2_开窗号{(channel - 350).ToString().PadLeft(3, '0')}";
        return "有问题的";
    }
}