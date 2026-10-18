using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using InfiniteOrbit.GameMath;

namespace InfiniteOrbit.Simulator
{
    /// <summary>
    /// Monte Carlo simulator for Infinite Orbit.
    ///   dotnet run -c Release -- [--spins N] [--seed S] [--bet-level L] [--par docs/PAR.md] [--sequential]
    /// </summary>
    public static class Program
    {
        public const long DefaultSpins = 10_000_000;
        public const ulong DefaultSeed = 20261018UL;

        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            long spins = DefaultSpins;
            ulong seed = DefaultSeed;
            int betLevel = 0;
            string parPath = null;
            bool parallel = true;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--spins": spins = long.Parse(args[++i].Replace("_", "")); break;
                    case "--seed": seed = ulong.Parse(args[++i]); break;
                    case "--bet-level": betLevel = int.Parse(args[++i]); break;
                    case "--par": parPath = args[++i]; break;
                    case "--sequential": parallel = false; break;
                    case "-h":
                    case "--help":
                        Console.WriteLine("Usage: InfiniteOrbit.Simulator [--spins N] [--seed S] [--bet-level 0-4] [--par path.md] [--sequential]");
                        return 0;
                    default:
                        Console.Error.WriteLine($"Unknown argument '{args[i]}'. Use --help.");
                        return 1;
                }
            }

            var config = InfiniteOrbitMath.CreateConfig();
            Console.WriteLine($"{config.GameName} math {config.Version} | {spins:N0} rounds | seed {seed} | bet {config.TotalBetForLevel(betLevel)} credits | {(parallel ? Environment.ProcessorCount + " threads" : "1 thread")}");

            var sw = Stopwatch.StartNew();
            long lastReport = 0;
            var stats = Simulation.Run(config, seed, spins, betLevel, parallel, done =>
            {
                if (done - lastReport < spins / 10 && done != spins) return;
                lastReport = done;
                Console.Error.Write($"\r  {100.0 * done / spins,5:F1}%  ");
            });
            Console.Error.WriteLine();
            sw.Stop();

            var report = new Report(config, stats, seed, betLevel, sw.Elapsed);
            Console.WriteLine(report.ConsoleSummary());

            if (parPath != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(parPath)));
                File.WriteAllText(parPath, new ParSheetWriter(config, report).Write(), new UTF8Encoding(false));
                Console.WriteLine($"PAR sheet written to {parPath}");
            }
            return 0;
        }
    }
}
