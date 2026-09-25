using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Tabletop.Domain;

namespace Tabletop.Application
{
    /// <summary>A person in the world who can be challenged to a match (D-025). Pure data.</summary>
    public sealed class EncounterDefinition
    {
        public EncounterDefinition(string id, string name, string title, string aiProfile, string unitA, string unitB,
            IList<string> intro, string winLine, string loseLine, string rematchLine, bool isChampion = false, string prizeUnit = null)
        {
            Id = id;
            Name = name;
            Title = title;
            AiProfile = aiProfile;
            UnitA = unitA;
            UnitB = unitB;
            Intro = new ReadOnlyCollection<string>(new List<string>(intro));
            WinLine = winLine;
            LoseLine = loseLine;
            RematchLine = rematchLine;
            IsChampion = isChampion;
            PrizeUnit = prizeUnit;
        }

        public string Id { get; }
        public string Name { get; }
        public string Title { get; }
        public string AiProfile { get; }
        public string UnitA { get; }
        public string UnitB { get; }
        /// <summary>Lines spoken before the challenge choice.</summary>
        public IReadOnlyList<string> Intro { get; }
        /// <summary>Spoken after the player beats them.</summary>
        public string WinLine { get; }
        /// <summary>Spoken after the player loses to them.</summary>
        public string LoseLine { get; }
        /// <summary>Spoken when greeting a player who has already beaten them.</summary>
        public string RematchLine { get; }
        public bool IsChampion { get; }
        /// <summary>Unit the player wins the first time they beat this opponent (town champions), or null (D-027).</summary>
        public string PrizeUnit { get; }
        public ReelTier Tier => ReelTier.Copper;
    }

    /// <summary>
    /// Everyone the player can challenge (D-025, D-027). A town's villagers all play the starting pair
    /// (Striker + Caster) as practice; the town champion adds one new piece, which the player wins by beating him.
    /// </summary>
    public static class EncounterCatalog
    {
        public const string Gran = "gran_oddly";
        public const string Wren = "wren";
        public const string Mira = "mira";
        public const string Tobin = "tobin";
        public const string Halvey = "halvey";
        public const string Champion = "corvin";

        /// <summary>Pieces every player starts with.</summary>
        public static readonly IReadOnlyList<string> StartingUnits = new ReadOnlyCollection<string>(new[] { ReferenceContent.Striker, ReferenceContent.Caster });

        public static readonly IReadOnlyList<EncounterDefinition> All = new ReadOnlyCollection<EncounterDefinition>(new List<EncounterDefinition>
        {
            new EncounterDefinition(Gran, "Gran Oddly", "retired player", ControllerIds.AiLearner, ReferenceContent.Striker, ReferenceContent.Caster,
                new[]
                {
                    "Off to Brindlecross, are you? Then you'd best know your way around a Reels table.",
                    "Spin up to three times. Lock the reels you like. Gems feed your pieces, hammers build your wall.",
                    "Knock the other Crown down to nothing and the table is yours. Care for a practice round?",
                },
                "Ha! You've got the knack. Go on north, and mind the bridge.",
                "Everyone loses to Gran the first time. Try again whenever you like.",
                "Back for more lessons? My pieces never get tired."),
            new EncounterDefinition(Wren, "Wren", "wandering tinker", ControllerIds.AiLearner, ReferenceContent.Striker, ReferenceContent.Caster,
                new[]
                {
                    "Road's long, fire's warm. I carry a travel set everywhere.",
                    "Same old Striker and Caster as everyone round here. Want a game before you cross the stream?",
                },
                "Well played. The folk in Brindlecross will want to meet you.",
                "The road teaches patience. Sit down again anytime.",
                "The fire's still going if you want another round."),
            new EncounterDefinition(Mira, "Mira Tallow", "candlemaker", ControllerIds.AiStandard, ReferenceContent.Striker, ReferenceContent.Caster,
                new[]
                {
                    "New face! You look like someone who plays Reels.",
                    "Everyone in Brindlecross plays the Striker and the Caster. Only the Champion has anything fancier. Shall we?",
                },
                "Burned right through my wall. Nicely done!",
                "Walls win games, dear. Come back when you've figured out how to climb them.",
                "My wall's rebuilt. Another round?"),
            new EncounterDefinition(Tobin, "Tobin Reed", "fisher", ControllerIds.AiStandard, ReferenceContent.Striker, ReferenceContent.Caster,
                new[]
                {
                    "Fish aren't biting. Reels are, though.",
                    "Patience and a good lock, that's my game. Fancy a round?",
                },
                "Hooked me clean. You'd give the Champion a scare.",
                "Reeled you in! Plenty more rounds where that came from.",
                "Back for another cast?"),
            new EncounterDefinition(Halvey, "Sister Halvey", "healer", ControllerIds.AiStandard, ReferenceContent.Striker, ReferenceContent.Caster,
                new[]
                {
                    "Peace, traveller. I mend people by day and Crowns by night.",
                    "Corvin keeps a Ranger, you know. It shoots high, right over a short wall. Practise on me first?",
                },
                "Well fought. You may be ready for the hall.",
                "Rest a little. A clear head spins better reels.",
                "Shall we play again? I promise to go easy. I will not."),
            new EncounterDefinition(Champion, "Corvin Vale", "Champion of Brindlecross", ControllerIds.AiExpert, ReferenceContent.Ranger, ReferenceContent.Striker,
                new[]
                {
                    "So you've come to my table. Sit, sit.",
                    "I've heard about you from the villagers. Words are cheap; reels are not.",
                    "My Ranger shoots high, over your wall, and my Striker finishes the job. Beat me and the Ranger is yours. Shall we begin?",
                },
                "...Remarkable. The title of Champion is yours to carry, and so is my Ranger. Come back and defend it anytime.",
                "The table remembers every loss. Come back when you've learned from this one.",
                "The Champion's chair is always warm for a challenger.",
                isChampion: true, prizeUnit: ReferenceContent.Ranger),
        });

        public static EncounterDefinition Get(string id)
        {
            foreach (var e in All) if (e.Id == id) return e;
            throw new ArgumentException("Unknown encounter " + id);
        }

        /// <summary>The opponent who awards this unit, or null if no one in the world does yet.</summary>
        public static EncounterDefinition PrizeSource(string unitId)
        {
            foreach (var e in All) if (e.PrizeUnit == unitId) return e;
            return null;
        }
    }

    /// <summary>Outcome of the most recent challenge, handed back to the world scene.</summary>
    public sealed class EncounterOutcome
    {
        public EncounterOutcome(string encounterId, Winner winner, string unlockedUnit = null)
        {
            EncounterId = encounterId;
            Winner = winner;
            UnlockedUnit = unlockedUnit;
        }

        public string EncounterId { get; }
        public Winner Winner { get; }
        public bool PlayerWon => Winner == Winner.Player;
        /// <summary>A piece the player just won (first win against a champion), or null.</summary>
        public string UnlockedUnit { get; }
    }

    /// <summary>
    /// The player's journey state and the hand-off between the world and the match table. Engine-free; the world
    /// saves it by reading these properties and loads it through <see cref="Restore"/> (D-027).
    /// </summary>
    public static class GameFlow
    {
        private static readonly HashSet<string> _defeated = new HashSet<string>();
        private static readonly Dictionary<string, int> _losses = new Dictionary<string, int>();
        private static readonly List<string> _unlocked = new List<string>(EncounterCatalog.StartingUnits);

        /// <summary>Encounter the next match scene should play, or null for a free practice match.</summary>
        public static EncounterDefinition PendingEncounter { get; private set; }
        /// <summary>Result waiting for the world scene to show, or null.</summary>
        public static EncounterOutcome LastOutcome { get; private set; }
        /// <summary>Where to put the player when the world loads again.</summary>
        public static float ReturnX { get; private set; }
        public static float ReturnZ { get; private set; }
        public static bool HasReturnPoint { get; private set; }
        public static bool TitleShown { get; set; }
        /// <summary>Player preference: explore in first person instead of the overhead camera.</summary>
        public static bool FirstPersonView { get; set; }
        public static int Wins => _defeated.Count;

        public static bool HasDefeated(string encounterId) => _defeated.Contains(encounterId);
        public static IEnumerable<string> Defeated => _defeated;
        public static IReadOnlyDictionary<string, int> Losses => _losses;
        /// <summary>Pieces the player owns, in the order they were won.</summary>
        public static IReadOnlyList<string> UnlockedUnits => _unlocked;
        public static bool IsUnlocked(string unitId) => _unlocked.Contains(unitId);

        public static void BeginEncounter(EncounterDefinition encounter, float returnX, float returnZ)
        {
            PendingEncounter = encounter;
            ReturnX = returnX;
            ReturnZ = returnZ;
            HasReturnPoint = true;
            LastOutcome = null;
        }

        /// <summary>Called by the match scene when the player leaves a finished (or abandoned) encounter.</summary>
        public static void CompleteEncounter(Winner winner)
        {
            if (PendingEncounter == null) return;
            string unlocked = null;
            if (winner == Winner.Player)
            {
                _defeated.Add(PendingEncounter.Id);
                var prize = PendingEncounter.PrizeUnit;
                if (prize != null && !_unlocked.Contains(prize))
                {
                    _unlocked.Add(prize);
                    unlocked = prize;
                }
            }
            else if (winner == Winner.Opponent)
                _losses[PendingEncounter.Id] = (_losses.TryGetValue(PendingEncounter.Id, out var n) ? n : 0) + 1;
            LastOutcome = winner == Winner.None ? null : new EncounterOutcome(PendingEncounter.Id, winner, unlocked);
            PendingEncounter = null;
        }

        public static EncounterOutcome ConsumeOutcome()
        {
            var o = LastOutcome;
            LastOutcome = null;
            return o;
        }

        public static void ConsumeReturnPoint() => HasReturnPoint = false;

        /// <summary>Loads a saved journey. Unknown encounter or unit ids (from older saves) are ignored.</summary>
        public static void Restore(IEnumerable<string> defeated, IEnumerable<KeyValuePair<string, int>> losses, IEnumerable<string> unlocked,
            bool firstPerson, Func<string, bool> unitExists)
        {
            Reset();
            var known = new HashSet<string>();
            foreach (var e in EncounterCatalog.All) known.Add(e.Id);
            if (defeated != null) foreach (var d in defeated) if (known.Contains(d)) _defeated.Add(d);
            if (losses != null) foreach (var l in losses) if (known.Contains(l.Key) && l.Value > 0) _losses[l.Key] = l.Value;
            if (unlocked != null)
                foreach (var u in unlocked)
                    if (!_unlocked.Contains(u) && (unitExists == null || unitExists(u))) _unlocked.Add(u);
            FirstPersonView = firstPerson;
            TitleShown = true;
        }

        /// <summary>Clears everything (tests and a new journey).</summary>
        public static void Reset()
        {
            _defeated.Clear();
            _losses.Clear();
            _unlocked.Clear();
            _unlocked.AddRange(EncounterCatalog.StartingUnits);
            PendingEncounter = null;
            LastOutcome = null;
            HasReturnPoint = false;
            TitleShown = false;
            FirstPersonView = false;
        }
    }
}
