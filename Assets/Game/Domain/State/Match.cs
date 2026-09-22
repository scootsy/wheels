using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Tabletop.Domain
{
    public sealed class CommandResult
    {
        internal CommandResult(bool accepted, CommandRejection rejection, IReadOnlyList<MatchEvent> events)
        {
            Accepted = accepted;
            Rejection = rejection;
            Events = events;
        }

        public bool Accepted { get; }
        public CommandRejection Rejection { get; }
        public IReadOnlyList<MatchEvent> Events { get; }
    }

    /// <summary>
    /// The deterministic match simulation. The only route in is <see cref="Execute"/>; the only
    /// routes out are snapshots, events, rejections, and the accepted command log.
    /// </summary>
    public sealed partial class Match
    {
        private sealed class UnitState
        {
            public UnitDefinition Def;
            public Channel Channel;
            public Rank Rank;
            public int Xp;
            public int Energy;
            public EnergySource ReadySource;
            public bool ActedFromReels;
            public bool ActedFromPriest;
            public int Cost => Def.Stats(Rank).EnergyCost;
            public bool Ready => Energy >= Cost;
        }

        private sealed class ReelState
        {
            public ReelDefinition Def;
            public int FaceIndex = -1;
            public bool Locked;
            public ReelFace Face => FaceIndex < 0 ? null : Def.Faces[FaceIndex];
        }

        private sealed class SideState
        {
            public int Crown = RulesConstants.StartingCrown;
            public int Barrier;
            public ReelTier Tier;
            public int SpinsUsed;
            public bool Committed;
            public readonly ReelState[] Reels = new ReelState[ReelSetDefinition.ReelCount];
            public readonly UnitState[] Units = new UnitState[RulesConstants.UnitsPerSide];
        }

        private readonly SideState[] _sides = new SideState[2];
        private readonly List<MatchEvent> _eventLog = new List<MatchEvent>();
        private readonly List<MatchCommand> _accepted = new List<MatchCommand>();
        private List<MatchEvent> _pending;
        private int _round;
        private MatchPhase _phase;
        private Winner _winner;
        private int _commandCounter;
        private int _stage;

        private Match(MatchConfig config, ContentCatalog catalog)
        {
            Config = config;
            Catalog = catalog;
        }

        public MatchConfig Config { get; }
        public ContentCatalog Catalog { get; }
        public long Seed => Config.Seed;
        public int Round => _round;
        public MatchPhase Phase => _phase;
        public Winner Winner => _winner;
        public IReadOnlyList<MatchEvent> EventLog => _eventLog.AsReadOnly();
        public IReadOnlyList<MatchCommand> AcceptedCommands => _accepted.AsReadOnly();

        /// <summary>StartMatch(seed, sideConfigs). Returns null match plus rejection when config is invalid.</summary>
        public static Match Start(MatchConfig config, ContentCatalog catalog, out CommandRejection rejection)
        {
            rejection = config == null
                ? new CommandRejection(-1, RejectionCode.ConfigInvalid, "No configuration.", MatchPhase.Setup)
                : config.Validate(catalog);
            if (rejection != null) return null;
            var match = new Match(config, catalog);
            match.Initialize();
            return match;
        }

        /// <summary>Convenience for tests and tools; throws on invalid configuration.</summary>
        public static Match Start(MatchConfig config, ContentCatalog catalog)
        {
            var m = Start(config, catalog, out var rejection);
            if (m == null) throw new InvalidOperationException(rejection.ToString());
            return m;
        }

        private void Initialize()
        {
            _pending = new List<MatchEvent>();
            for (int s = 0; s < 2; s++)
            {
                var cfg = Config.Sides[s];
                var set = Catalog.ReelSet(cfg.ReelTier);
                var side = new SideState { Tier = cfg.ReelTier };
                for (int r = 0; r < ReelSetDefinition.ReelCount; r++) side.Reels[r] = new ReelState { Def = set.Reels[r] };
                for (int u = 0; u < RulesConstants.UnitsPerSide; u++)
                    side.Units[u] = new UnitState { Def = Catalog.Unit(cfg.UnitIds[u]), Channel = (Channel)u, Rank = Rank.Bronze };
                _sides[s] = side;
            }
            var sc = Config.Scenario;
            if (sc != null)
            {
                for (int s = 0; s < 2; s++)
                {
                    if (sc.Crown[s].HasValue) _sides[s].Crown = sc.Crown[s].Value;
                    if (sc.Barrier[s].HasValue) _sides[s].Barrier = sc.Barrier[s].Value;
                    for (int u = 0; u < 2; u++)
                    {
                        var start = sc.Units[s, u];
                        if (start == null) continue;
                        var unit = _sides[s].Units[u];
                        unit.Rank = start.Rank;
                        unit.Xp = start.Xp;
                        unit.Energy = System.Math.Min(start.Energy, unit.Cost);
                    }
                }
            }
            _round = 1;
            _phase = MatchPhase.Spinning;
            Emit(MatchEventType.MatchStarted, note: Catalog.ContentHash);
            Emit(MatchEventType.RoundStarted);
            _eventLog.AddRange(_pending);
            _pending = null;
        }

        public MatchSnapshot Snapshot()
        {
            var sides = new SideSnapshot[2];
            for (int s = 0; s < 2; s++)
            {
                var side = _sides[s];
                var reels = new ReelSnapshot[ReelSetDefinition.ReelCount];
                for (int r = 0; r < reels.Length; r++) reels[r] = new ReelSnapshot(side.Reels[r].FaceIndex, side.Reels[r].Locked);
                var units = new UnitSnapshot[RulesConstants.UnitsPerSide];
                for (int u = 0; u < units.Length; u++)
                {
                    var us = side.Units[u];
                    units[u] = new UnitSnapshot(us.Def.Id, us.Channel, us.Rank, us.Xp, us.Energy, us.Cost, us.ReadySource, us.ActedFromReels, us.ActedFromPriest);
                }
                sides[s] = new SideSnapshot(side.Crown, side.Barrier, side.Tier, side.SpinsUsed, side.Committed, reels, units);
            }
            return new MatchSnapshot(Config.RulesVersion, Config.Seed, _round, _phase, _winner, sides);
        }

        public string StateHash() => Snapshot().Hash();

        /// <summary>Hash of the ordered event log (used for replay verification).</summary>
        public string EventLogHash()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var e in _eventLog) sb.Append(e.Describe()).Append('\n');
            return StableHash.Fnv1a64Hex(sb.ToString());
        }

        public ReelFace FaceOf(SideId side, int reelIndex) => _sides[(int)side].Reels[reelIndex].Face;

        public IReadOnlyList<ReelDefinition> ReelDefinitions(SideId side)
        {
            var list = new List<ReelDefinition>();
            foreach (var r in _sides[(int)side].Reels) list.Add(r.Def);
            return list.AsReadOnly();
        }

        public UnitDefinition UnitDefinition(SideId side, int slot) => _sides[(int)side].Units[slot].Def;

        // ----------------------------------------------------------------- commands

        public CommandResult Execute(MatchCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            int id = ++_commandCounter;
            var rejection = Validate(command, id);
            if (rejection != null) return new CommandResult(false, rejection, new MatchEvent[0]);

            _pending = new List<MatchEvent>();
            switch (command.Type)
            {
                case CommandType.Spin: DoSpin(command.Side); break;
                case CommandType.SetReelLock: DoLock(command.Side, command.ReelIndex, command.Locked); break;
                case CommandType.FinalizeSpin: DoFinalize(command.Side); break;
                case CommandType.ResolveRound: DoResolve(); break;
            }
            _accepted.Add(command);
            var produced = _pending;
            _pending = null;
            _eventLog.AddRange(produced);
            return new CommandResult(true, null, new ReadOnlyCollection<MatchEvent>(produced));
        }

        private CommandRejection Validate(MatchCommand c, int id)
        {
            CommandRejection R(RejectionCode code, string msg) => new CommandRejection(id, code, msg, _phase);

            if (_phase == MatchPhase.Ended) return R(RejectionCode.MatchAlreadyEnded, "The match is over.");
            if (c.Type == CommandType.ResolveRound)
            {
                if (_phase != MatchPhase.AwaitingResolution) return R(RejectionCode.SidesNotCommitted, "Both sides must finish spinning first.");
                return null;
            }
            if (c.Side != SideId.Player && c.Side != SideId.Opponent) return R(RejectionCode.InvalidSide, "Unknown side.");
            if (_phase != MatchPhase.Spinning) return R(RejectionCode.WrongPhase, "Reels cannot change now.");
            var side = _sides[(int)c.Side];
            if (side.Committed) return R(RejectionCode.SideAlreadyCommitted, "This result is final.");

            switch (c.Type)
            {
                case CommandType.Spin:
                    if (side.SpinsUsed >= RulesConstants.SpinsPerRound) return R(RejectionCode.NoSpinsRemaining, "No spins remain; this result is final.");
                    if (side.SpinsUsed > 0 && LockedCount(side) == ReelSetDefinition.ReelCount) return R(RejectionCode.AllReelsLocked, "All reels are locked.");
                    return null;
                case CommandType.SetReelLock:
                    if (c.ReelIndex < 0 || c.ReelIndex >= ReelSetDefinition.ReelCount) return R(RejectionCode.InvalidReelIndex, "No such reel.");
                    if (side.SpinsUsed == 0) return R(RejectionCode.FirstSpinRequired, "Spin all reels before locking.");
                    if (side.Reels[c.ReelIndex].Locked == c.Locked) return R(RejectionCode.ReelAlreadyInState, c.Locked ? "That reel is already locked." : "That reel is already unlocked.");
                    return null;
                case CommandType.FinalizeSpin:
                    if (side.SpinsUsed == 0) return R(RejectionCode.FirstSpinRequired, "Spin all reels before locking.");
                    if (LockedCount(side) != ReelSetDefinition.ReelCount) return R(RejectionCode.NotAllReelsLocked, "Lock all five reels to finish early.");
                    return null;
            }
            return R(RejectionCode.WrongPhase, "Unknown command.");
        }

        private static int LockedCount(SideState side)
        {
            int n = 0;
            foreach (var r in side.Reels) if (r.Locked) n++;
            return n;
        }

        private void DoSpin(SideId sideId)
        {
            int s = (int)sideId;
            var side = _sides[s];
            side.SpinsUsed++;
            var faces = new int[ReelSetDefinition.ReelCount];
            var changed = new bool[ReelSetDefinition.ReelCount];
            int[] forced = null;
            if (Config.Scenario != null) Config.Scenario.TryGetForcedFaces(s, _round, side.SpinsUsed, out forced);
            for (int r = 0; r < ReelSetDefinition.ReelCount; r++)
            {
                var reel = side.Reels[r];
                // Spin 1 always spins all five reels (locks are impossible before it, and reset each round).
                if (!reel.Locked)
                {
                    reel.FaceIndex = forced != null
                        ? forced[r]
                        : ReelRandom.FaceIndex(Config.Seed, s, _round, side.SpinsUsed, r, reel.Def.Faces.Count);
                    changed[r] = true;
                }
                faces[r] = reel.FaceIndex;
            }
            Emit(MatchEventType.ReelsSpun, side: s, spinNumber: side.SpinsUsed, faces: faces, changed: changed);
            if (side.SpinsUsed >= RulesConstants.SpinsPerRound) Commit(s, "third spin");
        }

        private void DoLock(SideId sideId, int reelIndex, bool locked)
        {
            int s = (int)sideId;
            _sides[s].Reels[reelIndex].Locked = locked;
            Emit(MatchEventType.ReelLockChanged, side: s, slot: reelIndex, amount: locked ? 1 : 0, before: locked ? 0 : 1, after: locked ? 1 : 0,
                spinNumber: _sides[s].SpinsUsed);
        }

        private void DoFinalize(SideId sideId) => Commit((int)sideId, "all reels locked");

        private void Commit(int s, string reason)
        {
            _sides[s].Committed = true;
            if (_sides[0].Committed && _sides[1].Committed) _phase = MatchPhase.AwaitingResolution;
            var faces = new int[ReelSetDefinition.ReelCount];
            for (int r = 0; r < faces.Length; r++) faces[r] = _sides[s].Reels[r].FaceIndex;
            Emit(MatchEventType.SpinFinalized, side: s, spinNumber: _sides[s].SpinsUsed, faces: faces, note: reason);
        }

        // ----------------------------------------------------------------- events

        private void Emit(MatchEventType type, int side = -1, int slot = -1, int targetSide = -1, int targetSlot = -1,
            int amount = 0, int attempted = 0, int before = 0, int after = 0, int height = 0, int projectileIndex = -1,
            bool targetIsCrown = false, EnergySource source = EnergySource.None, int spinNumber = 0,
            int[] faces = null, bool[] changed = null, string note = null)
        {
            int seq = _eventLog.Count + _pending.Count + 1;
            if (attempted < amount) attempted = amount;
            _pending.Add(new MatchEvent(seq, _round, type, side, slot, targetSide, targetSlot, amount, attempted, before, after,
                height, projectileIndex, targetIsCrown, source, _stage, spinNumber, faces, changed, note, Snapshot()));
        }
    }
}
