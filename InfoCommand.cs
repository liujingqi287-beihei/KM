using CommandDotNet;
using System;
using System.Reflection;

namespace KM.ConsoleDecode
{
    [Command("info")]
    public class InfoCommand
    {
        [Command("version")]
        public void Version()
        {
            Console.WriteLine($"当前版本号 {Assembly.GetEntryAssembly().GetName().Version}");
        }
    }
}
