using CommandDotNet;
using Decompress;
using Decompress.Codec.L100.Const;
using DemoConsole;
using KM.Codec.L100.Const;
using KM.Codec.L300.Decode.L310.Shared.Step0.TogetherPack;
using KM.Codec.L300.Decode.L340.WideViewWinDecoder;
using Protocols;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SocketSourceBlock = KM.ConsoleDecode.Dispatch.RecieveData.SocketSourceBlock;
using SocketTargetBlock = KM.ConsoleDecode.Dispatch.SendDataPack.SocketTargetBlock;

namespace KM.ConsoleDecode.Dispatch
{
    [Command("dispatch")]
    public class DispatchCommand
    {
        [Command("gethost")]
        public void Dispatch(string PathName, string IP, int RecPort, int PathNum, string togetherType, string root = "data", string taskID = "调试", bool saveSrc = false)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationTokenSource[] cancellationTokenTargets = new CancellationTokenSource[PathNum];

            $"{root}\\{taskID}\\数据报表\\{PathName}报表.csv".CreateFileDir();
            UTF8Encoding utf8 = new System.Text.UTF8Encoding(true);
            FileStream fs = File.Open($"{root}\\{taskID}\\数据报表\\{PathName}报表.csv", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            InfoTailInfo infoTail = new InfoTailInfo();
            InfoTailInfoForWin infoForWin = new InfoTailInfoForWin();

            int SendPort;
            //对收到的数据进行拆包和RS解码的预处理,不同类型数据的初始处理后发送的端口号不同，写死
            //UnpackParser unpackParser; 
            //ECCDecoderBlock ECCDecoder = new ECCDecoderBlock();
            //DispatchBlock dispatchBlock = new DispatchBlock(PathNum);
            TogetherUnpackBlock unpack;
            switch (togetherType)
            {
                case "每包3628":
                    unpack = new TogetherUnpackBlock(3628);
                    break;
                case "每包3626":
                    unpack = new TogetherUnpackBlock(3626);
                    break;
                default:
                    Console.WriteLine("不合法的数据类型");
                    return;
            }
            //unpack.ResetPackConfig(togetherConfig);
            switch (PathName)
            {
                case "窄视场通道":
                    SendPort = 34110;
                    fs.Write(utf8.GetPreamble());
                    fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoTail)));
                    break;
                case "宽视场通道1":
                    SendPort = 34210;
                    fs.Write(utf8.GetPreamble());
                    fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoTail)));
                    break;
                case "宽视场通道2":
                    SendPort = 34310;
                    fs.Write(utf8.GetPreamble());
                    fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoTail)));
                    break;
                case "宽视场开窗通道":
                    SendPort = 34410;
                    fs.Write(utf8.GetPreamble());
                    fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoForWin)));
                    break;
                default:
                    Console.WriteLine("不合法的数据类型");
                    return;
            }
            fs.Flush();

            //使用udp数据报发送日志
            NotifyService notifyService = new NotifyService("127.0.0.1", 22222);

            //使用tcp传递报表信息
            TCPHubService notifyHub = new TCPHubService(33333);

            //向指定主机请求数据
            SocketSourceBlock socketSource = new SocketSourceBlock(PathName, new List<HostOptions>
                    {
                        new HostOptions()
                        {
                           Host=IP,
                           Port=RecPort
                        }
                    });
            if (cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource = new CancellationTokenSource();
            }

            //开放发送数据的端口,根据配置的通道数开放多个端口
            SocketTargetBlock[] socketTargets = new SocketTargetBlock[PathNum];


            for (int i = 0; i < PathNum; i++)
            {
                socketTargets[i] = new SocketTargetBlock(PathName, SendPort + i);
                cancellationTokenTargets[i] = new CancellationTokenSource();
            }

            int beforeUnpackCount = 0;

            Dictionary<int, int> switchDictionary = new Dictionary<int, int>();//将不同通道的数据映射为从0开始的顺序，用于宽视场和宽视场开窗
            int switchNum = 0;
            unpack.PackArrived += (d, c, n, t,i) =>
            {
                switch (t)
                {
                    case "narrow_mid":
                        socketTargets[n + 1].Enqueue(d);
                        break;
                    case "narrow_short":
                        socketTargets[n].Enqueue(d);
                        break;
                    case "wide":
                        if (!switchDictionary.Keys.Contains(n))
                        {
                            switchDictionary[n] = switchNum;
                            switchNum++;
                        }
                        socketTargets[switchDictionary[n]].Enqueue(d);
                        break;
                    case "widewin":
                        if (!switchDictionary.Keys.Contains(n / 20))
                        {
                            switchDictionary[n / 20] = switchNum;
                            switchNum++;
                        }
                        socketTargets[switchDictionary[n / 20]].Enqueue(d);
                        break;
                    default:
                        Console.WriteLine("合路包数据有误");
                        return;
                }

            };
            foreach (var item in socketTargets)
            {
                item.OnConnected += (data) =>
                {
                    //item.IsConnected = true;
                };
            }
            for (int i = 0; i < PathNum; i++)
            {
                socketTargets[i].Start(cancellationTokenTargets[i].Token);
            }

            //判断数据来后是否存储
            if (saveSrc)
            {
                RollingSaveBlock DataStorage = new RollingSaveBlock();
                DataStorage.AddStorage("原始数据", new RollingFileStreamV2(root, taskID, PathName, PathName, "{root}/{TaskID}/{name}/原始数据/{name}_{BatchId}_{index_prex}.src"));
                //RollingFileStreamV2 beforeUnpack = new RollingFileStreamV2(root, taskID, PathName, PathName, "{root}/{TaskID}/{name}/原始数据/{name}_{BatchId}_{index_prex}.src");
                socketSource.DataArrived += (data) =>
                {
                    DataStorage.Save("原始数据", beforeUnpackCount / 1000, data);
                    //beforeUnpack.Write(data, beforeUnpackCount / 1000);
                    beforeUnpackCount++;
                    unpack.Enqueue(data);
                };
            }
            else
            {
                socketSource.DataArrived += (data) =>
                {
                    unpack.Enqueue(data);
                };
            }

            socketSource.Start(cancellationTokenSource.Token);

            Console.WriteLine("准备启动");
            notifyService.SetStatusFunc(() =>
            {
                Dictionary<string, object> status = new Dictionary<string, object>()
                {
                    {"调度缓冲区",""},
                    {"通道输入速率",""},
                    {"已读取数据量", ""}
                }; ;
                Dictionary<string, long> data = new Dictionary<string, long>()
                {
                    {"调度缓冲区",0},
                    {"通道输入速率",0},
                    {"已读取数据量", 0}
                };
                foreach (var s in socketTargets)
                {
                    foreach (var kv in s.Status)
                    {
                        //status[s.Name + " " + kv.Key] += kv.Value;
                        data[kv.Key] += kv.Value;
                    }
                }
                foreach (var item in data)
                {
                    if (item.Key == "通道输入速率")
                    {
                        status[item.Key] = item.Value.BytesSpeed(1000);
                    }
                    else
                    {
                        status[item.Key] = item.Value.BytesSize();
                    }
                }
                return status;
            });

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

            notifyService.Start();
            notifyHub.Start();
            while (true)
            {
                Thread.Sleep(1000);
                //Console.WriteLine($"{string.Join(",", socketTargets.Select(p=>p.Cache))}");
                //Thread.Sleep(100);
            }
        }
        [Command("offline")]
        public void Offline(string PathName, string FileType, string SrcPath, int PathNum, string togetherType, string root = "data", string taskID = "调试", bool saveSrc = false)
        {
            try
            {
                CancellationTokenSource[] cancellationTokenTargets = new CancellationTokenSource[PathNum];

                $"{root}\\{taskID}\\数据报表\\{PathName}报表.csv".CreateFileDir();
                UTF8Encoding utf8 = new System.Text.UTF8Encoding(true);
                FileStream fs = File.Open($"{root}\\{taskID}\\数据报表\\{PathName}报表.csv", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                InfoTailInfo infoTail = new InfoTailInfo();
                InfoTailInfoForWin infoForWin = new InfoTailInfoForWin();

                int SendPort;
                TogetherUnpackBlock unpack;
                switch (togetherType)
                {
                    case "每包3628":
                        unpack = new TogetherUnpackBlock(3628);
                        break;
                    case "每包3626":
                        unpack = new TogetherUnpackBlock(3626);
                        break;
                    default:
                        Console.WriteLine("不合法的数据类型");
                        return;
                }
                int InSpeed = 40;
                //unpack.ResetPackConfig(togetherConfig);
                switch (PathName)
                {
                    case "窄视场通道":
                        SendPort = 34110;
                        fs.Write(utf8.GetPreamble());
                        fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoTail)));
                        InSpeed = 40;
                        break;
                    case "宽视场通道1":
                        SendPort = 34210;
                        fs.Write(utf8.GetPreamble());
                        fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoTail)));
                        InSpeed = 100;
                        break;
                    case "宽视场通道2":
                        SendPort = 34310;
                        fs.Write(utf8.GetPreamble());
                        fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoTail)));
                        InSpeed = 100;
                        break;
                    case "宽视场开窗通道":
                        SendPort = 34410;
                        fs.Write(utf8.GetPreamble());
                        fs.Write(utf8.GetBytes(Decompress.CsvHelper.GetHeaders(infoForWin)));
                        InSpeed = 15;
                        break;
                    default:
                        Console.WriteLine("不合法的数据类型");
                        return;
                }
                fs.Flush();

                //使用udp数据报发送日志
                NotifyService notifyService = new NotifyService("127.0.0.1", 22222);
                TCPHubService notifyHub = new TCPHubService(33333);

                string[] files = null;

                if (FileType == "文件")
                {
                    files = new string[] { SrcPath };
                }
                if (FileType == "文件夹")
                {
                    files = Directory.EnumerateFiles(SrcPath, "*", SearchOption.AllDirectories).ToArray();

                }

                //开放发送数据的端口,根据配置的通道数开放多个端口
                SocketTargetBlock[] socketTargets = new SocketTargetBlock[PathNum];


                for (int i = 0; i < PathNum; i++)
                {
                    socketTargets[i] = new SocketTargetBlock(PathName, SendPort + i);
                    cancellationTokenTargets[i] = new CancellationTokenSource();
                }


                Dictionary<int, int> switchDictionary = new Dictionary<int, int>();//将不同通道的数据映射为从0开始的顺序，用于宽视场和宽视场开窗
                int switchNum = 0;
                unpack.PackArrived += (d, c, n, t,i) =>
                {
                    switch (t)
                    {
                        case "narrow_mid":
                            socketTargets[n + 1].Enqueue(d);
                            break;
                        case "narrow_short":
                            socketTargets[n].Enqueue(d);
                            break;
                        case "wide":
                            if (!switchDictionary.Keys.Contains(n))
                            {
                                switchDictionary[n] = switchNum;
                                switchNum++;
                            }
                            socketTargets[switchDictionary[n]].Enqueue(d);
                            break;

                        case "widewin":
                            if (!switchDictionary.Keys.Contains(n / 20))
                            {
                                switchDictionary[n / 20] = switchNum;
                                switchNum++;
                            }
                            socketTargets[switchDictionary[n / 20]].Enqueue(d);
                            break;
                        default:
                            Console.WriteLine("合路包数据有误");
                            return;
                    }

                };




                foreach (var item in socketTargets)
                {
                    item.OnConnected += (data) =>
                    {
                        //item.IsConnected = true;
                    };
                }
                for (int i = 0; i < PathNum; i++)
                {
                    socketTargets[i].Start(cancellationTokenTargets[i].Token);
                }

                Console.WriteLine("准备启动");
                notifyService.SetStatusFunc(() =>
                {
                    Dictionary<string, object> status = new Dictionary<string, object>()
                {
                    {"调度缓冲区",""},
                    {"通道输入速率",""},
                    {"已读取数据量", ""}
                };
                    Dictionary<string, long> data = new Dictionary<string, long>()
                {
                    {"调度缓冲区",0},
                    {"通道输入速率",0},
                    {"已读取数据量", 0}
                };
                    foreach (var s in socketTargets)
                    {
                        foreach (var kv in s.Status)
                        {
                            //status[s.Name + " " + kv.Key] += kv.Value;
                            data[kv.Key] += kv.Value;
                        }
                    }
                    foreach (var item in data)
                    {
                        if (item.Key == "通道输入速率")
                        {
                            status[item.Key] = item.Value.BytesSpeed(1000);
                        }
                        else
                        {
                            status[item.Key] = item.Value.BytesSize();
                        }
                    }
                    return status;
                });

                int TimerCount = 0;
                System.Timers.Timer timer = new System.Timers.Timer(2);
                timer.AutoReset = true;
                timer.Elapsed += (e, h) =>
                {
                    try
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
                            //Console.WriteLine($"剩余{notifyHub.LogData.Count}在写");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "报表写入有问题");
                    }
                };
                timer.Start();

                notifyService.Start();
                notifyHub.Start();
                //直接从文件读取数据
                foreach (var data in files.LoadBig())
                {
                    unpack.Enqueue(data);
                }

            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }
            while (true)
            {
                Thread.Sleep(1000);
                //Console.WriteLine($"{string.Join(",", socketTargets.Select(p=>p.Cache))}");
                //Thread.Sleep(100);
            }
        }
    }


}
