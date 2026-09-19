using MTW.Cache;
using MTW.Memory;
using MTW.Race;
using MTW.src.Dolphin;
using System.Diagnostics;

namespace MTW
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine(
                    "Usage: MTW.exe <DolphinPID>");

                return;
            }

            if (!int.TryParse(
                    args[0],
                    out int dolphinPid))
            {
                Console.WriteLine(
                    $"Invalid Dolphin PID: {args[0]}");

                return;
            }

            Process dolphinProcess;

            try
            {
                dolphinProcess =
                    Process.GetProcessById(dolphinPid);
            }
            catch
            {
                Console.WriteLine(
                    $"Could not find Dolphin process {dolphinPid}.");

                return;
            }

            Console.WriteLine(
                $"Connected to Dolphin process {dolphinProcess.Id}.");

            DolphinConnection dolphin = new DolphinConnection(dolphinProcess);

            PlayerPointerResolver resolver = new PlayerPointerResolver(dolphin);

            DynamicCacheWriter cacheWriter = new DynamicCacheWriter(Path.Combine( "games", "MTW", "DEV", "config", "cache.json"));

            CacheBuildController cacheBuildController = new CacheBuildController(dolphin, resolver, cacheWriter);
            cacheBuildController.Run(CancellationToken.None);


            dolphin.Dispose();
        }
    }
}