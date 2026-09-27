using Tabletop.Application;
using Tabletop.Domain;
using UnityEngine;

namespace Tabletop.Presentation
{
    /// <summary>
    /// The table's side of the journey (D-033): the player brings their own wheel and a charm, each finished match
    /// settles its stake, and the table has its sounds. Nothing here changes a match already in progress.
    /// </summary>
    public sealed partial class MatchApp
    {
        [Tooltip("Music, ambience and sound effects (D-033). Optional: silent without it.")]
        [SerializeField] private SoundBank sounds;
        [Tooltip("Sculpted figurines for the pieces on the table (D-033). Optional.")]
        [SerializeField] private FigurineSet figurines;

        private Match _settledMatch;
        private MatchSettlement _lastSettlement;

        /// <summary>The line under the result (stake, coins, charm used).</summary>
        public string ResultBodyText => _resultBody != null ? _resultBody.text : "";

        /// <summary>What the last finished match changed in the journey (coins, favour, charm), or null.</summary>
        public MatchSettlement LastSettlement => _lastSettlement;

        private void StartJourneyAudio()
        {
            var audio = AudioDirector.Ensure(sounds);
            audio.PlayMusic("music/table");
            audio.PlayAmbience(null);
            Presenter.EventStarted += TableSounds.Started;
            Presenter.EventImpact += TableSounds.Impact;
        }

        /// <summary>A world challenge: the player's own best wheel, and the charm they chose, for the first match.</summary>
        private void ApplyJourneyOptions()
        {
            Session.Options.PlayerTier = GameFlow.PlayerWheel;
            Session.Options.PlayerBoons = GameFlow.PendingBoons;
        }

        /// <summary>Settles the finished match once (rematches settle again, the charm only once).</summary>
        private void SettleIfNeeded()
        {
            var m = Session.Match;
            if (!InEncounter || m == null || m == _settledMatch) return;
            _settledMatch = m;
            _lastSettlement = GameFlow.SettleMatch(Session.Winner);
            Session.Options.PlayerBoons = null; // the charm is used up
        }

        private string SettlementLine()
        {
            if (!InEncounter || _lastSettlement == null) return null;
            var enc = Session.Options.Encounter;
            string stake = GameFlow.PendingStakeMode == StakeMode.Friendly ? "Friendly game" : GameFlow.PendingStakeMode == StakeMode.Favour ? "Played for a favour" : "Stake " + enc.Stake;
            string what = _lastSettlement.Describe();
            return stake + (what.Length > 0 ? "   ·   " + what : "") + "   ·   Coins: " + GameFlow.Coins;
        }
    }

    /// <summary>Which sound each table event makes (D-033). Presentation only.</summary>
    public static class TableSounds
    {
        public static void Started(MatchEvent e)
        {
            switch (e.Type)
            {
                case MatchEventType.ReelsSpun: AudioDirector.Play("sfx/spin", 0.7f); break;
                case MatchEventType.ReelLockChanged: AudioDirector.Play("sfx/lock", 0.6f); break;
                case MatchEventType.SpinFinalized: AudioDirector.Play("sfx/lever", 0.7f); break;
                case MatchEventType.UnitActivated: AudioDirector.Play("sfx/activate", 0.55f); break;
                case MatchEventType.MatchEnded: AudioDirector.Play(e.StateAfter != null && e.StateAfter.Winner == Winner.Player ? "sfx/win" : "sfx/lose", 0.9f); break;
            }
        }

        public static void Impact(MatchEvent e)
        {
            switch (e.Type)
            {
                case MatchEventType.CrownDamaged: AudioDirector.Play("sfx/crown_hit", 0.8f); break;
                case MatchEventType.BarrierDamaged: AudioDirector.Play("sfx/bulwark_hit", 0.7f); break;
                case MatchEventType.BarrierBuilt: AudioDirector.Play("sfx/build", 0.6f); break;
                case MatchEventType.UnitRankedUp: AudioDirector.Play("sfx/rankup", 0.8f); break;
                case MatchEventType.CrownHealed: AudioDirector.Play("sfx/heal", 0.6f); break;
                case MatchEventType.BombLaunched: AudioDirector.Play("sfx/bomb", 0.8f); break;
                case MatchEventType.EnergyGranted: AudioDirector.Play("sfx/energy", 0.3f); break;
            }
        }
    }
}
