using CommandDotNet;
using Decompress.Blocks;
using Decompress.Codec.L100.Const;
using DemoConsole;
using KM.ConsoleDecode.Dispatch.SendDataPack;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KM.ConsoleDecode.Send
{
    [Command("send")]
    public class SendCommand
    {
        [Command("startone")]
        public void Send(string pathName, string watchFolder, int port)
        {
            string patterns = pathName switch
            {
                "短波A0" => "短波压缩数据A_0.cam",
                "短波A1" => "短波压缩数据A_1.cam",
                "短波A2" => "短波压缩数据A_2.cam",
                "短波A3" => "短波压缩数据A_3.cam",
                "短波B0" => "短波压缩数据B_0.cam",
                "短波B1" => "短波压缩数据B_1.cam",
                "短波B2" => "短波压缩数据B_2.cam",
                "短波B3" => "短波压缩数据B_3.cam",
                "中波A0" => "中波压缩数据A_0.cam",
                "中波A1" => "中波压缩数据A_1.cam",
                "中波A2" => "中波压缩数据A_2.cam",
                "中波A3" => "中波压缩数据A_3.cam",
                "中波B0" => "中波压缩数据B_0.cam",
                "中波B1" => "中波压缩数据B_1.cam",
                "中波B2" => "中波压缩数据B_2.cam",
                "中波B3" => "中波压缩数据B_3.cam",
                "数据处理开窗" => "数据开窗.cam",
                "星上处理开窗" => "星上处理.cam",
                _ => throw new Exception()
            };

            string filter = pathName switch
            {
                "短波A0" => "*短波*.cam",
                "短波A1" => "*短波*.cam",
                "短波A2" => "*短波*.cam",
                "短波A3" => "*短波*.cam",
                "短波B0" => "*短波*.cam",
                "短波B1" => "*短波*.cam",
                "短波B2" => "*短波*.cam",
                "短波B3" => "*短波*.cam",
                "数据处理开窗" => "*数据处理*.cam",
                "星上处理开窗" => "*星上处理*.cam",
                _ => "*中波*.cam",
            };
            Queue<string> OutPath = new Queue<string>();

            try
            {
                SendFileBlock sendFile = new SendFileBlock(pathName, port);
                NotifyService service = new NotifyService("127.0.0.1", 55555);


                if (!Directory.Exists(watchFolder))
                {
                    Directory.CreateDirectory(watchFolder);
                }
                string selectedFolder = watchFolder;

                FileSystemWatcher watcher = new FileSystemWatcher(selectedFolder);
                watcher.Filter = filter;
                watcher.IncludeSubdirectories = true;
                watcher.EnableRaisingEvents = true;
                watcher.NotifyFilter = NotifyFilters.FileName;
                //Queue<string> test = new Queue<string>();
                watcher.Renamed += (s, e) =>
                {
                    if (e.FullPath.Contains(patterns))
                    {
                        Console.WriteLine(e.FullPath);
                        OutPath.Enqueue(e.FullPath);
                        Log.Logger.Information(OutPath.ToString());
                    }
                };
                watcher.IncludeSubdirectories = true;
                watcher.EnableRaisingEvents = true;

                service.SetStatusFunc(() =>
                {
                    return sendFile.Status;
                });
                service.Start();

                TimerAction timerAction = new TimerAction(() =>
                {
                    if (OutPath.Count > 0 && sendFile.connect == "已连接")
                    {
                        var path = OutPath.Dequeue();
                        sendFile.Enqueue(path);
                    }
                }, 1);
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }

        }
        [Command("offline")]
        public void Offline(string pathName, int port, string srcFolders)
        {

            //string[] patterns = pathName switch
            //{
            //    "窄视场中波通道" => new string[] { "窄视场中波.cam" },
            //    "窄视场短波通道" => new string[] { "窄视场短波.cam" },
            //    "宽视场镜头1通道" => new string[] { "宽视场000.cam", "宽视场003.cam", "宽视场005.cam", "宽视场008.cam", "宽视场011.cam", "宽视场013.cam" },
            //    "宽视场镜头2通道" => new string[] { "宽视场002.cam", "宽视场004.cam", "宽视场007.cam", "宽视场010.cam", "宽视场012.cam", "宽视场015.cam" },
            //    "宽视场镜头3通道" => new string[] { "宽视场001.cam", "宽视场006.cam", "宽视场009.cam", "宽视场014.cam" },
            //    "宽视场开窗通道" => new string[] { "宽视场开窗" },
            //    _ => throw new Exception()
            //};
            //string filter = pathName switch
            //{
            //    "窄视场中波通道" => "*窄视场中波*",
            //    "窄视场短波通道" => "*窄视场短波*",
            //    "宽视场镜头1通道" => "*宽视场*.cam",
            //    "宽视场镜头2通道" => "*宽视场*.cam",
            //    "宽视场镜头3通道" => "*宽视场*.cam",
            //    "宽视场开窗通道" => "*宽视场开窗*",
            //    _ => throw new Exception()
            //};
            Queue<string> OutPath = new Queue<string>();
            Dictionary<string, string[]> temp = new Dictionary<string, string[]>();
            int maxLength = 0;
            SendFileBlock sendFile = new SendFileBlock(pathName, port);
            NotifyService service = new NotifyService("127.0.0.1", 55555);
            string[] SrcFolders = new string[] { srcFolders };
            foreach (var directory in SrcFolders)
            {
                temp[directory] = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToArray();
                if (maxLength < temp[directory].Length)
                {
                    maxLength = temp[directory].Length;
                }
            }
            for (int i = 0; i < maxLength; i++)
            {
                foreach (var directory in SrcFolders)
                {
                    if (i < temp[directory].Length)
                    {
                        OutPath.Enqueue(temp[directory][i]);
                    }
                }
            }

            service.SetStatusFunc(() =>
            {
                return sendFile.Status;
            });
            service.Start();



            TimerAction timerAction = new TimerAction(() =>
            {
                if (OutPath.Count > 0 && sendFile.connect == "已连接")
                {
                    var path = OutPath.Dequeue();
                    sendFile.Enqueue(path);
                }
            }, 1);
            Console.ReadKey();
        }
        public void SendWithoutSave(string pathName,int port)
        {
            
            Queue<string> OutPath = new Queue<string>();

            try
            {
                SendFileBlock sendFile = new SendFileBlock(pathName, port);
                NotifyService service = new NotifyService("127.0.0.1", 55555);

                service.SetStatusFunc(() =>
                {
                    return sendFile.Status;
                });
                service.Start();

                
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }
        }
    }
}
