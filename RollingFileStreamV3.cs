using Decompress;
using Decompress.Codec.L100.Const;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;

namespace KM.ConsoleDecode
{
    public class RollingFileStreamV3 : IDisposable
    {
        //现用 算法不同
        public static string BatchId = DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒");
        private FileStream _fs;
        private string Template;
        private string Root;
        private string DataType; 
        private string Name;
        private string TaskID;
        private string LastPath = "";
        public string NewPath = ""; 
        private bool HasIndex = true;
        //仅记录此存储什么数据，不写进路径里面
        private static string PathName = "";
        private DateTime time;
        private DateTime daytime;
        string DayPath= DateTime.Now.ToString("yyyy年MM月dd日")+PathName;
        private string Path => Template.Replace("{root}", Root).Replace("{TaskID}",$"{DayPath}/{TaskID}").Replace("{name}", Name).Replace("{BatchId}", BatchId).Replace("{datatype}", DataType);
        public RollingFileStreamV3(string root, string taskID, string name,string datatype, string template = "{root}/{TaskID}/{name}/{index_prex}.n01.{datatype}.temp",string pathName="")
        {
            PathName = pathName;
            TaskID = taskID;
            Name = name;
            DataType = datatype; 
            Root = root;
            Template = template;
            time = DateTime.Now;
            daytime=DateTime.Now;
            DayPath= DateTime.Now.ToString("yyyy年MM月dd日")+PathName;
        }
        
        public void Dispose()
        {
            if (_fs != null)
            {
                ((IDisposable)_fs).Dispose();

            }
        }
        public static void RefreshBatchId()
        {
            BatchId = DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒");
        }

        int last = 1;//现在存储文件编号以1开头
        public void Write(byte[] data, int? index = null)
        {
            if ((DateTime.Now-daytime).TotalHours>=1)
            {
                DayPath= DateTime.Now.ToString("yyyy年MM月dd日")+PathName;
                daytime = DateTime.Now;
            }
            if ((DateTime.Now-time).TotalHours>=1)
            {
                time = DateTime.Now;
                TaskID=time.ToString("yyyy年MM月dd日HH时mm分ss秒")+PathName;
                var currentpath = Template.Replace("{root}", Root).Replace("{TaskID}",$"{DayPath}/{TaskID}").Replace("{name}", Name).Replace("{BatchId}", BatchId).Replace("{datatype}", DataType).Replace("{index_prex}", index.ToString().PadLeft(8, '0'));
                _fs = File.OpenWrite(currentpath);
            }
            if (_fs == null)
            {
                var currentpath = Path.Replace("{index_prex}", index.ToString().PadLeft(8, '0')).Replace("{TaskID}",$"{DayPath}/{TaskID}");

                currentpath.CreateFileDir();
                LastPath = currentpath;
                NewPath = currentpath.Replace(".temp", ".cam");

                _fs = File.OpenWrite(currentpath);
            }
            if (HasIndex && index.HasValue)
            {
                if (last != index.Value)
                {
                    _fs.Close();
                    //给临时存储文件改名为.cam后缀
                    try
                    {
                        File.Move(LastPath, NewPath);
                    }
                    catch(Exception ex)
                    {
                        Log.Logger.Error(ex,"文件存储存在异常");
                    }
                    var currentpath = Path.Replace("{index_prex}", index.ToString().PadLeft(8, '0')).Replace("{TaskID}",$"{DayPath}/{TaskID}");
                    currentpath.CreateFileDir();
                    LastPath = currentpath;
                    NewPath = currentpath.Replace(".temp", ".cam");
                    _fs = File.OpenWrite(currentpath);
                    last = index.Value;
                }

            }
            _fs.Write(data);
        }
  
    
        public void Flush()
        {
            if (_fs != null)
            {
                _fs.Close();
                //给临时存储文件改名为.cam后缀
                try
                {
                    if (!File.Exists(NewPath))
                    {
                        FileInfo fi = new FileInfo(LastPath);
                        fi.MoveTo(NewPath);
                    }
                }
                catch
                {
                    throw new Exception("文件存储存在异常");
                }
                _fs = null;
            }
        }
    }
}
