using Decompress;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace KM.ConsoleDecode.Dispatch.RecieveData
{
    public class ApplyClientSocket_WithVCID : IDisposable
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
        private byte[] DataType;
        TCPDataPack data = new TCPDataPack();

        public ApplyClientSocket_WithVCID(string name, string vcid, IEnumerable<HostOptions> hostOptions, int packlen = 1024 * 1024)
        {
            var first = Convert.ToByte(vcid[2..], 16);
            var secend = Convert.ToByte(vcid[..2], 16);
            DataType = name switch
            {
                "短波通道A" => data.ReplaceVCID(first, secend, data.ShortRequest),
                "短波通道B" => data.ReplaceVCID(first, secend, data.ShortRequest),
                "短波通道C" => data.ReplaceVCID(first, secend, data.ShortRequest),
                "短波通道D" => data.ReplaceVCID(first, secend, data.ShortRequest),
                "中波通道A" => data.ReplaceVCID(first, secend, data.MidRequest),
                "中波通道B" => data.ReplaceVCID(first, secend, data.MidRequest),
                "中波通道C" => data.ReplaceVCID(first, secend, data.MidRequest),
                "中波通道D" => data.ReplaceVCID(first, secend, data.MidRequest),
                "数据开窗通道A" => data.ReplaceVCID(first, secend, data.WideWinRequest),
                "数据开窗通道B" => data.ReplaceVCID(first, secend, data.WideWinRequest),
                "星上处理通道A" => data.ReplaceVCID(first, secend, data.WideWinRequest),
                "星上处理通道B" => data.ReplaceVCID(first, secend, data.WideWinRequest),
            };
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

                if (!Apply(_Client, token))
                // if (!Loop.Until(() => _Apply(clientsocket, token)))
                {
                    Console.WriteLine("申请失败 {0} {1}", _Client.LocalEndPoint, _Client.RemoteEndPoint);
                    _Client.Shutdown(SocketShutdown.Both);
                    _Client.Close(100);
                    return false;
                    // return true;
                }

                Console.WriteLine("{0}连接成功，等待接受数据", _Client.RemoteEndPoint);
                int BuffLen = 1024 * 100;
                byte[] buff = new byte[BuffLen];


                if (!DataApply(_Client, token))
                // if (!Loop.Until(() => _Apply(clientsocket, token)))
                {
                    Console.WriteLine("申请失败 {0} {1}", _Client.LocalEndPoint, _Client.RemoteEndPoint);
                    _Client.Shutdown(SocketShutdown.Both);
                    _Client.Close(100);
                    return false;
                    // return true;
                }
                int index = 0;

                while (true)
                {
                    //Console.WriteLine("----------------------------------------------------------");
                    //int receiveLength = _Client.Receive(buff, index, BuffLen - index, SocketFlags.None);
                    int receiveLength = _Client.Receive(buff, SocketFlags.None);
                    //Console.WriteLine($"数据长度：{receiveLength}Bytes");

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


                    RecvCount++;
                    var data = buff[..receiveLength];

                    ////SocketDataArrived?.Invoke(data);
                    DataArrived?.Invoke(data);
                    //index = 0;
                    //_processBlock.Post(data);
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


        private bool Apply(Socket socket, CancellationToken token)
        {
            Console.WriteLine("发送登录申请");
            var request = data.DataLogPack;
            var result = socket.Send(request, SocketFlags.None);

            var response = data.DataResponsePack;
            int received = socket.Receive(response);
            Console.WriteLine($"已接收{received}字节：" + BitConverter.ToString(response));

            return result == request.Length;
        }
        private bool DataApply(Socket socket, CancellationToken token)
        {
            Console.WriteLine("发送数据请求");
            var request = DataType;
            //申请帧
            var result = socket.Send(request, SocketFlags.None);
            return result == request.Length;
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
