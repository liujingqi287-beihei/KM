using Decompress.Codec.L100.Const;
using Decompress.Interface.Block;
using System;

namespace KM.ConsoleDecode.Dispatch
{
    public class DispatchBlock : ProcessBlock<DataPack>
    {
        private bool[,] PathReady;
        private int NeedPath { get; set; }
        private int Path = 0;
        private int PathOut;
        private bool HasGetPath = false;
        public DispatchBlock(int needpath)
        {
            NeedPath = needpath;
            PathReady = new bool[NeedPath, 20 / NeedPath];
        }
        //private UnpackSyncHead unpackSyncHead = new UnpackSyncHead();
        protected override void Action(DataPack p)
        {
            if (p.Type == DataPackType.SyncHead)
            {
                HasGetPath = false;
                //Path = unpackSyncHead.探测器识别码(p.Data);
                for (int j = 0; j < 20 / NeedPath; j++)
                {
                    for (int i = 0; i < NeedPath; i++)
                    {
                        if (PathReady[i, j])
                        {
                            continue;
                        }
                        PathOut = i;
                        PathReady[i, j] = true;
                        HasGetPath = true;
                        break;
                    }
                    if (HasGetPath)
                    {
                        break;
                    }
                }
            }

            PackArrived?.Invoke(p, PathOut);
        }
        public Action<DataPack, int> PackArrived { get; set; }
    }
}
