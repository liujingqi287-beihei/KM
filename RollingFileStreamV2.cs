using Decompress.Codec.L100.Const;
using Serilog;
using System;
using System.IO;

namespace KM.ConsoleDecode
{
    public class RollingFileStreamV2 : IDisposable
    {
        public static string BatchId = DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒");
        private FileStream _fs;
        private string Template;
        private string Root;
        private string DataType;
        //private string Key;
        private string Name;
        private string TaskID;
        private string LastPath = "";
        public string NewPath = "";
        //public Queue<string> NewPathQueue = new Queue<string>();
        private bool HasIndex = true;
        private string Path => Template.Replace("{root}", Root).Replace("{TaskID}", TaskID).Replace("{name}", Name).Replace("{BatchId}", BatchId).Replace("{datatype}", DataType);
        public RollingFileStreamV2(string root, string taskID, string name, string datatype, string template = "{root}/{TaskID}/{name}/{index_prex}.km.{datatype}.temp")
        {
            TaskID = taskID;
            Name = name;
            DataType = datatype;
            //Key = key;
            Root = root;
            Template = template;
            //if (template.Contains("{index}"))
            //{
            //    HasIndex = true;
            //}
        }
        public void ResetName(string name)
        {
            Name = name;
        }
        public void ResetTemplate(string template)
        {
            Template = template;
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
            if (_fs == null)
            {
                //RefreshBatchId();
                var currentpath = Path.Replace("{index_prex}", index.ToString().PadLeft(8, '0'));
                //.Replace("{index}", "_" + index.ToString().PadLeft(8, '0'));

                currentpath.CreateFileDir();
                LastPath = currentpath;
                NewPath = currentpath.Replace(".temp", ".cam");
                //NewPathQueue.Enqueue($"{Name}_{index.ToString().PadLeft(8, '0')}");

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
                        //if (!File.Exists(NewPath))
                        //{
                        //    File.Move(LastPath, NewPath);
                        //    //FileInfo fi = new FileInfo(LastPath);
                        //    //fi.MoveTo(NewPath);
                        //}
                    }
                    catch (Exception ex)
                    {
                        //Log.Logger.Error(ex, "文件存储存在异常");
                    }
                    //RefreshBatchId();
                    var currentpath = Path.Replace("{index_prex}", index.ToString().PadLeft(8, '0'));
                    //.Replace("{index}", "_" + index.ToString().PadLeft(8, '0'));
                    //Path.Replace("{index}", "_" + index.ToString().PadLeft(8, '0')).CreateFileDir();
                    currentpath.CreateFileDir();
                    LastPath = currentpath;
                    NewPath = currentpath.Replace(".temp", ".cam");
                    //NewPathQueue.Enqueue($"{Name}_{index.ToString().PadLeft(8, '0')}");
                    _fs = File.OpenWrite(currentpath);
                    last = index.Value;
                }

            }
            _fs.Write(data);
        }
        public void WriteTemp(byte[] data, int? index = null)
        {
            string currentpath = "";
            if (_fs == null)
            {
                currentpath = Path.Replace("{index_prex}", index.ToString().PadLeft(8, '0'));
                //.Replace("{index}", "_" + index.ToString().PadLeft(8, '0'));

                currentpath.CreateFileDir();


                _fs = File.OpenWrite(currentpath);
            }
            if (HasIndex && index.HasValue)
            {
                if (last != index.Value)
                {
                    _fs.Close();
                    currentpath = Path.Replace("{index_prex}", index.ToString().PadLeft(8, '0'));
                    //.Replace("{index}", "_" + index.ToString().PadLeft(8, '0'));
                    //Path.Replace("{index}", "_" + index.ToString().PadLeft(8, '0')).CreateFileDir();
                    currentpath.CreateFileDir();
                    _fs = File.OpenWrite(currentpath);
                    last = index.Value;
                }

            }
            _fs.Write(data);

        }
        public void WriteAndGetPath(byte[] data, out string path, int? index = null)
        {
            if (_fs == null)
            {
                var currentpath = Path.Replace("{index_prex}", index.ToString().PadLeft(8, '0'));
                //.Replace("{index}", "_" + index.ToString().PadLeft(8, '0'));

                currentpath.CreateFileDir();


                _fs = File.OpenWrite(currentpath);
            }
            if (HasIndex && index.HasValue)
            {
                if (last != index.Value)
                {
                    _fs.Close();
                    var currentpath = Path.Replace("{index_prex}", index.ToString().PadLeft(8, '0'));
                    //.Replace("{index}", "_" + index.ToString().PadLeft(8, '0'));
                    //Path.Replace("{index}", "_" + index.ToString().PadLeft(8, '0')).CreateFileDir();
                    currentpath.CreateFileDir();
                    _fs = File.OpenWrite(currentpath);
                    last = index.Value;
                }

            }
            _fs.Write(data);
            _fs.Close();
            //path = Dictory.Replace("{index_prex}", index.ToString().PadLeft(8, '0'));
            path = Path.Replace("{index_prex}", index.ToString().PadLeft(8, '0'));
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
