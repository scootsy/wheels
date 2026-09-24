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
            IList<string> intro, string winLine, string loseLine, string rematchLine, bool isChampion = false)
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
        public ReelTier Tier => ReelTier.Copper;
    }

    /// <summary>Everyone the player can challenge in the first world slice. Player units stay Striker/Caster.</summary>
    public static class EncounterCatalog
    {
        public const string Gran = "gran_oddly";
        public const string Wren = "wren";
        public const string Mira = "mira";
        public const string Tobin = "tobin";
        public const string Halvey = "halvey";
        public const string Champion = "corvin";

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
            new EncounterDefinition(Wren, "Wren", "wandering tinker", ControllerIds.AiLearner, ReferenceContent.Ranger, ReferenceContent.Striker,
                new[]
                {
                    "Road's long, fire's warm. I carry a travel set everywhere.",
                    "My Ranger shoots high, over short walls. Want a game before you cross the stream?",
                },
                "Well played. The folk in Brindlecross will want to meet you.",
                "The road teaches patience. Sit down again anytime.",
                "The fire's still going if you want another round."),
            new EncounterDefinition(Mira, "Mira Tallow", "candlemaker", ControllerIds.AiStandard, ReferenceContent.Mason, ReferenceContent.Striker,
                new[]
                {
                    "New face! You look like someone who plays Reels.",
                    "I build walls all day, candles and bricks alike. My Mason stacks your wall down and mine up. Shall we?",
                },
                "Burned right through my wall. Nicely done!",
                "Walls win games, dear. Come back when you've figured out how to climb them.",
                "My wall's rebuilt. Another round?"),
            new EncounterDefinition(Tobin, "Tobin Reed", "fisher", ControllerIds.AiStandard, ReferenceContent.Ranger, ReferenceContent.Caster,
                new[]
                {
                    "Fish aren't biting. Reels are, though.",
                    "Patience and a high arc, that's my game. Fancy a round?",
                },
                "Hooked me clean. You'd give the Champion a scare.",
                "Reeled you in! Plenty more rounds where that came from.",
                "Back for another cast?"),
            new EncounterDefinition(Halvey, "Sister Halvey", "healer", ControllerIds.AiStandard, ReferenceContent.Mender, ReferenceContent.Striker,
                new[]
                {
                    "Peace, traveller. I mend people by day and Crowns by night.",
                    "My Mender keeps my Crown whole and my Striker charged. Will you test that?",
                },
                "Even my blessings couldn't save that Crown. Well fought.",
                "Rest a little. A clear head spins better reels.",
                "Shall we play again? I promise to go easy. I will not."),
            new EncounterDefinition(Champion, "Corvin Vale", "Champion of Brindlecross", ControllerIds.AiExpert, ReferenceContent.Shade, ReferenceContent.Caster,
                new[]
                {
                    "So you've come to my table. Sit, sit.",
                    "I've heard about you from the villagers. Words are cheap; reels are not.",
                    "My Shade strikes first and drains whatever you're charging. My Caster finishes the job. Shall we begin?",
                },
                "...Remarkable. The title of Champion is yours to carry. Come back and defend it anytime.",
                "The table remembers every loss. Come back when you've learned from this one.",
                "The Champion's chair is always warm for a challenger.",
                isChampion: true),
        });

        public static EncounterDefinition Get(string id)
        {
            foreach (var e in All) if (e.Id == id) return e;
            throw new ArgumentException("Unknown encounter " + id);
        }
    }

    /// <summary>Outcome of the most recent challenge, handed back to the world scene.</summary>
    public sealed class EncounterOutcome
    {
        public EncounterOutcome(string encounterId, Winner winner)
        {
            EncounterId = encounterId;
            Winner = winner;
        }

        public string EncounterId { get; }
        public Winner Winner { get; }
        public bool PlayerWon => Winner == Winner.Player;
    }

    /// <summary>
    /// In-memory hand-off between the world and the match table (no save data: closing the game resets it).
    /// </summary>
    public static class GameFlow
    {
        private static readonly HashSet<string> _defeated = new HashSet<string>();
        private static readonly Dictionary<string, int> _losses = new Dictionary<string, int>();

        /// <summary>Encounter the next match scene should play, or null for a free practice match.</summary>
        public static EncounterDefinition PendingEncounter { get; private set; }
        /// <summary>Result waiting for the world scene to show, or null.</summary>
        public static EncounterOutcome LastOutcome { get; private set; }
        /// <summary>Where to put the player when the world loads again (x, z, and whether inside the hall).</summary>
        public static float ReturnX { get; private set; }
        public static float ReturnZ { get; private set; }
        public static bool HasReturnPoint { get; private set; }
        public static bool TitleShown { get; set; }
        public static int Wins => _defeated.Count;

        public static bool HasDefeated(string encounterId) => _defeated.Contains(encounterId);
        public static IEnumerable<string> Defeated => _defeated;

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
            if (winner == Winner.Player) _defeated.Add(PendingEncounter.Id);
            else if (winner == Winner.Opponent)
                _losses[PendingEncounter.Id] = (_losses.TryGetValue(PendingEncounter.Id, out var n) ? n : 0) + 1;
            LastOutcome = winner == Winner.None ? null : new EncounterOutcome(PendingEncounter.Id, winner);
            PendingEncounter = null;
        }

        public static EncounterOutcome ConsumeOutcome()
        {
            var o = LastOutcome;
            LastOutcome = null;
            return o;
        }

        public static void ConsumeReturnPoint() => HasReturnPoint = false;

        /// <summary>Clears everything (tests and "quit to title").</summary>
        public static void Reset()
        {
            _defeated.Clear();
            _losses.Clear();
            PendingEncounter = null;
            LastOutcome = null;
            HasReturnPoint = false;
            TitleShown = false;
        }
    }
}
