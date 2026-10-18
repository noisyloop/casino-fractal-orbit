using System.Collections.Generic;

namespace InfiniteOrbit.GameMath
{
    public enum SpinMode
    {
        Base,
        FreeSpin,
    }

    public enum OrbPrizeKind
    {
        Credits,
        Mini,
        Minor,
        Major,
        Grand,
    }

    public enum BigWinTier
    {
        None,
        Big,
        Mega,
        Epic,
    }

    /// <summary>The prize printed on an orb. Credit prizes are in line-bet multiples; jackpot prizes use the jackpot table.</summary>
    public readonly struct OrbPrize
    {
        public readonly OrbPrizeKind Kind;
        public readonly int LineBetMultiple;

        public OrbPrize(OrbPrizeKind kind, int lineBetMultiple)
        {
            Kind = kind;
            LineBetMultiple = lineBetMultiple;
        }

        public static OrbPrize Credits(int lineBetMultiple) => new OrbPrize(OrbPrizeKind.Credits, lineBetMultiple);
        public static OrbPrize Jackpot(OrbPrizeKind kind) => new OrbPrize(kind, 0);

        public bool IsJackpot => Kind != OrbPrizeKind.Credits;

        public override string ToString() => IsJackpot ? Kind.ToString().ToUpperInvariant() : $"{LineBetMultiple}xLB";
    }

    /// <summary>An orb sitting in a grid cell with its resolved credit value.</summary>
    public sealed class OrbCell
    {
        public int Cell { get; }
        public OrbPrize Prize { get; }
        public long Credits { get; }

        public OrbCell(int cell, OrbPrize prize, long credits)
        {
            Cell = cell;
            Prize = prize;
            Credits = credits;
        }
    }

    public sealed class LineWin
    {
        public int LineIndex { get; }
        public Symbol Symbol { get; }
        public int Count { get; }
        public long Credits { get; }
        /// <summary>Grid cells that form the win, left to right.</summary>
        public IReadOnlyList<int> Cells { get; }

        public LineWin(int lineIndex, Symbol symbol, int count, long credits, int[] cells)
        {
            LineIndex = lineIndex;
            Symbol = symbol;
            Count = count;
            Credits = credits;
            Cells = cells;
        }
    }

    /// <summary>One reel spin (base or free) fully evaluated. Credits already include any free-spin multiplier.</summary>
    public sealed class SpinResult
    {
        public SpinMode Mode { get; internal set; }
        public int[] Stops { get; internal set; }
        public Symbol[] Grid { get; internal set; }
        public List<LineWin> LineWins { get; } = new List<LineWin>();
        public long LineCredits { get; internal set; }
        public int ScatterCount { get; internal set; }
        public List<int> ScatterCells { get; } = new List<int>();
        public long ScatterCredits { get; internal set; }
        public List<OrbCell> Orbs { get; } = new List<OrbCell>();
        public int Multiplier { get; internal set; } = 1;

        /// <summary>(LineCredits + ScatterCredits) x Multiplier.</summary>
        public long TotalCredits { get; internal set; }

        public int FreeSpinsAwarded { get; internal set; }
        public bool TriggersFreeSpins => FreeSpinsAwarded > 0;
        public bool TriggersHoldAndSpin { get; internal set; }
    }

    public sealed class FreeSpinStep
    {
        public int Number { get; internal set; }
        public SpinResult Spin { get; internal set; }
        /// <summary>Multiplier applied to this spin's wins.</summary>
        public int Multiplier { get; internal set; }
        public int SpinsAdded { get; internal set; }
        public int SpinsRemaining { get; internal set; }
        public long CumulativeCredits { get; internal set; }
    }

    public sealed class FreeSpinsResult
    {
        public int InitialSpins { get; internal set; }
        public List<FreeSpinStep> Spins { get; } = new List<FreeSpinStep>();
        public int Retriggers { get; internal set; }
        public int FinalMultiplier { get; internal set; }
        public long TotalCredits { get; internal set; }
        public bool HitSpinCap { get; internal set; }
    }

    public sealed class RespinStep
    {
        public int Number { get; internal set; }
        public List<OrbCell> NewOrbs { get; } = new List<OrbCell>();
        public int RespinsRemaining { get; internal set; }
    }

    public sealed class HoldAndSpinResult
    {
        public List<OrbCell> InitialOrbs { get; } = new List<OrbCell>();
        public List<RespinStep> Respins { get; } = new List<RespinStep>();
        /// <summary>Final board, one entry per cell; null where no orb landed.</summary>
        public OrbCell[] Board { get; } = new OrbCell[GameConfig.Cells];
        public long OrbCredits { get; internal set; }
        public List<OrbPrizeKind> JackpotsWon { get; } = new List<OrbPrizeKind>();
        public long JackpotCredits { get; internal set; }
        public bool FullScreen { get; internal set; }
        public long TotalCredits { get; internal set; }

        public int OrbCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Board.Length; i++)
                    if (Board[i] != null) n++;
                return n;
            }
        }
    }

    /// <summary>
    /// Complete, pre-determined outcome of one paid game round. The presentation layer receives this object
    /// before any reel moves and only plays it back.
    /// </summary>
    public sealed class RoundResult
    {
        public long TotalBet { get; internal set; }
        public long LineBet { get; internal set; }
        public SpinResult BaseSpin { get; internal set; }
        public HoldAndSpinResult HoldAndSpin { get; internal set; }
        public FreeSpinsResult FreeSpins { get; internal set; }
        public long TotalWin { get; internal set; }
        public BigWinTier BigWinTier { get; internal set; }

        public double WinMultiple => TotalBet == 0 ? 0 : (double)TotalWin / TotalBet;
    }
}
