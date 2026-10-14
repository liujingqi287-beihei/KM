using Decompress;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;


namespace KM.ConsoleDecode.Dispatch.SendDataPack
{
    public class SocketTargetBlock_special : Block, IDisposable
    {
        public Dictionary<string, long> Status => new Dictionary<string, long>()
        {
            {"调度缓冲区",((long)Buffer.Length)},
            {"通道输入速率",InputMetric.Last},
            {"已读取数据量", AllDataLength}
        };
        private LimitByteQueue Buffer { get; set; } = new();
        private ApplyServerSocket ZeroLevelSocket { get; set; }
        private ActionBlock<byte[]> InputBuffer { get; set; }
        //RollingFileStreamV2 SocSave = new RollingFileStreamV2("D:\\data", "refsocket", "narrow", "narrow");

        private long AllDataLength = 0;
        public override bool IsRunning => ZeroLevelSocket.IsRunning;
        public override int Cache => Buffer.Length;
        public bool IsConnected = false;
        public void Enqueue(byte[] buffer)
        {
            //SocSave.WriteTemp(buffer, 0);
            //ConnectReadyEvent.WaitOne();
            InputBuffer.Post(buffer);
        }
        public int Length = 0;
        /// <summary>
        /// 构造一个socket发送模块
        /// </summary> 
        /// <param name="port">发送的端口</param>
        public SocketTargetBlock_special(string name, int port) : base(name)
        {
            ZeroLevelSocket = new ApplyServerSocket(name, port);
            ZeroLevelSocket._Process += Process;
        }
        public uint PackCount { get; private set; } = 0;

        public override Task Start(CancellationToken token)
        {
            InputBuffer = new ActionBlock<byte[]>(data =>
            {

                if (Buffer.Length > 80 * 1024 * 1024)
                {
                    Log.Logger.Warning("丢数,数据处理慢");
                    return;
                }
                AllDataLength += data.Length;
                InputMetric.Now(data.Length);
                Buffer.Enqueue(data);
            });

            return ZeroLevelSocket.Start(token);
        }
        public Action<Socket> OnConnected;
        public Action<byte[]> OnSend;
        public Action OnNotEnoughData;
        public int packcount = 0;
        public bool Overflow => Buffer.Overflow;



        public bool Process(Socket socket, CancellationToken token)
        {
            OnConnected?.Invoke(socket);
            //ConnectReadyEvent.Set();
            //var packSize = 100 * 1024;


            while (!token.IsCancellationRequested)
            {

                if (!socket.IsConnected())
                {
                    return false;
                }
                if (Buffer.Length == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }


                ////当有满满一帧时
                //while (Buffer.Length >= packSize)
                //{
                var waitforsend = Buffer.DequeueAll(); //取出队列里的第一个元素
                var data = waitforsend;
                OnSend?.Invoke(data);
                var sendLength = socket.Send(data);

                Length += sendLength;

                packcount++;
                if (sendLength <= 0)
                {
                    //Console.WriteLine("发送完成共{0}包", packcount);
                    return false;

                }

                //}

                ////如果数据不足
                //var currentSize = Buffer.Length;
                //if (currentSize == 0)
                //{
                //    return false;
                //}
                //if (currentSize < packSize)
                //{
                //    var data1 = Buffer.Dequeue(currentSize);
                //    var sendLength = socket.Send(data1);

                //    Length += sendLength;

                //    packcount++;
                //    if (sendLength <= 0)
                //    {
                //        Console.WriteLine("发送完成共{0}包", packcount);
                //        return false;

                //    }
                //}


            }
            return false;

        }

        public void Dispose()
        {
            ((IDisposable)ZeroLevelSocket).Dispose();
        }

        //private int PackDataSize => (int)(ZeroLevelSocket.FrameSize * ZeroLevelSocket.FrameCount);

        //public override bool IsRunning => ZeroLevelSocket.IsConnected || Buffer.Length == 0;
        //public override int Cache => Buffer.Length;
    }
}
