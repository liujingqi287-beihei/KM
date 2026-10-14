using CommandDotNet;
using Decompress;
using Decompress.Codec.L100.Const;
using Decompress.Codec.L100.Const.BitWiden;
using Decompress.Codec.L200.Decoder.L210.Unpack;
using DemoConsole;
using MessagePack;
using KM.Codec.L100.Const;
using KM.Codec.L300.Decode.L310.Shared.Step0.TogetherPack;
using KM.Codec.L300.Decode.L310.Shared.Step1.SetInfoTail;
using KM.Codec.L300.Decode.L310.Shared.Step2.ECC;
using KM.Codec.L300.Decode.L310.Shared.Step3._5.RefimageMatch;
using KM.Codec.L300.Decode.L310.Shared.Step3._6.DispatchPack;
using KM.Codec.L300.Decode.L310.Shared.Step3._9.DecodeAdaptor;
using KM.Codec.L300.Decode.L310.Shared.Step3.EDC;
using KM.Codec.L300.Decode.L340.WideViewWinDecoder;
using KM.Codec.L300.Decode.L360.NewStructDecoder;
using KM.ConsoleDecode.Dispatch.RecieveData;
using FileCompareModule;
using Newtonsoft.Json;
using Protocols;
using Serilog;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KM.ConsoleDecode.Send;

namespace KM.ConsoleDecode
{
    [Command("newdecode")]
    public class NewDecodeCommand
    {
        private object locker = new object();

        public object SoftDecodeCommand { get; private set; }

        [Command("offline")]
        public void Offline(string pathName, string fileType, string filePath, int speed, int sendTimes, string savePath, string taskID, bool saveSrc, int times = 1, int FrameRate = 30)
        {
            int cpuMask = 0xf;
            Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)cpuMask;
            SetLog($"{savePath}");
            //long cpu = (long)(Math.Pow(2, 32) + Math.Pow(2, 33) + Math.Pow(2, 34) + Math.Pow(2, 35));
            //IntPtr cpuindex = new IntPtr(cpu);
            //Process.GetCurrentProcess().ProcessorAffinity = cpuindex;
            string[] files = null;

            if (fileType == "文件")
            {
                files = new string[] { filePath };
            }
            if (fileType == "文件夹")
            {
                files = Directory.EnumerateFiles(filePath, "*", SearchOption.AllDirectories).ToArray();

            }
            $"{savePath}\\{taskID}\\{pathName}\\数据报表\\{pathName}报表.csv".CreateFileDir();
            UTF8Encoding utf8 = new System.Text.UTF8Encoding(true);
            FileStream fs = File.Open($"{savePath}\\{taskID}\\{pathName}\\数据报表\\{pathName}报表.csv", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            InfoTailInfo infoTail = new InfoTailInfo();
            InfoTailInfoForWin infoForWin = new InfoTailInfoForWin();


            TogetherUnpackBlock unpack;
            int innerPort = 0;
            int LogPort = 33333;
            switch (pathName)
            {
                case "短波通道":
                    unpack = new TogetherUnpackBlock(3488,(ulong)cpuMask);
                    fs.Write(utf8.GetPreamble());
                    fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoTail)));
                    innerPort = 12120;
                    LogPort = 33333;
                    break;
                case "中波通道":
                    unpack = new TogetherUnpackBlock(3488, (ulong)cpuMask);
                    fs.Write(utf8.GetPreamble());
                    fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoTail)));
                    innerPort = 12130;
                    LogPort = 33334;
                    break;
                case "数据开窗通道":
                    unpack = new TogetherUnpackBlock(3488, (ulong)cpuMask);
                    fs.Write(utf8.GetPreamble());
                    fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoForWin)));
                    innerPort = 12150;
                    LogPort = 33336;
                    break;
                default:
                    Log.Logger.Error("拆合路判断通道类型:不合法的通道类型");
                    return;
            }
            //使用tcp传递报表信息
            TCPHubService notifyHub = new TCPHubService(LogPort);
            TCPDispatchServiceBlock server = new TCPDispatchServiceBlock(innerPort);

            server.DataArrived += (endpoint, data) =>
            {
                Log.Logger.Information(utf8.GetString(data) + $"拆合路{unpack.togetherPack.QueueCount}  输入{unpack.InputMetric.Last.BytesSpeed(1000)}  输出{unpack.OutputMetric.Last.BytesSpeed(1000)}  内存占用量{MemorySize.MEMORYINFO.GetMemoryUsed()}");
            };
            var errorPath = $"{savePath}/{taskID}/{pathName}/错误收集";
            Directory.CreateDirectory(errorPath);
            Dictionary<string, int> currentCount = new Dictionary<string, int>();
            unpack.PackArrived += (d, c, n, t,i) =>
            {
                //if (!currentCount.Keys.Contains(t + n))
                //{
                //    currentCount[t + n] = 0;
                //}
                //if (c != 0 && c - currentCount[t + n] != 1)
                //{
                //    File.WriteAllBytes($"{errorPath}/{DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒")}_{t}_{n}_{c}_合路包缺失.src", d);
                //    Log.Logger.Error($"合路包出现缺失错误,缺失项为{t}_{n}的{c - 1}包数据,该包数据的 下一包 已保存在{errorPath}");
                //}
                //currentCount[t + n] = c;
                if (i == "short_A")
                {
                    n += 10;
                }
                if (i == "short_B")
                {
                    n += 14;
                }
                if (i == "short_C")
                {
                    n += 20;
                }
                if (i == "short_D")
                {
                    n += 24;
                }
                if (i == "mid_A")
                {
                    n += 30;
                }
                if (i == "mid_B")
                {
                    n += 34;
                }
                if (i == "mid_C")
                {
                    n += 40;
                }
                if (i == "mid_D")
                {
                    n += 44;
                }
                if (i == "data_win")
                {
                    if (t == "短波FPGA1")
                        n += 50;
                    else if (t == "短波FPGA2")
                        n += 150;
                    else if (t == "中波FPGA1")
                        n += 250;
                    else
                        n += 350;
                }
                //var pack = new TCPPack(n,d,true);
                //var data = MessagePackSerializer.Serialize(pack);
                server.Enqueue((n.ToString(), d));
            };
            int TimerCount = 0;
            System.Timers.Timer timer = new System.Timers.Timer(2);
            timer.AutoReset = true;
            timer.Elapsed += (e, h) =>
            {
                if (notifyHub.LogData.TryDequeue(out byte[] result))
                {
                    fs.Write(result);
                }
                TimerCount++;
                if (TimerCount > 250)
                {
                    TimerCount = 0;
                    fs.Flush();
                }
            };
            timer.Start();
            System.Timers.Timer timer2 = new System.Timers.Timer(1000);
            timer2.AutoReset = true;
            timer2.Elapsed += (e, h) =>
            {
                Console.WriteLine($"输入{unpack.InputMetric.Last.BytesSpeed(1000)} 输出{unpack.OutputMetric.Last.BytesSpeed(1000)} 堆积{unpack.QueueCount}");
            };
            timer2.Start();
            notifyHub.Start();


            server.Start();
            Thread.Sleep(1000);


            int count = 0;
            var save = new RollingFileStreamV2(savePath, taskID, "", "", "{root}/{TaskID}/{pathName}/aa/{index_prex}.src".Replace("{pathName}", pathName).Replace("aa", pathName + "原始码流"));
            //直接从文件读取数据
            foreach (var data in files.LoadBig(speed,sendTimes))
            {
                if (saveSrc)
                {
                    save.Write(data, count / 1000 + 1);
                    count++;
                }
                unpack.Enqueue(data);
                count++;
                //if (count % 1000 == 0)
                //{
                //    Console.WriteLine(unpack.OutputMetric.Last.BytesSpeed(1000));
                //}
            }

            while (true)
            {
                //Thread.Sleep(1000);
                //Console.WriteLine(unpack.OutputMetric.Last.BytesSpeed(1000));

            }
        }
        [Command("online")]
        public void Online(string pathName, string IP, int Port, string vcid, string savePath, string taskID, bool saveSrc)
        {
            int cpuMask = 0xf;
            Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)cpuMask;
            SetLog($"{savePath}");

            //long cpu = (long)(Math.Pow(2, 32) + Math.Pow(2, 33) + Math.Pow(2, 34) + Math.Pow(2, 35));
            //IntPtr cpuindex = new IntPtr(cpu);
            //Process.GetCurrentProcess().ProcessorAffinity = cpuindex;

            SocketSourceBlock_WithVCID socketSource = new SocketSourceBlock_WithVCID(pathName, vcid, new List<HostOptions>
            {
                new HostOptions()
                {
                    Host=IP,
                    Port=Port
                }
            });
            $"{savePath}\\{taskID}\\{pathName}\\数据报表\\{pathName}报表.csv".CreateFileDir();
            UTF8Encoding utf8 = new System.Text.UTF8Encoding(true);
            FileStream fs = File.Open($"{savePath}\\{taskID}\\{pathName}\\数据报表\\{pathName}报表.csv", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            InfoTailInfo infoTail = new InfoTailInfo();
            InfoTailInfoForWin infoForWin = new InfoTailInfoForWin();

            TogetherUnpackBlock unpack;
            int innerPort = 0;
            int LogPort = 33333;
            switch (pathName)
            {
                case "短波通道":
                    unpack = new TogetherUnpackBlock(3488,(ulong)cpuMask);
                    fs.Write(utf8.GetPreamble());
                    fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoTail)));
                    innerPort = 12120;
                    LogPort = 33333;
                    break;
                case "中波通道":
                    unpack = new TogetherUnpackBlock(3488, (ulong)cpuMask);
                    fs.Write(utf8.GetPreamble());
                    fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoTail)));
                    innerPort = 12130;
                    LogPort = 33334;
                    break;
                case "数据开窗通道":
                    unpack = new TogetherUnpackBlock(3488, (ulong)cpuMask);
                    fs.Write(utf8.GetPreamble());
                    fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoForWin)));
                    innerPort = 12150;
                    LogPort = 33336;
                    break;
                default:
                    Log.Logger.Error("拆合路判断通道类型:不合法的通道类型");
                    return;
            }
            //使用tcp传递报表信息
            TCPHubService notifyHub = new TCPHubService(LogPort);
            TCPDispatchServiceBlock server = new TCPDispatchServiceBlock(innerPort);


            int count = 0;
            var save = new RollingFileStreamV2(savePath, taskID, "", "", "{root}/{TaskID}/{pathName}/aa/{index_prex}.src".Replace("{pathName}", pathName).Replace("aa", pathName + "原始码流"));
            socketSource.DataArrived += (data) =>
            {
                if (saveSrc)
                {
                    save.Write(data, count / 1000 + 1);
                    count++;
                }
                unpack.Enqueue(data);
            };
            server.DataArrived += (endpoint, data) =>
            {
                Log.Logger.Information(utf8.GetString(data)/* + Environment.NewLine + $"拆合路{unpack.togetherPack.QueueCount}  输入{unpack.InputMetric.Last.BytesSpeed(1000)}  输出{unpack.OutputMetric.Last.BytesSpeed(1000)}  内存占用量{MemorySize.MEMORYINFO.GetMemoryUsed()}"*/);
            };
            var errorPath = $"{savePath}/{taskID}/{pathName}/错误收集";
            Directory.CreateDirectory(errorPath);
            Dictionary<string, int> currentCount = new Dictionary<string, int>();
            unpack.PackArrived += (d, c, n, t, i) =>
            {
                //if (!currentCount.Keys.Contains(t + n))
                //{
                //    currentCount[t + n] = 0;
                //}
                //if (c != 0 && c - currentCount[t + n] != 1)
                //{
                //    File.WriteAllBytes($"{errorPath}/{DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒")}_{t}_{n}_{c}_合路包缺失.src", d);
                //    Log.Logger.Error($"合路包出现缺失错误,缺失项为{t}_{n}的{c - 1}包数据,该包数据的 下一包 已保存在{errorPath}");
                //}
                //currentCount[t + n] = c;
                if (i == "short_A")
                {
                    n += 10;
                }
                if (i == "short_B")
                {
                    n += 14;
                }
                if (i == "short_C")
                {
                    n += 20;
                }
                if (i == "short_D")
                {
                    n += 24;
                }
                if (i == "mid_A")
                {
                    n += 30;
                }
                if (i == "mid_B")
                {
                    n += 34;
                }
                if (i == "mid_C")
                {
                    n += 40;
                }
                if (i == "mid_D")
                {
                    n += 44;
                }
                if(i == "data_win")
                {
                    if (t == "短波FPGA1")
                        n += 50;
                    else if (t == "短波FPGA2")
                        n += 150;
                    else if (t == "中波FPGA1")
                        n += 250;
                    else
                        n += 350;
                }
                //var pack = new TCPPack(n,d,true);
                //var data = MessagePackSerializer.Serialize(pack);
                server.Enqueue((n.ToString(), d));
            };
            int TimerCount = 0;
            System.Timers.Timer timer = new System.Timers.Timer(2);
            timer.AutoReset = true;
            timer.Elapsed += (e, h) =>
            {
                if (notifyHub.LogData.TryDequeue(out byte[] result))
                {
                    fs.Write(result);
                }
                TimerCount++;
                if (TimerCount > 250)
                {
                    TimerCount = 0;
                    fs.Flush();
                }
            };
            timer.Start();

            System.Timers.Timer timer2 = new System.Timers.Timer(1000);
            timer2.AutoReset = true;
            timer2.Elapsed += (e, h) =>
            {
                Console.WriteLine($"输入{unpack.InputMetric.Last.BytesSpeed(1000)} 输出{unpack.OutputMetric.Last.BytesSpeed(1000)} 堆积{unpack.QueueCount}");
            };
            timer2.Start();

            notifyHub.Start();


            server.Start();
            ////发送令适配器端重置通道状态的数据包
            //client.Send(MessagePackSerializer.Serialize(new TCPPack(0,new byte[1],false)));
            Thread.Sleep(1000);
            socketSource.Start(new System.Threading.CancellationToken());
            while (true)
            {

            }

        }

        [Command("Decode")]
        public void Decode(string pathName, string savePath, string taskID, bool saveSrc, bool saveData, bool saveRaw, bool saveCam, bool downStreamRepost, bool IsCompare, bool IsOffline,string fileType, string filePath, int speed, int sendTimes=1)
        {
            //Thread.Sleep(10000);
            var save = new RollingFileStreamV2(savePath, taskID, "", "", "{root}/{TaskID}/{pathName}/aa/{index_prex}.src".Replace("aa", pathName + "原始码流").Replace("{pathName}", pathName));
            int readSpeed;
            int ChunkSize;
            SoftDecodeCommand DecodeCommand;
            SendFileBlock sendFile;
            //NotifyService service = new NotifyService("127.0.0.1", 55555);

            //pathName = pathName switch
            //{
            //    "数据开窗通道A" => "数据开窗通道",
            //    "数据开窗通道B" => "数据开窗通道",
            //    "星上处理通道A" => "星上处理通道",
            //    "星上处理通道B" => "星上处理通道",
            //    "短波通道A" => "短波通道",
            //    "短波通道B" => "短波通道",
            //    "中波通道A" => "中波通道",
            //    "中波通道B" => "中波通道",
            //};
            //Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0xf;
            if (pathName == "数据开窗通道A")
            {
                sendFile = new SendFileBlock(pathName, 33333);
                int[] eccCore= Enumerable.Range(20, 2).ToArray();
                int[] dispatchCore = Enumerable.Range(22, 6).ToArray();
                DecodeCommand = new SoftDecodeCommand(eccCore, dispatchCore, SaveBlockConfig.KM_WIN, pathName, savePath, taskID, saveData, saveRaw, saveCam, downStreamRepost, IsCompare);
                readSpeed = 100;
                ChunkSize = 100;
            }
            else if (pathName == "数据开窗通道B")
            {
                sendFile = new SendFileBlock(pathName, 33333);
                int[] eccCore = Enumerable.Range(20, 2).ToArray();
                int[] dispatchCore = Enumerable.Range(22, 6).ToArray();
                DecodeCommand = new SoftDecodeCommand(eccCore, dispatchCore, SaveBlockConfig.KM_WIN, pathName, savePath, taskID, saveData, saveRaw, saveCam, downStreamRepost, IsCompare);
                readSpeed = 100;
                ChunkSize = 100;
            }
            else if (pathName == "短波通道A")
            {
                sendFile = new SendFileBlock(pathName, 33333);
                int[] eccCore = Enumerable.Range(4, 1).ToArray();
                int[] dispatchCore = Enumerable.Range(34, 28).ToArray();
                DecodeCommand = new SoftDecodeCommand(eccCore, dispatchCore, SaveBlockConfig.KM_SHORT, pathName, savePath, taskID, saveData, saveRaw, saveCam, downStreamRepost, IsCompare);
                readSpeed = 500;
                ChunkSize = 500;
            }
            else if (pathName == "短波通道B")
            {
                sendFile = new SendFileBlock(pathName, 33333);
                int[] eccCore = Enumerable.Range(4, 1).ToArray();
                int[] dispatchCore = Enumerable.Range(34, 28).ToArray();
                DecodeCommand = new SoftDecodeCommand(eccCore, dispatchCore, SaveBlockConfig.KM_SHORT, pathName, savePath, taskID, saveData, saveRaw, saveCam, downStreamRepost, IsCompare);
                readSpeed = 500;
                ChunkSize = 500;
            }
            else if (pathName == "短波通道C")
            {
                sendFile = new SendFileBlock(pathName, 33333);
                int[] eccCore = Enumerable.Range(4, 1).ToArray();
                int[] dispatchCore = Enumerable.Range(34, 28).ToArray();
                DecodeCommand = new SoftDecodeCommand(eccCore, dispatchCore, SaveBlockConfig.KM_SHORT, pathName, savePath, taskID, saveData, saveRaw, saveCam, downStreamRepost, IsCompare);
                readSpeed = 500;
                ChunkSize = 500;
            }
            else if (pathName == "短波通道D")
            {
                sendFile = new SendFileBlock(pathName, 33333);
                int[] eccCore = Enumerable.Range(4, 1).ToArray();
                int[] dispatchCore = Enumerable.Range(34, 28).ToArray();
                DecodeCommand = new SoftDecodeCommand(eccCore, dispatchCore, SaveBlockConfig.KM_SHORT, pathName, savePath, taskID, saveData, saveRaw, saveCam, downStreamRepost, IsCompare);
                readSpeed = 500;
                ChunkSize = 500;
            }
            else if (pathName == "星上处理通道A")
            {
                sendFile = new SendFileBlock(pathName, 33333);
                int[] eccCore = Enumerable.Range(28, 28).ToArray();
                int[] dispatchCore = Enumerable.Range(28, 28).ToArray();
                DecodeCommand = new SoftDecodeCommand(eccCore, dispatchCore, SaveBlockConfig.KM_WIN, pathName, savePath, taskID, saveData, saveRaw, saveCam, downStreamRepost, IsCompare);
                readSpeed = 100;
                ChunkSize = 100;
            }
            else if (pathName == "星上处理通道B")
            {
                sendFile = new SendFileBlock(pathName, 33333);
                int[] eccCore = Enumerable.Range(28, 28).ToArray();
                int[] dispatchCore = Enumerable.Range(28, 28).ToArray();
                DecodeCommand = new SoftDecodeCommand(eccCore, dispatchCore, SaveBlockConfig.KM_WIN, pathName, savePath, taskID, saveData, saveRaw, saveCam, downStreamRepost, IsCompare);
                readSpeed = 100;
                ChunkSize = 100;
            }
            else
            {
                sendFile = new SendFileBlock(pathName, 33333);
                int[] eccCore = Enumerable.Range(28, 28).ToArray();
                int[] dispatchCore = Enumerable.Range(28, 28).ToArray();
                DecodeCommand = new SoftDecodeCommand(eccCore, dispatchCore, SaveBlockConfig.KM_MID, pathName, savePath, taskID, saveData, saveRaw, saveCam, downStreamRepost, IsCompare);
                readSpeed = 500;
                ChunkSize = 500;
            }

            //service.SetStatusFunc(() =>
            //{
            //    return sendFile.Status;
            //});
            //service.Start();

            int count = 0;

            Func<bool> IsNeedWait = () =>
            {
                
                return DecodeCommand.QueueSumCount > 1500;
            };
            // FileCacheBlock _Cache = new FileCacheBlock(@"D:\DataCache\" + taskID +@"\" + pathName, readSpeed, ChunkSize, saveSrc, IsNeedWait);

            if (downStreamRepost)
            {
                DecodeCommand.DataArrived += (data) =>
                {
                    sendFile.Equeue(data);
                };
            }

            //_Cache.DataArrived += (data) =>
            //{
            //    DecodeCommand.Enqueue(data);
            //};
            if (IsOffline)
            {
                string[] files = null;
                if (fileType == "文件")
                {
                    files = new string[] { filePath };
                }
                if (fileType == "文件夹")
                {
                    files = Directory.EnumerateFiles(filePath, "*", SearchOption.AllDirectories).ToArray();
                }
                foreach (var data in files.LoadBig(speed, sendTimes))
                {
                    //if (saveSrc)
                    //{
                    //    save.Write(data, count / 1000 + 1);
                    //    count++;
                    //}

                    DecodeCommand.UpdateStatus(data.Length);
                    DecodeCommand.Enqueue(data);
                    // _Cache.Enqueue(data);
                }
                while(true)
                {

                };
            }
            else
            {
                
                string vcid = fileType;
                string IP = filePath;
                int Port = speed;
                SocketSourceBlock_WithVCID socketSource = new SocketSourceBlock_WithVCID(pathName, vcid, new List<HostOptions>
                {
                    new HostOptions()
                    {
                        Host=IP,
                        Port=Port
                    }
                });
                socketSource.DataArrived += (data) =>
                {
                    //if (saveSrc)
                    //{
                    //    save.Write(data, count / 1000 + 1);
                    //    count++;
                    //}

                    DecodeCommand.UpdateStatus(data.Length);
                    DecodeCommand.Enqueue(data);
                    // _Cache.Enqueue(data);
                    
                };
                socketSource.Start(new System.Threading.CancellationToken());
                while (true)
                {
                };
            }
            
        }

        [Command("OfflineDecodeWin")]
        public void OfflineDecodeWin(string pathName, string fileType, string filePath, int speed, int sendTimes, string savePath, string taskID, bool saveSrc, bool saveData, bool saveRaw, bool saveCam, bool downStreamRepost, bool IsCompare)
        {
            //拆包部分
            ulong cpuMask = 0x3c00f;
            Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)cpuMask;

            TogetherUnpackBlock unpack = new TogetherUnpackBlock(3488, (ulong)cpuMask);
            NewStructDecodeBlock_temp decodeBlock = new NewStructDecodeBlock_temp(new int[2] { 32, 33 }, new int[2] { 34, 35 }, DispatchType.轮询调度, 14, cpuMask);
            RollingSaveBlockV2 saveBlock;
            
            UTF8Encoding utf8 = new UTF8Encoding();
            ConcurrentDictionary<string, int> saveCount = new ConcurrentDictionary<string, int>();

            Dictionary<string, Stopwatch> sws = new Dictionary<string, Stopwatch>();
            Dictionary<string, Queue<double>> avglists = new Dictionary<string, Queue<double>>();
            Dictionary<string, List<double>> avgrates = new Dictionary<string, List<double>>();

            TCPClientBlock client = new TCPClientBlock("数据开窗", "127.0.0.1", 9999);
            GetInfoBlock GetInfo = new GetInfoBlock($"{savePath}\\{taskID}\\{pathName}\\数据报表\\{pathName}报表.csv");
            int count = 0;
            int FrameFre = 0;
            long _totalCount = 0;
            var save = new RollingFileStreamV2(savePath, taskID, "", "", "{root}/{TaskID}/{pathName}/aa/{index_prex}.src".Replace("aa", pathName + "原始码流").Replace("{pathName}", pathName));
            saveBlock = new RollingSaveBlockV2(SaveBlockConfig.KM_WIN, (ulong)cpuMask);

            //var CompareBlock = new DataCompare();

            SetLog($"{savePath}");

            string[] files = null;

            if (fileType == "文件")
            {
                files = new string[] { filePath };
            }
            if (fileType == "文件夹")
            {
                files = Directory.EnumerateFiles(filePath, "*", SearchOption.AllDirectories).ToArray();

            }

            unpack.PackArrived += (d, c, n, t, i) =>
            {
                if (i == "short_A")
                {
                    n += 10;
                }

                if (i == "short_B")
                {
                    n += 14;
                }

                if (i == "short_C")
                {
                    n += 20;
                }

                if (i == "short_D")
                {
                    n += 24;
                }

                if (i == "mid_A")
                {
                    n += 30;
                }

                if (i == "mid_B")
                {
                    n += 34;
                }
                if (i == "mid_C")
                {
                    n += 40;
                }
                if (i == "mid_D")
                {
                    n += 44;
                }
                if (i == "data_win")
                {
                    if (t == "短波FPGA1")
                        n += 50;
                    else if (t == "短波FPGA2")
                        n += 150;
                    else if (t == "中波FPGA1")
                        n += 250;
                    else
                        n += 350;
                }
                decodeBlock.Enqueue(n, d);
            };

            ////解码部分
            List<byte> CompareData = new List<byte>();
            decodeBlock.MidArrived += (data) =>
            {
                var name = SwitchName(data.Channel);
                if (!saveBlock.Storage.Keys.Contains($"DATA_{name}"))
                {
                    if (saveData)
                    {
                        saveBlock.AddStorage($"DATA_{name}", savePath, taskID, name, "{root}/{TaskID}/{pathName}/aa/{name}/{index_prex}.KM.{datatype}.src".Replace("aa", pathName + "分帧码流").Replace("{pathName}", pathName));
                    }

                }
                if (!saveCount.Keys.Contains($"DATA_{name}"))
                {
                    saveCount[$"DATA_{name}"] = 0;
                }
                if (data.Type == DataPackType.SyncHead)
                {
                    saveCount[$"DATA_{name}"]++;
                }
                if (saveData)
                {
                    saveBlock.Save($"DATA_{name}", saveCount[$"DATA_{name}"], data.Data);
                }
                if (IsCompare)
                {
                    CompareData.AddRange(data.Data.ToArray());
                    if (data.Type == DataPackType.FrameTail)
                    {
                        //CompareBlock.StartCompare(CompareData.ToArray(), name);
                        CompareData.Clear();
                    }
                }
            };
            decodeBlock.PackArrived += (key, data) =>
            {
                var nameKey = key.ToName();
                var Key = data.Channel.ToString();
                string[] TypeKey = nameKey.Split("_");
                try
                {
                    if (!saveBlock.Storage.Keys.Contains(Key))
                    {
                        if(saveCam)
                        {
                            saveBlock.AddStorage(Key, savePath, taskID, TypeKey[0], "探测器" + TypeKey[1].PadLeft(3,'0'), "{root}/{TaskID}/{pathName}/{name}/{datatype}/{index_prex}.KM{datatype}.temp".Replace("{pathName}", pathName));
                        }
                        if (saveRaw)
                        {
                            saveBlock.AddStorage("RAW" + Key, savePath, taskID, TypeKey[0].PadLeft(3,'0'),"探测器" + TypeKey[1], "{root}/{TaskID}/{pathName}/aa/{name}_RAW/{datatype}/{index_prex}.KM{datatype}.raw".Replace("aa", pathName + "RAW图数据").Replace("{pathName}", pathName));
                        }
                    }
                    if (!saveCount.Keys.Contains(Key))
                    {
                        saveCount[Key] = 0;
                    }
                    switch (data.Type)
                    {
                        case DataPackType.ImagePatch:
                            if (saveCam)
                            {
                                saveBlock.Save(Key, saveCount[Key], data.Data);
                            }
                            if (saveRaw)
                            {
                                saveBlock.Save("RAW" + Key, saveCount[Key], data.Data);
                            }
                            break;
                        case DataPackType.SyncHead:
                            saveCount[Key]++;
                            break;
                        case DataPackType.InfoTail:
                            if (!sws.Keys.Contains(nameKey))
                            {
                                sws[nameKey] = new Stopwatch();
                            }
                            if (!avglists.Keys.Contains(nameKey))
                            {
                                avglists[nameKey] = new Queue<double>();
                            }
                            var spend = sws[nameKey].Elapsed.TotalMilliseconds;
                            avglists[nameKey].Enqueue(spend);
                            if (avglists[nameKey].Count > 6)
                            {
                                avglists[nameKey].Dequeue();
                            }
                            sws[nameKey].Restart();
                            GetInfo.Enqueue(data.WithInfo(utf8.GetBytes($"{nameKey}_文件号{saveCount[Key]}")).WithSpend(spend));
                            //Console.WriteLine(ExcuteAverage(avglists[nameKey].Where(p => p > 5)));
                            var result = $"{nameKey}已完成{saveCount[Key]}帧";
                            Console.WriteLine(result);
                            FrameFre++;
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(utf8.GetBytes(ex.StackTrace));
                }
            };

            GetInfo.InfoArrived += (e, t, a, i) =>
            {
                StatusInfoPack pack = new StatusInfoPack();
                try
                {
                    var WinInfo = MessagePackSerializer.Deserialize<InfoTailInfoForWin>(i);
                    if (!avgrates.Keys.Contains(WinInfo.数据类型))
                    {
                        avgrates[WinInfo.数据类型] = new List<double>();
                    }
                    avgrates[WinInfo.数据类型].Add(WinInfo.压缩比);
                    pack = new StatusInfoPack()
                    {
                        数据类型 = WinInfo.数据类型,
                        帧计数 = WinInfo.帧计数,
                        压缩比 = $"{WinInfo.压缩比.ToString("#0.00")}/{avgrates[WinInfo.数据类型].Average().ToString("#0.00")}",
                        数据状态 = WinInfo.数据状态,
                        耗时 = ExcuteAverage(avglists[WinInfo.数据类型].Where(p => p > 5)),
                        error = e,
                        total = t
                    };
                }
                catch (Exception ex)
                { 
                    Console.WriteLine(ex);
                }
                client.Enqueue(MessagePackSerializer.Serialize(pack));
            };
            client.Start();

            DateTime _startTime = DateTime.Now;
            System.Timers.Timer _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (sender, e) =>
            {
                int currentCount = Interlocked.Exchange(ref FrameFre, 0);
                Interlocked.Add(ref _totalCount, currentCount);
                TimeSpan elapsed = DateTime.Now - _startTime;
                double average = elapsed.TotalSeconds > 0 ? _totalCount / elapsed.TotalSeconds : 0;
                Console.WriteLine($"{DateTime.Now:HH:MM:ss}" +
                    $"当前秒帧数：{currentCount}|" +
                    $"平均次数/秒：{average:F2}|" +
                    $"运行时常：{elapsed.TotalSeconds:F1}秒");
                Console.WriteLine(decodeBlock.GetStatus());
            };
            _timer.AutoReset = true;
            _timer.Start();

            foreach (var data in files.LoadBig(speed, sendTimes))
            {
                if (saveSrc)
                {
                    save.Write(data, count / 1000 + 1);
                    count++;
                }
                unpack.Enqueue(data);
                count++;
            }

            //while (true)
            //{
            //    Thread.Sleep(1000);
            //    Console.WriteLine(decodeBlock.GetStatus());
            //}

        }

        [Command("OnlineDecodeWin")]
        public void OnlineDecodeWin(string pathName, string IP, int Port, string vcid, string savePath, string taskID, bool saveSrc, bool saveData, bool saveRaw, bool saveCam, bool downStreamRepost, bool IsCompare)
        {
            ulong cpuMask = 0x3c00f;
            Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)cpuMask;
            SetLog($"{savePath}");

            TogetherUnpackBlock unpack = new TogetherUnpackBlock(3488, (ulong)cpuMask);
            NewStructDecodeBlock_temp decodeBlock = new NewStructDecodeBlock_temp(new int[2] { 32,33}, new int[2] { 34,35}, DispatchType.轮询调度, 14, cpuMask);
            RollingSaveBlockV2 saveBlock;

            UTF8Encoding utf8 = new UTF8Encoding();
            ConcurrentDictionary<string, int> saveCount = new ConcurrentDictionary<string, int>();

            Dictionary<string, Stopwatch> sws = new Dictionary<string, Stopwatch>();
            Dictionary<string, Queue<double>> avglists = new Dictionary<string, Queue<double>>();
            Dictionary<string, List<double>> avgrates = new Dictionary<string, List<double>>();

            TCPClientBlock client = new TCPClientBlock("数据开窗", "127.0.0.1", 9999);
            GetInfoBlock GetInfo = new GetInfoBlock($"{savePath}\\{taskID}\\{pathName}\\数据报表\\{pathName}报表.csv");
            int count = 0;
            int FrameFre = 0;
            long _totalCount = 0;
            var save = new RollingFileStreamV2(savePath, taskID, "", "", "{root}/{TaskID}/{pathName}/aa/{index_prex}.src".Replace("aa", pathName + "原始码流").Replace("{pathName}", pathName));
            saveBlock = new RollingSaveBlockV2(SaveBlockConfig.KM_WIN, (ulong)cpuMask);
            //var CompareBlock = new DataCompare();
            /******************************TCP接收数据********************************/
            SocketSourceBlock_WithVCID socketSource = new SocketSourceBlock_WithVCID(pathName, vcid, new List<HostOptions>
            {
                new HostOptions()
                {
                    Host=IP,
                    Port=Port
                }
            });
            socketSource.DataArrived += (data) =>
            {
                if (saveSrc)
                {
                    save.Write(data, count / 1000 + 1);
                    count++;
                }
                unpack.Enqueue(data);
            };
            /******************************拆包部分***************************************/
            unpack.PackArrived += (d, c, n, t, i) =>
            {
                if (i == "short_A")
                {
                    n += 10;
                }
                if (i == "short_B")
                {
                    n += 14;
                }
                if (i == "short_C")
                {
                    n += 20;
                }
                if (i == "short_D")
                {
                    n += 24;
                }
                if (i == "mid_A")
                {
                    n += 30;
                }
                if (i == "mid_B")
                {
                    n += 34;
                }
                if (i == "mid_C")
                {
                    n += 40;
                }
                if (i == "mid_D")
                {
                    n += 44;
                }
                if (i == "data_win")
                {
                    if (t == "短波FPGA1")
                        n += 50;
                    else if (t == "短波FPGA2")
                        n += 150;
                    else if (t == "中波FPGA1")
                        n += 250;
                    else
                        n += 350;
                }
                decodeBlock.Enqueue(n, d);
            };

            /*************************解码部分*************************************/
            List<byte> CompareData = new List<byte>();
            decodeBlock.MidArrived += (data) =>
            {
                var name = SwitchName(data.Channel);
                if (!saveBlock.Storage.Keys.Contains($"DATA_{name}"))
                {
                    if (saveData)
                    {
                        saveBlock.AddStorage($"DATA_{name}", savePath, taskID, name, "{root}/{TaskID}/{pathName}/aa/{name}/{index_prex}.KM.{datatype}.src".Replace("aa", pathName + "分帧码流").Replace("{pathName}", pathName));
                    }

                }
                if (!saveCount.Keys.Contains($"DATA_{name}"))
                {
                    saveCount[$"DATA_{name}"] = 0;
                }
                if (data.Type == DataPackType.SyncHead)
                {
                    saveCount[$"DATA_{name}"]++;
                }
                if (saveData)
                {
                    saveBlock.Save($"DATA_{name}", saveCount[$"DATA_{name}"], data.Data);
                }
                if (IsCompare)
                {
                    CompareData.AddRange(data.Data.ToArray());
                    if (data.Type == DataPackType.FrameTail)
                    {
                        //CompareBlock.StartCompare(CompareData.ToArray(), name);
                        CompareData.Clear();
                    }
                }
            };
            decodeBlock.PackArrived += (key, data) =>
            {
                var nameKey = key.ToName();
                var Key = data.Channel.ToString();
                try
                {
                    if (!saveBlock.Storage.Keys.Contains(Key))
                    {
                        if (saveCam)
                        {
                            saveBlock.AddStorage(Key, savePath, taskID, nameKey, "{root}/{TaskID}/{pathName}/{name}/{index_prex}.KM.{datatype}.temp".Replace("{pathName}", pathName));
                        }
                        if (saveRaw)
                        {
                            saveBlock.AddStorage("RAW" + Key, savePath, taskID, nameKey, "{root}/{TaskID}/{pathName}/aa/{name}_RAW/{index_prex}.KM.{datatype}.raw".Replace("aa", pathName + "RAW图数据").Replace("{pathName}", pathName));
                        }
                    }
                    if (!saveCount.Keys.Contains(Key))
                    {
                        saveCount[Key] = 0;
                    }
                    switch (data.Type)
                    {
                        case DataPackType.ImagePatch:
                            if (saveCam)
                            {
                                saveBlock.Save(Key, saveCount[Key], data.Data);
                            }
                            if (saveRaw)
                            {
                                saveBlock.Save("RAW" + Key, saveCount[Key], data.Data);
                            }
                            break;
                        case DataPackType.SyncHead:
                            saveCount[Key]++;
                            break;
                        case DataPackType.InfoTail:
                            if (!sws.Keys.Contains(nameKey))
                            {
                                sws[nameKey] = new Stopwatch();
                            }
                            if (!avglists.Keys.Contains(nameKey))
                            {
                                avglists[nameKey] = new Queue<double>();
                            }
                            var spend = sws[nameKey].Elapsed.TotalMilliseconds;
                            avglists[nameKey].Enqueue(spend);
                            if (avglists[nameKey].Count > 6)
                            {
                                avglists[nameKey].Dequeue();
                            }
                            sws[nameKey].Restart();
                            GetInfo.Enqueue(data.WithInfo(utf8.GetBytes($"{nameKey}_文件号{saveCount[Key]}")).WithSpend(spend));
                            Console.WriteLine(ExcuteAverage(avglists[nameKey].Where(p => p > 5)));
                            var result = $"{nameKey}已完成{saveCount[Key]}帧";
                            Console.WriteLine(result);
                            FrameFre++;
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(utf8.GetBytes(ex.StackTrace));
                }
            };

            GetInfo.InfoArrived += (e, t, a, i) =>
            {
                StatusInfoPack pack = new StatusInfoPack();
                try
                {
                    var WinInfo = MessagePackSerializer.Deserialize<InfoTailInfoForWin>(i);
                    if (!avgrates.Keys.Contains(WinInfo.数据类型))
                    {
                        avgrates[WinInfo.数据类型] = new List<double>();
                    }
                    avgrates[WinInfo.数据类型].Add(WinInfo.压缩比);
                    pack = new StatusInfoPack()
                    {
                        数据类型 = WinInfo.数据类型,
                        帧计数 = WinInfo.帧计数,
                        压缩比 = $"{WinInfo.压缩比.ToString("#0.00")}/{avgrates[WinInfo.数据类型].Average().ToString("#0.00")}",
                        数据状态 = WinInfo.数据状态,
                        耗时 = ExcuteAverage(avglists[WinInfo.数据类型].Where(p => p > 5)),
                        error = e,
                        total = t
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                client.Enqueue(MessagePackSerializer.Serialize(pack));
            };
            client.Start();
            socketSource.Start(new System.Threading.CancellationToken());

            DateTime _startTime = DateTime.Now;
            System.Timers.Timer _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (sender, e) =>
            {
                int currentCount = Interlocked.Exchange(ref FrameFre, 0);
                Interlocked.Add(ref _totalCount, currentCount);
                TimeSpan elapsed = DateTime.Now - _startTime;
                double average = elapsed.TotalSeconds > 0 ? _totalCount / elapsed.TotalSeconds : 0;
                Console.WriteLine($"{DateTime.Now:HH:MM:ss}" +
                    $"当前秒帧数：{currentCount}|" +
                    $"平均次数/秒：{average:F2}|" +
                    $"运行时常：{elapsed.TotalSeconds:F1}秒");
            };
            _timer.AutoReset = true;
            _timer.Start();

            while (true)
            {
                Thread.Sleep(1000);
                Console.WriteLine(decodeBlock.GetStatus());
            }

        }

        [Command("OnlineDecodeShort")]
        public void OnlineDecodeShort(string pathName, string IP, int Port, string vcid, string savePath, string taskID, bool saveSrc, bool saveData, bool saveRaw, bool saveCam, bool downStreamRepost, bool IsCompare)
        {
            int cpuMask = 0x3c00f;
            Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)cpuMask;
            SetLog($"{savePath}");

            CPUAdaptor_temp[] adaptors = new CPUAdaptor_temp[32];
            TogetherUnpackBlock unpack = new TogetherUnpackBlock(3488, (ulong)cpuMask);
            NewStructDecodeBlock_temp decodeBlock = new NewStructDecodeBlock_temp(new int[4] { 14,15,16,17 },new int[6] {22,23,24,25,26,27}, DispatchType.轮询调度, 14, (ulong)cpuMask);
            RollingSaveBlockV2 saveBlock;

            UTF8Encoding utf8 = new UTF8Encoding();
            ConcurrentDictionary<string, int> saveCount = new ConcurrentDictionary<string, int>();

            Dictionary<string, Stopwatch> sws = new Dictionary<string, Stopwatch>();
            Dictionary<string, Queue<double>> avglists = new Dictionary<string, Queue<double>>();
            Dictionary<string, List<double>> avgrates = new Dictionary<string, List<double>>();

            TCPClientBlock client = new TCPClientBlock("短波压缩", "127.0.0.1", 9999);
            GetInfoBlock GetInfo = new GetInfoBlock($"{savePath}\\{taskID}\\{pathName}\\数据报表\\{pathName}报表.csv");
            int count = 0;
            int FrameFre = 0;
            long _totalCount = 0;
            var save = new RollingFileStreamV2(savePath, taskID, "", "", "{root}/{TaskID}/{pathName}/aa/{index_prex}.src".Replace("aa", pathName + "原始码流").Replace("{pathName}", pathName));
            saveBlock = new RollingSaveBlockV2(SaveBlockConfig.KM_SHORT, (ulong)cpuMask);
            //var CompareBlock = new DataCompare();
            /******************************TCP接收数据********************************/
            SocketSourceBlock_WithVCID socketSource = new SocketSourceBlock_WithVCID(pathName, vcid, new List<HostOptions>
            {
                new HostOptions()
                {
                    Host=IP,
                    Port=Port
                }
            });
            socketSource.DataArrived += (data) =>
            {
                if (saveSrc)
                {
                    save.Write(data, count / 1000 + 1);
                    count++;
                }
                unpack.Enqueue(data);
            };
            /******************************拆包部分***************************************/
            unpack.PackArrived += (d, c, n, t, i) =>
            {
                if (i == "short_A")
                {
                    n += 10;
                }
                if (i == "short_B")
                {
                    n += 14;
                }
                if (i == "short_C")
                {
                    n += 20;
                }
                if (i == "short_D")
                {
                    n += 24;
                }
                if (i == "mid_A")
                {
                    n += 30;
                }
                if (i == "mid_B")
                {
                    n += 34;
                }
                if (i == "mid_C")
                {
                    n += 40;
                }
                if (i == "mid_D")
                {
                    n += 44;
                }
                if (i == "data_win")
                {
                    if (t == "短波FPGA1")
                        n += 50;
                    else if (t == "短波FPGA2")
                        n += 150;
                    else if (t == "中波FPGA1")
                        n += 250;
                    else
                        n += 350;
                }
                decodeBlock.Enqueue(n, d);
            };

            /*************************解码部分*************************************/
            List<byte> CompareData = new List<byte>();
            decodeBlock.MidArrived += (data) =>
            {
                var name = SwitchName(data.Channel);
                if (!saveBlock.Storage.Keys.Contains($"DATA_{name}"))
                {
                    if (saveData)
                    {
                        saveBlock.AddStorage($"DATA_{name}", savePath, taskID, name, "{root}/{TaskID}/{pathName}/aa/{name}/{index_prex}.KM.{datatype}.src".Replace("aa", pathName + "分帧码流").Replace("{pathName}", pathName));
                    }

                }
                if (!saveCount.Keys.Contains($"DATA_{name}"))
                {
                    saveCount[$"DATA_{name}"] = 0;
                }
                if (data.Type == DataPackType.SyncHead)
                {
                    saveCount[$"DATA_{name}"]++;
                }
                if (saveData)
                {
                    saveBlock.Save($"DATA_{name}", saveCount[$"DATA_{name}"], data.Data);
                }
                if (IsCompare)
                {
                    CompareData.AddRange(data.Data.ToArray());
                    if (data.Type == DataPackType.FrameTail)
                    {
                        //CompareBlock.StartCompare(CompareData.ToArray(), name);
                        CompareData.Clear();
                    }
                }
            };
            decodeBlock.PackArrived += (key, data) =>
            {
                var nameKey = key.ToName();
                var Key = data.Channel.ToString();
                try
                {
                    if (!saveBlock.Storage.Keys.Contains(Key))
                    {
                        if (saveCam)
                        {
                            saveBlock.AddStorage(Key, savePath, taskID, nameKey, "{root}/{TaskID}/{pathName}/{name}/{index_prex}.KM.{datatype}.temp".Replace("{pathName}", pathName));
                        }
                        if (saveRaw)
                        {
                            saveBlock.AddStorage("RAW" + Key, savePath, taskID, nameKey, "{root}/{TaskID}/{pathName}/aa/{name}_RAW/{index_prex}.KM.{datatype}.raw".Replace("aa", pathName + "RAW图数据").Replace("{pathName}", pathName));
                        }
                    }
                    if (!saveCount.Keys.Contains(Key))
                    {
                        saveCount[Key] = 0;
                    }
                    switch (data.Type)
                    {
                        case DataPackType.ImagePatch:
                            if (saveCam)
                            {
                                saveBlock.Save(Key, saveCount[Key], data.Data);
                            }
                            if (saveRaw)
                            {
                                saveBlock.Save("RAW" + Key, saveCount[Key], data.Data);
                            }
                            break;
                        case DataPackType.SyncHead:
                            saveCount[Key]++;
                            break;
                        case DataPackType.InfoTail:
                            if (!sws.Keys.Contains(nameKey))
                            {
                                sws[nameKey] = new Stopwatch();
                            }
                            if (!avglists.Keys.Contains(nameKey))
                            {
                                avglists[nameKey] = new Queue<double>();
                            }
                            var spend = sws[nameKey].Elapsed.TotalMilliseconds;
                            avglists[nameKey].Enqueue(spend);
                            if (avglists[nameKey].Count > 6)
                            {
                                avglists[nameKey].Dequeue();
                            }
                            sws[nameKey].Restart();
                            GetInfo.Enqueue(data.WithInfo(utf8.GetBytes($"{nameKey}_文件号{saveCount[Key]}")).WithSpend(spend));
                            Console.WriteLine(ExcuteAverage(avglists[nameKey].Where(p => p > 5)));
                            var result = $"{nameKey}已完成{saveCount[Key]}帧";
                            Console.WriteLine(result);
                            FrameFre++;
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(utf8.GetBytes(ex.StackTrace));
                }
            };

            GetInfo.InfoArrived += (e, t, a, i) =>
            {
                StatusInfoPack pack = new StatusInfoPack();
                    try
                    {
                        var info = MessagePackSerializer.Deserialize<InfoTailInfo>(i);
                        if (!avgrates.Keys.Contains(info.数据类型))
                        {
                            avgrates[info.数据类型] = new List<double>();
                        }
                        avgrates[info.数据类型].Add(info.压缩比);
                        pack = new StatusInfoPack()
                        {
                            数据类型 = info.数据类型,
                            帧计数 = info.帧计数,
                            压缩比 = $"{info.压缩比:#0.00}/{avgrates[info.数据类型].Average():#0.00}",
                            数据状态 = info.数据状态,
                            耗时 = ExcuteAverage(avglists[info.数据类型].Where(p => p > 5)),
                            error = e,
                            total = t
                        };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                client.Enqueue(MessagePackSerializer.Serialize(pack));
            };
            client.Start();
            socketSource.Start(new System.Threading.CancellationToken());

            //DateTime _startTime = DateTime.Now;
            //System.Timers.Timer _timer = new System.Timers.Timer(1000);
            //_timer.Elapsed += (sender, e) =>
            //{
            //    int currentCount = Interlocked.Exchange(ref FrameFre, 0);
            //    Interlocked.Add(ref _totalCount, currentCount);
            //    TimeSpan elapsed = DateTime.Now - _startTime;
            //    double average = elapsed.TotalSeconds > 0 ? _totalCount / elapsed.TotalSeconds : 0;
            //    Console.WriteLine($"{DateTime.Now:HH:MM:ss}" +
            //        $"当前秒帧数：{currentCount}|" +
            //        $"平均次数/秒：{average:F2}|" +
            //        $"运行时常：{elapsed.TotalSeconds:F1}秒");
            //};
            //_timer.AutoReset = true;
            //_timer.Start();

            while (true)
            {
                Thread.Sleep(1000);
                Console.WriteLine(decodeBlock.GetStatus());
            }
        }

        [Command("OnlineDecodeMid")]
        public void OnlineDecodeMid(string pathName, string IP, int Port, string vcid, string savePath, string taskID, bool saveSrc, bool saveData, bool saveRaw, bool saveCam, bool downStreamRepost , bool IsCompare)
        {
            ulong cpuMask = 0x3c00f;
            Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)cpuMask;
            SetLog($"{savePath}");

            TogetherUnpackBlock unpack = new TogetherUnpackBlock(3488, (ulong)cpuMask);
            NewStructDecodeBlock_temp decodeBlock = new NewStructDecodeBlock_temp(new int[4] { 4, 5, 6, 7 }, new int[6] { 8, 9, 10, 11, 12, 13 }, DispatchType.轮询调度, 14, cpuMask);
            RollingSaveBlockV2 saveBlock;

            UTF8Encoding utf8 = new UTF8Encoding();
            ConcurrentDictionary<string, int> saveCount = new ConcurrentDictionary<string, int>();

            Dictionary<string, Stopwatch> sws = new Dictionary<string, Stopwatch>();
            Dictionary<string, Queue<double>> avglists = new Dictionary<string, Queue<double>>();
            Dictionary<string, List<double>> avgrates = new Dictionary<string, List<double>>();

            TCPClientBlock client = new TCPClientBlock("中波压缩", "127.0.0.1", 9999);
            GetInfoBlock GetInfo = new GetInfoBlock($"{savePath}\\{taskID}\\{pathName}\\数据报表\\{pathName}报表.csv");
            int count = 0;
            int FrameFre = 0;
            long _totalCount = 0;
            var save = new RollingFileStreamV2(savePath, taskID, "", "", "{root}/{TaskID}/{pathName}/aa/{index_prex}.src".Replace("aa", pathName + "原始码流").Replace("{pathName}", pathName));
            saveBlock = new RollingSaveBlockV2(SaveBlockConfig.KM_MID, (ulong)cpuMask);
            //var CompareBlock = new DataCompare();
            /******************************TCP接收数据********************************/
            SocketSourceBlock_WithVCID socketSource = new SocketSourceBlock_WithVCID(pathName, vcid, new List<HostOptions>
            {
                new HostOptions()
                {
                    Host=IP,
                    Port=Port
                }
            });
            socketSource.DataArrived += (data) =>
            {
                if (saveSrc)
                {
                    save.Write(data, count / 1000 + 1);
                    count++;
                }
                unpack.Enqueue(data);
            };
            /******************************拆包部分***************************************/
            unpack.PackArrived += (d, c, n, t, i) =>
            {
                if (i == "short_A")
                {
                    n += 10;
                }
                if (i == "short_B")
                {
                    n += 14;
                }
                if (i == "short_C")
                {
                    n += 20;
                }
                if (i == "short_D")
                {
                    n += 24;
                }
                if (i == "mid_A")
                {
                    n += 30;
                }
                if (i == "mid_B")
                {
                    n += 34;
                }
                if (i == "mid_C")
                {
                    n += 40;
                }
                if (i == "mid_D")
                {
                    n += 44;
                }
                if (i == "data_win")
                {
                    if (t == "短波FPGA1")
                        n += 50;
                    else if (t == "短波FPGA2")
                        n += 150;
                    else if (t == "中波FPGA1")
                        n += 250;
                    else
                        n += 350;
                }
                decodeBlock.Enqueue(n, d);
            };

            /*************************解码部分*************************************/
            List<byte> CompareData = new List<byte>();
            decodeBlock.MidArrived += (data) =>
            {
                var name = SwitchName(data.Channel);
                if (!saveBlock.Storage.Keys.Contains($"DATA_{name}"))
                {
                    if (saveData)
                    {
                        saveBlock.AddStorage($"DATA_{name}", savePath, taskID, name, "{root}/{TaskID}/{pathName}/aa/{name}/{index_prex}.KM.{datatype}.src".Replace("aa", pathName + "分帧码流").Replace("{pathName}", pathName));
                    }

                }
                if (!saveCount.Keys.Contains($"DATA_{name}"))
                {
                    saveCount[$"DATA_{name}"] = 0;
                }
                if (data.Type == DataPackType.SyncHead)
                {
                    saveCount[$"DATA_{name}"]++;
                }
                if (saveData)
                {
                    saveBlock.Save($"DATA_{name}", saveCount[$"DATA_{name}"], data.Data);
                }
                if (IsCompare)
                {
                    CompareData.AddRange(data.Data.ToArray());
                    if (data.Type == DataPackType.FrameTail)
                    {
                        //CompareBlock.StartCompare(CompareData.ToArray(), name);
                        CompareData.Clear();
                    }
                }
            };
            decodeBlock.PackArrived += (key, data) =>
            {
                var nameKey = key.ToName();
                var Key = data.Channel.ToString();
                try
                {
                    if (!saveBlock.Storage.Keys.Contains(Key))
                    {
                        if (saveCam)
                        {
                            saveBlock.AddStorage(Key, savePath, taskID, nameKey, "{root}/{TaskID}/{pathName}/{name}/{index_prex}.KM.{datatype}.temp".Replace("{pathName}", pathName));
                        }
                        if (saveRaw)
                        {
                            saveBlock.AddStorage("RAW" + Key, savePath, taskID, nameKey, "{root}/{TaskID}/{pathName}/aa/{name}_RAW/{index_prex}.KM.{datatype}.raw".Replace("aa", pathName + "RAW图数据").Replace("{pathName}", pathName));
                        }
                    }
                    if (!saveCount.Keys.Contains(Key))
                    {
                        saveCount[Key] = 0;
                    }
                    switch (data.Type)
                    {
                        case DataPackType.ImagePatch:
                            if (saveCam)
                            {
                                saveBlock.Save(Key, saveCount[Key], data.Data);
                            }
                            if (saveRaw)
                            {
                                saveBlock.Save("RAW" + Key, saveCount[Key], data.Data);
                            }
                            break;
                        case DataPackType.SyncHead:
                            saveCount[Key]++;
                            break;
                        case DataPackType.InfoTail:
                            if (!sws.Keys.Contains(nameKey))
                            {
                                sws[nameKey] = new Stopwatch();
                            }
                            if (!avglists.Keys.Contains(nameKey))
                            {
                                avglists[nameKey] = new Queue<double>();
                            }
                            var spend = sws[nameKey].Elapsed.TotalMilliseconds;
                            avglists[nameKey].Enqueue(spend);
                            if (avglists[nameKey].Count > 6)
                            {
                                avglists[nameKey].Dequeue();
                            }
                            sws[nameKey].Restart();
                            GetInfo.Enqueue(data.WithInfo(utf8.GetBytes($"{nameKey}_文件号{saveCount[Key]}")).WithSpend(spend));
                            Console.WriteLine(ExcuteAverage(avglists[nameKey].Where(p => p > 5)));
                            var result = $"{nameKey}已完成{saveCount[Key]}帧";
                            Console.WriteLine(result);
                            FrameFre++;
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(utf8.GetBytes(ex.StackTrace));
                }
            };

            GetInfo.InfoArrived += (e, t, a, i) =>
            {
                StatusInfoPack pack = new StatusInfoPack();
                try
                {
                    var info = MessagePackSerializer.Deserialize<InfoTailInfo>(i);
                    if (!avgrates.Keys.Contains(info.数据类型))
                    {
                        avgrates[info.数据类型] = new List<double>();
                    }
                    avgrates[info.数据类型].Add(info.压缩比);
                    pack = new StatusInfoPack()
                    {
                        数据类型 = info.数据类型,
                        帧计数 = info.帧计数,
                        压缩比 = $"{info.压缩比:#0.00}/{avgrates[info.数据类型].Average():#0.00}",
                        数据状态 = info.数据状态,
                        耗时 = ExcuteAverage(avglists[info.数据类型].Where(p => p > 5)),
                        error = e,
                        total = t
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                client.Enqueue(MessagePackSerializer.Serialize(pack));
            };
            client.Start();
            socketSource.Start(new System.Threading.CancellationToken());

            //DateTime _startTime = DateTime.Now;
            //System.Timers.Timer _timer = new System.Timers.Timer(1000);
            //_timer.Elapsed += (sender, e) =>
            //{
            //    int currentCount = Interlocked.Exchange(ref FrameFre, 0);
            //    Interlocked.Add(ref _totalCount, currentCount);
            //    TimeSpan elapsed = DateTime.Now - _startTime;
            //    double average = elapsed.TotalSeconds > 0 ? _totalCount / elapsed.TotalSeconds : 0 ;
            //    Console.WriteLine($"{DateTime.Now:HH:MM:ss}" + 
            //        $"当前秒帧数：{currentCount}|" + 
            //        $"平均次数/秒：{average:F2}|" + 
            //        $"运行时常：{elapsed.TotalSeconds:F1}秒");
            //};
            //_timer.AutoReset = true;
            //_timer.Start();

            while (true)
            {
                Thread.Sleep(1000);
                Console.WriteLine(decodeBlock.GetStatus());
            }

        }

        [Command("OfflineDecodeShort")]
        public void OfflineDecodeShort(string pathName, string fileType, string filePath, int speed, int sendTimes, string savePath, string taskID, bool saveSrc, bool saveData, bool saveRaw, bool saveCam, bool downStreamRepost, bool IsCompare)
        {
            int cpuMask = 0x3c00f;
            Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)cpuMask;

            TogetherUnpackBlock unpack = new TogetherUnpackBlock(3488, (ulong)cpuMask);
            NewStructDecodeBlock_temp decodeBlock = new NewStructDecodeBlock_temp(new int[4] { 14, 15, 16, 17 }, new int[6] { 22, 23, 24, 25, 26, 27 }, DispatchType.轮询调度, 14, (ulong)cpuMask);
            RollingSaveBlockV2 saveBlock;

            UTF8Encoding utf8 = new UTF8Encoding();
            ConcurrentDictionary<string, int> saveCount = new ConcurrentDictionary<string, int>();

            Dictionary<string, Stopwatch> sws = new Dictionary<string, Stopwatch>();
            Dictionary<string, Queue<double>> avglists = new Dictionary<string, Queue<double>>();
            Dictionary<string, List<double>> avgrates = new Dictionary<string, List<double>>();

            TCPClientBlock client = new TCPClientBlock("短波压缩", "127.0.0.1", 9999);
            GetInfoBlock GetInfo = new GetInfoBlock($"{savePath}\\{taskID}\\{pathName}\\数据报表\\{pathName}报表.csv");
            int count = 0;
            int FrameFre = 0;
            long _totalCount = 0;
            var save = new RollingFileStreamV2(savePath, taskID, "", "", "{root}/{TaskID}/{pathName}/aa/{index_prex}.src".Replace("aa", pathName + "原始码流").Replace("{pathName}", pathName));
            saveBlock = new RollingSaveBlockV2(SaveBlockConfig.KM_SHORT, (ulong)cpuMask);
            //var CompareBlock = new DataCompare();
            /******************************TCP接收数据********************************/
            SetLog($"{savePath}");

            string[] files = null;

            if (fileType == "文件")
            {
                files = new string[] { filePath };
            }
            if (fileType == "文件夹")
            {
                files = Directory.EnumerateFiles(filePath, "*", SearchOption.AllDirectories).ToArray();

            }
            /******************************拆包部分***************************************/
            unpack.PackArrived += (d, c, n, t, i) =>
            {
                if (i == "short_A")
                {
                    n += 10;
                }
                if (i == "short_B")
                {
                    n += 14;
                }
                if (i == "short_C")
                {
                    n += 20;
                }
                if (i == "short_D")
                {
                    n += 24;
                }
                if (i == "mid_A")
                {
                    n += 30;
                }
                if (i == "mid_B")
                {
                    n += 34;
                }
                if (i == "mid_C")
                {
                    n += 40;
                }
                if (i == "mid_D")
                {
                    n += 44;
                }
                if (i == "data_win")
                {
                    if (t == "短波FPGA1")
                        n += 50;
                    else if (t == "短波FPGA2")
                        n += 150;
                    else if (t == "中波FPGA1")
                        n += 250;
                    else
                        n += 350;
                }
                decodeBlock.Enqueue(n, d);
            };

            /*************************解码部分*************************************/
            List<byte> CompareData = new List<byte>();
            decodeBlock.MidArrived += (data) =>
            {
                var name = SwitchName(data.Channel);
                if (!saveBlock.Storage.Keys.Contains($"DATA_{name}"))
                {
                    if (saveData)
                    {
                        saveBlock.AddStorage($"DATA_{name}", savePath, taskID, name, "{root}/{TaskID}/{pathName}/aa/{name}/{index_prex}.KM.{datatype}.src".Replace("aa", pathName + "分帧码流").Replace("{pathName}", pathName));
                    }

                }
                if (!saveCount.Keys.Contains($"DATA_{name}"))
                {
                    saveCount[$"DATA_{name}"] = 0;
                }
                if (data.Type == DataPackType.SyncHead)
                {
                    saveCount[$"DATA_{name}"]++;
                }
                if (saveData)
                {
                    saveBlock.Save($"DATA_{name}", saveCount[$"DATA_{name}"], data.Data);
                }
                if (IsCompare)
                {
                    CompareData.AddRange(data.Data.ToArray());
                    if (data.Type == DataPackType.FrameTail)
                    {
                        //CompareBlock.StartCompare(CompareData.ToArray(), name);
                        CompareData.Clear();
                    }
                }
            };
            decodeBlock.PackArrived += (key, data) =>
            {
                var nameKey = key.ToName();
                var Key = data.Channel.ToString();
                try
                {
                    if (!saveBlock.Storage.Keys.Contains(Key))
                    {
                        if(saveCam)
                        {
                            saveBlock.AddStorage(Key, savePath, taskID, nameKey, "{root}/{TaskID}/{pathName}/{name}/{index_prex}.KM.{datatype}.temp".Replace("{pathName}", pathName));
                        }
                        if (saveRaw)
                        {
                            saveBlock.AddStorage("RAW" + Key, savePath, taskID, nameKey, "{root}/{TaskID}/{pathName}/aa/{name}_RAW/{index_prex}.KM.{datatype}.raw".Replace("aa", pathName + "RAW图数据").Replace("{pathName}", pathName));
                        }
                    }
                    if (!saveCount.Keys.Contains(Key))
                    {
                        saveCount[Key] = 0;
                    }
                    switch (data.Type)
                    {
                        case DataPackType.ImagePatch:
                            if (saveCam)
                            {
                                saveBlock.Save(Key, saveCount[Key], data.Data);
                            }
                            if (saveRaw)
                            {
                                saveBlock.Save("RAW" + Key, saveCount[Key], data.Data);
                            }
                            break;
                        case DataPackType.SyncHead:
                            saveCount[Key]++;
                            break;
                        case DataPackType.InfoTail:
                            if (!sws.Keys.Contains(nameKey))
                            {
                                sws[nameKey] = new Stopwatch();
                            }
                            if (!avglists.Keys.Contains(nameKey))
                            {
                                avglists[nameKey] = new Queue<double>();
                            }
                            var spend = sws[nameKey].Elapsed.TotalMilliseconds;
                            avglists[nameKey].Enqueue(spend);
                            if (avglists[nameKey].Count > 6)
                            {
                                avglists[nameKey].Dequeue();
                            }
                            sws[nameKey].Restart();
                            GetInfo.Enqueue(data.WithInfo(utf8.GetBytes($"{nameKey}_文件号{saveCount[Key]}")).WithSpend(spend));
                            Console.WriteLine(ExcuteAverage(avglists[nameKey].Where(p => p > 5)));
                            var result = $"{nameKey}已完成{saveCount[Key]}帧";
                            Console.WriteLine(result);
                            FrameFre++;
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(utf8.GetBytes(ex.StackTrace));
                }
            };

            GetInfo.InfoArrived += (e, t, a, i) =>
            {
                StatusInfoPack pack = new StatusInfoPack();
                try
                {
                    var info = MessagePackSerializer.Deserialize<InfoTailInfo>(i);
                    if (!avgrates.Keys.Contains(info.数据类型))
                    {
                        avgrates[info.数据类型] = new List<double>();
                    }
                    avgrates[info.数据类型].Add(info.压缩比);
                    pack = new StatusInfoPack()
                    {
                        数据类型 = info.数据类型,
                        帧计数 = info.帧计数,
                        压缩比 = $"{info.压缩比:#0.00}/{avgrates[info.数据类型].Average():#0.00}",
                        数据状态 = info.数据状态,
                        耗时 = ExcuteAverage(avglists[info.数据类型].Where(p => p > 5)),
                        error = e,
                        total = t
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                client.Enqueue(MessagePackSerializer.Serialize(pack));
            };
            client.Start();

            DateTime _startTime = DateTime.Now;
            System.Timers.Timer _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (sender, e) =>
            {
                int currentCount = Interlocked.Exchange(ref FrameFre, 0);
                Interlocked.Add(ref _totalCount, currentCount);
                TimeSpan elapsed = DateTime.Now - _startTime;
                double average = elapsed.TotalSeconds > 0 ? _totalCount / elapsed.TotalSeconds : 0;
                Console.WriteLine($"{DateTime.Now:HH:MM:ss}" +
                    $"当前秒帧数：{currentCount}|" +
                    $"平均次数/秒：{average:F2}|" +
                    $"运行时常：{elapsed.TotalSeconds:F1}秒");
                Console.WriteLine(decodeBlock.GetStatus());
            };
            _timer.AutoReset = true;
            _timer.Start();

            foreach (var data in files.LoadBig(speed, sendTimes))
            {
                if (saveSrc)
                {
                    save.Write(data, count / 1000 + 1);
                    count++;
                }
                unpack.Enqueue(data);
                count++;
            }

            //while (true)
            //{
            //    Thread.Sleep(1000);
            //    Console.WriteLine(decodeBlock.GetStatus());
            //}
        }

        [Command("OfflineDecodeMid")]
        public void OfflineDecodeMid(string pathName, string fileType, string filePath, int speed, int sendTimes, string savePath, string taskID, bool saveSrc, bool saveData, bool saveRaw, bool saveCam, bool downStreamRepost, bool IsCompare)
        {
            ulong cpuMask = 0x3c00f;
            Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)cpuMask;

            TogetherUnpackBlock unpack = new TogetherUnpackBlock(3488, (ulong)cpuMask);
            NewStructDecodeBlock_temp decodeBlock = new NewStructDecodeBlock_temp(new int[4] { 4,5,6,7},new int[6] { 8,9,10,11,12,13}, DispatchType.轮询调度, 14, cpuMask);
            RollingSaveBlockV2 saveBlock;

            UTF8Encoding utf8 = new UTF8Encoding();
            ConcurrentDictionary<string, int> saveCount = new ConcurrentDictionary<string, int>();

            Dictionary<string, Stopwatch> sws = new Dictionary<string, Stopwatch>();
            Dictionary<string, Queue<double>> avglists = new Dictionary<string, Queue<double>>();
            Dictionary<string, List<double>> avgrates = new Dictionary<string, List<double>>();

            TCPClientBlock client = new TCPClientBlock("中波压缩", "127.0.0.1", 9999);
            GetInfoBlock GetInfo = new GetInfoBlock($"{savePath}\\{taskID}\\{pathName}\\数据报表\\{pathName}报表.csv");
            int count = 0;
            int FrameFre = 0;
            long _totalCount = 0;
            var save = new RollingFileStreamV2(savePath, taskID, "", "", "{root}/{TaskID}/{pathName}/aa/{index_prex}.src".Replace("aa", pathName + "原始码流").Replace("{pathName}", pathName));
            saveBlock = new RollingSaveBlockV2(SaveBlockConfig.KM_MID, (ulong)cpuMask);
            //DataCompare CompareBlock = new DataCompare();
            /******************************TCP接收数据********************************/
            SetLog($"{savePath}");

            string[] files = null;

            if (fileType == "文件")
            {
                files = new string[] { filePath };
            }
            if (fileType == "文件夹")
            {
                files = Directory.EnumerateFiles(filePath, "*", SearchOption.AllDirectories).ToArray();

            }
            /******************************拆包部分***************************************/
            List<byte> CompareData = new List<byte>();
            unpack.PackArrived += (d, c, n, t, i) =>
            {
                if (i == "short_A")
                {
                    n += 10;
                }
                if (i == "short_B")
                {
                    n += 14;
                }
                if (i == "short_C")
                {
                    n += 20;
                }
                if (i == "short_D")
                {
                    n += 24;
                }
                if (i == "mid_A")
                {
                    n += 30;
                }
                if (i == "mid_B")
                {
                    n += 34;
                }
                if (i == "mid_C")
                {
                    n += 40;
                }
                if (i == "mid_D")
                {
                    n += 44;
                }
                if (i == "data_win")
                {
                    if (t == "短波FPGA1")
                        n += 50;
                    else if (t == "短波FPGA2")
                        n += 150;
                    else if (t == "中波FPGA1")
                        n += 250;
                    else
                        n += 350;
                }
                decodeBlock.Enqueue(n, d);
            };

            /*************************解码部分*************************************/
            decodeBlock.MidArrived += (data) =>
            {
                var name = SwitchName(data.Channel);
                if (!saveBlock.Storage.Keys.Contains($"DATA_{name}"))
                {
                    if (saveData)
                    {
                        saveBlock.AddStorage($"DATA_{name}", savePath, taskID, name, "{root}/{TaskID}/{pathName}/aa/{name}/{index_prex}.KM.{datatype}.src".Replace("aa", pathName + "分帧码流").Replace("{pathName}", pathName));
                    }

                }
                if (!saveCount.Keys.Contains($"DATA_{name}"))
                {
                    saveCount[$"DATA_{name}"] = 0;
                }
                if (data.Type == DataPackType.SyncHead)
                {
                    saveCount[$"DATA_{name}"]++;
                }
                if (saveData)
                {
                    saveBlock.Save($"DATA_{name}", saveCount[$"DATA_{name}"], data.Data);
                }
                if (IsCompare)
                {
                    CompareData.AddRange(data.Data.ToArray());
                    if (data.Type == DataPackType.FrameTail)
                    {
                        //CompareBlock.StartCompare(CompareData.ToArray(), name);
                        CompareData.Clear();
                    }
                }
            };
            decodeBlock.PackArrived += (key, data) =>
            {
                var nameKey = key.ToName();
                var Key = data.Channel.ToString();
                try
                {
                    if (!saveBlock.Storage.Keys.Contains(Key))
                    {
                        if (saveCam)
                        {
                            saveBlock.AddStorage(Key, savePath, taskID, nameKey, "{root}/{TaskID}/{pathName}/{name}/{index_prex}.KM.{datatype}.temp".Replace("{pathName}", pathName));
                        }
                        if (saveRaw)
                        {
                            saveBlock.AddStorage("RAW" + Key, savePath, taskID, nameKey, "{root}/{TaskID}/{pathName}/aa/{name}_RAW/{index_prex}.KM.{datatype}.raw".Replace("aa", pathName + "RAW图数据").Replace("{pathName}", pathName));
                        }
                    }
                    if (!saveCount.Keys.Contains(Key))
                    {
                        saveCount[Key] = 0;
                    }
                    switch (data.Type)
                    {
                        case DataPackType.ImagePatch:
                            if (saveCam)
                            {
                                saveBlock.Save(Key, saveCount[Key], data.Data);
                            }
                            if (saveRaw)
                            {
                                saveBlock.Save("RAW" + Key, saveCount[Key], data.Data);
                            }
                            break;
                        case DataPackType.SyncHead:
                            saveCount[Key]++;
                            break;
                        case DataPackType.InfoTail:
                            if (!sws.Keys.Contains(nameKey))
                            {
                                sws[nameKey] = new Stopwatch();
                            }
                            if (!avglists.Keys.Contains(nameKey))
                            {
                                avglists[nameKey] = new Queue<double>();
                            }
                            var spend = sws[nameKey].Elapsed.TotalMilliseconds;
                            avglists[nameKey].Enqueue(spend);
                            if (avglists[nameKey].Count > 6)
                            {
                                avglists[nameKey].Dequeue();
                            }
                            sws[nameKey].Restart();
                            GetInfo.Enqueue(data.WithInfo(utf8.GetBytes($"{nameKey}_文件号{saveCount[Key]}")).WithSpend(spend));
                            //Console.WriteLine(ExcuteAverage(avglists[nameKey].Where(p => p > 5)));
                            //var result = $"{nameKey}已完成{saveCount[Key]}帧  Cache:{saveBlock.QueueCount}";
                            //Console.WriteLine(result);
                            FrameFre++;
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(utf8.GetBytes(ex.StackTrace));
                }
            };

            GetInfo.InfoArrived += (e, t, a, i) =>
            {
                StatusInfoPack pack = new StatusInfoPack();
                try
                {
                    var info = MessagePackSerializer.Deserialize<InfoTailInfo>(i);
                    if (!avgrates.Keys.Contains(info.数据类型))
                    {
                        avgrates[info.数据类型] = new List<double>();
                    }
                    avgrates[info.数据类型].Add(info.压缩比);
                    pack = new StatusInfoPack()
                    {
                        数据类型 = info.数据类型,
                        帧计数 = info.帧计数,
                        压缩比 = $"{info.压缩比:#0.00}/{avgrates[info.数据类型].Average():#0.00}",
                        数据状态 = info.数据状态,
                        耗时 = ExcuteAverage(avglists[info.数据类型].Where(p => p > 5)),
                        error = e,
                        total = t
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                client.Enqueue(MessagePackSerializer.Serialize(pack));
            };
            client.Start();

            DateTime _startTime = DateTime.Now;
            System.Timers.Timer _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (sender, e) =>
            {
                int currentCount = Interlocked.Exchange(ref FrameFre, 0);
                Interlocked.Add(ref _totalCount, currentCount);
                TimeSpan elapsed = DateTime.Now - _startTime;
                double average = elapsed.TotalSeconds > 0 ? _totalCount / elapsed.TotalSeconds : 0;
                Console.WriteLine($"{DateTime.Now:HH:MM:ss}" +
                    $"当前秒帧数：{currentCount}|" +
                    $"平均次数/秒：{average:F2}|" +
                    $"运行时常：{elapsed.TotalSeconds:F1}秒");
                Console.WriteLine(decodeBlock.GetStatus());
            };
            _timer.AutoReset = true;
            _timer.Start();

            foreach (var data in files.LoadBig(speed, sendTimes))
            {
                if (saveSrc)
                {
                    save.Write(data, count / 1000 + 1);
                    count++;
                }
                unpack.Enqueue(data);
                count++;
            }

            //while (true)
            //{
            //    Thread.Sleep(1000);
            //    Console.WriteLine(decodeBlock.GetStatus());
            //}
        }

        [Command("test")]
        public void Test(string pathName, string fileType, string filePath, int speed, int sendTimes, string savePath, string taskID)
        {
        
            int cpuMask = 0xfff;
        
            Process.GetCurrentProcess().ProcessorAffinity = (IntPtr) cpuMask;
        
            SetLog($"{savePath}");
        
        
            string[] files = null;

            
            if (fileType == "文件")
            
            {
                
                files = new string[] { filePath};
            }
            if (fileType == "文件夹")
            {
    
                files = Directory.EnumerateFiles(filePath, "*", SearchOption.AllDirectories).ToArray();
            }

            RollingSaveBlockV2 saveBlock = new RollingSaveBlockV2(SaveBlockConfig.KM_MID, (ulong)cpuMask);
            if (!saveBlock.Storage.Keys.Contains(pathName))
            {
                    saveBlock.AddStorage(pathName, savePath, taskID, pathName, "{root}/{TaskID}/{pathName}/{name}/{index_prex}.KM.{datatype}.src".Replace("{pathName}", pathName));
            }
            var count = 0;
            Console.WriteLine("循环写入");
            while(true)
            {
                foreach (var data in files.LoadBig(speed, sendTimes))
                {
                    count++;
                    saveBlock.Save(pathName, count, data);
                }
            }
        }


        [Command("TimeDelayTest")]
        public void TimeDelay()
        {
            string taskID = DateTime.Now.ToString();
            SoftDecodeCommand DecodeCommand = new SoftDecodeCommand(new int[2] { 0, 1 }, new int[8] { 2, 3, 4, 5, 6, 7, 8, 9 }, SaveBlockConfig.KM_MID, "短中波", @"D:\data", taskID, false, false, false, false, false);

            SocketSourceBlock socketSource = new SocketSourceBlock("短中波", new List<HostOptions>
                {
                    new HostOptions()
                    {
                        Host= "127.0.0.1",
                        Port= 9050
                    }
                });
            socketSource.DataArrived += (data) =>
            {
                DecodeCommand.Enqueue(data);

            };
            socketSource.Start(new System.Threading.CancellationToken());
            while (true)
            {
            };
        }


        private string ExcuteAverage(IEnumerable<double> List)
        {
            if (List.Count() <= 0)
            {
                return "暂无数据";
            }
            else
            {
                return List.Average().ToString("#0.00") + "ms/p";
            }
        }

        public void SetLog(string home)
        {
            string path = string.IsNullOrEmpty(home)
            ? Path.Combine("logs", "service")
            : Path.Combine(home, "logs", "service");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var builder = new LoggerConfiguration().MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.WithProperty("app name", "decompress").WriteTo.Console()
            .WriteTo.File(Path.Combine(path, "log.txt"), rollingInterval: RollingInterval.Hour, outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Name} {Status} {Message}{NewLine}{Exception}", retainedFileCountLimit: 24 * 90);
            Log.Logger = builder.CreateLogger();
        }

        private string SwitchName(int channel)
        {
            if (channel >= 10 && channel < 14)
            {
                return $"短波_探测器A{(channel - 10).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 14 && channel < 20)
            {
                return $"短波_探测器B{(channel - 14).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 20 && channel < 24)
            {
                return $"短波_探测器C{(channel - 20).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 24 && channel < 30)
            {
                return $"短波_探测器D{(channel - 24).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 30 && channel < 34)
            {
                return $"中波_探测器A{(channel - 30).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 34 && channel < 40)
            {
                return $"中波_探测器B{(channel - 34).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 40 && channel < 44)
            {
                return $"中波_探测器C{(channel - 40).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 44 && channel < 50)
            {
                return $"中波_探测器D{(channel - 44).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 50 && channel < 150)
            {
                return $"数据开窗短波FPGA1_开窗号{(channel - 50).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 150 && channel < 250)
            {
                return $"数据开窗短波FPGA2_开窗号{(channel - 150).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 250 && channel < 350)
            {
                return $"数据开窗中波FPGA1_开窗号{(channel - 250).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 350 && channel < 450)
            {
                return $"数据开窗中波FPGA2_开窗号{(channel - 350).ToString().PadLeft(3, '0')}";
            }
            return "有问题的";
        }
    }
}
