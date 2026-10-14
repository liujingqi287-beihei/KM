using Decompress;
using Decompress.Codec.L100.Const;
using FileCompareModule;
using KM.Codec.L100.Const;
using KM.Codec.L300.Decode.L310.Shared.Step0.TogetherPack;
using KM.Codec.L300.Decode.L310.Shared.Step3._6.DispatchPack;
using KM.Codec.L300.Decode.L340.WideViewWinDecoder;
using KM.Codec.L300.Decode.L360.NewStructDecoder;
using MessagePack;
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

namespace KM.ConsoleDecode
{
    public class SoftDecodeCommand
    {
        TogetherUnpackBlock unpack;
        RollingSaveBlockV2 saveBlock;
        Dictionary<int, NewStructDecodeBlock_temp> decodeBlock = new Dictionary<int, NewStructDecodeBlock_temp>();
        public int QueueSumCount => decodeBlock.Sum(p=>p.Value.Cache);
        public bool DecodeProcess;
        public int InputBytes = 0;
        public SoftDecodeCommand(int[] EccCore,int[] DispatchCore, SaveBlockConfig Config, string pathName, string savePath, string taskID, bool saveData, bool saveRaw, bool saveCam, bool downStreamRepost, bool IsCompare)
        {
            SetLog($"{savePath}");

CThreadPool ecc = new CThreadPool(EccCore);
           CThreadPool decode = new CThreadPool(DispatchCore);
            ulong cpuMask = 0;
            //Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)cpuMask;

            unpack = new TogetherUnpackBlock(3488, (ulong)cpuMask);
           // decodeBlock = new NewStructDecodeBlock_temp(EccCore, DispatchCore, DispatchType.轮询调度, 14, cpuMask);

            saveBlock = new RollingSaveBlockV2(Config, (ulong)cpuMask);
            //var save = new RollingFileStreamV2(savePath, taskID, "", "", "{root}/{TaskID}/{pathName}/aa/{index_prex}.src".Replace("aa", pathName + "原始码流").Replace("{pathName}", pathName));

            ConcurrentDictionary<string, int> saveCount = new ConcurrentDictionary<string, int>();
            Dictionary<string, Stopwatch> sws = new Dictionary<string, Stopwatch>();
            Dictionary<string, Queue<double>> avglists = new Dictionary<string, Queue<double>>();
            Dictionary<string, List<double>> avgrates = new Dictionary<string, List<double>>();

            TCPClientBlock client = new TCPClientBlock(pathName, "127.0.0.1", 9999);
            GetInfoBlock GetInfo = new GetInfoBlock($"{savePath}\\{taskID}\\{pathName}\\数据报表\\{pathName}报表.csv");

            unpack.PackArrived += (d, c, n, t, i) =>
            {
                n = TypeToChannnel(n, t, i);


                if (!decodeBlock.ContainsKey(n))
                {
                    decodeBlock[n] = new NewStructDecodeBlock_temp(ecc, decode, DispatchType.轮询调度, 14, cpuMask);
                    //解码部分
                    if (IsCompare)
                    {
                        var CompareBlock = new DataCompare();
                        decodeBlock[n].MidArrived += (data) =>
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
                            CompareBlock.Enqueue(data);
                        };
                    }
                    else
                    {
                        decodeBlock[n].MidArrived += (data) =>
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
                        };
                    }
                    List<int> TimeDelay = new List<int>();
                    decodeBlock[n].PackArrived += (key, data) =>
                    {
                        var nameKey = key.ToName();
                        var Key = data.Channel.ToString();
                        try
                        {
                            if (!saveBlock.Storage.Keys.Contains(Key) && !saveBlock.Storage.Keys.Contains("RAW" + Key))
                            {
                                string datatype = nameKey;
                                if (pathName == "数据开窗通道")
                                {
                                    datatype = datatype + "数据开窗";
                                }
                                if (pathName == "星上处理通道")
                                {
                                    datatype = datatype + "星上处理";
                                }
                                if (saveCam)
                                {
                                    saveBlock.AddStorage(Key, savePath, taskID, nameKey, datatype, "{root}/{TaskID}/{pathName}/{name}/{index_prex}.KM.{datatype}.temp".Replace("{pathName}", pathName));
                                }
                                if (saveRaw)
                                {
                                    saveBlock.AddStorage("RAW" + Key, savePath, taskID, nameKey, datatype, "{root}/{TaskID}/{pathName}/aa/{name}_RAW/{index_prex}.KM.{datatype}.raw".Replace("aa", pathName + "RAW图数据").Replace("{pathName}", pathName));
                                }
                            }
                            if (!saveCount.Keys.Contains(Key))
                            {
                                saveCount[Key] = 0;
                            }
                            var dataFrame = new Dictionary<string, List<byte>>();
                            switch (data.Type)
                            {
                                case DataPackType.CameraHead:
                                    if (saveCam)
                                    {
                                        saveBlock.Save(Key, saveCount[Key], data.Data);
                                    }
                                    if (downStreamRepost)
                                    {
                                        dataFrame[Key].AddRange(data.Data.ToList());
                                    }
                                    var temp = data.Data.AsSpan()[8..16];
                                    var ts = Convert.ToUInt64((DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalMilliseconds);
                                    var times = (int)(ts - BinaryPrimitives.ReadUInt64LittleEndian(temp));
                                    TimeDelay.Add(times);
                                    Console.WriteLine($"当前帧压缩延时{times}平均压缩延时{TimeDelay.Average()}");
                                    break;
                                case DataPackType.CameraTail:

                                    if (saveCam)
                                    {
                                        saveBlock.Save(Key, saveCount[Key], data.Data);
                                    }
                                    if (downStreamRepost)
                                    {
                                        dataFrame[Key].AddRange(data.Data.ToList());
                                        DataArrived?.Invoke(dataFrame[Key].ToArray());
                                        dataFrame[Key].Clear();
                                    }
                                    break;
                                case DataPackType.ImageRow:
                                    /*DataArrived?.Invoke(data.Data);
                                    if (saveCam)
                                    {
                                        saveBlock.Save(Key, saveCount[Key], data.Data);
                                    }
                                    if (saveRaw)
                                    {
                                        if (data.Channel < 50)
                                        {
                                            saveBlock.Save("RAW" + Key, saveCount[Key], data.Data[8..]);
                                        }
                                        else
                                        {
                                            saveBlock.Save("RAW" + Key, saveCount[Key], data.Data);
                                        }
                                    }
                                    if (downStreamRepost)
                                    {
                                        dataFrame[Key].AddRange(data.Data.ToList());
                                    }*/
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
                                    GetInfo.Enqueue(data.WithInfo(UTF8Encoding.UTF8.GetBytes($"{nameKey}_文件号{saveCount[Key]}")).WithSpend(spend));
                                    break;
                                default:
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(UTF8Encoding.UTF8.GetBytes(ex.StackTrace));
                        }
                    };
                }
                decodeBlock[n].Enqueue(n,d);
              
            };

            List<byte> Image = new List<byte>();
           

            if(pathName == "数据开窗通道"|| pathName == "星上处理通道")
            {
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
                            输入帧频 = WinInfo.输入帧频,
                            输出帧频 = WinInfo.输出帧频,
                            输入字节数 = InputBytes,
                            error = e,
                            total = t
                        };
                     /*   if(decodeBlock.QueueSumCount == 0)
                        {
                            pack.解码状态 = false;
                        }
                        else
                        {
                            pack.解码状态 = true;
                        }*/
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                    client.Enqueue(MessagePackSerializer.Serialize(pack));
                };
                client.Start();
            }
            else
            {
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
                            输入帧频 = info.输入帧频,
                            输出帧频 = info.输出帧频,
                            输入字节数 = InputBytes,
                            error = e,
                            total = t
                        };
                        /*if (decodeBlock.QueueSumCount == 0)
                        {
                            pack.解码状态 = false;
                        }
                        else
                        {
                            pack.解码状态 = true;
                        }*/
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                    client.Enqueue(MessagePackSerializer.Serialize(pack));
                };
                client.Start();
            }
            

            DateTime _startTime = DateTime.Now;
            System.Timers.Timer _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (sender, e) =>
            {
                Console.WriteLine();
                Console.WriteLine("======================");
                foreach(var d in decodeBlock.Values)
                {
                    Console.WriteLine(d.GetStatus());
                }
                //Console.WriteLine(decodeBlock.GetStatus());
                Console.WriteLine($"存储队列数：{saveBlock.QueueCount}");
            };
            _timer.AutoReset = true;
            _timer.Start();
        }

        private int TypeToChannnel(int n, string t, string i)
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
            if (i == "star_win")
            {
                if (t == "短波FPGA1")
                    n += 450;
                else if (t == "短波FPGA2")
                    n += 550;
                else if (t == "中波FPGA1")
                    n += 650;
                else
                    n += 750;
            }
            return n;
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
            if (channel >= 450 && channel < 550)
            {
                return $"星上处理短波FPGA1_开窗号{(channel - 450).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 550 && channel < 650)
            {
                return $"星上处理短波FPGA2_开窗号{(channel - 550).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 650 && channel < 750)
            {
                return $"星上处理中波FPGA1_开窗号{(channel - 650).ToString().PadLeft(3, '0')}";
            }
            if (channel >= 750 && channel < 850)
            {
                return $"星上处理中波FPGA2_开窗号{(channel - 750).ToString().PadLeft(3, '0')}";
            }
            return "有问题的";
        }
        private void SetLog(string home)
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
        public void Enqueue(byte[] data)
        {
            unpack.Enqueue(data);
        }

        public void UpdateStatus(int inputBytes)
        {
            InputBytes += inputBytes;
        }
        public Action<byte[]> DataArrived { get; set; }
    }
}

