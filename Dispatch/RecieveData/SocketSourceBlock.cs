using Decompress;
using Decompress.Blocks.ProtocolSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KM.ConsoleDecode.Dispatch.RecieveData
{
    public class SocketSourceBlock : IDisposable
    {
        private readonly List<ApplyClientSocket> _formatSockets;//readonly即该list只读
        public int CurrentSocketIndex = 0;
        public uint PackCount = 0;
        public string _Name;
        public ISocketProtocolsParser _protocolsParser;
        public bool IsRunning;


        public SocketSourceBlock(string name, IEnumerable<HostOptions> hostOptions)
        {
            _Name = name;
            _formatSockets = hostOptions.Select((p, i) =>
            {
                var socket = new ApplyClientSocket(new List<HostOptions>() { p });
                socket.DataArrived += (d) =>
                {
                    DataArrived?.Invoke(d);
                };
                socket.RunningStateArrived += (data, length) =>
                {

                };
                //socket.SocketDataArrived += (d) =>
                //{
                //    SocketDataArrived?.Invoke(d);
                //};
                return socket;

            }).ToList();
        }

        public Task Start(CancellationToken token)
        {
            //Console.WriteLine("{0}启动",_Name);
            return Task.WhenAll(_formatSockets.Select(p => p.Start(token)).ToList());
        }

        public void Dispose()
        {
            ((IDisposable)_formatSockets.ElementAt(CurrentSocketIndex)).Dispose();
        }

        //public Action<byte[]> SocketDataArrived;
        public Action<byte[]> DataArrived;
    }
}
