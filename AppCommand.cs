using CommandDotNet;
using KM.ConsoleDecode.Command;
using KM.ConsoleDecode.Decode;
using KM.ConsoleDecode.Dispatch;
using KM.ConsoleDecode.Send;

namespace KM.ConsoleDecode
{
    public class AppCommand
    {
        [Subcommand]
        public TestCommand Test { get; set; }
        [Subcommand]
        public DispatchCommand Dispatch { get; set; }
        [Subcommand]
        public InfoCommand Info { get; set; }
        [Subcommand]
        public SendCommand Send { get; set; }
        [Subcommand]
        public NewDecodeCommand NewDecode { get; set; }
        [Subcommand]
        public KMDecodeWindowsCommand KMDecodeWindows { get; set; }
        [Subcommand]
        public XDMATestCommand XDMATest { get; set; }
    }
}
