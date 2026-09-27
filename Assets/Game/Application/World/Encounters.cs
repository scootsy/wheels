using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Tabletop.Domain;

namespace Tabletop.Application
{
    /// <summary>Where a person lives (drives the errand journal and the per-area wins in the HUD).</summary>
    public static class Areas
    {
        public const string Hearthmoor = "Hearthmoor";
        public const string NorthRoad = "The North Road";
        public const string Brindlecross = "Brindlecross";
        public const string Hall = "The Champion's Hall";
        public const string Outpost = "Stonemasons' Outpost";
        public const string QuarryPath = "The Quarry Path";
    }

    /// <summary>A person in the world who can be challenged to a match (D-025). Pure data.</summary>
    public sealed class EncounterDefinition
    {
        public EncounterDefinition(string id, string name, string title, string aiProfile, string unitA, string unitB,
            IList<string> intro, string winLine, string loseLine, string rematchLine, bool isChampion = false, string prizeUnit = null,
            int stake = 0, ReelTier tier = ReelTier.Copper, string area = Areas.Hearthmoor, string favourChore = null)
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
            Stake = stake;
            Tier = tier;
            Area = area;
            FavourChore = favourChore ?? "You spend the afternoon running errands for " + name + ".";
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
        /// <summary>Coins each side puts up (D-033). 0 = a friendly table that never costs anything.</summary>
        public int Stake { get; }
        /// <summary>The opponent's own fifth wheel (D-033); the player brings theirs.</summary>
        public ReelTier Tier { get; }
        public string Area { get; }
        /// <summary>What losing a favour match costs you: shown as a short scene instead of taking coins.</summary>
        public string FavourChore { get; }
        public bool Friendly => Stake == 0;
    }

    /// <summary>
    /// Everyone the player can challenge (D-025, D-027, D-033). The first two towns' villagers play the starting pair
    /// (Warrior + Mage); each town's champion adds one new figurine, which the player wins by beating them.
    /// </summary>
    public static class EncounterCatalog
    {
        public const string Gran = "gran_oddly";
        public const string Wren = "wren";
        public const string Mira = "mira";
        public const string Tobin = "tobin";
        public const string Halvey = "halvey";
        public const string Champion = "corvin";
        public const string Ilse = "ilse";
        public const string Kettle = "kettle";
        public const string Grist = "grist";
        public const string OutpostChampion = "dorran";

        /// <summary>Figurines every player starts with.</summary>
        public static readonly IReadOnlyList<string> StartingUnits = new ReadOnlyCollection<string>(new[] { ReferenceContent.Striker, ReferenceContent.Caster });

        public static readonly IReadOnlyList<EncounterDefinition> All = new ReadOnlyCollection<EncounterDefinition>(new List<EncounterDefinition>
        {
            new EncounterDefinition(Gran, "Gran Oddly", "retired player", ControllerIds.AiLearner, ReferenceContent.Striker, ReferenceContent.Caster,
                new[]
                {
                    "Off to Brindlecross, are you? Then you'd best know your way around a Wheels table.",
                    "Spin up to three times. Lock the wheels you like. Squares and diamonds feed your figurines, hammers build your Bulwark.",
                    "Knock the other Crown down to nothing and the table is yours. I never play for money, so it costs you nothing. Care for a round?",
                },
                "Ha! You've got the knack. Here, a few coins for the road. Mind the bridge.",
                "Everyone loses to Gran the first time. Try again whenever you like.",
                "Back for more lessons? My figurines never get tired, and I still pay a few coins to anyone who beats me.",
                stake: 0, tier: ReelTier.Copper, area: Areas.Hearthmoor),
            new EncounterDefinition(Wren, "Wren", "wandering tinker", ControllerIds.AiLearner, ReferenceContent.Striker, ReferenceContent.Caster,
                new[]
                {
                    "Road's long, fire's warm. I carry a travel set everywhere.",
                    "Same old Warrior and Mage as everyone round here. Five coins a side, before you cross the stream?",
                },
                "Well played. The folk in Brindlecross will want to meet you.",
                "The road teaches patience. Sit down again anytime.",
                "The fire's still going if you want another round.",
                stake: 5, tier: ReelTier.Copper, area: Areas.NorthRoad,
                favourChore: "You gather an armful of firewood for Wren's camp and help her mend a kettle."),
            new EncounterDefinition(Mira, "Mira Tallow", "candlemaker", ControllerIds.AiStandard, ReferenceContent.Striker, ReferenceContent.Caster,
                new[]
                {
                    "New face! You look like someone who plays Wheels.",
                    "Everyone in Brindlecross plays the Warrior and the Mage. Only the Champion has anything fancier. Eight coins a side?",
                },
                "Burned right through my Bulwark. Nicely done!",
                "Bulwarks win games, dear. Come back when you've figured out how to climb them.",
                "My Bulwark's rebuilt. Another round?",
                stake: 8, tier: ReelTier.Copper, area: Areas.Brindlecross,
                favourChore: "You spend an hour stirring hot wax and dipping candles for Mira."),
            new EncounterDefinition(Tobin, "Tobin Reed", "fisher", ControllerIds.AiStandard, ReferenceContent.Striker, ReferenceContent.Caster,
                new[]
                {
                    "Fish aren't biting. Wheels are, though.",
                    "I play a Bronze wheel, mind. Patience and a good lock, that's my game. Ten coins a side?",
                },
                "Hooked me clean. You'd give the Champion a scare.",
                "Reeled you in! Plenty more rounds where that came from.",
                "Back for another cast?",
                stake: 10, tier: ReelTier.Bronze, area: Areas.Brindlecross,
                favourChore: "You untangle and mend Tobin's nets until your fingers ache."),
            new EncounterDefinition(Halvey, "Sister Halvey", "healer", ControllerIds.AiStandard, ReferenceContent.Striker, ReferenceContent.Caster,
                new[]
                {
                    "Peace, traveller. I mend people by day and Crowns by night.",
                    "Corvin keeps an Archer, you know. It shoots high, right over a short Bulwark. Ten coins for the infirmary if I win?",
                },
                "Well fought. You may be ready for the hall.",
                "Rest a little. A clear head spins better wheels.",
                "Shall we play again? I promise to go easy. I will not.",
                stake: 10, tier: ReelTier.Bronze, area: Areas.Brindlecross,
                favourChore: "You grind herbs and roll bandages at the infirmary for the afternoon."),
            new EncounterDefinition(Champion, "Corvin Vale", "Champion of Brindlecross", ControllerIds.AiExpert, ReferenceContent.Ranger, ReferenceContent.Striker,
                new[]
                {
                    "So you've come to my table. Sit, sit.",
                    "I've heard about you from the villagers. Words are cheap; wheels are not. Twenty-five coins a side.",
                    "My Archer shoots high, over your Bulwark, and my Warrior finishes the job. Beat me and the Archer is yours. Shall we begin?",
                },
                "...Remarkable. The title of Champion is yours to carry, and so is my Archer. They say the stonemasons up the quarry path play for real stakes.",
                "The table remembers every loss. Come back when you've learned from this one.",
                "The Champion's chair is always warm for a challenger.",
                isChampion: true, prizeUnit: ReferenceContent.Ranger, stake: 25, tier: ReelTier.Bronze, area: Areas.Hall,
                favourChore: "You polish every trophy in the Champion's Hall while Corvin watches."),

            // Stonemasons' Outpost (D-033): harder tables, better wheels, bigger stakes.
            new EncounterDefinition(Ilse, "Ilse Marrow", "quarrymaster", ControllerIds.AiStandard, ReferenceContent.Striker, ReferenceContent.Ranger,
                new[]
                {
                    "A lowlander, up here? You'll be wanting a game, then. Everyone does after the climb.",
                    "Up here we play a Warrior and an Archer, on Silver wheels. Fifteen coins a side.",
                },
                "Hah! Cut clean along the grain. Well played.",
                "Stone doesn't hurry and neither should you. Again sometime.",
                "Back to test your edge on my table?",
                stake: 15, tier: ReelTier.Silver, area: Areas.Outpost,
                favourChore: "You haul chipped stone to the spoil heap until the quarry bell rings."),
            new EncounterDefinition(Kettle, "Kettle", "camp cook", ControllerIds.AiStandard, ReferenceContent.Caster, ReferenceContent.Ranger,
                new[]
                {
                    "Stew's on, table's set. I'll play you while it simmers.",
                    "My Mage and my Archer, twelve coins a side. Lose and you're peeling turnips.",
                },
                "Well, that's my evening's coin gone. Worth it for a good game.",
                "Pass me that knife, the turnips won't peel themselves.",
                "Stew's on again. Another round?",
                stake: 12, tier: ReelTier.Bronze, area: Areas.Outpost,
                favourChore: "You peel a mountain of turnips for Kettle's stew pot."),
            new EncounterDefinition(Grist, "Old Grist", "master carver", ControllerIds.AiExpert, ReferenceContent.Striker, ReferenceContent.Caster,
                new[]
                {
                    "Forty years carving Crowns out of granite. I know how they break.",
                    "Plain Warrior and Mage, but on Silver. Eighteen coins. Don't blink.",
                },
                "...Clean work. Very clean. You've a carver's eye.",
                "Every stone has a flaw. You found yours.",
                "Chisel's sharp. Table's ready.",
                stake: 18, tier: ReelTier.Silver, area: Areas.Outpost,
                favourChore: "You sweep the carving shed and sharpen every one of Grist's chisels."),
            new EncounterDefinition(OutpostChampion, "Master Dorran Hale", "Champion of the Outpost", ControllerIds.AiExpert, ReferenceContent.Mason, ReferenceContent.Striker,
                new[]
                {
                    "You climbed all this way to sit at my table. Good.",
                    "My Engineer builds while it fights; your Bulwark will crumble and mine will only grow. Forty coins a side.",
                    "Beat me and the Engineer is yours. Shall we?",
                },
                "Hm. The mountain doesn't bow to many. The Engineer is yours, Champion of the Outpost.",
                "Walls hold. Come back when you've learned to go over them.",
                "The table on the ledge is always ready.",
                isChampion: true, prizeUnit: ReferenceContent.Mason, stake: 40, tier: ReelTier.Silver, area: Areas.Outpost,
                favourChore: "You carry water up to the ledge for Dorran's masons until sundown."),
        });

        public static EncounterDefinition Get(string id)
        {
            foreach (var e in All) if (e.Id == id) return e;
            throw new ArgumentException("Unknown encounter " + id);
        }

        public static EncounterDefinition Find(string id)
        {
            foreach (var e in All) if (e.Id == id) return e;
            return null;
        }

        /// <summary>The opponent who awards this unit, or null if no one in the world does yet.</summary>
        public static EncounterDefinition PrizeSource(string unitId)
        {
            foreach (var e in All) if (e.PrizeUnit == unitId) return e;
            return null;
        }
    }

    /// <summary>How the player is paying to sit down (D-033).</summary>
    public enum StakeMode
    {
        /// <summary>A friendly table: nothing to lose, a small purse for winning.</summary>
        Friendly,
        /// <summary>Both put up the opponent's stake; the winner takes it.</summary>
        Coins,
        /// <summary>Can't cover the stake: win half of it, or lose and do them a favour instead of paying.</summary>
        Favour,
    }

    /// <summary>What one finished match changed in the journey (shown on the result screen).</summary>
    public sealed class MatchSettlement
    {
        public int CoinDelta;
        public bool FavourOwed;
        public string CharmUsed;
        public string UnlockedUnit;
        public Winner Winner;

        public string Describe()
        {
            var parts = new List<string>();
            if (CoinDelta > 0) parts.Add("+" + CoinDelta + " coins");
            else if (CoinDelta < 0) parts.Add(CoinDelta + " coins");
            if (FavourOwed) parts.Add("you owe a favour");
            if (CharmUsed != null && ItemCatalog.Find(CharmUsed) != null) parts.Add(ItemCatalog.Find(CharmUsed).Name + " used");
            return string.Join("   ·   ", parts);
        }
    }

    /// <summary>Outcome of the most recent challenge, handed back to the world scene.</summary>
    public sealed class EncounterOutcome
    {
        public EncounterOutcome(string encounterId, Winner winner, string unlockedUnit = null, int coinDelta = 0, bool favourOwed = false)
        {
            EncounterId = encounterId;
            Winner = winner;
            UnlockedUnit = unlockedUnit;
            CoinDelta = coinDelta;
            FavourOwed = favourOwed;
        }

        public string EncounterId { get; }
        public Winner Winner { get; }
        public bool PlayerWon => Winner == Winner.Player;
        /// <summary>A figurine the player just won (first win against a champion), or null.</summary>
        public string UnlockedUnit { get; }
        /// <summary>Net coins won or lost at this table (all matches played there this visit).</summary>
        public int CoinDelta { get; }
        /// <summary>The player lost a favour match: the world plays the chore.</summary>
        public bool FavourOwed { get; }
    }

    /// <summary>
    /// The player's journey state and the hand-off between the world and the match table. Engine-free; the world
    /// saves it by reading these properties and loads it through <see cref="Restore"/> (D-027, D-033).
    /// </summary>
    public static class GameFlow
    {
        public const int StartingCoins = 20;
        /// <summary>What a friendly table pays the winner.</summary>
        public const int FriendlyPurse = 3;

        private static readonly HashSet<string> _defeated = new HashSet<string>();
        private static readonly Dictionary<string, int> _losses = new Dictionary<string, int>();
        private static readonly List<string> _unlocked = new List<string>(EncounterCatalog.StartingUnits);
        private static readonly Dictionary<string, int> _items = new Dictionary<string, int>();
        private static readonly Dictionary<string, ErrandStage> _errands = new Dictionary<string, ErrandStage>();
        private static int _settledThisVisit;
        private static int _visitCoinDelta;
        private static bool _visitFavour;
        private static string _visitUnlocked;

        /// <summary>Encounter the next match scene should play, or null for a free practice match.</summary>
        public static EncounterDefinition PendingEncounter { get; private set; }
        public static StakeMode PendingStakeMode { get; private set; }
        /// <summary>Charm (item id) the player brought to the next match, or null. Used up when that match finishes.</summary>
        public static string PendingCharm { get; private set; }
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
        public static int Coins { get; private set; } = StartingCoins;

        public static bool HasDefeated(string encounterId) => _defeated.Contains(encounterId);
        public static IEnumerable<string> Defeated => _defeated;
        public static IReadOnlyDictionary<string, int> Losses => _losses;
        /// <summary>Figurines the player owns, in the order they were won.</summary>
        public static IReadOnlyList<string> UnlockedUnits => _unlocked;
        public static bool IsUnlocked(string unitId) => _unlocked.Contains(unitId);
        public static IReadOnlyDictionary<string, int> Items => _items;
        public static IReadOnlyDictionary<string, ErrandStage> Errands => _errands;

        // ------------------------------------------------------------------ coins and items

        public static bool CanAfford(int coins) => Coins >= coins;

        public static void AddCoins(int amount)
        {
            Coins = Math.Max(0, Coins + amount);
        }

        public static int ItemCount(string itemId) => _items.TryGetValue(itemId, out var n) ? n : 0;

        /// <summary>Buys one of an item. Returns null on success, or why not.</summary>
        public static string Buy(string itemId)
        {
            var item = ItemCatalog.Find(itemId);
            if (item == null) return "Unknown item.";
            if (item.Kind == ItemKind.Wheel && ItemCount(itemId) > 0) return "You already own it.";
            if (!CanAfford(item.Price)) return "Not enough coins.";
            Coins -= item.Price;
            _items[itemId] = ItemCount(itemId) + 1;
            return null;
        }

        /// <summary>The best fifth wheel the player owns (D-033). Every player starts with Copper.</summary>
        public static ReelTier PlayerWheel
        {
            get
            {
                var best = ReelTier.Copper;
                foreach (var item in ItemCatalog.All)
                    if (item.Kind == ItemKind.Wheel && ItemCount(item.Id) > 0 && item.Wheel > best) best = item.Wheel;
                return best;
            }
        }

        /// <summary>Charms the player can bring to a table (owned, one or more).</summary>
        public static List<ItemDefinition> OwnedCharms()
        {
            var list = new List<ItemDefinition>();
            foreach (var item in ItemCatalog.All) if (item.Kind == ItemKind.Charm && ItemCount(item.Id) > 0) list.Add(item);
            return list;
        }

        /// <summary>Head starts for the player's side in the next match (the pending charm), or none.</summary>
        public static SideBoons PendingBoons => PendingCharm != null && ItemCatalog.Find(PendingCharm) != null ? ItemCatalog.Find(PendingCharm).Boons : SideBoons.None;

        // ------------------------------------------------------------------ stakes

        /// <summary>How the player would sit down at this table right now (friendly, coins, or a favour if short).</summary>
        public static StakeMode StakeModeFor(EncounterDefinition e) => e == null || e.Friendly ? StakeMode.Friendly : CanAfford(e.Stake) ? StakeMode.Coins : StakeMode.Favour;

        /// <summary>Coins the player wins (positive) or loses (negative) for this result.</summary>
        public static int CoinsFor(EncounterDefinition e, StakeMode mode, Winner winner)
        {
            if (e == null || winner == Winner.None || winner == Winner.Tie) return 0;
            bool won = winner == Winner.Player;
            switch (mode)
            {
                case StakeMode.Friendly: return won ? FriendlyPurse : 0;
                case StakeMode.Coins: return won ? e.Stake : -e.Stake;
                default: return won ? (e.Stake + 1) / 2 : 0;
            }
        }

        // ------------------------------------------------------------------ encounters

        public static void BeginEncounter(EncounterDefinition encounter, float returnX, float returnZ, StakeMode? mode = null, string charm = null)
        {
            PendingEncounter = encounter;
            PendingStakeMode = mode ?? StakeModeFor(encounter);
            PendingCharm = charm != null && ItemCount(charm) > 0 ? charm : null;
            ReturnX = returnX;
            ReturnZ = returnZ;
            HasReturnPoint = true;
            LastOutcome = null;
            _settledThisVisit = 0;
            _visitCoinDelta = 0;
            _visitFavour = false;
            _visitUnlocked = null;
        }

        /// <summary>
        /// Settles one finished match at the pending table: coins, favours, charm use, wins and prizes (D-033).
        /// The table calls this once per finished match (rematches included).
        /// </summary>
        public static MatchSettlement SettleMatch(Winner winner)
        {
            var e = PendingEncounter;
            var s = new MatchSettlement { Winner = winner };
            if (e == null || winner == Winner.None) return s;
            _settledThisVisit++;
            if (PendingCharm != null)
            {
                s.CharmUsed = PendingCharm;
                _items[PendingCharm] = Math.Max(0, ItemCount(PendingCharm) - 1);
                if (_items[PendingCharm] == 0) _items.Remove(PendingCharm);
                PendingCharm = null; // one charm, one match
            }
            s.CoinDelta = CoinsFor(e, PendingStakeMode, winner);
            AddCoins(s.CoinDelta);
            _visitCoinDelta += s.CoinDelta;
            if (winner == Winner.Player)
            {
                _defeated.Add(e.Id);
                var prize = e.PrizeUnit;
                if (prize != null && !_unlocked.Contains(prize))
                {
                    _unlocked.Add(prize);
                    s.UnlockedUnit = prize;
                    _visitUnlocked = prize;
                }
            }
            else if (winner == Winner.Opponent)
            {
                _losses[e.Id] = (_losses.TryGetValue(e.Id, out var n) ? n : 0) + 1;
                if (PendingStakeMode == StakeMode.Favour) { s.FavourOwed = true; _visitFavour = true; }
            }
            // The next match at this table (a rematch) is staked afresh: fall back to a favour if the purse ran dry.
            if (PendingStakeMode == StakeMode.Coins && !CanAfford(e.Stake)) PendingStakeMode = StakeMode.Favour;
            return s;
        }

        /// <summary>Called by the match scene when the player leaves a finished (or abandoned) encounter.</summary>
        public static void CompleteEncounter(Winner winner)
        {
            if (PendingEncounter == null) return;
            // Callers that never settled a match (tests, older flows) settle the final result here.
            if (_settledThisVisit == 0 && winner != Winner.None) SettleMatch(winner);
            LastOutcome = _settledThisVisit == 0 ? null
                : new EncounterOutcome(PendingEncounter.Id, winner == Winner.None ? Winner.Tie : winner, _visitUnlocked, _visitCoinDelta, _visitFavour);
            PendingEncounter = null;
            PendingCharm = null;
        }

        public static EncounterOutcome ConsumeOutcome()
        {
            var o = LastOutcome;
            LastOutcome = null;
            return o;
        }

        public static void ConsumeReturnPoint() => HasReturnPoint = false;

        // ------------------------------------------------------------------ errands

        public static ErrandStage ErrandState(string errandId) => _errands.TryGetValue(errandId, out var s) ? s : ErrandStage.NotStarted;

        public static void SetErrand(string errandId, ErrandStage stage)
        {
            if (stage == ErrandStage.NotStarted) _errands.Remove(errandId);
            else _errands[errandId] = stage;
        }

        /// <summary>Finishes an errand and pays its reward (once).</summary>
        public static int CompleteErrand(ErrandDefinition errand)
        {
            if (ErrandState(errand.Id) == ErrandStage.Done) return 0;
            _errands[errand.Id] = ErrandStage.Done;
            AddCoins(errand.Reward);
            return errand.Reward;
        }

        // ------------------------------------------------------------------ save / reset

        /// <summary>Loads a saved journey. Unknown encounter, unit, item or errand ids (from older saves) are ignored.</summary>
        public static void Restore(IEnumerable<string> defeated, IEnumerable<KeyValuePair<string, int>> losses, IEnumerable<string> unlocked,
            bool firstPerson, Func<string, bool> unitExists, int? coins = null, IEnumerable<KeyValuePair<string, int>> items = null,
            IEnumerable<KeyValuePair<string, int>> errands = null)
        {
            Reset();
            var known = new HashSet<string>();
            foreach (var e in EncounterCatalog.All) known.Add(e.Id);
            if (defeated != null) foreach (var d in defeated) if (known.Contains(d)) _defeated.Add(d);
            if (losses != null) foreach (var l in losses) if (known.Contains(l.Key) && l.Value > 0) _losses[l.Key] = l.Value;
            if (unlocked != null)
                foreach (var u in unlocked)
                    if (!_unlocked.Contains(u) && (unitExists == null || unitExists(u))) _unlocked.Add(u);
            if (coins.HasValue) Coins = Math.Max(0, coins.Value);
            if (items != null) foreach (var i in items) if (ItemCatalog.Find(i.Key) != null && i.Value > 0) _items[i.Key] = i.Value;
            if (errands != null)
                foreach (var e in errands)
                    if (ErrandCatalog.Find(e.Key) != null && e.Value > 0 && e.Value <= (int)ErrandStage.Done) _errands[e.Key] = (ErrandStage)e.Value;
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
            _items.Clear();
            _errands.Clear();
            Coins = StartingCoins;
            PendingEncounter = null;
            PendingCharm = null;
            PendingStakeMode = StakeMode.Friendly;
            LastOutcome = null;
            HasReturnPoint = false;
            TitleShown = false;
            FirstPersonView = false;
            _settledThisVisit = 0;
            _visitCoinDelta = 0;
            _visitFavour = false;
            _visitUnlocked = null;
        }
    }
}
