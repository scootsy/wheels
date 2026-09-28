using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tabletop.Application;
using Tabletop.Domain;
using Tabletop.World;
using UnityEngine;

namespace Tabletop.Tests.EditMode
{
    /// <summary>
    /// D-038: the rest of the journey. Every figurine can be won, the Grand Tournament runs three rounds in a row, and
    /// the player can wander the land but not into water, up cliffs or off the edge of the map.
    /// </summary>
    public class RestOfJourneyTests
    {
        [SetUp]
        public void Reset() => GameFlow.Reset();

        [TearDown]
        public void TearDown() => GameFlow.Reset();

        private static void HoldEveryTitle(int coins = 20) =>
            GameFlow.Restore(EncounterCatalog.TownChampions.Select(e => e.Id), null, null, false, null, coins);

        private static MatchSettlement Play(EncounterDefinition e, Winner winner)
        {
            GameFlow.BeginEncounter(e, 0, 240);
            var s = GameFlow.SettleMatch(winner);
            GameFlow.CompleteEncounter(winner);
            return s;
        }

        // ------------------------------------------------------------------ figurines

        [Test]
        public void EveryFigurine_IsHeldBySomeChampion()
        {
            foreach (var unit in ReferenceContent.Catalog.Units)
            {
                if (EncounterCatalog.StartingUnits.Contains(unit.Id)) continue;
                var source = EncounterCatalog.PrizeSource(unit.Id);
                Assert.IsNotNull(source, unit.DisplayName + " can be won");
                Assert.IsTrue(source.TownChampion, unit.DisplayName + " is a town champion's prize");
            }
        }

        [Test]
        public void BeatingTheNewChampions_WinsPriestAssassinAndWarlock()
        {
            GameFlow.AddCoins(1000);
            foreach (var (id, unit) in new[] { (EncounterCatalog.LakeChampion, ReferenceContent.Mender), (EncounterCatalog.HollowChampion, ReferenceContent.Shade), (EncounterCatalog.BellChampion, ReferenceContent.Hexer) })
            {
                Assert.IsFalse(GameFlow.IsUnlocked(unit));
                var s = Play(EncounterCatalog.Get(id), Winner.Player);
                Assert.AreEqual(unit, s.UnlockedUnit, id);
                Assert.IsTrue(GameFlow.IsUnlocked(unit), id);
                Assert.AreEqual(EncounterCatalog.Get(id).Stake, s.CoinDelta, "champions play for their stake");
            }
        }

        // ------------------------------------------------------------------ the Grand Tournament

        [Test]
        public void Tournament_IsOnlyOpenToAChampionOfEveryTown()
        {
            Assert.IsFalse(GameFlow.TournamentQualified);
            Assert.IsFalse(GameFlow.EnterTournament());
            Assert.AreEqual(0, GameFlow.TournamentRound);
            var champions = EncounterCatalog.TownChampions.Select(e => e.Id).ToList();
            GameFlow.Restore(champions.Take(champions.Count - 1), null, null, false, null);
            Assert.AreEqual(champions.Count - 1, GameFlow.ChampionTitles);
            Assert.IsFalse(GameFlow.EnterTournament(), "four titles are not enough");
            HoldEveryTitle();
            Assert.IsTrue(GameFlow.TournamentQualified);
            Assert.IsTrue(GameFlow.EnterTournament());
            Assert.AreEqual(1, GameFlow.TournamentRound);
            Assert.AreEqual(EncounterCatalog.TourneyFirst, GameFlow.CurrentTournamentOpponent.Id);
        }

        [Test]
        public void Tournament_ThreeWinsInARow_MakeTheGrandChampion()
        {
            HoldEveryTitle();
            GameFlow.EnterTournament();
            int coins = GameFlow.Coins;
            var r1 = Play(GameFlow.CurrentTournamentOpponent, Winner.Player);
            Assert.AreEqual(60, r1.CoinDelta, "round one's purse");
            Assert.AreEqual(2, GameFlow.TournamentRound);
            Assert.AreEqual(TournamentResult.Advanced, GameFlow.ConsumeOutcome().Tournament);
            Assert.AreEqual(EncounterCatalog.TourneySecond, GameFlow.CurrentTournamentOpponent.Id);
            Play(GameFlow.CurrentTournamentOpponent, Winner.Player);
            Assert.AreEqual(3, GameFlow.TournamentRound);
            var final = GameFlow.CurrentTournamentOpponent;
            Assert.AreEqual(EncounterCatalog.GrandChampion, final.Id);
            Assert.AreEqual(ReelTier.Platinum, final.Tier);
            Assert.IsFalse(GameFlow.IsGrandChampion);
            var won = Play(final, Winner.Player);
            Assert.IsTrue(GameFlow.IsGrandChampion);
            Assert.AreEqual(0, GameFlow.TournamentRound, "the run is over");
            Assert.AreEqual(ItemCatalog.PlatinumWheel, won.WheelWon);
            Assert.AreEqual(ReelTier.Platinum, GameFlow.PlayerWheel, "the Grand Champion brings the Platinum Wheel");
            Assert.AreEqual(TournamentResult.Won, GameFlow.ConsumeOutcome().Tournament);
            Assert.AreEqual(coins + 60 + 90 + 250, GameFlow.Coins, "purses, never stakes");
        }

        [Test]
        public void Tournament_ALoss_StartsYouAgainFromRoundOne_ATieReplaysTheRound()
        {
            HoldEveryTitle();
            GameFlow.EnterTournament();
            Play(GameFlow.CurrentTournamentOpponent, Winner.Player);
            int coins = GameFlow.Coins;
            var tie = Play(GameFlow.CurrentTournamentOpponent, Winner.Tie);
            Assert.AreEqual(2, GameFlow.TournamentRound, "a tie plays the round again");
            Assert.AreEqual(0, tie.CoinDelta);
            var loss = Play(GameFlow.CurrentTournamentOpponent, Winner.Opponent);
            Assert.AreEqual(0, GameFlow.TournamentRound, "out of the tournament");
            Assert.AreEqual(0, loss.CoinDelta, "losing a round costs nothing");
            Assert.AreEqual(coins, GameFlow.Coins);
            Assert.AreEqual(TournamentResult.Eliminated, GameFlow.ConsumeOutcome().Tournament);
            Assert.IsTrue(GameFlow.EnterTournament(), "enter again any time");
            Assert.AreEqual(1, GameFlow.TournamentRound);
            Assert.AreEqual(2, GameFlow.TournamentAttempts);
        }

        [Test]
        public void Tournament_OnlyTheRoundYouAreDueToPlay_Counts()
        {
            HoldEveryTitle();
            GameFlow.EnterTournament();
            var first = GameFlow.CurrentTournamentOpponent;
            Play(first, Winner.Player);
            // A rematch at the same table (the result screen's REMATCH) is an exhibition game.
            int coins = GameFlow.Coins;
            var again = Play(first, Winner.Opponent);
            Assert.AreEqual(2, GameFlow.TournamentRound, "the exhibition does not knock you out");
            Assert.AreEqual(coins, GameFlow.Coins);
            StringAssert.Contains("exhibition", again.TournamentNote);
            // Nor can the final be played early.
            Play(EncounterCatalog.Get(EncounterCatalog.GrandChampion), Winner.Player);
            Assert.IsFalse(GameFlow.IsGrandChampion);
        }

        [Test]
        public void GrandChampion_NeverPlaysTheSamePairTwiceInARow()
        {
            string last = null;
            for (int attempt = 1; attempt <= EncounterCatalog.FinalPairs.Count * 2; attempt++)
            {
                var e = EncounterCatalog.TournamentOpponent(EncounterCatalog.TournamentRounds, attempt);
                string pair = e.UnitA + "+" + e.UnitB;
                Assert.AreNotEqual(last, pair, "attempt " + attempt);
                Assert.AreEqual(e.Id, EncounterCatalog.GrandChampion);
                Assert.IsNotNull(Match.Start(MatchConfig.Standard(3, ReferenceContent.Striker, ReferenceContent.Caster, e.UnitA, e.UnitB, e.Tier, e.AiProfile), TestKit.Catalog, out var rej), rej?.ToString());
                last = pair;
            }
        }

        [Test]
        public void SaveGame_KeepsTheTournamentRun_AndOlderSavesStartOutsideIt()
        {
            var dir = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "TabletopTourneySaves");
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            SaveGame.DirectoryOverride = dir;
            try
            {
                HoldEveryTitle();
                GameFlow.EnterTournament();
                Play(GameFlow.CurrentTournamentOpponent, Winner.Player);
                GameFlow.TitleShown = true;
                Assert.IsTrue(SaveGame.Save(new Vector3(0, 0, 237), 0));
                GameFlow.Reset();
                Assert.IsTrue(SaveGame.TryLoad(out var data));
                Assert.AreEqual(3, data.version);
                SaveGame.Apply(data);
                Assert.AreEqual(2, GameFlow.TournamentRound);
                Assert.AreEqual(1, GameFlow.TournamentAttempts);
                Assert.AreEqual(EncounterCatalog.TourneySecond, GameFlow.CurrentTournamentOpponent.Id);

                System.IO.File.WriteAllText(SaveGame.FilePath, "{\"version\":2,\"defeated\":[\"corvin\"],\"coins\":55,\"x\":0,\"z\":0}");
                Assert.IsTrue(SaveGame.TryLoad(out var old));
                SaveGame.Apply(old);
                Assert.AreEqual(0, GameFlow.TournamentRound);
                Assert.AreEqual(55, GameFlow.Coins);
                Assert.IsTrue(GameFlow.HasDefeated(EncounterCatalog.Champion));
            }
            finally
            {
                SaveGame.DirectoryOverride = null;
                if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            }
        }

        [Test]
        public void Shops_SellTheDiamondWheel_ButNeverThePlatinum()
        {
            Assert.IsTrue(ShopCatalog.All.Any(s => s.Items.Contains(ItemCatalog.DiamondWheel)));
            Assert.IsFalse(ShopCatalog.All.Any(s => s.Items.Contains(ItemCatalog.PlatinumWheel)));
            foreach (var shop in ShopCatalog.All)
                foreach (var id in shop.Items) Assert.IsNotNull(ItemCatalog.Find(id), shop.Id + " sells " + id);
            GameFlow.AddCoins(400);
            Assert.IsNull(GameFlow.Buy(ItemCatalog.DiamondWheel));
            Assert.AreEqual(ReelTier.Diamond, GameFlow.PlayerWheel);
        }

        // ------------------------------------------------------------------ wandering

        [Test]
        public void Wandering_OpenLandIsWalkable_WaterCliffsAndTheEdgeAreNot()
        {
            var paths = WorldTestLayout.Walkable();
            var roam = new RoamArea(paths);
            Assert.IsFalse(paths.Contains(-20, 95), "this meadow is off the paths...");
            Assert.IsTrue(roam.CanStand(-20, 95), "...but the player can wander into it");
            Assert.IsTrue(roam.CanStand(-30, 40), "the hills west of the North Road");
            Assert.IsFalse(roam.CanStand(-30, WorldGround.StreamZ), "the stream");
            Assert.IsFalse(roam.CanStand(WorldGround.LakeCenter.x, WorldGround.LakeCenter.y), "the middle of the lake");
            Assert.IsTrue(roam.CanStand(WorldBuilder.PierTable.x, WorldBuilder.PierTable.y - 2.1f), "the pier over the lake");
            Assert.IsTrue(roam.CanStand(-5.5f, WorldGround.StreamZ), "the bridge");
            var e = WorldGround.Extent;
            Assert.IsFalse(roam.CanStand(e.xMin + 2f, 100), "the west edge of the map");
            Assert.IsFalse(roam.CanStand(0, e.yMin + 2f), "the south edge of the map");
            Assert.IsFalse(roam.CanStand(-75, 20), "far from anywhere");
            Assert.IsFalse(roam.CanStand(WorldBuilder.InteriorX + 20f, 0), "outside the hall's interior");
            // Somewhere within reach is too steep to climb (the mountains beyond the paths).
            bool anySteep = false;
            for (float x = -100; x < 140 && !anySteep; x += 3)
                for (float z = -40; z < 270 && !anySteep; z += 3)
                    if (paths.Distance(x, z, out _) < RoamArea.Reach && !WorldGround.WaterAt(x, z) && WorldGround.SlopeDegrees(x, z, paths) > RoamArea.MaxSlope)
                        anySteep = !roam.CanStand(x, z);
            Assert.IsTrue(anySteep, "steep ground stops the player");
        }

        [Test]
        public void Wandering_TheLandHasNoHiddenSteps()
        {
            // The visible land is continuous: no creases where one path's influence meets another's (they felt like
            // invisible walls once the player could walk there). River and lake banks are real banks.
            var paths = WorldTestLayout.Walkable();
            var e = WorldGround.Extent;
            var creases = new List<Vector2>();
            for (float z = e.yMin + 14; z < e.yMax - 14; z += 2f)
                for (float x = e.xMin + 14; x < e.xMax - 14; x += 2f)
                {
                    if (paths.Distance(x, z, out _) > RoamArea.Reach || WorldGround.StreamMask(x, z) > 0.01f || WorldGround.LakeMask(x, z) > 0.01f) continue;
                    float a = WorldGround.Terrain(x, z, paths);
                    float g = Mathf.Max(Mathf.Abs(a - WorldGround.Terrain(x + 0.25f, z, paths)), Mathf.Abs(a - WorldGround.Terrain(x, z + 0.25f, paths))) / 0.25f;
                    if (g > 2.5f) creases.Add(new Vector2(x, z));
                }
            Assert.IsEmpty(creases, "sudden steps in the land at " + string.Join(", ", creases.Take(8)));
        }

        [Test]
        public void NewPlaces_HaveTheirNames()
        {
            Assert.AreEqual(Areas.Lanternmere, WorldLayout.AreaAt(new Vector3(-84, 0, 100)));
            Assert.AreEqual(Areas.Lanternmere, WorldLayout.AreaAt(new Vector3(WorldBuilder.PierTable.x, 0, WorldBuilder.PierTable.y)));
            Assert.AreEqual(Areas.StreamPath, WorldLayout.AreaAt(new Vector3(-30, 0, 73)));
            Assert.AreEqual(Areas.Duskhollow, WorldLayout.AreaAt(new Vector3(-62, 0, 215)));
            Assert.AreEqual(Areas.HollowPath, WorldLayout.AreaAt(new Vector3(-40, 0, 174)));
            Assert.AreEqual(Areas.BellRoad, WorldLayout.AreaAt(new Vector3(40, 0, 83)));
            Assert.AreEqual(Areas.Ironbell, WorldLayout.AreaAt(new Vector3(92, 0, 79)));
            Assert.AreEqual(Areas.MoorTrack, WorldLayout.AreaAt(new Vector3(95, 0, 130)));
            Assert.AreEqual(Areas.TourneyRoad, WorldLayout.AreaAt(new Vector3(21, 0, 184)));
            Assert.AreEqual(Areas.Crownhold, WorldLayout.AreaAt(new Vector3(0, 0, 240)));
            // The older places keep theirs.
            Assert.AreEqual(Areas.Brindlecross, WorldLayout.AreaAt(new Vector3(0, 0, 134)));
            Assert.AreEqual(Areas.NorthRoad, WorldLayout.AreaAt(new Vector3(0, 0, 60)));
            Assert.AreEqual(Areas.Hall, WorldLayout.AreaAt(new Vector3(WorldBuilder.InteriorX, 0, 0)));
            foreach (var area in new[] { Areas.Lanternmere, Areas.Duskhollow, Areas.Ironbell, Areas.Crownhold, Areas.StreamPath, Areas.HollowPath, Areas.BellRoad, Areas.MoorTrack, Areas.TourneyRoad })
                Assert.IsNotNull(WorldApp.AreaSounds(area).music, area + " has music");
        }
    }
}
