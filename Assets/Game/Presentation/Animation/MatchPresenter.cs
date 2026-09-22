using System;
using System.Collections.Generic;
using Tabletop.Application;
using Tabletop.Domain;

namespace Tabletop.Presentation
{
    /// <summary>Presenter-owned visual mirror of the board. Changed only by applying events; checked against snapshots.</summary>
    public sealed class VisualState
    {
        public int Round;
        public readonly int[] Crown = new int[2];
        public readonly int[] Barrier = new int[2];
        public readonly int[] SpinsUsed = new int[2];
        public readonly bool[] Committed = new bool[2];
        public readonly Rank[,] Rank = new Rank[2, 2];
        public readonly int[,] Xp = new int[2, 2];
        public readonly int[,] Energy = new int[2, 2];
        public readonly int[,] Face = new int[2, 5];
        public readonly bool[,] Locked = new bool[2, 5];

        public void CopyFrom(MatchSnapshot s)
        {
            Round = s.Round;
            for (int side = 0; side < 2; side++)
            {
                var ss = s.Sides[side];
                Crown[side] = ss.CrownHp;
                Barrier[side] = ss.Barrier;
                SpinsUsed[side] = ss.SpinsUsed;
                Committed[side] = ss.Committed;
                for (int u = 0; u < 2; u++)
                {
                    Rank[side, u] = ss.Units[u].Rank;
                    Xp[side, u] = ss.Units[u].Xp;
                    Energy[side, u] = ss.Units[u].Energy;
                }
                for (int r = 0; r < 5; r++)
                {
                    Face[side, r] = ss.Reels[r].FaceIndex;
                    Locked[side, r] = ss.Reels[r].Locked;
                }
            }
        }

        /// <summary>Returns null when this visual state matches the snapshot, otherwise the first difference.</summary>
        public string Diff(MatchSnapshot s)
        {
            if (Round != s.Round) return "round " + Round + " vs " + s.Round;
            for (int side = 0; side < 2; side++)
            {
                var ss = s.Sides[side];
                if (Crown[side] != ss.CrownHp) return "crown[" + side + "] " + Crown[side] + " vs " + ss.CrownHp;
                if (Barrier[side] != ss.Barrier) return "barrier[" + side + "] " + Barrier[side] + " vs " + ss.Barrier;
                if (SpinsUsed[side] != ss.SpinsUsed) return "spins[" + side + "]";
                if (Committed[side] != ss.Committed) return "committed[" + side + "]";
                for (int u = 0; u < 2; u++)
                {
                    if (Rank[side, u] != ss.Units[u].Rank) return "rank[" + side + "," + u + "]";
                    if (Xp[side, u] != ss.Units[u].Xp) return "xp[" + side + "," + u + "] " + Xp[side, u] + " vs " + ss.Units[u].Xp;
                    if (Energy[side, u] != ss.Units[u].Energy) return "energy[" + side + "," + u + "] " + Energy[side, u] + " vs " + ss.Units[u].Energy;
                }
                for (int r = 0; r < 5; r++)
                {
                    if (Face[side, r] != ss.Reels[r].FaceIndex) return "face[" + side + "," + r + "]";
                    if (Locked[side, r] != ss.Reels[r].Locked) return "lock[" + side + "," + r + "]";
                }
            }
            return null;
        }

        public void Apply(MatchEvent e)
        {
            switch (e.Type)
            {
                case MatchEventType.RoundStarted:
                    Round = e.Round;
                    for (int s = 0; s < 2; s++)
                    {
                        SpinsUsed[s] = 0;
                        Committed[s] = false;
                        for (int r = 0; r < 5; r++) Locked[s, r] = false;
                    }
                    break;
                case MatchEventType.ReelsSpun:
                    for (int r = 0; r < 5; r++) Face[e.Side, r] = e.Faces[r];
                    SpinsUsed[e.Side] = e.SpinNumber;
                    break;
                case MatchEventType.ReelLockChanged: Locked[e.Side, e.Slot] = e.Amount == 1; break;
                case MatchEventType.SpinFinalized: Committed[e.Side] = true; break;
                case MatchEventType.PanelXpGranted:
                case MatchEventType.ActionXpGranted:
                case MatchEventType.BombQueued:
                    Xp[e.Side, e.Slot] = e.After;
                    break;
                case MatchEventType.UnitRankedUp:
                    Rank[e.Side, e.Slot] = (Rank)e.After;
                    Xp[e.Side, e.Slot] = 0;
                    Energy[e.Side, e.Slot] += e.Amount;
                    break;
                case MatchEventType.BarrierBuilt: Barrier[e.Side] = e.After; break;
                case MatchEventType.EnergyGranted:
                case MatchEventType.UnitActivated:
                    Energy[e.Side, e.Slot] = e.After;
                    break;
                case MatchEventType.EnergyDelayed: Energy[e.TargetSide, e.TargetSlot] = e.After; break;
                case MatchEventType.CrownDamaged: Crown[e.TargetSide] = e.After; break;
                case MatchEventType.CrownHealed: Crown[e.Side] = e.After; break;
                case MatchEventType.BarrierDamaged: Barrier[e.TargetSide] = e.After; break;
            }
        }
    }

    public sealed class PresentationSettings
    {
        public bool ReducedMotion;
        public bool ScreenShake;
        /// <summary>Cosmetic "opponent choosing" wait. Tests and reduced-wait settings set 0.</summary>
        public float AiThinkSeconds = 0.45f;
        public float RevealSeconds = 0.6f;
        public float UiScale = 1f;
        /// <summary>Multiplier for every duration (tests use a small value).</summary>
        public float TimeScale = 1f;
        /// <summary>Developer check: compare visual and authoritative state after every event.</summary>
        public bool Reconcile = true;
    }

    /// <summary>
    /// Consumes simulation events strictly in order. Owns timing, never rules: it cannot issue
    /// gameplay commands or change match state. Frame rate and speed only change when things appear.
    /// </summary>
    public sealed class MatchPresenter
    {
        private readonly MatchSession _session;
        private readonly PresentationSettings _settings;
        private MatchEvent _current;
        private float _t;
        private float _duration;
        private bool _applied;
        private float _stateTimer;
        private float _speed = 1f;
        private readonly List<string> _log = new List<string>();
        private Match _match;

        public MatchPresenter(MatchSession session, PresentationSettings settings)
        {
            _session = session;
            _settings = settings;
            Visual = new VisualState();
        }

        public VisualState Visual { get; }
        public MatchEvent Current => _current;
        public float Progress => _duration <= 0 ? 1f : Math.Min(1f, _t / _duration);
        public bool Applied => _applied;
        public string Banner { get; private set; } = "";
        public IReadOnlyList<string> Log => _log;
        public bool Accelerated => _speed > 1f;
        public bool OpponentRevealed { get; private set; }
        /// <summary>Test hook: corrupt the visual state before the next reconciliation.</summary>
        public bool InjectDesyncForTest;
        public int EventsPresented { get; private set; }

        public event Action<MatchEvent> EventStarted;
        public event Action<MatchEvent> EventImpact;
        public event Action<MatchEvent> EventCompleted;

        /// <summary>Reset the visual mirror to the match's current authoritative state.</summary>
        public void Reset()
        {
            _current = null;
            _t = 0;
            _log.Clear();
            Banner = "";
            OpponentRevealed = false;
            _stateTimer = 0;
            _match = _session.Match;
            if (_match == null) return;
            // The visual mirror starts from the authoritative state just before the first event still
            // waiting in the queue (commands may already have been accepted this frame).
            var log = _match.EventLog;
            int index = Math.Max(0, log.Count - _session.PresentationQueue.Count - 1);
            Visual.CopyFrom(log[index].StateAfter);
        }

        public static float BaseDuration(MatchEvent e, bool opponentHidden)
        {
            if (opponentHidden && e.Side == 1 && (e.Type == MatchEventType.ReelsSpun || e.Type == MatchEventType.ReelLockChanged || e.Type == MatchEventType.SpinFinalized))
                return 0f;
            switch (e.Type)
            {
                case MatchEventType.MatchStarted: return 0f;
                case MatchEventType.RoundStarted: return 0.25f;
                case MatchEventType.ReelsSpun: return 0.6f;
                case MatchEventType.ReelLockChanged: return 0.15f;
                case MatchEventType.SpinFinalized: return 0.2f;
                case MatchEventType.PanelXpGranted:
                case MatchEventType.ActionXpGranted:
                case MatchEventType.EnergyGranted:
                case MatchEventType.BombQueued:
                    return 0.3f;
                case MatchEventType.UnitActivated: return 0.3f;
                case MatchEventType.EnergyDelayed: return 0.4f;
                case MatchEventType.ProjectileResolved: return 0.45f;
                case MatchEventType.CrownDamaged:
                case MatchEventType.BarrierDamaged:
                    return 0.35f;
                case MatchEventType.UnitRankedUp:
                case MatchEventType.BarrierBuilt:
                case MatchEventType.CrownHealed:
                case MatchEventType.BombLaunched:
                    return 0.5f;
                case MatchEventType.RoundEnded: return 0.45f;
                case MatchEventType.MatchEnded: return 0.4f;
            }
            return 0.3f;
        }

        /// <param name="accelerateHeldSeconds">How long the accelerate action has been held (0 when released).</param>
        public void Tick(float deltaTime, float accelerateHeldSeconds)
        {
            if (_session.Match != _match) Reset();
            if (_session.Paused || _session.Match == null || _session.FatalError != null) return;
            // Accelerate applies immediately; releasing returns to normal at the next event boundary.
            if (accelerateHeldSeconds >= 0.35f) _speed = 4f;
            float dt = deltaTime * _speed / Math.Max(0.0001f, _settings.TimeScale);

            int guard = 0;
            while (dt >= 0 && guard++ < 10000)
            {
                if (_current == null)
                {
                    if (accelerateHeldSeconds < 0.35f) _speed = 1f;
                    var next = _session.DequeueEvent();
                    if (next == null)
                    {
                        HandleIdle(dt);
                        return;
                    }
                    Begin(next);
                }
                float remaining = _duration - _t;
                if (dt < remaining)
                {
                    _t += dt;
                    if (!_applied && _t >= _duration * 0.5f) Impact();
                    return;
                }
                dt -= remaining;
                _t = _duration;
                if (!_applied) Impact();
                Complete();
                if (_session.FatalError != null) return;
            }
        }

        /// <summary>Developer SKIP ALL: present every queued event immediately, in order.</summary>
        public void SkipAll()
        {
            int guard = 0;
            while (guard++ < 100000)
            {
                if (_current == null)
                {
                    var next = _session.DequeueEvent();
                    if (next == null) break;
                    Begin(next);
                }
                if (!_applied) Impact();
                Complete();
                if (_session.FatalError != null) return;
            }
        }

        private void Begin(MatchEvent e)
        {
            _current = e;
            _t = 0;
            _applied = false;
            bool hidden = !OpponentRevealed;
            _duration = BaseDuration(e, hidden);
            if (e.Type == MatchEventType.RoundStarted) OpponentRevealed = false;
            if (!(hidden && e.Side == 1 && e.Stage == 0 && e.Type != MatchEventType.RoundStarted))
            {
                Banner = EventNarrator.Describe(e, _session.Match);
                if (_duration > 0 || e.Type == MatchEventType.MatchStarted) AddLog(Banner);
            }
            EventStarted?.Invoke(e);
        }

        private void Impact()
        {
            _applied = true;
            Visual.Apply(_current);
            EventImpact?.Invoke(_current);
        }

        private void Complete()
        {
            var e = _current;
            if (InjectDesyncForTest)
            {
                InjectDesyncForTest = false;
                Visual.Crown[0] += 1;
            }
            if (_settings.Reconcile)
            {
                var diff = Visual.Diff(e.StateAfter);
                if (diff != null)
                {
                    _current = null;
                    _session.Fail("Presenter/simulation desynchronization after event " + e.Sequence + " (" + e.Type + "): " + diff);
                    return;
                }
            }
            _current = null;
            EventsPresented++;
            EventCompleted?.Invoke(e);
        }

        private void HandleIdle(float dt)
        {
            switch (_session.State)
            {
                case UxState.Spinning:
                    _session.NotifySpinPresented();
                    break;
                case UxState.AiCommit:
                    _stateTimer += dt;
                    Banner = "Opponent choosing...";
                    if (_stateTimer >= _settings.AiThinkSeconds)
                    {
                        _stateTimer = 0;
                        _session.RunAiCommit();
                        // Opponent spin events are presented hidden (instantly) before the reveal.
                    }
                    break;
                case UxState.Reveal:
                    if (!OpponentRevealed)
                    {
                        OpponentRevealed = true;
                        _stateTimer = 0;
                        Banner = "REVEAL: both sides' final reels.";
                        AddLog(Banner);
                    }
                    _stateTimer += dt;
                    if (_stateTimer >= _settings.RevealSeconds)
                    {
                        _stateTimer = 0;
                        _session.NotifyRevealPresented();
                    }
                    break;
                case UxState.Resolving:
                    _session.NotifyResolutionPresented();
                    break;
                default:
                    _stateTimer = 0;
                    break;
            }
        }

        private void AddLog(string line)
        {
            _log.Add(line);
            if (_log.Count > 200) _log.RemoveAt(0);
        }
    }
}
