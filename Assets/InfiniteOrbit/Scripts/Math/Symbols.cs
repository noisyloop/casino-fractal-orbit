using System.Collections.Generic;

namespace InfiniteOrbit.GameMath
{
    /// <summary>All symbols that can appear on the reels. Values are stable IDs used in logs and the PAR sheet.</summary>
    public enum Symbol : byte
    {
        Ten = 0,
        Jack = 1,
        Queen = 2,
        King = 3,
        Ace = 4,
        Comet = 5,
        Nebula = 6,
        Ringworld = 7,
        Pulsar = 8,
        Wild = 9,
        Scatter = 10,
        Orb = 11,
    }

    public enum SymbolKind
    {
        Low,
        High,
        Wild,
        Scatter,
        Bonus,
    }

    /// <summary>Static definition of a symbol: identity, display text and line pays.</summary>
    public sealed class SymbolInfo
    {
        public Symbol Id { get; }
        public string Code { get; }
        public string Name { get; }
        public SymbolKind Kind { get; }

        /// <summary>Line pay in line-bet multiples, indexed by match count (0..5). Scatter pays live in <see cref="GameConfig.ScatterPays"/>.</summary>
        public IReadOnlyList<int> Pays { get; }

        public string Description { get; }

        public SymbolInfo(Symbol id, string code, string name, SymbolKind kind, int[] pays, string description)
        {
            Id = id;
            Code = code;
            Name = name;
            Kind = kind;
            Pays = (int[])pays.Clone();
            Description = description;
        }

        public bool PaysOnLines
        {
            get
            {
                for (int i = 0; i < Pays.Count; i++)
                    if (Pays[i] > 0) return true;
                return false;
            }
        }
    }
}
