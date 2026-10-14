using Decompress;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace KM.ConsoleDecode.Dispatch.SendDataPack
{
    public class ApplyServerSocket : IDisposable
    {
        private string _Name;
        public bool IsRunning = false;
        private Socket _ListenerSocket { get; set; }
        public IPEndPoint _LocalEndPoint { get; set; }
        /// <summary>
        /// true表示正常结束当前连接，false表明当前服务器需要重启
        /// </summary>
        public Func<Socket, CancellationToken, bool> _Process { get; set; }
        public ApplyServerSocket(string name, int Port)
        {
            _Name = name;
            _LocalEndPoint = new IPEndPoint(IPAddress.Any, Port);

        }
        public Task Start(CancellationToken token)
        {
            IsRunning = true;
            return Loop.NeverStopAsync((token) =>
            {
                _ListenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _ListenerSocket.Bind(_LocalEndPoint);
                _ListenerSocket.Listen(10);
                Console.WriteLine("初始化零级{0}", _LocalEndPoint); // socket
                Loop.UntilException(EstablishServer, token);


                return true;
            }, token);

        }
        private bool EstablishServer(CancellationToken token)
        {

            Console.WriteLine("{0}监听中", _Name);
            while (!token.IsCancellationRequested)
            {
                var clientsocket = _ListenerSocket.Accept();
                Console.WriteLine("客户端发起请求  {0} {1}", clientsocket.LocalEndPoint, clientsocket.RemoteEndPoint);
                if (!EstablishConnect(clientsocket, token))
                {
                    return true;
                }
                Thread.Sleep(100);

            }
            return false;
        }
        public Socket Client { get; private set; }
        public bool IsConnected = false;
        public EndPoint Remote
        {
            get
            {
                return Client?.RemoteEndPoint;

            }
        }
        public EndPoint Local
        {
            get
            {
                return Client?.LocalEndPoint;

            }
        }
        /// <summary>
        /// 建立连接
        /// </summary>
        /// <param name="clientsocket"></param>
        /// <returns>false表明永久结束这次连接，重启服务器，true 只会中断当前连接</returns>
        private bool EstablishConnect(Socket clientsocket, CancellationToken token)
        {
            IsConnected = true;
            Console.WriteLine("申请成功  {0} {1}", clientsocket.LocalEndPoint, clientsocket.RemoteEndPoint);
            Client = clientsocket;
            Client.SetKeepAlive(1000, 1000);


            Console.WriteLine("开始处理 {0} {1}", clientsocket.LocalEndPoint, clientsocket.RemoteEndPoint);
            _Process(clientsocket, token);
            Console.WriteLine("发送完成");


            IsConnected = false;
            clientsocket.Shutdown(SocketShutdown.Both);
            clientsocket.Close(100);
            Console.WriteLine("结束处理");

            IsRunning = false;

            Client = null;
            return false;
        }
        public void Dispose()
        {
            _ListenerSocket.Dispose();
        }
    }
}
