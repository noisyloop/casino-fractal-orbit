using System;
using System.Diagnostics;
using InfiniteOrbit.GameMath;

var config = InfiniteOrbitMath.CreateConfig();
long rounds = args.Length > 0 ? long.Parse(args[0]) : 1_000_000;
var sw = Stopwatch.StartNew();
var s = Simulation.Run(config, 20261018UL, rounds);
Console.WriteLine($"rounds {s.Rounds:N0} in {sw.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"RTP {s.Rtp:P3} ±{s.Rtp95HalfWidth:P3}  base lines {s.RtpOf(s.BaseLineWin):P2} (exact {Analytics.LineRtp(config, config.BaseReels):P2}) scatter {s.RtpOf(s.BaseScatterWin):P2} (exact {Analytics.ScatterRtp(config, config.BaseReels):P2}) FS {s.RtpOf(s.FreeSpinsWin):P2} HS {s.RtpOf(s.HoldAndSpinWin):P2}");
Console.WriteLine($"hit {s.HitFrequency:P2} SD {s.StdDev:F2} FS {SimulationStats.OneIn(s.FreeSpinTriggers, s.Rounds)} HS {SimulationStats.OneIn(s.HoldAndSpinTriggers, s.Rounds)} max {s.MaxWinMultiple:F1}x");
Console.WriteLine($"FS avg {(double)s.FreeSpinsWin / Math.Max(1, s.FreeSpinTriggers) / 25:F1}x spins {(double)s.FreeSpinsPlayed / Math.Max(1, s.FreeSpinTriggers):F1}; HS avg {(double)s.HoldAndSpinWin / Math.Max(1, s.HoldAndSpinTriggers) / 25:F1}x orbs {(double)s.HoldAndSpinFinalOrbs / Math.Max(1, s.HoldAndSpinTriggers):F2} grand {s.HoldAndSpinFullScreens}");
