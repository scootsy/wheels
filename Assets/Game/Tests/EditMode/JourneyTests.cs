using System.Linq;
using NUnit.Framework;
using Tabletop.Application;
using Tabletop.Domain;
using Tabletop.World;
using UnityEngine;

namespace Tabletop.Tests.EditMode
{
    /// <summary>Coins, stakes, favours, charms, wheels, errands and the land (D-033).</summary>
    public class JourneyTests
    {
        [SetUp]
        public void Reset() => GameFlow.Reset();

        private static EncounterDefinition Mira => EncounterCatalog.Get(EncounterCatalog.Mira);

        // ------------------------------------------------------------------ stakes

        [Test]
        public void Coins_WinTakesTheStake_LossPaysIt()
        {
            Assert.AreEqual(GameFlow.StartingCoins, GameFlow.Coins);
            GameFlow.BeginEncounter(Mira, 0, 0);
            Assert.AreEqual(StakeMode.Coins, GameFlow.PendingStakeMode);
            var s = GameFlow.SettleMatch(Winner.Player);
            Assert.AreEqual(Mira.Stake, s.CoinDelta);
            Assert.AreEqual(GameFlow.StartingCoins + Mira.Stake, GameFlow.Coins);
            s = GameFlow.SettleMatch(Winner.Opponent); // a rematch at the same table
            Assert.AreEqual(-Mira.Stake, s.CoinDelta);
            Assert.AreEqual(GameFlow.StartingCoins, GameFlow.Coins);
            GameFlow.CompleteEncounter(Winner.Opponent);
            var o = GameFlow.ConsumeOutcome();
            Assert.AreEqual(0, o.CoinDelta, "net over the visit");
            Assert.IsTrue(GameFlow.HasDefeated(EncounterCatalog.Mira), "a win in any match of the visit counts");
        }

        [Test]
        public void FriendlyTables_NeverCost_AndPayASmallPurse()
        {
            var gran = EncounterCatalog.Get(EncounterCatalog.Gran);
            Assert.IsTrue(gran.Friendly);
            GameFlow.BeginEncounter(gran, 0, 0);
            Assert.AreEqual(StakeMode.Friendly, GameFlow.PendingStakeMode);
            Assert.AreEqual(0, GameFlow.SettleMatch(Winner.Opponent).CoinDelta);
            Assert.AreEqual(GameFlow.FriendlyPurse, GameFlow.SettleMatch(Winner.Player).CoinDelta);
        }

        [Test]
        public void BrokePlayers_PlayForAFavour_AndNeverGoBelowZero()
        {
            GameFlow.AddCoins(-100);
            Assert.AreEqual(0, GameFlow.Coins);
            var tobin = EncounterCatalog.Get(EncounterCatalog.Tobin);
            Assert.AreEqual(StakeMode.Favour, GameFlow.StakeModeFor(tobin));
            GameFlow.BeginEncounter(tobin, 0, 0);
            var lost = GameFlow.SettleMatch(Winner.Opponent);
            Assert.AreEqual(0, lost.CoinDelta, "a lost favour match costs no coins");
            Assert.IsTrue(lost.FavourOwed);
            var won = GameFlow.SettleMatch(Winner.Player);
            Assert.AreEqual((tobin.Stake + 1) / 2, won.CoinDelta, "a favour win pays half the stake");
            GameFlow.CompleteEncounter(Winner.Player);
            Assert.IsTrue(GameFlow.ConsumeOutcome().FavourOwed, "the world still plays the chore");
        }

        [Test]
        public void Rematch_FallsBackToAFavour_WhenThePurseRunsDry()
        {
            GameFlow.AddCoins(Mira.Stake - GameFlow.Coins); // exactly one stake
            GameFlow.BeginEncounter(Mira, 0, 0);
            Assert.AreEqual(StakeMode.Coins, GameFlow.PendingStakeMode);
            GameFlow.SettleMatch(Winner.Opponent);
            Assert.AreEqual(0, GameFlow.Coins);
            Assert.AreEqual(StakeMode.Favour, GameFlow.PendingStakeMode);
        }

        [Test]
        public void EveryTableHasAStakeAndItsOwnWheel()
        {
            foreach (var e in EncounterCatalog.All)
            {
                Assert.GreaterOrEqual(e.Stake, 0, e.Id);
                Assert.IsFalse(string.IsNullOrEmpty(e.FavourChore), e.Id);
            }
            Assert.Greater(EncounterCatalog.Get(EncounterCatalog.OutpostChampion).Stake, EncounterCatalog.Get(EncounterCatalog.Champion).Stake, "stakes rise up the mountain");
            Assert.AreEqual(ReelTier.Silver, EncounterCatalog.Get(EncounterCatalog.OutpostChampion).Tier);
        }

        // ------------------------------------------------------------------ items

        [Test]
        public void Buying_TakesCoins_AndWheelsAreBoughtOnce()
        {
            GameFlow.AddCoins(100);
            int before = GameFlow.Coins;
            Assert.IsNull(GameFlow.Buy(ItemCatalog.Tonic));
            Assert.AreEqual(before - ItemCatalog.Find(ItemCatalog.Tonic).Price, GameFlow.Coins);
            Assert.AreEqual(1, GameFlow.ItemCount(ItemCatalog.Tonic));
            Assert.AreEqual(ReelTier.Copper, GameFlow.PlayerWheel);
            Assert.IsNull(GameFlow.Buy(ItemCatalog.BronzeWheel));
            Assert.AreEqual(ReelTier.Bronze, GameFlow.PlayerWheel, "you bring your best wheel");
            Assert.IsNotNull(GameFlow.Buy(ItemCatalog.BronzeWheel), "a wheel is bought once");
            GameFlow.AddCoins(-GameFlow.Coins);
            Assert.IsNotNull(GameFlow.Buy(ItemCatalog.Mortar), "no credit");
            Assert.AreEqual(0, GameFlow.Coins);
        }

        [Test]
        public void EveryShopSellsRealItems_AndCharmsAreSmall()
        {
            foreach (var shop in ShopCatalog.All)
                foreach (var id in shop.Items) Assert.IsNotNull(ItemCatalog.Find(id), shop.Id + ": " + id);
            foreach (var item in ItemCatalog.All.Where(i => i.Kind == ItemKind.Charm))
            {
                var b = item.Boons;
                Assert.IsFalse(b.IsEmpty, item.Id);
                Assert.IsNull(b.Validate(), item.Id);
                // Small head starts: at most one rank, two Bulwark, two Crown, two energy.
                Assert.LessOrEqual((int)b.RankA + (int)b.RankB, 1, item.Id);
                Assert.LessOrEqual(b.Barrier, 2, item.Id);
                Assert.LessOrEqual(b.CrownBonus, 2, item.Id);
                Assert.LessOrEqual(b.EnergyA + b.EnergyB, 4, item.Id);
            }
        }

        [Test]
        public void Charm_IsUsedByOneMatchOnly()
        {
            GameFlow.AddCoins(100);
            GameFlow.Buy(ItemCatalog.Tonic);
            GameFlow.BeginEncounter(Mira, 0, 0, null, ItemCatalog.Tonic);
            Assert.AreEqual(2, GameFlow.PendingBoons.CrownBonus);
            var s = GameFlow.SettleMatch(Winner.Player);
            Assert.AreEqual(ItemCatalog.Tonic, s.CharmUsed);
            Assert.AreEqual(0, GameFlow.ItemCount(ItemCatalog.Tonic));
            Assert.IsTrue(GameFlow.PendingBoons.IsEmpty, "the rematch has no charm");
            Assert.IsNull(GameFlow.SettleMatch(Winner.Player).CharmUsed);
        }

        [Test]
        public void Charm_CannotBeBroughtIfNotOwned()
        {
            GameFlow.BeginEncounter(Mira, 0, 0, null, ItemCatalog.Tonic);
            Assert.IsNull(GameFlow.PendingCharm);
            Assert.IsTrue(GameFlow.PendingBoons.IsEmpty);
        }

        // ------------------------------------------------------------------ boons and wheels in the rules

        [TestCase(ItemCatalog.Tonic)]
        [TestCase(ItemCatalog.Mortar)]
        [TestCase(ItemCatalog.Flint)]
        [TestCase(ItemCatalog.SquareMedal)]
        [TestCase(ItemCatalog.DiamondMedal)]
        public void Boons_ChangeOnlyTheStartingState_AndReplay(string charm)
        {
            var boons = ItemCatalog.Find(charm).Boons;
            var cfg = new MatchConfig(RulesConstants.RulesVersion, 77,
                new SideConfig(ControllerIds.AiStandard, ReelTier.Copper, ReferenceContent.Striker, ReferenceContent.Caster, boons),
                new SideConfig(ControllerIds.AiStandard, ReelTier.Copper, ReferenceContent.Striker, ReferenceContent.Caster));
            var m = Match.Start(cfg, TestKit.Catalog);
            var me = m.Side(SideId.Player);
            var them = m.Side(SideId.Opponent);
            Assert.AreEqual(RulesConstants.StartingCrown + boons.CrownBonus, me.CrownHp);
            Assert.AreEqual(boons.Barrier, me.Barrier);
            Assert.AreEqual(boons.RankA, m.Unit(SideId.Player, 0).Rank);
            Assert.AreEqual(boons.RankB, m.Unit(SideId.Player, 1).Rank);
            Assert.AreEqual(System.Math.Min(boons.EnergyA, m.Unit(SideId.Player, 0).EnergyCost - 1), m.Unit(SideId.Player, 0).Energy);
            Assert.AreEqual(RulesConstants.StartingCrown, them.CrownHp, "the opponent is untouched");
            Assert.AreEqual(0, them.Barrier);
            // Boons travel with the configuration, so replays reproduce the match.
            var decoded = SideConfig.Decode(cfg.Sides[0].Encode());
            Assert.AreEqual(boons.Encode(), decoded.Boons.Encode());
        }

        [Test]
        public void SideConfig_WithoutBoons_EncodesAsBefore()
        {
            var side = new SideConfig(ControllerIds.Human, ReelTier.Copper, ReferenceContent.Striker, ReferenceContent.Caster);
            Assert.AreEqual("human:0:striker,caster", side.Encode(), "older replays stay readable");
            Assert.IsTrue(SideConfig.Decode("human:0:striker,caster").Boons.IsEmpty);
        }

        [Test]
        public void EachSide_SpinsItsOwnWheel()
        {
            var cfg = new MatchConfig(RulesConstants.RulesVersion, 5,
                new SideConfig(ControllerIds.AiStandard, ReelTier.Silver, ReferenceContent.Striker, ReferenceContent.Caster),
                new SideConfig(ControllerIds.AiStandard, ReelTier.Copper, ReferenceContent.Striker, ReferenceContent.Caster));
            var m = Match.Start(cfg, TestKit.Catalog);
            Assert.AreEqual(ReelTier.Silver, m.Side(SideId.Player).ReelTier);
            Assert.AreEqual(ReelTier.Copper, m.Side(SideId.Opponent).ReelTier);
            Assert.AreEqual("reel_5_silver", m.ReelDefinitions(SideId.Player)[4].Id);
            Assert.AreEqual("reel_5_copper", m.ReelDefinitions(SideId.Opponent)[4].Id);
            var full = TestKit.PlayFullMatch(5, ReferenceContent.Striker, ReferenceContent.Caster, ReferenceContent.Striker, ReferenceContent.Caster, ReelTier.Silver);
            Assert.AreEqual(MatchPhase.Ended, full.Phase);
        }

        [Test]
        public void Session_BringsThePlayersWheelAndCharm_ToAWorldChallenge()
        {
            var s = new MatchSession(TestKit.Catalog, () => 99);
            s.Options.Encounter = EncounterCatalog.Get(EncounterCatalog.OutpostChampion);
            s.Options.PlayerTier = ReelTier.Bronze;
            s.Options.PlayerBoons = ItemCatalog.Find(ItemCatalog.Mortar).Boons;
            s.ContinueFromSetup();
            s.Selection.Toggle(ReferenceContent.Striker);
            s.Selection.Toggle(ReferenceContent.Caster);
            Assert.IsTrue(s.ConfirmUnits());
            var cfg = s.Match.Config;
            Assert.AreEqual(ReelTier.Bronze, cfg.Sides[0].ReelTier);
            Assert.AreEqual(ReelTier.Silver, cfg.Sides[1].ReelTier, "the opponent brings their own wheel");
            Assert.AreEqual(2, s.Match.Snapshot().Side(SideId.Player).Barrier);
            Assert.AreEqual(ReferenceContent.Mason, cfg.Sides[1].UnitIds[0]);
        }

        // ------------------------------------------------------------------ errands

        [Test]
        public void DeliveryErrand_PaysOnce_WhenHandedOver()
        {
            var e = ErrandCatalog.Find("hollis_loaf");
            Assert.IsTrue(ErrandCatalog.Offerable(e));
            GameFlow.SetErrand(e.Id, ErrandStage.Active);
            Assert.IsFalse(ErrandCatalog.Offerable(e));
            int before = GameFlow.Coins;
            Assert.AreEqual(e.Reward, GameFlow.CompleteErrand(e));
            Assert.AreEqual(before + e.Reward, GameFlow.Coins);
            Assert.AreEqual(0, GameFlow.CompleteErrand(e), "paid once");
        }

        [Test]
        public void ChainedErrands_WaitForTheirPrerequisite()
        {
            var lantern = ErrandCatalog.Find("wren_lantern");
            Assert.AreEqual("wren_hammer", lantern.Requires);
            Assert.IsFalse(ErrandCatalog.Offerable(lantern));
            GameFlow.CompleteErrand(ErrandCatalog.Find("wren_hammer"));
            Assert.IsTrue(ErrandCatalog.Offerable(lantern));
        }

        [Test]
        public void Errands_AreWellFormed_AndEveryTownHasSeveral()
        {
            CollectionAssert.AllItemsAreUnique(ErrandCatalog.All.Select(e => e.Id));
            foreach (var e in ErrandCatalog.All)
            {
                Assert.Greater(e.Reward, 0, e.Id);
                Assert.IsNotEmpty(e.Offer, e.Id);
                Assert.IsFalse(string.IsNullOrEmpty(e.Reminder), e.Id);
                if (e.IsDelivery) Assert.IsFalse(string.IsNullOrEmpty(e.DeliverLine), e.Id);
                else
                {
                    Assert.IsTrue(WorldBuilder.Pickups.ContainsKey(e.PickupId), e.Id + ": its pickup is placed in the world");
                    Assert.IsFalse(string.IsNullOrEmpty(e.FoundLine) || string.IsNullOrEmpty(e.ThanksLine), e.Id);
                }
                if (e.Requires != null) Assert.IsNotNull(ErrandCatalog.Find(e.Requires), e.Id);
            }
            foreach (var area in new[] { Areas.Hearthmoor, Areas.Brindlecross, Areas.Outpost, Areas.Lanternmere, Areas.Duskhollow, Areas.Ironbell })
                Assert.GreaterOrEqual(ErrandCatalog.All.Count(e => e.Area == area), 3, area);
        }

        // ------------------------------------------------------------------ save

        [Test]
        public void SaveGame_KeepsCoinsSatchelAndErrands_AndOldSavesGetAPurse()
        {
            var dir = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "TabletopJourneySaves");
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            SaveGame.DirectoryOverride = dir;
            try
            {
                GameFlow.AddCoins(40);
                GameFlow.Buy(ItemCatalog.Flint);
                GameFlow.Buy(ItemCatalog.BronzeWheel);
                GameFlow.SetErrand("kit_figurine", ErrandStage.Found);
                GameFlow.CompleteErrand(ErrandCatalog.Find("hollis_loaf"));
                GameFlow.TitleShown = true;
                int coins = GameFlow.Coins;
                Assert.IsTrue(SaveGame.Save(new Vector3(1, 0, 2), 0));
                GameFlow.Reset();
                Assert.IsTrue(SaveGame.TryLoad(out var data));
                SaveGame.Apply(data);
                Assert.AreEqual(coins, GameFlow.Coins);
                Assert.AreEqual(1, GameFlow.ItemCount(ItemCatalog.Flint));
                Assert.AreEqual(ReelTier.Bronze, GameFlow.PlayerWheel);
                Assert.AreEqual(ErrandStage.Found, GameFlow.ErrandState("kit_figurine"));
                Assert.AreEqual(ErrandStage.Done, GameFlow.ErrandState("hollis_loaf"));

                // A journey saved before coins existed (version 1) starts with the usual purse.
                System.IO.File.WriteAllText(SaveGame.FilePath, "{\"version\":1,\"defeated\":[\"mira\"],\"x\":0,\"z\":0}");
                Assert.IsTrue(SaveGame.TryLoad(out var old));
                SaveGame.Apply(old);
                Assert.AreEqual(GameFlow.StartingCoins, GameFlow.Coins);
                Assert.IsTrue(GameFlow.HasDefeated(EncounterCatalog.Mira));
            }
            finally
            {
                SaveGame.DirectoryOverride = null;
                if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            }
        }

        // ------------------------------------------------------------------ the land

        [Test]
        public void Villages_AreLevel_AndPathsNeverStep()
        {
            Assert.AreEqual(0f, WorldGround.Walk(0, 0), 0.01f, "Hearthmoor");
            Assert.AreEqual(2.5f, WorldGround.Walk(0, 134), 0.01f, "Brindlecross");
            Assert.AreEqual(WorldGround.OutpostLevel, WorldGround.Walk(80, 170), 0.01f, "the Outpost plateau");
            Assert.AreEqual(1.2f, WorldGround.Walk(-85, 100), 0.01f, "Lanternmere");
            Assert.AreEqual(0.2f, WorldGround.Walk(-62, 212), 0.01f, "Duskhollow, sunk into the wood");
            Assert.AreEqual(WorldGround.OutpostLevel, WorldGround.Walk(92, 79), 0.01f, "Ironbell on the moor");
            Assert.AreEqual(6.5f, WorldGround.Walk(0, 240), 0.01f, "Crownhold on its hill");
            Assert.AreEqual(6.5f, WorldGround.Walk(25, 218), 0.05f, "Crownhold is level to its walls");
            Assert.AreEqual(0f, WorldGround.Walk(WorldBuilder.InteriorX, 0), 0.01f, "inside the hall");
            Assert.Less(WorldGround.Walk(WorldGround.PitCenter.x, WorldGround.PitCenter.y), WorldGround.OutpostLevel - 3f, "the quarry pit is sunk");
            // Walking the road and the quarry path in half-metre steps never climbs a wall.
            foreach (var path in new[] { WorldBuilder.RoadPoints, WorldBuilder.QuarryPath, WorldBuilder.StreamPath, WorldBuilder.HollowPath, WorldBuilder.BellRoad, WorldBuilder.MoorTrack, WorldBuilder.TourneyRoad })
                for (int i = 0; i < path.Length - 1; i++)
                    for (float t = 0; t < 1f; t += 0.02f)
                    {
                        var a = Vector2.Lerp(path[i], path[i + 1], t);
                        var b = Vector2.Lerp(path[i], path[i + 1], t + 0.02f);
                        float step = Mathf.Abs(WorldGround.Walk(a.x, a.y) - WorldGround.Walk(b.x, b.y));
                        float run = Mathf.Max(0.01f, (b - a).magnitude);
                        Assert.Less(step / run, 0.75f, "too steep near " + a);
                    }
        }

        [Test]
        public void TheQuarryPath_ConnectsBrindlecross_ToTheOutpost()
        {
            var area = WorldTestLayout.Walkable();
            var path = WorldBuilder.QuarryPath;
            for (int i = 0; i < path.Length - 1; i++)
                for (float t = 0; t <= 1f; t += 0.05f)
                {
                    var p = Vector2.Lerp(path[i], path[i + 1], t);
                    Assert.IsTrue(area.Contains(p.x, p.y), "path " + p);
                }
            Assert.IsTrue(area.Contains(80, 170), "plateau");
            Assert.IsTrue(area.Contains(WorldGround.PitCenter.x, WorldGround.PitCenter.y), "quarry pit");
            Assert.IsTrue(area.Contains(WorldGround.KnollCenter.x, WorldGround.KnollCenter.y), "lookout knoll");
            var roam = new RoamArea(area);
            foreach (var spot in WorldBuilder.Pickups) Assert.IsTrue(roam.CanStand(spot.Value.x, spot.Value.y), "pickup " + spot.Key + " is reachable");
            Assert.AreEqual(Areas.QuarryPath, WorldLayout.AreaAt(new Vector3(44, 0, 131)));
            Assert.AreEqual(Areas.Outpost, WorldLayout.AreaAt(new Vector3(80, 0, 170)));
        }

        [Test]
        public void TheStream_CanOnlyBeCrossedOnTheBridge()
        {
            var area = WorldTestLayout.Walkable();
            Assert.IsTrue(area.Contains(-5.5f, WorldGround.StreamZ), "on the bridge");
            Assert.IsFalse(area.Contains(-1.5f, WorldGround.StreamZ), "beside the bridge is water");
            Assert.IsFalse(area.Contains(-9f, WorldGround.StreamZ), "beside the bridge is water");
            Assert.Less(WorldGround.Terrain(-20, WorldGround.StreamZ, area), WorldGround.WaterLevel, "the stream bed is below the water");
        }
    }

    /// <summary>The walkable area exactly as the builder defines it (built headless, no scene).</summary>
    internal static class WorldTestLayout
    {
        public static WalkableArea Walkable()
        {
            var root = new GameObject("TestWorld");
            try
            {
                var kit = new WorldKit(null);
                return new WorldBuilder(kit, null).Build(root.transform).Walkable;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
