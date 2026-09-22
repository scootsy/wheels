using System.Linq;
using NUnit.Framework;
using Tabletop.Domain;
using static Tabletop.Domain.ReferenceContent;

namespace Tabletop.Tests.EditMode
{
    /// <summary>RULES_SPEC 17.2 Symbol Evaluation, 17.3 Energy and Actions, 17.4 XP and Bombs.</summary>
    public class SymbolEnergyXpTests
    {
        // ------------------------------------------------------------ 17.2

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        [TestCase(6, 4)]
        [TestCase(7, 5)]
        public void ResourceFormula(int symbols, int expected)
        {
            Assert.AreEqual(expected, RulesConstants.ResourceFor(symbols));
        }

        [Test]
        public void MultiSymbolFaces_CountEveryPrintedSymbol()
        {
            var t = SymbolEvaluator.Evaluate(new[] { ReelFace.Parse("SS"), ReelFace.Parse("S"), ReelFace.Parse("DD+"), ReelFace.Parse("HHH"), ReelFace.Parse("-") });
            Assert.AreEqual(3, t.ChannelA);
            Assert.AreEqual(1, t.Energy(Channel.A));
            Assert.AreEqual(2, t.ChannelB);
            Assert.AreEqual(0, t.Energy(Channel.B));
            Assert.AreEqual(3, t.Hammer);
            Assert.AreEqual(1, t.BarrierGain);
        }

        [Test]
        public void XpFace_GrantsExactlyOneXp_RegardlessOfPrintedCount()
        {
            // Reel 1 "DD+" is a double-symbol XP face for Channel B.
            var m = TestKit.Start(TestKit.Scenario().Faces(1, new[] { "DD+", "S", "S", "S", "-" }, null));
            var events = m.PlayRound();
            var xp = events.For(MatchEventType.PanelXpGranted, SideId.Player, 1).Single();
            Assert.AreEqual(1, xp.Amount);
            Assert.AreEqual(1, m.Unit(SideId.Player, 1).Xp);
            Assert.AreEqual(0, m.Unit(SideId.Player, 0).Xp);
        }

        [Test]
        public void XpApplies_EvenWhenEnergyThresholdIsNotMet()
        {
            var m = TestKit.Start(TestKit.Scenario().Faces(1, new[] { "S+", "D", "D", "D", "-" }, null));
            m.PlayRound();
            Assert.AreEqual(1, m.Unit(SideId.Player, 0).Xp);
            Assert.AreEqual(0, m.Unit(SideId.Player, 0).Energy, "one A symbol grants no energy");
        }

        // ------------------------------------------------------------ 17.3

        [Test]
        public void Energy_PersistsBetweenRounds()
        {
            // Player Caster (B, cost 5) gains 1 energy from three B symbols.
            var m = TestKit.Start(TestKit.Scenario().Faces(1, new[] { "D", "D", "D", "S", "-" }, null));
            m.PlayRound();
            Assert.AreEqual(1, m.Unit(SideId.Player, 1).Energy);
            Assert.AreEqual(2, m.Round);
            Assert.AreEqual(1, m.Unit(SideId.Player, 1).Energy);
        }

        [Test]
        public void ReachingCost_MakesReady_AndActingResetsTheMeter()
        {
            // Five A symbols: 3 energy for the Striker (cost 3).
            var m = TestKit.Start(TestKit.Scenario().Faces(1, new[] { "S", "SS", "S", "S", "D" }, null));
            var events = m.PlayRound();
            var grant = events.For(MatchEventType.EnergyGranted, SideId.Player, 0).Single();
            Assert.AreEqual(3, grant.Amount);
            Assert.AreEqual(1, events.For(MatchEventType.UnitActivated, SideId.Player, 0).Count);
            Assert.AreEqual(0, m.Unit(SideId.Player, 0).Energy);
            Assert.AreEqual(7, m.Side(SideId.Opponent).CrownHp);
        }

        [Test]
        public void ExcessEnergy_IsDiscarded()
        {
            // Six A symbols = 4 energy toward cost 3: 1 wasted, nothing carries after acting.
            var m = TestKit.Start(TestKit.Scenario().Faces(1, new[] { "S", "SS", "SS", "S", "D" }, null));
            var events = m.PlayRound();
            var grant = events.For(MatchEventType.EnergyGranted, SideId.Player, 0).Single();
            Assert.AreEqual(3, grant.Amount);
            Assert.AreEqual(4, grant.Attempted);
            Assert.AreEqual(1, grant.Wasted);
            Assert.AreEqual(0, m.Unit(SideId.Player, 0).Energy);
        }

        [Test]
        public void CurrentRank_DeterminesCostAndActionStatistics()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 0, Rank.Silver, 0, 3);
            var m = TestKit.Start(sc);
            Assert.AreEqual(3, m.Unit(SideId.Player, 0).EnergyCost);
            var events = m.PlayRound();
            var dmg = events.OfType(MatchEventType.CrownDamaged).Single(e => e.Side == 0);
            Assert.AreEqual(5, dmg.Amount, "Silver Striker deals 5 Crown damage");

            var caster = TestKit.Start(TestKit.Scenario().SetUnit(SideId.Player, 1, Rank.Silver, 0, 0));
            Assert.AreEqual(4, caster.Unit(SideId.Player, 1).EnergyCost, "Silver Caster costs 4");
        }

        [Test]
        public void AUnitDelayedBelowCost_DoesNotActInItsLaterPhase()
        {
            // Opponent Shade (ready) drains the player's ready Striker at stage 4.
            var sc = TestKit.Scenario().Faces(1, null, null)
                .SetUnit(SideId.Player, 0, Rank.Bronze, 0, 3)
                .SetUnit(SideId.Opponent, 0, Rank.Bronze, 0, 3);
            var m = TestKit.Start(sc, oA: Shade, oB: Caster);
            var events = m.PlayRound();
            var delay = events.OfType(MatchEventType.EnergyDelayed).Single();
            Assert.AreEqual(0, delay.TargetSide);
            Assert.AreEqual(0, delay.TargetSlot);
            Assert.AreEqual(1, delay.Amount);
            Assert.AreEqual(0, events.For(MatchEventType.UnitActivated, SideId.Player, 0).Count, "delayed Striker must not act");
            Assert.AreEqual(2, m.Unit(SideId.Player, 0).Energy);
        }

        // ------------------------------------------------------------ 17.4

        [Test]
        public void Bronze_RanksToSilver_At6Xp()
        {
            var m = TestKit.Start(TestKit.Scenario().Faces(1, new[] { "S+", "D", "D", "D", "-" }, null).SetUnit(SideId.Player, 0, Rank.Bronze, 5, 0));
            var events = m.PlayRound();
            var up = events.OfType(MatchEventType.UnitRankedUp).Single();
            Assert.AreEqual((int)Rank.Bronze, up.Before);
            Assert.AreEqual((int)Rank.Silver, up.After);
            Assert.AreEqual(Rank.Silver, m.Unit(SideId.Player, 0).Rank);
            Assert.AreEqual(0, m.Unit(SideId.Player, 0).Xp);
        }

        [Test]
        public void Silver_RanksToGold_At6Xp()
        {
            var m = TestKit.Start(TestKit.Scenario().Faces(1, new[] { "S+", "D", "D", "D", "-" }, null).SetUnit(SideId.Player, 0, Rank.Silver, 5, 0));
            m.PlayRound();
            Assert.AreEqual(Rank.Gold, m.Unit(SideId.Player, 0).Rank);
            Assert.AreEqual(0, m.Unit(SideId.Player, 0).Xp);
        }

        [Test]
        public void Gold_Produces2DamageBomb_At6Xp_IgnoringBarrier()
        {
            var sc = TestKit.Scenario().Faces(1, new[] { "S+", "D", "D", "D", "-" }, null)
                .SetUnit(SideId.Player, 0, Rank.Gold, 5, 0)
                .SetBarrier(SideId.Opponent, 5);
            var m = TestKit.Start(sc);
            var events = m.PlayRound();
            Assert.AreEqual(1, events.OfType(MatchEventType.BombQueued).Count);
            var bomb = events.OfType(MatchEventType.BombLaunched).Single();
            Assert.AreEqual(7, bomb.Stage, "panel-XP bombs resolve in the early bomb step");
            var dmg = events.OfType(MatchEventType.CrownDamaged).Single(e => e.Note == "bomb");
            Assert.AreEqual(2, dmg.Amount);
            Assert.AreEqual(8, m.Side(SideId.Opponent).CrownHp);
            Assert.AreEqual(5, m.Side(SideId.Opponent).Barrier, "bomb ignores Barrier");
            Assert.AreEqual(Rank.Gold, m.Unit(SideId.Player, 0).Rank);
            Assert.AreEqual(0, m.Unit(SideId.Player, 0).Xp);
        }

        [Test]
        public void XpOverflow_IsDiscarded()
        {
            // Three A XP faces (reels 1-3) onto 5/6 XP: ranks up to 0/6, not 2/6 (O-07: one trigger).
            var m = TestKit.Start(TestKit.Scenario().Faces(1, new[] { "S+", "S+", "S+", "D", "-" }, null).SetUnit(SideId.Player, 0, Rank.Bronze, 5, 0));
            var events = m.PlayRound();
            var grant = events.For(MatchEventType.PanelXpGranted, SideId.Player, 0).Single();
            Assert.AreEqual(3, grant.Attempted);
            Assert.AreEqual(1, grant.Amount);
            Assert.AreEqual(Rank.Silver, m.Unit(SideId.Player, 0).Rank);
            Assert.AreEqual(0, m.Unit(SideId.Player, 0).Xp);
            Assert.AreEqual(1, events.OfType(MatchEventType.UnitRankedUp).Count);
        }

        [Test]
        public void PanelXp_RanksBeforeEnergyAndActions()
        {
            // Caster at 4/5 energy and 5/6 XP. A B XP face ranks it to Silver (cost 4) before energy,
            // so it becomes ready (O-02 clamp) and fires with Silver stats this round.
            var sc = TestKit.Scenario().Faces(1, new[] { "S", "D+", "S", "S", "-" }, null)
                .SetUnit(SideId.Player, 1, Rank.Bronze, 5, 4);
            var m = TestKit.Start(sc);
            var events = m.PlayRound();
            var rankIndex = events.FindIndex(e => e.Type == MatchEventType.UnitRankedUp && e.Side == 0 && e.Slot == 1);
            var actIndex = events.FindIndex(e => e.Type == MatchEventType.UnitActivated && e.Side == 0 && e.Slot == 1);
            Assert.GreaterOrEqual(rankIndex, 0);
            Assert.Greater(actIndex, rankIndex);
            var hits = events.OfType(MatchEventType.CrownDamaged).Where(e => e.Side == 0 && e.Slot == 1).ToList();
            Assert.AreEqual(2, hits.Count);
            Assert.IsTrue(hits.All(h => h.Amount == 3), "Silver Caster deals 3 per projectile");
        }

        [Test]
        public void ActionXp_RanksAfterTheAction()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 0, Rank.Bronze, 4, 3);
            var m = TestKit.Start(sc);
            var events = m.PlayRound();
            var dmg = events.OfType(MatchEventType.CrownDamaged).Single(e => e.Side == 0);
            Assert.AreEqual(3, dmg.Amount, "action uses pre-rank Bronze damage");
            var xpIndex = events.FindIndex(e => e.Type == MatchEventType.ActionXpGranted);
            var dmgIndex = events.IndexOf(dmg);
            Assert.Greater(xpIndex, dmgIndex);
            Assert.AreEqual(Rank.Silver, m.Unit(SideId.Player, 0).Rank);
        }

        [Test]
        public void GoldActionXpBomb_ResolvesInLateBombStep()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 0, Rank.Gold, 4, 3);
            var m = TestKit.Start(sc);
            var events = m.PlayRound();
            var bomb = events.OfType(MatchEventType.BombLaunched).Single();
            Assert.AreEqual(11, bomb.Stage);
            // Gold Striker hits for 7, then the late bomb deals 2: 10 - 7 - 2 = 1.
            Assert.AreEqual(1, m.Side(SideId.Opponent).CrownHp);
        }

        [Test]
        public void RankUpLoweringCost_PreservesAbsoluteEnergyClampedToNewCost()
        {
            // O-02: Caster at 5/5 energy (ready) ranks to Silver (cost 4): energy clamps to 4, still ready.
            var sc = TestKit.Scenario().Faces(1, new[] { "S", "D+", "S", "S", "-" }, null).SetUnit(SideId.Player, 1, Rank.Bronze, 5, 5);
            var m = TestKit.Start(sc);
            var events = m.PlayRound();
            var up = events.OfType(MatchEventType.UnitRankedUp).Single();
            Assert.AreEqual(-1, up.Amount, "energy change reported on rank-up");
            Assert.AreEqual(1, events.For(MatchEventType.UnitActivated, SideId.Player, 1).Count);
        }
    }
}
