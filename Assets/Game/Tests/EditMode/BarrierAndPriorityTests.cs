using System.Linq;
using NUnit.Framework;
using Tabletop.Domain;
using static Tabletop.Domain.ReferenceContent;

namespace Tabletop.Tests.EditMode
{
    /// <summary>RULES_SPEC 17.5 Barrier and Attacks, 17.6 Priority and End State, Section 15 decisions.</summary>
    public class BarrierAndPriorityTests
    {
        // ------------------------------------------------------------ 17.5

        [Test]
        public void Barrier_CapsAt5()
        {
            // H + HH + HH + HH + H = 8 hammers -> 6 Barrier attempted, capped at 5.
            var m = TestKit.Start(TestKit.Scenario().Faces(1, new[] { "H", "HH", "HH", "HH", "H" }, null));
            var built = m.PlayRound().OfType(MatchEventType.BarrierBuilt).Single();
            Assert.AreEqual(5, built.Amount);
            Assert.AreEqual(6, built.Attempted);
            Assert.AreEqual(5, m.Side(SideId.Player).Barrier);
        }

        [Test]
        public void Barrier_DoesNotDecayBetweenRounds()
        {
            var m = TestKit.Start(TestKit.Scenario().SetBarrier(SideId.Player, 3).Faces(1, null, null).Faces(2, null, null));
            m.PlayRound();
            m.PlayRound();
            Assert.AreEqual(3, m.Side(SideId.Player).Barrier);
        }

        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(5, false)]
        public void Ranger_Height3_ClearsBarrier0To2_AndIsBlockedBy3To5(int barrier, bool hitsCrown)
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 0, Rank.Bronze, 0, 4).SetBarrier(SideId.Opponent, barrier);
            var m = TestKit.Start(sc, pA: Ranger);
            var events = m.PlayRound();
            var proj = events.OfType(MatchEventType.ProjectileResolved).Single(e => e.Side == 0);
            Assert.AreEqual(3, proj.Height);
            Assert.AreEqual(hitsCrown, proj.TargetIsCrown);
            if (hitsCrown)
            {
                Assert.AreEqual(7, m.Side(SideId.Opponent).CrownHp);
                Assert.AreEqual(barrier, m.Side(SideId.Opponent).Barrier);
            }
            else
            {
                Assert.AreEqual(10, m.Side(SideId.Opponent).CrownHp);
                Assert.AreEqual(barrier - 1, m.Side(SideId.Opponent).Barrier, "Bronze Ranger deals 1 Barrier damage");
            }
        }

        [Test]
        public void HeightEqualToBarrier_HitsBarrier_AndHeight1IsBlockedByAnyBarrier()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 0, Rank.Bronze, 0, 3).SetBarrier(SideId.Opponent, 1);
            var m = TestKit.Start(sc);
            var events = m.PlayRound();
            var proj = events.OfType(MatchEventType.ProjectileResolved).Single(e => e.Side == 0);
            Assert.IsFalse(proj.TargetIsCrown);
            Assert.AreEqual(0, events.OfType(MatchEventType.CrownDamaged).Count(e => e.Side == 0));
        }

        [Test]
        public void BarrierDamage_DoesNotOverflowIntoCrown()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 0, Rank.Silver, 0, 3).SetBarrier(SideId.Opponent, 1);
            var m = TestKit.Start(sc);
            var events = m.PlayRound();
            var hit = events.OfType(MatchEventType.BarrierDamaged).Single();
            Assert.AreEqual(1, hit.Amount);
            Assert.AreEqual(5, hit.Attempted);
            Assert.AreEqual(4, hit.Wasted);
            Assert.AreEqual(0, m.Side(SideId.Opponent).Barrier);
            Assert.AreEqual(10, m.Side(SideId.Opponent).CrownHp);
        }

        [Test]
        public void CasterProjectiles_ResolveSequentially_UpdatingBarrierBetweenHits()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 1, Rank.Bronze, 0, 5).SetBarrier(SideId.Opponent, 2);
            var m = TestKit.Start(sc);
            var events = m.PlayRound();
            var projs = events.OfType(MatchEventType.ProjectileResolved).Where(e => e.Side == 0).ToList();
            Assert.AreEqual(new[] { 1, 6 }, projs.Select(p => p.Height).ToArray());
            Assert.IsFalse(projs[0].TargetIsCrown);
            Assert.AreEqual(2, projs[0].Before);
            Assert.IsTrue(projs[1].TargetIsCrown);
            Assert.AreEqual(0, projs[1].Before, "second projectile sees Barrier already reduced to 0");
            Assert.AreEqual(8, m.Side(SideId.Opponent).CrownHp);

            var clear = TestKit.Start(TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 1, Rank.Bronze, 0, 5));
            clear.PlayRound();
            Assert.AreEqual(6, clear.Side(SideId.Opponent).CrownHp, "both projectiles hit an unprotected Crown");
        }

        [Test]
        public void Mason_AttacksThenAdds2FriendlyBarrier()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 0, Rank.Bronze, 0, 4).SetBarrier(SideId.Player, 4).SetBarrier(SideId.Opponent, 2);
            var m = TestKit.Start(sc, pA: Mason);
            var events = m.PlayRound();
            var attack = events.FindIndex(e => e.Type == MatchEventType.BarrierDamaged && e.Side == 0);
            var build = events.FindIndex(e => e.Type == MatchEventType.BarrierBuilt && e.Side == 0);
            Assert.Greater(build, attack);
            Assert.AreEqual(0, m.Side(SideId.Opponent).Barrier);
            Assert.AreEqual(5, m.Side(SideId.Player).Barrier, "capped at 5");
            Assert.AreEqual(1, events[build].Amount);
        }

        [Test]
        public void Hexer_SelfDamageFloorsAt1_AndThreeAttacksUpdateBarrierSequentially()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 0, Rank.Bronze, 0, 4)
                .SetCrown(SideId.Player, 2).SetBarrier(SideId.Opponent, 4);
            var m = TestKit.Start(sc, pA: Hexer);
            var events = m.PlayRound();
            var self = events.OfType(MatchEventType.CrownDamaged).Single(e => e.Note == "self");
            Assert.AreEqual(1, self.Amount);
            Assert.AreEqual(1, m.Side(SideId.Player).CrownHp);
            var projs = events.OfType(MatchEventType.ProjectileResolved).Where(e => e.Side == 0).ToList();
            Assert.AreEqual(new[] { 5, 3, 1 }, projs.Select(p => p.Height).ToArray());
            Assert.AreEqual(new[] { true, false, false }, projs.Select(p => p.TargetIsCrown).ToArray());
            Assert.AreEqual(new[] { 4, 4, 3 }, projs.Select(p => p.Before).ToArray());
            Assert.AreEqual(2, m.Side(SideId.Opponent).Barrier);
            Assert.AreEqual(9, m.Side(SideId.Opponent).CrownHp);

            var atOne = TestKit.Start(TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 0, Rank.Bronze, 0, 4).SetCrown(SideId.Player, 1), pA: Hexer);
            atOne.PlayRound();
            Assert.AreEqual(1, atOne.Side(SideId.Player).CrownHp, "self-damage never lowers the Crown below 1");
        }

        [Test]
        public void Hexer_Height5_DoesNotBypassMaxBarrier()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 0, Rank.Bronze, 0, 4).SetBarrier(SideId.Opponent, 5);
            var m = TestKit.Start(sc, pA: Hexer);
            var proj = m.PlayRound().OfType(MatchEventType.ProjectileResolved).First(e => e.Side == 0);
            Assert.IsFalse(proj.TargetIsCrown);
        }

        // ------------------------------------------------------------ 17.6

        [Test]
        public void Shade_ResolvesBeforeMenderMasonAndOrdinaryAttackers()
        {
            var sc = TestKit.Scenario().Faces(1, null, null)
                .SetUnit(SideId.Player, 0, Rank.Bronze, 0, 4)   // Mason
                .SetUnit(SideId.Player, 1, Rank.Bronze, 0, 4)   // Mender
                .SetUnit(SideId.Opponent, 0, Rank.Bronze, 0, 3) // Striker
                .SetUnit(SideId.Opponent, 1, Rank.Bronze, 0, 3); // Shade
            var m = TestKit.Start(sc, pA: Mason, pB: Mender, oA: Striker, oB: Shade);
            var acts = m.PlayRound().OfType(MatchEventType.UnitActivated).ToList();
            var order = acts.Select(a => m.UnitDefinition((SideId)a.Side, a.Slot).Id).ToArray();
            // Shade (stage 4) drains the tied Channel A Mason below cost; Mender (stage 5) heals and
            // re-readies it with blessing energy, so the Mason acts after the Striker, at stage 10.
            CollectionAssert.AreEqual(new[] { Shade, Mender, Striker, Mason }, order);
            CollectionAssert.AreEqual(new[] { 4, 5, 8, 10 }, acts.Select(a => a.Stage).ToArray());
        }

        [Test]
        public void Mason_ResolvesBeforeOrdinaryAttackers()
        {
            var sc = TestKit.Scenario().Faces(1, null, null)
                .SetUnit(SideId.Player, 0, Rank.Bronze, 0, 3)    // Striker (stage 8)
                .SetUnit(SideId.Opponent, 0, Rank.Bronze, 0, 4); // Mason (stage 6)
            var m = TestKit.Start(sc, oA: Mason, oB: Caster);
            var events = m.PlayRound();
            var acts = events.OfType(MatchEventType.UnitActivated).ToList();
            Assert.AreEqual(1, acts[0].Side, "opponent Mason (stage 6) acts before player Striker (stage 8)");
            Assert.AreEqual(6, acts[0].Stage);
            Assert.AreEqual(8, acts[1].Stage);
            // Mason raised the opponent Barrier to 2 before the Striker fired, so the Striker is blocked.
            Assert.AreEqual(10, m.Side(SideId.Opponent).CrownHp);
        }

        [Test]
        public void LeftBeforeRight_AndPlayerBeforeOpponent_WithinAStage()
        {
            var sc = TestKit.Scenario().Faces(1, null, null)
                .SetUnit(SideId.Player, 0, Rank.Bronze, 0, 3).SetUnit(SideId.Player, 1, Rank.Bronze, 0, 5)
                .SetUnit(SideId.Opponent, 0, Rank.Bronze, 0, 3).SetUnit(SideId.Opponent, 1, Rank.Bronze, 0, 5);
            var m = TestKit.Start(sc);
            var acts = m.PlayRound().OfType(MatchEventType.UnitActivated).Select(a => (a.Side, a.Slot)).ToArray();
            CollectionAssert.AreEqual(new[] { (0, 0), (0, 1), (1, 0), (1, 1) }, acts);
        }

        [Test]
        public void ASideAtZeroCrown_ContinuesResolvingActions()
        {
            // Opponent Shade (stage 4) drops the player to 0; the player's Caster still fires at stage 8.
            var sc = TestKit.Scenario().Faces(1, null, null).SetCrown(SideId.Player, 1)
                .SetUnit(SideId.Player, 0, Rank.Bronze, 0, 3).SetUnit(SideId.Player, 1, Rank.Bronze, 0, 5)
                .SetUnit(SideId.Opponent, 0, Rank.Bronze, 0, 3);
            var m = TestKit.Start(sc, oA: Shade, oB: Caster);
            var events = m.PlayRound();
            // Tie on need (both 0) -> Channel A Striker is drained; the Caster still acts although the Crown is 0.
            var zeroAt = events.FindIndex(e => e.Type == MatchEventType.CrownDamaged && e.TargetSide == 0 && e.After == 0);
            var casterAct = events.FindIndex(e => e.Type == MatchEventType.UnitActivated && e.Side == 0 && e.Slot == 1);
            Assert.GreaterOrEqual(zeroAt, 0);
            Assert.Greater(casterAct, zeroAt);
            Assert.AreEqual(6, m.Side(SideId.Opponent).CrownHp);
            Assert.AreEqual(Winner.Opponent, m.Winner);
        }

        [Test]
        public void OneSideAtZero_AfterAllSteps_Loses()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetCrown(SideId.Opponent, 3).SetUnit(SideId.Player, 0, Rank.Bronze, 0, 3);
            var m = TestKit.Start(sc);
            var events = m.PlayRound();
            Assert.AreEqual(MatchPhase.Ended, m.Phase);
            Assert.AreEqual(Winner.Player, m.Winner);
            Assert.AreEqual(MatchEventType.MatchEnded, events.Last().Type);
            Assert.AreEqual(RejectionCode.MatchAlreadyEnded, m.Rejected(MatchCommand.Spin(SideId.Player)).Code);
        }

        [Test]
        public void BothSidesAtZero_AfterAllSteps_Tie()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetCrown(SideId.Player, 3).SetCrown(SideId.Opponent, 3)
                .SetUnit(SideId.Player, 0, Rank.Bronze, 0, 3).SetUnit(SideId.Opponent, 0, Rank.Bronze, 0, 3);
            var m = TestKit.Start(sc);
            m.PlayRound();
            Assert.AreEqual(Winner.Tie, m.Winner);
        }

        [Test]
        public void Mender_HealsBeforeFinalCheck_AndCanRescueACrownReducedTo0()
        {
            // Opponent Shade hits the player to 0 at stage 4 (draining the tied Channel A Striker);
            // the player's Mender heals at stage 5.
            var sc = TestKit.Scenario().Faces(1, null, null).SetCrown(SideId.Player, 1)
                .SetUnit(SideId.Player, 0, Rank.Bronze, 0, 3)
                .SetUnit(SideId.Player, 1, Rank.Bronze, 0, 4)
                .SetUnit(SideId.Opponent, 0, Rank.Bronze, 0, 3);
            var m = TestKit.Start(sc, pA: Striker, pB: Mender, oA: Shade, oB: Caster);
            var events = m.PlayRound();
            var zero = events.FindIndex(e => e.Type == MatchEventType.CrownDamaged && e.TargetSide == 0 && e.After == 0);
            var heal = events.FindIndex(e => e.Type == MatchEventType.CrownHealed && e.Side == 0);
            Assert.GreaterOrEqual(zero, 0, "Shade hit the Crown to 0");
            Assert.Greater(heal, zero);
            Assert.AreEqual(Winner.None, m.Winner);
            Assert.AreEqual(1, m.Side(SideId.Player).CrownHp);
        }

        [Test]
        public void MenderHealing_CapsAt12()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetCrown(SideId.Player, 11).SetUnit(SideId.Player, 1, Rank.Silver, 0, 3);
            var m = TestKit.Start(sc, pA: Striker, pB: Mender);
            var heal = m.PlayRound().OfType(MatchEventType.CrownHealed).Single();
            Assert.AreEqual(1, heal.Amount);
            Assert.AreEqual(2, heal.Attempted);
            Assert.AreEqual(12, m.Side(SideId.Player).CrownHp);
        }

        [Test]
        public void MenderDeferredEnergy_CanCreateASecondPartnerActivation()
        {
            // Striker ready from reels; Gold Mender ready. Mender defers 3 energy to stage 9;
            // Striker acts at stage 8, receives 3 at stage 9, and acts again at stage 10.
            var sc = TestKit.Scenario().Faces(1, null, null)
                .SetUnit(SideId.Player, 0, Rank.Bronze, 0, 3)
                .SetUnit(SideId.Player, 1, Rank.Gold, 0, 3);
            var m = TestKit.Start(sc, pA: Striker, pB: Mender);
            var events = m.PlayRound();
            var strikes = events.For(MatchEventType.UnitActivated, SideId.Player, 0);
            Assert.AreEqual(2, strikes.Count);
            Assert.AreEqual(8, strikes[0].Stage);
            Assert.AreEqual(10, strikes[1].Stage);
            Assert.AreEqual(EnergySource.Priest, strikes[1].Source);
            var deferred = events.For(MatchEventType.EnergyGranted, SideId.Player, 0).Single(e => e.Source == EnergySource.Priest);
            Assert.AreEqual(9, deferred.Stage);
            Assert.AreEqual(4, m.Side(SideId.Opponent).CrownHp);
        }

        [Test]
        public void MenderImmediateEnergy_ReadiesAPartnerThatActsAtStage10()
        {
            // Caster at 3/5; Bronze Mender grants 2 immediately at stage 5 -> Caster acts at stage 10.
            var sc = TestKit.Scenario().Faces(1, null, null)
                .SetUnit(SideId.Player, 0, Rank.Bronze, 0, 4)
                .SetUnit(SideId.Player, 1, Rank.Bronze, 0, 3);
            var m = TestKit.Start(sc, pA: Mender, pB: Caster);
            var events = m.PlayRound();
            var grant = events.For(MatchEventType.EnergyGranted, SideId.Player, 1).Single();
            Assert.AreEqual(5, grant.Stage);
            var act = events.For(MatchEventType.UnitActivated, SideId.Player, 1).Single();
            Assert.AreEqual(10, act.Stage);
        }

        // ------------------------------------------------------------ Section 15 decisions

        [Test]
        public void O01_ShadeTie_TargetsChannelA_AndLogsTheTie()
        {
            var sc = TestKit.Scenario().Faces(1, null, null)
                .SetUnit(SideId.Player, 0, Rank.Bronze, 0, 1)   // Striker needs 2
                .SetUnit(SideId.Player, 1, Rank.Bronze, 0, 3)   // Caster needs 2
                .SetUnit(SideId.Opponent, 0, Rank.Bronze, 0, 3);
            var m = TestKit.Start(sc, oA: Shade, oB: Caster);
            var delay = m.PlayRound().OfType(MatchEventType.EnergyDelayed).Single();
            Assert.AreEqual(0, delay.TargetSlot);
            StringAssert.Contains("tie", delay.Note);
        }

        [Test]
        public void O05_Hexer_ResolvesInTheNormalActionStage()
        {
            var sc = TestKit.Scenario().Faces(1, null, null).SetUnit(SideId.Player, 0, Rank.Bronze, 0, 4);
            var m = TestKit.Start(sc, pA: Hexer);
            Assert.AreEqual(8, m.PlayRound().OfType(MatchEventType.UnitActivated).Single().Stage);
        }
    }
}
