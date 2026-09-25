using System;
using System.Collections.Generic;
using Tabletop.Domain;

namespace Tabletop.Application
{
    /// <summary>MATCH_UX_SPEC Section 4 states. Pause and inspection are overlays, not states.</summary>
    public enum UxState
    {
        MatchSetup,
        UnitSelect,
        RoundReady,
        Spinning,
        SpinDecision,
        AiCommit,
        Reveal,
        Resolving,
        MatchResult,
    }

    /// <summary>Developer-mode match options. Normal play uses the defaults.</summary>
    public sealed class SetupOptions
    {
        public bool DeveloperMode;
        public long? FixedSeed;
        public ReelTier Tier = ReelTier.Copper;
        public string AiProfile = ControllerIds.AiStandard;
        public string OpponentA = ReferenceContent.Striker;
        public string OpponentB = ReferenceContent.Caster;
        /// <summary>Name of a forced scenario from <see cref="ScenarioLibrary"/>, or null.</summary>
        public string ScenarioName;
        /// <summary>Pieces the player owns when arriving from the world (D-027); null = default player-facing units.</summary>
        public System.Collections.Generic.IReadOnlyCollection<string> UnlockedUnits;
        /// <summary>World challenge being played (opponent, units, AI), or null for a free match.</summary>
        public EncounterDefinition Encounter;
    }

    /// <summary>
    /// The match controller boundary: validates the UX phase and dispatches domain commands.
    /// Holds no rules state of its own; everything authoritative lives in <see cref="Domain.Match"/>.
    /// </summary>
    public sealed class MatchSession
    {
        private readonly ContentCatalog _catalog;
        private readonly Func<long> _seedSource;
        private readonly List<MatchEvent> _presentationQueue = new List<MatchEvent>();
        private readonly List<CommandRejection> _diagnosticRejections = new List<CommandRejection>();
        private AiDecisionInput _aiInput;
        private int _commandIds;

        public MatchSession(ContentCatalog catalog, Func<long> seedSource)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
            Options = new SetupOptions();
            Selection = new UnitSelection(catalog);
            State = UxState.MatchSetup;
        }

        public ContentCatalog Catalog => _catalog;
        public SetupOptions Options { get; }
        public UnitSelection Selection { get; }
        public UxState State { get; private set; }
        public Match Match { get; private set; }
        public bool Paused { get; private set; }
        public string LastStatus { get; private set; } = "";
        public CommandRejection LastRejection { get; private set; }
        /// <summary>Events accepted by the simulation but not yet consumed by the presenter, in order.</summary>
        public IReadOnlyList<MatchEvent> PresentationQueue => _presentationQueue;
        public IReadOnlyList<CommandRejection> DiagnosticRejections => _diagnosticRejections;
        public string FatalError { get; private set; }

        public event Action<UxState> StateChanged;

        public SideSnapshot PlayerSide => Match?.Snapshot().Side(SideId.Player);

        /// <summary>Who the player is facing (encounter name, or the default practice opponent).</summary>
        public string OpponentName => Options.Encounter != null ? Options.Encounter.Name : "The Tinkerer";

        // ------------------------------------------------------------------ setup

        public IReadOnlyList<UnitDefinition> SelectableUnits(bool developer) => Selection.Available(developer);

        public void ContinueFromSetup()
        {
            if (!RequireState(UxState.MatchSetup)) return;
            Selection.DeveloperMode = Options.DeveloperMode;
            Selection.Unlocked = Options.UnlockedUnits;
            SetState(UxState.UnitSelect);
        }

        public void BackToSetup()
        {
            if (State != UxState.UnitSelect && State != UxState.MatchResult) { Reject(RejectionCode.WrongPhase, "Not available now."); return; }
            Match = null;
            _presentationQueue.Clear();
            SetState(UxState.MatchSetup);
        }

        public MatchConfig BuildConfig(long seed)
        {
            ScenarioSetup scenario = null;
            if (Options.DeveloperMode && !string.IsNullOrEmpty(Options.ScenarioName))
                scenario = ScenarioLibrary.Build(Options.ScenarioName, _catalog.ReelSet(Options.Tier));
            string oppA = Options.DeveloperMode ? Options.OpponentA : ReferenceContent.Striker;
            string oppB = Options.DeveloperMode ? Options.OpponentB : ReferenceContent.Caster;
            var tier = Options.DeveloperMode ? Options.Tier : ReelTier.Copper;
            var ai = Options.DeveloperMode ? Options.AiProfile : ControllerIds.AiStandard;
            var enc = Options.Encounter;
            if (enc != null)
            {
                oppA = enc.UnitA;
                oppB = enc.UnitB;
                tier = enc.Tier;
                ai = enc.AiProfile;
            }
            return new MatchConfig(RulesConstants.RulesVersion, seed,
                new SideConfig(ControllerIds.Human, tier, Selection.Slots[0], Selection.Slots[1]),
                new SideConfig(ai, tier, oppA, oppB), false, scenario);
        }

        /// <summary>Confirm unit selection and start the match.</summary>
        public bool ConfirmUnits()
        {
            if (!RequireState(UxState.UnitSelect)) return false;
            var selectionError = Selection.Validate();
            if (selectionError != null) { Reject(selectionError.Code, selectionError.Message); return false; }
            long seed = Options.DeveloperMode && Options.FixedSeed.HasValue ? Options.FixedSeed.Value : NewSeed();
            return StartMatch(seed);
        }

        private long NewSeed()
        {
            long s = _seedSource();
            if (s < 0) s = -s;
            return s % (MatchConfig.MaxSeed + 1);
        }

        private bool StartMatch(long seed)
        {
            var config = BuildConfig(seed);
            var match = Domain.Match.Start(config, _catalog, out var rejection);
            if (match == null)
            {
                Reject(rejection.Code, rejection.Message);
                return false;
            }
            Match = match;
            Paused = false;
            _presentationQueue.Clear();
            _diagnosticRejections.Clear();
            FatalError = null;
            EnterRoundReady();
            return true;
        }

        // ------------------------------------------------------------------ round flow

        private void EnterRoundReady()
        {
            // Capture the public round-start state the AI may evaluate (MATCH_UX_SPEC 4.3, 4.6).
            var side = Match.Config.Sides[1];
            _aiInput = AiDecisionInput.Capture(Match.Snapshot(), SideId.Opponent, _catalog, Match.ReelDefinitions(SideId.Opponent));
            LastStatus = "Round " + Match.Round + ": spin the reels.";
            SetState(UxState.RoundReady);
        }

        public bool CanSpin =>
            !Paused && Match != null && (State == UxState.RoundReady || State == UxState.SpinDecision)
            && PlayerSide.SpinsUsed < RulesConstants.SpinsPerRound && PlayerSide.LockedCount < ReelSetDefinition.ReelCount;

        public bool CanToggleLocks => !Paused && Match != null && State == UxState.SpinDecision;

        public bool RequestSpin()
        {
            if (Paused) { Reject(RejectionCode.WrongPhase, "Resume the game first."); return false; }
            if (State != UxState.RoundReady && State != UxState.SpinDecision) { Reject(RejectionCode.WrongPhase, "You cannot spin now."); return false; }
            var result = Dispatch(MatchCommand.Spin(SideId.Player));
            if (!result.Accepted) return false;
            Enqueue(result.Events);
            LastStatus = PlayerSide.Committed ? "Third spin: result is final." : "Spinning unlocked reels.";
            SetState(UxState.Spinning);
            return true;
        }

        public bool RequestToggleLock(int reelIndex)
        {
            if (Paused) { Reject(RejectionCode.WrongPhase, "Resume the game first."); return false; }
            if (State == UxState.RoundReady) { Reject(RejectionCode.FirstSpinRequired, "Spin all reels before locking."); return false; }
            if (State != UxState.SpinDecision) { Reject(RejectionCode.WrongPhase, "Reels cannot change now."); return false; }
            if (reelIndex < 0 || reelIndex >= ReelSetDefinition.ReelCount) { Reject(RejectionCode.InvalidReelIndex, "No such reel."); return false; }
            bool locked = PlayerSide.Reels[reelIndex].Locked;
            var result = Dispatch(MatchCommand.SetReelLock(SideId.Player, reelIndex, !locked));
            if (!result.Accepted) return false;
            Enqueue(result.Events);
            LastStatus = "Reel " + (reelIndex + 1) + (locked ? " unlocked." : " locked.");
            if (PlayerSide.LockedCount == ReelSetDefinition.ReelCount)
            {
                // Locking the fifth reel after spin 1 or 2 finalizes immediately (RULES_SPEC 4.3).
                var fin = Dispatch(MatchCommand.FinalizeSpin(SideId.Player));
                if (!fin.Accepted) return false;
                Enqueue(fin.Events);
                LastStatus = "All five reels locked: result is final.";
                SetState(UxState.AiCommit);
            }
            return true;
        }

        /// <summary>Presenter reports that the reels finished their authoritative spin presentation.</summary>
        public void NotifySpinPresented()
        {
            if (State != UxState.Spinning) return;
            SetState(PlayerSide.Committed ? UxState.AiCommit : UxState.SpinDecision);
            if (State == UxState.SpinDecision)
                LastStatus = "Lock reels to keep them, or spin the unlocked reels (" + PlayerSide.SpinsRemaining + " left).";
        }

        /// <summary>Runs the AI's whole spin phase through the legal command surface.</summary>
        public void RunAiCommit()
        {
            if (!RequireState(UxState.AiCommit)) return;
            var policy = AiProfiles.Create(Match.Config.Sides[1].ControllerId);
            try
            {
                AiTurnRunner.PlayTurn(policy, _aiInput, c =>
                {
                    var r = Dispatch(c);
                    if (r.Accepted) Enqueue(r.Events);
                    return r;
                });
            }
            catch (InvalidOperationException ex)
            {
                Fail("AI desynchronization: " + ex.Message);
                return;
            }
            LastStatus = "Both sides committed. Revealing.";
            SetState(UxState.Reveal);
        }

        /// <summary>Presenter reports the reveal finished; resolve the round.</summary>
        public void NotifyRevealPresented()
        {
            if (!RequireState(UxState.Reveal)) return;
            var result = Dispatch(MatchCommand.ResolveRound());
            if (!result.Accepted) { Fail("ResolveRound rejected: " + result.Rejection); return; }
            Enqueue(result.Events);
            LastStatus = "Resolving round " + Match.Round + ".";
            SetState(UxState.Resolving);
        }

        /// <summary>Presenter reports that every resolution event has been shown.</summary>
        public void NotifyResolutionPresented()
        {
            if (!RequireState(UxState.Resolving)) return;
            if (Match.Phase == MatchPhase.Ended)
            {
                LastStatus = ResultTitle + ".";
                SetState(UxState.MatchResult);
            }
            else
            {
                EnterRoundReady();
            }
        }

        // ------------------------------------------------------------------ presenter queue

        /// <summary>Removes and returns the next event for presentation, or null.</summary>
        public MatchEvent DequeueEvent()
        {
            if (_presentationQueue.Count == 0) return null;
            var e = _presentationQueue[0];
            _presentationQueue.RemoveAt(0);
            return e;
        }

        private void Enqueue(IReadOnlyList<MatchEvent> events)
        {
            foreach (var e in events) _presentationQueue.Add(e);
        }

        // ------------------------------------------------------------------ pause / result

        public void SetPaused(bool paused)
        {
            if (Match == null && paused) return;
            Paused = paused;
            LastStatus = paused ? "Paused." : "Resumed.";
        }

        public Winner Winner => Match?.Winner ?? Winner.None;

        public string ResultTitle
        {
            get
            {
                switch (Winner)
                {
                    case Winner.Player: return "Victory";
                    case Winner.Opponent: return "Defeat";
                    case Winner.Tie: return "Tie";
                    default: return "In progress";
                }
            }
        }

        public string ReplayText => Match == null ? "" : ReplayRecord.FromMatch(Match).Encode();

        /// <summary>REMATCH / RESTART: same setup with a new seed.</summary>
        public bool Rematch()
        {
            if (Match == null) { Reject(RejectionCode.WrongPhase, "No match to restart."); return false; }
            long seed = NewSeed();
            if (seed == Match.Seed) seed = (seed + 1) % (MatchConfig.MaxSeed + 1);
            return StartMatch(seed);
        }

        /// <summary>Developer-only REPLAY SAME SEED.</summary>
        public bool ReplaySameSeed()
        {
            if (Match == null || !Options.DeveloperMode) { Reject(RejectionCode.WrongPhase, "Developer mode only."); return false; }
            return StartMatch(Match.Seed);
        }

        public void ChangeUnits()
        {
            Match = null;
            Paused = false;
            _presentationQueue.Clear();
            SetState(UxState.UnitSelect);
        }

        public void ExitMatch()
        {
            Match = null;
            Paused = false;
            _presentationQueue.Clear();
            SetState(UxState.MatchSetup);
        }

        // ------------------------------------------------------------------ helpers

        private CommandResult Dispatch(MatchCommand command)
        {
            _commandIds++;
            var result = Match.Execute(command);
            if (!result.Accepted)
            {
                LastRejection = result.Rejection;
                LastStatus = result.Rejection.Message;
                _diagnosticRejections.Add(result.Rejection);
            }
            else
            {
                LastRejection = null;
            }
            return result;
        }

        private bool RequireState(UxState expected)
        {
            if (State == expected && !Paused) return true;
            Reject(RejectionCode.WrongPhase, Paused ? "Resume the game first." : "Not available now.");
            return false;
        }

        private void Reject(RejectionCode code, string message)
        {
            LastRejection = new CommandRejection(-1, code, message, Match?.Phase ?? MatchPhase.Setup);
            LastStatus = message;
            _diagnosticRejections.Add(LastRejection);
        }

        /// <summary>Unrecoverable desynchronization: stop, keep replay data, surface diagnostics.</summary>
        public void Fail(string message)
        {
            FatalError = message + "\nReplay: " + ReplayText;
            LastStatus = "Match stopped: " + message;
        }

        private void SetState(UxState next)
        {
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
