using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Decompress;
using Decompress.Codec.L100.Const;
using Decompress.Interface.Block;
using KM.ConsoleDecode.Dispatch.SendDataPack;

namespace KM.ConsoleDecode.Send
{
    public class SendFileBlock : ProcessBlock<string>
    {
        protected override string _Name => "成像发送模块";
        public override Dictionary<string, object> Status => new Dictionary<string, object>()
        {
            {"通道名",PathName },
            { "发送速率", OutputMetric.Last.BytesSpeed(1000)},
            { "已发送数据量",DataLength.BytesSize() },
            {"连接状态",connect }
        };
        long DataLength = 0;
        string PathName = "";
        public string connect => socketTarget.IsConnected switch
        {
            true => "已连接",
            false => "未连接"
        };
        SocketTargetBlock socketTarget;
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public SendFileBlock(string pathname, int port)
        {

            PathName = pathname;
            socketTarget = new SocketTargetBlock(pathname, port);
            if (cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource = new CancellationTokenSource();
            }
            socketTarget.Start(cancellationTokenSource.Token);

        }
        protected override void Action(string p)
        {
            foreach (var item in p.Load())
            {
                DataLength += item.Length;
                OutputMetric.Now(item.Length);
                socketTarget.Enqueue(item);
            }
        }
        public void Equeue(byte[] data)
        {
            socketTarget.Enqueue(data);
        }
    }


}
