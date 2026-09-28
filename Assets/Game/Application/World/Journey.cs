using System.Collections.Generic;
using System.Collections.ObjectModel;
using Tabletop.Domain;

namespace Tabletop.Application
{
    public enum ItemKind
    {
        /// <summary>Used up by one match: a small head start (D-033).</summary>
        Charm,
        /// <summary>A better fifth wheel, kept forever; the player always brings their best.</summary>
        Wheel,
    }

    /// <summary>Something sold at a stall (D-033). Pure data.</summary>
    public sealed class ItemDefinition
    {
        public ItemDefinition(string id, string name, string description, int price, ItemKind kind, SideBoons boons = null, ReelTier wheel = ReelTier.Copper)
        {
            Id = id;
            Name = name;
            Description = description;
            Price = price;
            Kind = kind;
            Boons = boons ?? SideBoons.None;
            Wheel = wheel;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Price { get; }
        public ItemKind Kind { get; }
        /// <summary>Charms: the head start they give the player's side.</summary>
        public SideBoons Boons { get; }
        /// <summary>Wheels: the tier of fifth wheel.</summary>
        public ReelTier Wheel { get; }
    }

    /// <summary>
    /// Everything for sale (D-033). Charms are deliberately small: one per match, a head start rather than a
    /// different game. Wheels only improve the player's own fifth wheel; champions up the mountain bring better ones.
    /// </summary>
    public static class ItemCatalog
    {
        public const string SquareMedal = "charm_square_medal";
        public const string DiamondMedal = "charm_diamond_medal";
        public const string Mortar = "charm_mortar";
        public const string Tonic = "charm_tonic";
        public const string Flint = "charm_flint";
        public const string BronzeWheel = "wheel_bronze";
        public const string SilverWheel = "wheel_silver";
        public const string GoldWheel = "wheel_gold";
        public const string DiamondWheel = "wheel_diamond";
        /// <summary>The Grand Tournament's prize (D-038); never sold.</summary>
        public const string PlatinumWheel = "wheel_platinum";

        public static readonly IReadOnlyList<ItemDefinition> All = new ReadOnlyCollection<ItemDefinition>(new List<ItemDefinition>
        {
            new ItemDefinition(Tonic, "Crown Tonic", "Your Crown starts the match at 12 instead of 10.", 8, ItemKind.Charm, new SideBoons(crownBonus: 2)),
            new ItemDefinition(Mortar, "Bag of Mortar", "Start the match with a Bulwark of 2 already built.", 10, ItemKind.Charm, new SideBoons(barrier: 2)),
            new ItemDefinition(Flint, "Spark Flint", "Both of your figurines start with 2 energy.", 10, ItemKind.Charm, new SideBoons(energyA: 2, energyB: 2)),
            new ItemDefinition(SquareMedal, "Square Medal", "Your Square figurine (left) starts at Silver rank.", 14, ItemKind.Charm, new SideBoons(rankA: Rank.Silver)),
            new ItemDefinition(DiamondMedal, "Diamond Medal", "Your Diamond figurine (right) starts at Silver rank.", 14, ItemKind.Charm, new SideBoons(rankB: Rank.Silver)),
            new ItemDefinition(BronzeWheel, "Bronze Wheel", "A better fifth wheel: adds a double hammer. Yours to keep.", 35, ItemKind.Wheel, wheel: ReelTier.Bronze),
            new ItemDefinition(SilverWheel, "Silver Wheel", "A fifth wheel with more energy and an XP face. Yours to keep.", 90, ItemKind.Wheel, wheel: ReelTier.Silver),
            new ItemDefinition(GoldWheel, "Gold Wheel", "The finest wheel a stonemason can cut. Yours to keep.", 200, ItemKind.Wheel, wheel: ReelTier.Gold),
            new ItemDefinition(DiamondWheel, "Diamond Wheel", "Cast in bell-bronze and set with cut stones. Yours to keep.", 400, ItemKind.Wheel, wheel: ReelTier.Diamond),
            new ItemDefinition(PlatinumWheel, "Platinum Wheel", "The Grand Champion's own wheel. Won, never sold.", 0, ItemKind.Wheel, wheel: ReelTier.Platinum),
        });

        public static ItemDefinition Find(string id)
        {
            foreach (var i in All) if (i.Id == id) return i;
            return null;
        }
    }

    /// <summary>A stall and what it sells.</summary>
    public sealed class ShopDefinition
    {
        public ShopDefinition(string id, string keeper, string name, string greeting, params string[] items)
        {
            Id = id;
            Keeper = keeper;
            Name = name;
            Greeting = greeting;
            Items = new ReadOnlyCollection<string>(new List<string>(items));
        }

        public string Id { get; }
        /// <summary>Name of the person who runs it.</summary>
        public string Keeper { get; }
        public string Name { get; }
        public string Greeting { get; }
        public IReadOnlyList<string> Items { get; }
    }

    public static class ShopCatalog
    {
        public const string AdasStall = "ada";
        public const string Forge = "anvara";
        public const string LanternStall = "maudie";
        public const string Bellfoundry = "oskar";

        public static readonly IReadOnlyList<ShopDefinition> All = new ReadOnlyCollection<ShopDefinition>(new List<ShopDefinition>
        {
            new ShopDefinition(AdasStall, "Ada Pell", "Ada's Stall",
                "Charms for the table, dear, and a proper Bronze wheel. Every player in Brindlecross swears by something from my stall.",
                ItemCatalog.Tonic, ItemCatalog.Mortar, ItemCatalog.Flint, ItemCatalog.SquareMedal, ItemCatalog.BronzeWheel),
            new ShopDefinition(Forge, "Anvara", "Anvara's Forge",
                "Wheels cut from mountain stone and bound in iron. Nothing finer this side of the capital.",
                ItemCatalog.Tonic, ItemCatalog.Mortar, ItemCatalog.Flint, ItemCatalog.SquareMedal, ItemCatalog.DiamondMedal,
                ItemCatalog.BronzeWheel, ItemCatalog.SilverWheel, ItemCatalog.GoldWheel),
            new ShopDefinition(LanternStall, "Old Maudie", "Maudie's Lanterns",
                "Lanterns, lamp oil, and a few charms the lake folk swear by. Mind the wicks.",
                ItemCatalog.Tonic, ItemCatalog.Mortar, ItemCatalog.Flint, ItemCatalog.SquareMedal, ItemCatalog.DiamondMedal),
            new ShopDefinition(Bellfoundry, "Oskar Bell", "The Bellfoundry",
                "Bells, gears and the best wheels this side of Crownhold. The Diamond wheel rings true, every time.",
                ItemCatalog.Tonic, ItemCatalog.Mortar, ItemCatalog.Flint, ItemCatalog.SquareMedal, ItemCatalog.DiamondMedal,
                ItemCatalog.GoldWheel, ItemCatalog.DiamondWheel),
        });

        public static ShopDefinition Find(string id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            return null;
        }

        public static ShopDefinition ForKeeper(string keeper)
        {
            foreach (var s in All) if (s.Keeper == keeper) return s;
            return null;
        }
    }

    public enum ErrandStage
    {
        NotStarted = 0,
        /// <summary>Accepted: carrying the thing (delivery) or looking for it (fetch).</summary>
        Active = 1,
        /// <summary>Fetch errands: found it, now take it back.</summary>
        Found = 2,
        Done = 3,
    }

    /// <summary>
    /// A small job for coins (D-033). Delivery: take <see cref="ItemName"/> from the giver to the receiver, who pays.
    /// Fetch: find it at <see cref="PickupId"/> and bring it back to the giver, who pays.
    /// </summary>
    public sealed class ErrandDefinition
    {
        public ErrandDefinition(string id, string area, string title, string giver, string itemName, int reward, string[] offer,
            string reminder, string receiver = null, string deliverLine = null, string pickupId = null, string foundLine = null,
            string thanksLine = null, string requires = null)
        {
            Id = id;
            Area = area;
            Title = title;
            Giver = giver;
            ItemName = itemName;
            Reward = reward;
            Offer = new ReadOnlyCollection<string>(new List<string>(offer));
            Reminder = reminder;
            Receiver = receiver;
            DeliverLine = deliverLine;
            PickupId = pickupId;
            FoundLine = foundLine;
            ThanksLine = thanksLine;
            Requires = requires;
        }

        public string Id { get; }
        public string Area { get; }
        public string Title { get; }
        public string Giver { get; }
        public string ItemName { get; }
        public int Reward { get; }
        public IReadOnlyList<string> Offer { get; }
        /// <summary>What the giver says while the errand is under way (also the journal hint).</summary>
        public string Reminder { get; }
        public string Receiver { get; }
        public string DeliverLine { get; }
        public string PickupId { get; }
        public string FoundLine { get; }
        public string ThanksLine { get; }
        /// <summary>Another errand that must be done first, or null.</summary>
        public string Requires { get; }
        public bool IsDelivery => Receiver != null;
    }

    public static class ErrandCatalog
    {
        public static readonly IReadOnlyList<ErrandDefinition> All = new ReadOnlyCollection<ErrandDefinition>(new List<ErrandDefinition>
        {
            // ---- Hearthmoor
            new ErrandDefinition("hollis_loaf", Areas.Hearthmoor, "Bread for the tinker", "Hollis", "a warm loaf", 6,
                new[] { "Wren the tinker camps up the North Road and never comes down for bread. So the bread goes up to her.", "Would you take her a loaf? She pays better than I do." },
                "Wren's camp is just west of the North Road, before the stream.",
                receiver: "Wren", deliverLine: "Bread from Hollis? Still warm! Here, a few coins for your trouble."),
            new ErrandDefinition("kit_figurine", Areas.Hearthmoor, "Kit's lost Warrior", "Kit", "a tiny carved Warrior", 4,
                new[] { "I lost my Warrior figurine! I was playing by the barn and now it's gone.", "Can you find it? Please?" },
                "It was by the barn, on the east side of the village. I think.",
                pickupId: "kit_toy", foundLine: "A little carved Warrior, half buried in the straw by the barn.",
                thanksLine: "My Warrior! Here, it's all my savings. Don't tell Gran."),
            new ErrandDefinition("marta_specs", Areas.Hearthmoor, "Marta's spectacles", "Marta", "Marta's spectacles", 5,
                new[] { "I can't see a thing without my spectacles, and I've lost them somewhere between the mill and the well.", "Young eyes are better at this. Would you look?" },
                "Somewhere between the mill and the well, I'm sure of it.",
                pickupId: "marta_specs", foundLine: "A pair of round spectacles, glinting on the rim of the well.",
                thanksLine: "Oh, bless you. The world has edges again! Take these coins."),
            new ErrandDefinition("tam_letter", Areas.Hearthmoor, "A letter for Peg", "Old Tam", "a letter for Peg", 8,
                new[] { "My daughter Peg keeps the Champion's Hall up in Brindlecross. I've written her a letter.", "My knees won't carry me that far any more. Would you?" },
                "Peg stands by the door of the Champion's Hall, at the top of Brindlecross.",
                receiver: "Peg", deliverLine: "A letter from Dad? He never writes! Thank you. Here, take this."),

            // ---- The North Road
            new ErrandDefinition("wren_hammer", Areas.NorthRoad, "The tinker's hammer", "Wren", "Wren's hammer", 7,
                new[] { "I dropped my good hammer by the bridge when I was fixing a rail. Didn't notice till I got back to camp.", "Fetch it for me? My knees and that bank don't agree." },
                "By the bridge over the Willow Stream, on the south bank.",
                pickupId: "wren_hammer", foundLine: "A small tinker's hammer, lying in the reeds by the bridge.",
                thanksLine: "That's the one! A tinker without a hammer is just a traveller. Here."),
            new ErrandDefinition("wren_lantern", Areas.NorthRoad, "A mended lantern", "Wren", "a mended lantern", 6,
                new[] { "Bram the town crier sent his lantern down to me for mending. It's done.", "Would you carry it to Brindlecross for me? He'll pay you." },
                "Bram cries the news near the south gate of Brindlecross.",
                receiver: "Bram", deliverLine: "My lantern! Good as new. Hear ye, hear ye: the traveller gets paid!", requires: "wren_hammer"),

            // ---- Brindlecross
            new ErrandDefinition("bram_notice", Areas.Brindlecross, "The Champion's notice", "Bram", "a rolled notice", 8,
                new[] { "The Champion wants every village to know his table is open to all comers.", "Take this notice down to Gran Oddly in Hearthmoor. She'll pin it up." },
                "Gran Oddly sits by the bench in Hearthmoor, south down the North Road.",
                receiver: "Gran Oddly", deliverLine: "A notice from Corvin? That old show-off. I'll pin it up. Here's for your legs."),
            new ErrandDefinition("mira_wax", Areas.Brindlecross, "A crock of beeswax", "Mira Tallow", "a crock of beeswax", 8,
                new[] { "I'm out of beeswax, and the hives are behind my chandlery.", "The bees know me. They might not know you. Would you fetch me a crock anyway?" },
                "The hives are behind the chandlery, on the west side of the plaza.",
                pickupId: "mira_wax", foundLine: "A heavy clay crock of beeswax, waiting by the hives. The bees ignore you.",
                thanksLine: "Wonderful. Candles for a week. Here you are."),
            new ErrandDefinition("tobin_float", Areas.Brindlecross, "Tobin's lucky float", "Tobin Reed", "a painted cork float", 8,
                new[] { "My lucky float came off the line down at the Willow Stream.", "It'll have washed up by the bridge. I'd go, but the fish might start biting." },
                "The Willow Stream, by the bridge on the North Road.",
                pickupId: "tobin_float", foundLine: "A red-and-white cork float, caught in the reeds on the north bank.",
                thanksLine: "My float! Now they'll bite. Take these, you've earned them."),
            new ErrandDefinition("halvey_salve", Areas.Brindlecross, "Salve for Old Tam", "Sister Halvey", "a pot of salve", 10,
                new[] { "Old Tam in Hearthmoor has knees older than the road. I've made him a salve.", "Would you carry it down to him?" },
                "Old Tam lives in Hearthmoor, near the cottage on the west side.",
                receiver: "Old Tam", deliverLine: "From Sister Halvey? Ah, that's the stuff. My knees thank you, and so do I."),
            new ErrandDefinition("peg_invite", Areas.Brindlecross, "Greetings to the Outpost", "Peg", "a sealed invitation", 12,
                new[] { "The Champion wants to invite the stonemasons' master to play at the hall. Nobody's gone up the quarry path in a month.", "It starts at the east side of town and climbs. Take this to Foreman Bask?" },
                "Foreman Bask runs the Stonemasons' Outpost, up the quarry path east of Brindlecross.",
                receiver: "Foreman Bask", deliverLine: "An invitation from the lowlands. Dorran will laugh. Here, for the climb."),

            // ---- Stonemasons' Outpost
            new ErrandDefinition("bask_chisels", Areas.Outpost, "A roll of chisels", "Foreman Bask", "a roll of chisels", 12,
                new[] { "Somebody left a roll of good chisels down in the quarry pit.", "Fetch them before the rain rusts them." },
                "Down in the quarry pit, north of the outpost.",
                pickupId: "bask_chisels", foundLine: "A leather roll of chisels, tucked against the quarry wall.",
                thanksLine: "Good. Those are worth more than you are. Here's your share."),
            new ErrandDefinition("kettle_stew", Areas.Outpost, "Stew for the lookout", "Kettle", "a pot of stew", 10,
                new[] { "Rook's been on the lookout since dawn and hasn't eaten.", "Take him this stew? Mind, it's hot." },
                "Rook keeps the lookout at the east end of the outpost.",
                receiver: "Rook", deliverLine: "Stew! Kettle's a saint. Here, I've nothing else to spend it on up here."),
            new ErrandDefinition("anvara_ore", Areas.Outpost, "Fire ore for the forge", "Anvara", "a lump of fire ore", 15,
                new[] { "The best wheel-iron comes from the glowing fire ore in the quarry.", "Bring me a lump and I'll pay you in coin, not promises." },
                "Fire ore glows orange among the rocks in the quarry pit.",
                pickupId: "anvara_ore", foundLine: "A fist-sized lump of ore, still warm, veined with glowing orange.",
                thanksLine: "That's good ore. It'll make a fine wheel. Here."),

            // ---- Lanternmere (D-038)
            new ErrandDefinition("maudie_oil", Areas.Lanternmere, "A jar of lamp oil", "Old Maudie", "a jar of lamp oil", 14,
                new[] { "Finn borrowed my good jar of lamp oil for his tar pot and left it in the boathouse, the scoundrel.", "Fetch it back for me?" },
                "The boathouse is on the east shore, past Finn's boats.",
                pickupId: "maudie_oil", foundLine: "A heavy clay jar of lamp oil, smelling faintly of tar.",
                thanksLine: "My oil! A hundred lanterns thank you. Here."),
            new ErrandDefinition("pim_boat", Areas.Lanternmere, "Pim's toy boat", "Pim", "a little toy boat", 9,
                new[] { "My boat sailed away! The wind took it right across the water.", "It always ends up in the reeds on the far side, by the big rocks. Can you get it?" },
                "Across the lake, in the reeds on the west shore by the big rocks.",
                pickupId: "pim_boat", foundLine: "A little carved boat with a paper sail, beached in the reeds.",
                thanksLine: "My boat! You're the best. Here, it's all my fishing money."),
            new ErrandDefinition("finn_rope", Areas.Lanternmere, "Tarred rope for Tobin", "Finn Harrow", "a coil of tarred rope", 14,
                new[] { "Tobin Reed down in Brindlecross ordered a coil of my tarred rope for his nets.", "Carry it down to him? He's good for the coin." },
                "Tobin Reed fishes in Brindlecross, on the east side of the plaza.",
                receiver: "Tobin Reed", deliverLine: "Finn's rope! Best on the water. Here, take this for the walk."),

            // ---- Duskhollow (D-038)
            new ErrandDefinition("hob_axe", Areas.Duskhollow, "Hob's axe head", "Hob", "Hob's axe head", 16,
                new[] { "My axe head flew off the haft and into the woods. Somewhere up the slope, west of the hollow.", "I'd look, but the owls up there don't like me. Would you?" },
                "Up the wooded slope west of the hollow. Look for the fallen pines.",
                pickupId: "hob_axe", foundLine: "An axe head, bitten deep into a fallen pine.",
                thanksLine: "There she is! Back to work, then. Here's for your trouble."),
            new ErrandDefinition("agathe_caps", Areas.Duskhollow, "A basket of silvercaps", "Grey Agathe", "a basket of silvercaps", 15,
                new[] { "Silvercaps only grow under the great tree, and my knees won't bend that low any more.", "Pick me a basket? They're the ones that shine." },
                "Under the great tree on the north side of the hollow.",
                pickupId: "agathe_caps", foundLine: "A basket's worth of pale mushrooms that shine faintly, like moonlight.",
                thanksLine: "Beautiful. Don't ask what they're for. Here."),
            new ErrandDefinition("moth_tea", Areas.Duskhollow, "Tea for the infirmary", "Moth", "a packet of dreamleaf tea", 18,
                new[] { "Sister Halvey in Brindlecross can't get dreamleaf anywhere else. It helps her patients sleep.", "Would you take her a packet?" },
                "Sister Halvey runs the infirmary on the west side of Brindlecross.",
                receiver: "Sister Halvey", deliverLine: "Dreamleaf from Moth! My patients will sleep tonight. Bless you, and take this."),

            // ---- Ironbell (D-038)
            new ErrandDefinition("rusk_rope", Areas.Ironbell, "The lost bell rope", "Captain Rusk", "a coil of bell rope", 18,
                new[] { "THE WIND TOOK THE SPARE BELL ROPE!", "IT'LL BE OUT ON THE MOOR, BY THE OLD STANDING STONES SOUTH OF TOWN! FETCH IT, WOULD YOU?" },
                "Out on the moor south of Ironbell, by the old standing stones.",
                pickupId: "rusk_rope", foundLine: "A long coil of bell rope, snagged on a standing stone.",
                thanksLine: "THAT'S THE ONE! HERE! FOR YOUR TROUBLE!"),
            new ErrandDefinition("tilda_spring", Areas.Ironbell, "A runaway mainspring", "Tilda Brass", "a brass mainspring", 16,
                new[] { "A mainspring got away from me. They do that. It bounced off the wall and down the bell road.", "It's shiny. You'll see it. Would you?" },
                "Somewhere along the bell road, west of town, where it climbs the hill.",
                pickupId: "tilda_spring", foundLine: "A coiled brass spring, gleaming in the heather beside the road.",
                thanksLine: "Got it! Twelve turns per minute, exactly. Here, you've earned these."),
            new ErrandDefinition("oskar_ingot", Areas.Ironbell, "Bell-bronze for the forge", "Oskar Bell", "an ingot of bell-bronze", 22,
                new[] { "Anvara at the Stonemasons' Outpost wants bell-bronze for her wheels. Best bronze there is.", "The moor track runs north from here straight to the Outpost. Take her this ingot?" },
                "The moor track runs north from Ironbell to the Stonemasons' Outpost. Anvara keeps the forge there.",
                receiver: "Anvara", deliverLine: "Bell-bronze! Oskar keeps his word. Here, this is yours."),
        });

        public static ErrandDefinition Find(string id)
        {
            foreach (var e in All) if (e.Id == id) return e;
            return null;
        }

        /// <summary>Can this errand be offered now (not started, prerequisite done)?</summary>
        public static bool Offerable(ErrandDefinition e) =>
            GameFlow.ErrandState(e.Id) == ErrandStage.NotStarted && (e.Requires == null || GameFlow.ErrandState(e.Requires) == ErrandStage.Done);
    }
}
