# DEVLOG — Infinite Orbit

Short notes on the decisions behind the build, in the order the stages were done.

## Stage 1 — Math module

* **Outcome first, presentation second.** `SlotEngine.PlayRound()` returns a `RoundResult` holding every stop,
  line win, orb prize, respin and free spin. This mirrors cabinet practice (the RNG outcome is fixed and logged
  before animation) and makes the presentation impossible to "cheat" with: result setters are `internal`.
* **No Unity in the math.** `InfiniteOrbit.Math` uses `noEngineReferences`; the same files compile in a
  netstandard2.1 / C# 9 project, which is what Unity 6 supports, so tests and the simulator run anywhere.
* **RNG:** xoshiro256** seeded through SplitMix64, Lemire's unbiased range reduction. The math only needs
  `NextInt(max)`, so tests swap in `ScriptedRng` to force exact screens.
* **Fixed draw order** (stops → orb prizes → respins → free spins) is what makes seed replay possible; a golden-value
  test fails if it changes.
* **Reel strips** are physical strips (uniform stops) laid out from per-reel symbol counts by a seeded,
  deterministic builder. It keeps scatters ≥ 3 stops apart (max one scatter per reel window) and stacks orbs in
  threes so 6+ orb screens are reachable. Strips are printed in full in the PAR sheet.
* **Decisions confirmed with the product owner before tuning:** orb is an 11th, non-paying symbol; free-spin
  multiplier +1 per *winning* spin capped at ×10; orbs carry Mini/Minor/Major labels and a full screen awards
  the Grand; scatters pay 2/10/50× total bet; bets 25–500; hold-and-spin is base-game only.
* **Exact cross-checks.** Base-game line RTP is computed exactly by enumerating 12⁵ symbol combinations against
  per-reel marginals (valid because each line reads one cell per independent reel). Scatter/orb screen
  distributions are exact convolutions over all stops. Simulation agrees with both to ~0.01%.

## Stage 2 — Simulation and PAR

* Simulation splits work into fixed 100k-round chunks with RNG streams derived from (seed, chunk), so results are
  bit-identical on any thread count (tested). ~2–3M rounds/s on 4 cores.
* **Tuning path:** first pass was ~60% RTP with almost no line return; I raised wild density on reels 2–4 and the
  paytable, then shifted return from base lines into free spins (more FS wilds, bigger Pulsar 5-of-a-kind) to move
  volatility from medium (SD ≈ 6.3) toward moderate-high (SD ≈ 8.4). Final split: lines 46.6 / scatter 1.6 /
  free spins 30.4 / Orbit Lock 16.8 (jackpots 3.6).
* The PAR is generated from the live config so it can't drift from the code. It includes a 95% confidence interval
  because a 10M-spin RTP for this volatility is only good to about ±0.5%; the published sheet uses 100M spins.

## Stage 3 — State machine and session

* `GameStateMachine` is an explicit transition table; illegal transitions throw. I added a **HoldAndSpin** state
  next to FreeSpins (the brief listed FreeSpins only) so each feature has its own phase.
* `GameSession` turns a `RoundResult` into a queue of phases. The presentation calls `Advance()` when it finishes a
  phase — it can make a phase shorter (skip) but can't reorder or change it.
* **Meters:** bet debited at spin start, win credited at settle; `MetersReconcile()` checks
  credits = start + added + won − wagered after every round and tilts on mismatch.
* **Tilt:** any state → Error, remembering the interrupted state; clearing resumes exactly there with the same
  result object (tested), as a real cabinet resumes after a door-open.
* **Auto-play** stop conditions: rounds done, feature, single win ≥ X, loss limit, balance gain, insufficient credits,
  tilt. Bet changes are locked during play and auto-play.
* **History:** a 100-entry ring buffer of settled rounds (bet, stops, screen, wins, feature state, time).

## Stages 4–6 — Presentation, features, polish

* **World-space sprites instead of uGUI.** Cabinet UIs are usually 2D sprites; it also keeps the project
  dependency-free and let me type-check every Unity script in CI against UnityEngine reference assemblies
  (`Tools/InfiniteOrbit.UnityCompileCheck`) even without an editor.
* **Fully procedural content:** symbol art, button sprites and placeholder audio are generated at startup, so the
  repo has no binary assets. Each has a Resources override path for real art/audio.
* **Reels** are continuous strip positions that land on the result's stops with an ease-out-back bounce; opaque
  frame bars hide overflow (the classic cabinet mask). Scatter anticipation slows the later reels and loops a hum
  when 2+ scatters are showing.
* **Top screen** is a separate camera/world region: on a second monitor it targets display 2, otherwise it
  renders in the top strip of the main display. It shows jackpot meters and celebrations.
* **Big wins** escalate Big → Mega → Epic *while the meter counts*, each step raising audio, particle fountain rate
  and background intensity.
* **Fractal background:** a Julia-set shader whose constant orbits near the Mandelbrot boundary (the game's
  "infinite orbit"). Intensity/speed/hue follow the game state; tilt red-shifts and dims it. Iteration count and
  particle budgets follow a persisted Low/Medium/High quality setting.
* **Paytable/help** is generated from `GameConfig` — the same object the engine and PAR use — so displayed pays
  can't disagree with the math.

## What I'd do next

* Play-test in Unity 6 and tune layout/timings (written without an editor in this environment).
* Persist session/history to disk for power-loss recovery (the round model already supports replay).
* Split RTP by bet level / add a max-win cap if a jurisdiction requires one.
