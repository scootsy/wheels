using System.Linq;
using NUnit.Framework;
using Tabletop.Domain;

namespace Tabletop.Tests.EditMode
{
    /// <summary>RULES_SPEC 17.1 Reels and Locking.</summary>
    public class ReelAndLockTests
    {
        private static Match NewMatch(long seed = 12345) =>
            Match.Start(MatchConfig.Standard(seed, ReferenceContent.Striker, ReferenceContent.Caster), TestKit.Catalog);

        [Test]
        public void SameSeedAndCommands_ProduceIdenticalFacesAndEventLogs()
        {
            var a = TestKit.PlayFullMatch(424242);
            var b = TestKit.PlayFullMatch(424242);
            Assert.AreEqual(a.EventLogHash(), b.EventLogHash());
            Assert.AreEqual(a.StateHash(), b.StateHash());
            Assert.AreEqual(a.EventLog.Count, b.EventLog.Count);
            Assert.AreEqual(string.Join(".", a.AcceptedCommands.Select(c => c.Encode())), string.Join(".", b.AcceptedCommands.Select(c => c.Encode())));
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentFaces()
        {
            var a = NewMatch(1);
            var b = NewMatch(2);
            a.Ok(MatchCommand.Spin(SideId.Player));
            b.Ok(MatchCommand.Spin(SideId.Player));
            bool anyDifferent = false;
            // Across several rounds of reels the two seeds must diverge.
            for (int r = 0; r < 5; r++) anyDifferent |= a.Side(SideId.Player).Reels[r].FaceIndex != b.Side(SideId.Player).Reels[r].FaceIndex;
            var c = NewMatch(3);
            c.Ok(MatchCommand.Spin(SideId.Player));
            for (int r = 0; r < 5; r++) anyDifferent |= a.Side(SideId.Player).Reels[r].FaceIndex != c.Side(SideId.Player).Reels[r].FaceIndex;
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void FirstSpin_AffectsAllFiveReels()
        {
            var m = NewMatch();
            var e = m.Ok(MatchCommand.Spin(SideId.Player)).Events.OfType(MatchEventType.ReelsSpun).Single();
            Assert.AreEqual(1, e.SpinNumber);
            Assert.IsTrue(e.Changed.All(c => c));
            Assert.IsTrue(m.Side(SideId.Player).Reels.All(r => r.FaceIndex >= 0 && r.FaceIndex < 8));
        }

        [Test]
        public void LockedReels_DoNotChangeOnNextSpin()
        {
            for (long seed = 0; seed < 40; seed++)
            {
                var m = NewMatch(seed);
                m.Ok(MatchCommand.Spin(SideId.Player));
                var before = m.Side(SideId.Player).Reels.Select(r => r.FaceIndex).ToArray();
                m.Ok(MatchCommand.SetReelLock(SideId.Player, 0, true));
                m.Ok(MatchCommand.SetReelLock(SideId.Player, 3, true));
                var e = m.Ok(MatchCommand.Spin(SideId.Player)).Events.OfType(MatchEventType.ReelsSpun).Single();
                var after = m.Side(SideId.Player).Reels.Select(r => r.FaceIndex).ToArray();
                Assert.AreEqual(before[0], after[0]);
                Assert.AreEqual(before[3], after[3]);
                CollectionAssert.AreEqual(new[] { false, true, true, false, true }, e.Changed.ToArray());
            }
        }

        [Test]
        public void UnlockingAReel_AllowsItToChangeAgain()
        {
            var m = NewMatch();
            m.Ok(MatchCommand.Spin(SideId.Player));
            m.Ok(MatchCommand.SetReelLock(SideId.Player, 1, true));
            var spin2 = m.Ok(MatchCommand.Spin(SideId.Player)).Events.OfType(MatchEventType.ReelsSpun).Single();
            Assert.IsFalse(spin2.Changed[1]);
            m.Ok(MatchCommand.SetReelLock(SideId.Player, 1, false));
            var spin3 = m.Ok(MatchCommand.Spin(SideId.Player)).Events.OfType(MatchEventType.ReelsSpun).Single();
            Assert.IsTrue(spin3.Changed[1]);
        }

        [Test]
        public void ARound_AcceptsNoMoreThanThreeSpins_AndThirdSpinFinalizes()
        {
            var m = NewMatch();
            m.Ok(MatchCommand.Spin(SideId.Player));
            m.Ok(MatchCommand.Spin(SideId.Player));
            var third = m.Ok(MatchCommand.Spin(SideId.Player)).Events;
            Assert.AreEqual(1, third.OfType(MatchEventType.SpinFinalized).Count);
            Assert.IsTrue(m.Side(SideId.Player).Committed);
            Assert.AreEqual(3, m.Side(SideId.Player).SpinsUsed);
            var rej = m.Rejected(MatchCommand.Spin(SideId.Player));
            Assert.AreEqual(RejectionCode.SideAlreadyCommitted, rej.Code);
        }

        [Test]
        public void LockingAllFiveReels_CanFinalizeEarly()
        {
            var m = NewMatch();
            m.Ok(MatchCommand.Spin(SideId.Player));
            for (int r = 0; r < 4; r++) m.Ok(MatchCommand.SetReelLock(SideId.Player, r, true));
            Assert.AreEqual(RejectionCode.NotAllReelsLocked, m.Rejected(MatchCommand.FinalizeSpin(SideId.Player)).Code);
            m.Ok(MatchCommand.SetReelLock(SideId.Player, 4, true));
            Assert.AreEqual(RejectionCode.AllReelsLocked, m.Rejected(MatchCommand.Spin(SideId.Player)).Code);
            var e = m.Ok(MatchCommand.FinalizeSpin(SideId.Player)).Events.OfType(MatchEventType.SpinFinalized).Single();
            Assert.AreEqual(1, e.SpinNumber);
            Assert.IsTrue(m.Side(SideId.Player).Committed);
        }

        [Test]
        public void EachReel_ProducesOnlyFacesInItsDefinition_WithRoughlyUniformFrequency()
        {
            foreach (ReelTier tier in System.Enum.GetValues(typeof(ReelTier)))
            {
                var counts = new int[5, 8];
                const int trials = 4000;
                for (int seed = 0; seed < trials; seed++)
                {
                    var m = Match.Start(MatchConfig.Standard(seed, ReferenceContent.Striker, ReferenceContent.Caster, tier: tier), TestKit.Catalog);
                    m.Ok(MatchCommand.Spin(SideId.Player));
                    for (int r = 0; r < 5; r++)
                    {
                        var face = m.FaceOf(SideId.Player, r);
                        Assert.IsTrue(TestKit.Catalog.ReelSet(tier).Reels[r].Faces.Contains(face));
                        counts[r, m.Side(SideId.Player).Reels[r].FaceIndex]++;
                    }
                }
                for (int r = 0; r < 5; r++)
                    for (int f = 0; f < 8; f++)
                        Assert.That(counts[r, f], Is.InRange(trials / 8 - 120, trials / 8 + 120), "tier " + tier + " reel " + r + " face " + f);
            }
        }

        [Test]
        public void LockingBeforeFirstSpin_IsRejected()
        {
            var m = NewMatch();
            Assert.AreEqual(RejectionCode.FirstSpinRequired, m.Rejected(MatchCommand.SetReelLock(SideId.Player, 0, true)).Code);
            Assert.AreEqual(RejectionCode.FirstSpinRequired, m.Rejected(MatchCommand.FinalizeSpin(SideId.Player)).Code);
        }

        [Test]
        public void InvalidCommands_AreRejectedWithoutMutation()
        {
            var m = NewMatch();
            m.Ok(MatchCommand.Spin(SideId.Player));
            Assert.AreEqual(RejectionCode.InvalidReelIndex, m.Rejected(MatchCommand.SetReelLock(SideId.Player, 5, true)).Code);
            Assert.AreEqual(RejectionCode.ReelAlreadyInState, m.Rejected(MatchCommand.SetReelLock(SideId.Player, 2, false)).Code);
            Assert.AreEqual(RejectionCode.SidesNotCommitted, m.Rejected(MatchCommand.ResolveRound()).Code);
            Assert.AreEqual("REEL_ALREADY_IN_STATE", new CommandRejection(1, RejectionCode.ReelAlreadyInState, "", MatchPhase.Spinning).ReasonCode);
        }

        [Test]
        public void NewRound_UnlocksReelsAndResetsSpins()
        {
            var m = TestKit.Start(TestKit.Scenario().Faces(1, null, null));
            m.PlayRound();
            var p = m.Side(SideId.Player);
            Assert.AreEqual(2, m.Round);
            Assert.AreEqual(0, p.SpinsUsed);
            Assert.IsFalse(p.Committed);
            Assert.IsTrue(p.Reels.All(r => !r.Locked));
        }

        [Test]
        public void EachSidesDraws_AreIndependentOfTheOtherSidesChoices()
        {
            var a = NewMatch(99);
            var b = NewMatch(99);
            a.Ok(MatchCommand.Spin(SideId.Player));
            b.Ok(MatchCommand.Spin(SideId.Player));
            b.Ok(MatchCommand.SetReelLock(SideId.Player, 0, true));
            b.Ok(MatchCommand.Spin(SideId.Player));
            a.Ok(MatchCommand.Spin(SideId.Opponent));
            b.Ok(MatchCommand.Spin(SideId.Opponent));
            for (int r = 0; r < 5; r++)
                Assert.AreEqual(a.Side(SideId.Opponent).Reels[r].FaceIndex, b.Side(SideId.Opponent).Reels[r].FaceIndex);
        }
    }
}
