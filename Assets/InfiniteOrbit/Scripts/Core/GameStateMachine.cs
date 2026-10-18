using System;
using System.Collections.Generic;

namespace InfiniteOrbit.Core
{
    public enum GameState
    {
        Idle,
        Spinning,
        WinPresentation,
        BonusIntro,
        FreeSpins,
        HoldAndSpin,
        BigWin,
        Error,
    }

    /// <summary>
    /// Explicit transition table for the cabinet flow. Any state may tilt into <see cref="GameState.Error"/>;
    /// leaving Error is only allowed back to the state that was interrupted.
    /// </summary>
    public sealed class GameStateMachine
    {
        private static readonly Dictionary<GameState, GameState[]> Allowed = new Dictionary<GameState, GameState[]>
        {
            { GameState.Idle, new[] { GameState.Spinning } },
            { GameState.Spinning, new[] { GameState.WinPresentation, GameState.BonusIntro, GameState.BigWin, GameState.Idle } },
            { GameState.BonusIntro, new[] { GameState.FreeSpins, GameState.HoldAndSpin } },
            { GameState.HoldAndSpin, new[] { GameState.BonusIntro, GameState.BigWin, GameState.WinPresentation, GameState.Idle } },
            { GameState.FreeSpins, new[] { GameState.BigWin, GameState.WinPresentation, GameState.Idle } },
            { GameState.BigWin, new[] { GameState.WinPresentation, GameState.Idle } },
            { GameState.WinPresentation, new[] { GameState.Idle } },
            { GameState.Error, new GameState[0] },
        };

        public GameState Current { get; private set; } = GameState.Idle;

        /// <summary>The state that was interrupted by the current tilt (only meaningful while in Error).</summary>
        public GameState ResumeState { get; private set; } = GameState.Idle;

        public event Action<GameState, GameState> Changed;

        public bool CanTransition(GameState to)
        {
            if (to == GameState.Error) return Current != GameState.Error;
            if (Current == GameState.Error) return to == ResumeState;
            return Array.IndexOf(Allowed[Current], to) >= 0;
        }

        public void TransitionTo(GameState to)
        {
            if (!CanTransition(to))
                throw new InvalidOperationException($"Illegal state transition {Current} -> {to}.");
            var from = Current;
            if (to == GameState.Error) ResumeState = from;
            Current = to;
            Changed?.Invoke(from, to);
        }
    }
}
