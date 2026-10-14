
using CommandDotNet;
using Serilog;
using System;
using Spectre.Console;
using Decompress;
using System.IO;
using KM.ConsoleDecode;

namespace N01.ConsoleDecode
{
    class Program
    {
        private static long buildcount = 20250315035937;
        static int Main(string[] args)
        {


         

            if (args.Length == 0)
            {

                AnsiConsole.Render(new FigletText("DECODE TEST"));
                Console.WriteLine("编译计数:" + buildcount);
                AnsiConsole.MarkupLine("[green]你好！[/]欢迎使用[blue]HUST解码测试工具[/]");
                AssemblyHelper.PrintAssembiesVersion();
            }


            string home = (string)Environment.GetEnvironmentVariable("HOME");
            string path = string.IsNullOrEmpty(home)
            ? Path.Combine("logs", "service")
            : Path.Combine(home, "logs", "service");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var builder = new LoggerConfiguration().MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.WithProperty("app name", "decompress")
            .WriteTo.File(Path.Combine(path, "log.txt"), rollingInterval: RollingInterval.Hour, outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Name} {Status} {Message}{NewLine}{Exception}", retainedFileCountLimit: 24 * 90);
            //.WriteTo.Console(outputTemplate:
            //   "[{Timestamp:HH:mm:ss} {Level:u3}] {Name} {Status} {Message}{NewLine}{Exception}");
            Log.Logger = builder.CreateLogger();


            return new AppRunner<AppCommand>().Configure(c=> {
                c.AppSettings.Help.UsageAppName = "kmcodec";
            }).Run(args);

        }
    }
}
