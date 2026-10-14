using Decompress;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace KM.ConsoleDecode.Dispatch.RecieveData
{
    public class ApplyClientSocket : IDisposable
    {
        public ILogger logger { get; set; }
        private Socket _Client { get; set; }
        public EndPoint Remote
        {
            get
            {
                try
                {
                    return _Client?.RemoteEndPoint;
                }
                catch
                {
                    return null;
                }

            }
        }
        public EndPoint Local
        {
            get
            {
                try
                {
                    return _Client?.LocalEndPoint;
                }
                catch
                {
                    return null;
                }
            }
        }
        private CircleList<IPEndPoint> _RemoteEndPoints { get; set; }

        private List<byte> _waitForPack = new List<byte>();

        public ApplyClientSocket(IEnumerable<HostOptions> hostOptions, int packlen = 1024 * 1024)
        {
            _RemoteEndPoints = new CircleList<IPEndPoint>(hostOptions.Select(p => new IPEndPoint(IPAddress.Parse(p.Host), p.Port)).ToList());


        }
        public Action<byte[]> SocketDataArrived { get; set; }
        public Action<List<byte>, int> RunningStateArrived { get; set; }
        public Action<byte[]> DataArrived { get; set; }
        public ulong RecvCount = 0;
        public ulong PackCount = 0;
        public ulong ErrorCount = 0;
        public ulong ConnectCount = 0;


        public Task Start(CancellationToken token)
        {
            return Loop.NeverStopAsync((token) =>
            {
                _Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _Client.SetKeepAlive(1000, 1000);
                return EstablishClient(token);
            }, token);

        }
        private uint reconnect = 0;
        public uint Reconnect => reconnect;
        public bool IsConnected { get; private set; }
        private bool EstablishClient(CancellationToken token)
        {
            var remote = _RemoteEndPoints.Next();
            try
            {

                _Client.Connect(remote);
                Console.WriteLine("连接到远程{0}", remote.ToString());
                Console.WriteLine("认证成功开始处理");
                IsConnected = true;
                reconnect = 0;
                Console.WriteLine("{0}连接成功，等待接受数据", _Client.RemoteEndPoint);
                int BuffLen = 1024 * 100;
                byte[] buff = new byte[BuffLen];
                //int index = 0;

                Stopwatch sw = Stopwatch.StartNew();
                long totalBytes = 0;
                while (true)
                {
                    //int receiveLength = _Client.Receive(buff, index, BuffLen - index, SocketFlags.None);
                    int receiveLength = _Client.Receive(buff, SocketFlags.None);


                    //收到的数据小于等于0 说明不正常，结束当前循环循环
                    //if (receiveLength < BuffLen)
                    //{
                    //    index += receiveLength;
                    //    continue;
                    //}
                    if (receiveLength < 0)
                    {
                        break;
                    }
                    else if (receiveLength == 0)
                    {
                        break;
                    }

                    totalBytes += receiveLength;
                    RecvCount++;
                    var data = buff[..receiveLength];

                    ////SocketDataArrived?.Invoke(data);
                    DataArrived?.Invoke(data);
                    //index = 0;
                    //_processBlock.Post(data);


                    double sec = sw.Elapsed.TotalSeconds;
                    if(sec > 0)
                    {
                        double mbs = (totalBytes / 1024.0/1024.0) / sec;
                        Console.WriteLine($"接收速率:{mbs:0.00}MB/s");
                    }
                }
                Console.WriteLine("结束处理");

                //RunningStateArrived?.Invoke(file, Length);

                IsConnected = false;

            }
            catch (Exception ex)
            {

            }
            Thread.Sleep(1000);

            ConnectCount++;

            return false;
        }

        //private bool Apply(Socket socket, CancellationToken token)
        //{
        //    _Logger.Information("发送申请帧");
        //    var request = PackFactory.FormatRequestPack();
        //    request.SetFrame(FrameCount, FrameSize);
        //    //申请帧
        //    var result = socket.Send(request.ToBytes(), SocketFlags.None);
        //    return result == request.ToBytes().Length;
        //}

        public void Dispose()
        {
            ((IDisposable)_Client).Dispose();
        }
    }
}
