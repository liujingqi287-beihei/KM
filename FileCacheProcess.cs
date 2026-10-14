using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Decompress.Codec.L100.Const;
using Decompress.Interface.Block;
using System.Threading;
using System.Collections.Concurrent;

namespace KM.ConsoleDecode
{
    public class FileCacheBlock : ProcessBlock<byte[]>
    {
        public string CacheFolder;
        public bool SaveSrc;
        private int ChunkSize;
        private BufferedStream currentFile;
        private Thread _processThread;
        private BlockingCollection<string> _fileQueue = new BlockingCollection<string>();

        public FileCacheBlock(string cacheFolder, int Speed,int chunkSize,bool saveSrc, Func<bool> IsNeedWait)
        {
            ChunkSize = chunkSize*1024*1024;
            CacheFolder = cacheFolder;
            SaveSrc = saveSrc;
            Directory.CreateDirectory(CacheFolder);
            ClearFiles();
            RotateFile();
            _processThread = new Thread(() =>
            {
                foreach (var item in _fileQueue.GetConsumingEnumerable())
                {
                    try
                    {
                        if(!File.Exists(item))
                        {
                        }
                        else
                        {
                            foreach (var data in item.LoadS(Speed))
                            {
                                if (data == null)
                                {
                                }
                                else
                                {
                                    while (IsNeedWait())
                                    {
                                        Thread.Sleep(10);
                                    }
                                    DataArrived?.Invoke(data);
                                }

                            }
                            if(!SaveSrc)
                            {
                                File.Delete(item);
                            }
                            
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error(ex, "FileQueue Error");
                    }

                }
            });
            _processThread.Start();
        }

        protected override void Action(byte[] data)
        {
            currentFile.Write(data);
            if(currentFile.Length > ChunkSize)
            {
                RotateFile();
            }

        }
        string lastfilename = "";
        public void RotateFile()
        {

            if (currentFile != null)
            {
                currentFile.Close();
                var newfilename = Path.Combine(CacheFolder, Path.GetFileNameWithoutExtension(lastfilename) + ".src");
                File.Move(Path.Combine(CacheFolder, lastfilename), newfilename, overwrite: true);
                _fileQueue.Add(newfilename);
            }
            lastfilename = $"{DateTime.Now:yyyyMMddHHmmss}.temp";
            currentFile =new BufferedStream(File.OpenWrite(
                Path.Combine(CacheFolder, lastfilename)),48*1024*1024);
        }
        private void ClearFiles()
        {
            foreach (var file in Directory.GetFiles(CacheFolder, "*"))
            {
                File.Delete(file);
            }
        }
        public void Flush()
        {
            if (currentFile != null)
            { 
                currentFile.Close();
                var newfilename = Path.Combine(CacheFolder,Path.GetFileNameWithoutExtension(lastfilename) + ".src"); 
                File.Move(lastfilename, newfilename,overwrite:true);
                _fileQueue.Add(newfilename);
            }
            _fileQueue.CompleteAdding();
            _processThread?.Join();
        }
        public Action<byte[]> DataArrived { get; set; }
    }
}