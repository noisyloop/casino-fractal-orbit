using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InfiniteOrbit.GameMath;

namespace InfiniteOrbit.Core
{
    /// <summary>One settled round as recorded in the game history log.</summary>
    public sealed class HistoryEntry
    {
        public long RoundNumber { get; set; }
        public DateTime Time { get; set; }
        public long TotalBet { get; set; }
        public long CreditsBefore { get; set; }
        public long CreditsAfter { get; set; }
        public int[] BaseStops { get; set; }
        public Symbol[] BaseGrid { get; set; }
        public long LineWin { get; set; }
        public long ScatterWin { get; set; }
        public long TotalWin { get; set; }
        public int FreeSpinsPlayed { get; set; }
        public int FreeSpinFinalMultiplier { get; set; }
        public long FreeSpinsWin { get; set; }
        public int HoldAndSpinOrbs { get; set; }
        public long HoldAndSpinWin { get; set; }
        public OrbPrizeKind[] Jackpots { get; set; } = new OrbPrizeKind[0];
        public BigWinTier BigWinTier { get; set; }
        public bool AutoPlay { get; set; }

        /// <summary>Full outcome object, kept so the history panel can show feature detail.</summary>
        public RoundResult Result { get; set; }

        public string FeatureSummary
        {
            get
            {
                var parts = new List<string>();
                if (HoldAndSpinOrbs > 0) parts.Add($"Orbit Lock {HoldAndSpinOrbs} orbs");
                if (Jackpots.Length > 0) parts.Add(string.Join("+", Jackpots.Select(j => j.ToString().ToUpperInvariant())));
                if (FreeSpinsPlayed > 0) parts.Add($"FS {FreeSpinsPlayed} spins x{FreeSpinFinalMultiplier}");
                return parts.Count == 0 ? "-" : string.Join(", ", parts);
            }
        }

        public static HistoryEntry FromRound(long roundNumber, DateTime time, long creditsBefore, long creditsAfter, RoundResult r, bool autoPlay)
        {
            return new HistoryEntry
            {
                RoundNumber = roundNumber,
                Time = time,
                TotalBet = r.TotalBet,
                CreditsBefore = creditsBefore,
                CreditsAfter = creditsAfter,
                BaseStops = (int[])r.BaseSpin.Stops.Clone(),
                BaseGrid = (Symbol[])r.BaseSpin.Grid.Clone(),
                LineWin = r.BaseSpin.LineCredits,
                ScatterWin = r.BaseSpin.ScatterCredits,
                TotalWin = r.TotalWin,
                FreeSpinsPlayed = r.FreeSpins?.Spins.Count ?? 0,
                FreeSpinFinalMultiplier = r.FreeSpins?.FinalMultiplier ?? 0,
                FreeSpinsWin = r.FreeSpins?.TotalCredits ?? 0,
                HoldAndSpinOrbs = r.HoldAndSpin?.OrbCount ?? 0,
                HoldAndSpinWin = r.HoldAndSpin?.TotalCredits ?? 0,
                Jackpots = r.HoldAndSpin?.JackpotsWon.ToArray() ?? new OrbPrizeKind[0],
                BigWinTier = r.BigWinTier,
                AutoPlay = autoPlay,
                Result = r,
            };
        }
    }

    /// <summary>Fixed-capacity ring buffer of settled rounds. Enumerates newest first.</summary>
    public sealed class GameHistory : IEnumerable<HistoryEntry>
    {
        public const int DefaultCapacity = 100;

        private readonly HistoryEntry[] _buffer;
        private int _next;

        public GameHistory(int capacity = DefaultCapacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _buffer = new HistoryEntry[capacity];
        }

        public int Capacity => _buffer.Length;
        public int Count { get; private set; }

        public event Action<HistoryEntry> Added;

        public void Add(HistoryEntry entry)
        {
            _buffer[_next] = entry ?? throw new ArgumentNullException(nameof(entry));
            _next = (_next + 1) % _buffer.Length;
            if (Count < _buffer.Length) Count++;
            Added?.Invoke(entry);
        }

        /// <summary>0 = most recent round.</summary>
        public HistoryEntry this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                int i = (_next - 1 - index + _buffer.Length * 2) % _buffer.Length;
                return _buffer[i];
            }
        }

        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _next = 0;
            Count = 0;
        }

        public IEnumerator<HistoryEntry> GetEnumerator()
        {
            for (int i = 0; i < Count; i++) yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
