using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tabletop.Domain;
using static Tabletop.Domain.ReferenceContent;

namespace Tabletop.Tests.EditMode
{
    /// <summary>AI legality and information isolation, replay reproduction, content and configuration validation.</summary>
    public class AiReplayContentTests
    {
        // ------------------------------------------------------------ AI

        [TestCase(ControllerIds.AiLearner)]
        [TestCase(ControllerIds.AiStandard)]
        [TestCase(ControllerIds.AiExpert)]
        public void AiProfiles_CompleteMatchesUsingOnlyLegalCommands(string profile)
        {
            for (long seed = 1; seed <= 25; seed++)
            {
                // AiTurnRunner throws on any rejected command.
                var m = TestKit.PlayFullMatch(seed, playerProfile: profile, opponentProfile: ControllerIds.AiStandard);
                Assert.AreEqual(MatchPhase.Ended, m.Phase);
                Assert.AreNotEqual(Winner.None, m.Winner);
                AssertInvariants(m);
            }
        }

        [Test]
        public void AllTiersAndUnits_ProduceValidCompleteMatches()
        {
            var ids = TestKit.Catalog.Units.Select(u => u.Id).ToArray();
            long seed = 100;
            foreach (ReelTier tier in System.Enum.GetValues(typeof(ReelTier)))
                for (int i = 0; i < ids.Length; i++)
                {
                    var m = TestKit.PlayFullMatch(seed++, ids[i], ids[(i + 1) % ids.Length], ids[(i + 2) % ids.Length], ids[(i + 3) % ids.Length], tier);
                    Assert.AreEqual(MatchPhase.Ended, m.Phase);
                    AssertInvariants(m);
                }
        }

        private static void AssertInvariants(Match m)
        {
            foreach (var e in m.EventLog)
                foreach (var side in e.StateAfter.Sides)
                {
                    Assert.That(side.CrownHp, Is.InRange(0, RulesConstants.HardCrownCap));
                    Assert.That(side.Barrier, Is.InRange(0, RulesConstants.MaxBarrier));
                    foreach (var u in side.Units)
                    {
                        Assert.That(u.Energy, Is.InRange(0, u.EnergyCost));
                        Assert.That(u.Xp, Is.InRange(0, RulesConstants.XpThreshold));
                    }
                }
        }

        [Test]
        public void AiDecisionInput_ExcludesPlayerCurrentRoundFacesAndLocks()
        {
            var a = Match.Start(MatchConfig.Standard(77, Striker, Caster), TestKit.Catalog);
            var b = Match.Start(MatchConfig.Standard(77, Striker, Caster), TestKit.Catalog);
            var roundStartA = a.Snapshot();
            var roundStartB = b.Snapshot();

            // The human plays differently in each match before the AI acts.
            a.Ok(MatchCommand.Spin(SideId.Player));
            for (int r = 0; r < 5; r++) a.Ok(MatchCommand.SetReelLock(SideId.Player, r, true));
            a.Ok(MatchCommand.FinalizeSpin(SideId.Player));
            b.Ok(MatchCommand.Spin(SideId.Player));
            b.Ok(MatchCommand.SetReelLock(SideId.Player, 2, true));
            b.Ok(MatchCommand.Spin(SideId.Player));
            b.Ok(MatchCommand.Spin(SideId.Player));

            var inA = AiDecisionInput.Capture(roundStartA, SideId.Opponent, TestKit.Catalog, a.ReelDefinitions(SideId.Opponent));
            var inB = AiDecisionInput.Capture(roundStartB, SideId.Opponent, TestKit.Catalog, b.ReelDefinitions(SideId.Opponent));
            Assert.AreEqual(inA.CanonicalText(), inB.CanonicalText());

            var policy = AiProfiles.Create(ControllerIds.AiStandard);
            var cmdA = AiTurnRunner.PlayTurn(policy, inA, a.Execute).Select(c => c.Encode()).ToArray();
            var cmdB = AiTurnRunner.PlayTurn(policy, inB, b.Execute).Select(c => c.Encode()).ToArray();
            CollectionAssert.AreEqual(cmdA, cmdB, "AI choices must not depend on the player's current-round play");
            Assert.IsTrue(cmdA.All(c => c[1] == '1'), "AI only commands its own side");
        }

        [Test]
        public void AiDecisionInput_HasNoAccessToReelFacesOrLocks()
        {
            var props = typeof(AiDecisionInput).GetProperties().Select(p => p.PropertyType)
                .Concat(typeof(AiSideView).GetProperties().Select(p => p.PropertyType))
                .Concat(typeof(AiUnitView).GetProperties().Select(p => p.PropertyType)).ToList();
            Assert.IsFalse(props.Contains(typeof(MatchSnapshot)));
            Assert.IsFalse(props.Contains(typeof(SideSnapshot)));
            Assert.IsFalse(props.Any(t => t == typeof(IReadOnlyList<ReelSnapshot>)));
            Assert.IsFalse(props.Contains(typeof(Match)));
        }

        [Test]
        public void StandardAi_TakesAGuaranteedLethalResult()
        {
            // Opponent Crown at 3; AI (as player side) holds a ready-making Striker result.
            var policy = (EvaluatingAiPolicy)AiProfiles.Create(ControllerIds.AiStandard);
            var sc = TestKit.Scenario().SetCrown(SideId.Opponent, 3).SetUnit(SideId.Player, 0, Rank.Bronze, 0, 2)
                .ForceCodes(SideId.Player, 1, 1, TestKit.Copper, "S", "SS", "D", "D", "-");
            var m = TestKit.Start(sc);
            var input = AiDecisionInput.Capture(m.Snapshot(), SideId.Player, TestKit.Catalog, m.ReelDefinitions(SideId.Player));
            m.Ok(MatchCommand.Spin(SideId.Player));
            var faces = m.Side(SideId.Player).Reels.Select(r => r.FaceIndex).ToArray();
            var locks = policy.ChooseLocks(input, faces, 1);
            // Reels 1-2 carry the three A symbols that make the Striker ready for a lethal hit.
            // The AI must keep them; rerolling the rest cannot remove lethal.
            Assert.IsTrue(locks[0] && locks[1], "AI must keep the guaranteed lethal symbols");
            Assert.Greater(policy.Utility(input, m.Side(SideId.Player).Reels.Select((r, i) => TestKit.Copper.Reels[i].Faces[r.FaceIndex]).ToList()), 9000);
        }

        [Test]
        public void ChangingNothingButPresentation_DoesNotChangeAiChoicesOrRng()
        {
            // The simulation has no notion of presentation speed; two identical runs are identical.
            var a = TestKit.PlayFullMatch(31337);
            var b = TestKit.PlayFullMatch(31337);
            Assert.AreEqual(a.EventLogHash(), b.EventLogHash());
        }

        // ------------------------------------------------------------ Replay

        [Test]
        public void Replay_RoundTripsAndReproducesEventsAndFinalHash()
        {
            for (long seed = 500; seed < 510; seed++)
            {
                var original = TestKit.PlayFullMatch(seed);
                var text = ReplayRecord.FromMatch(original).Encode();
                StringAssert.StartsWith("TTR1;0.1.0;" + TestKit.Catalog.ContentHash + ";" + seed + ";", text);
                var decoded = ReplayRecord.Decode(text);
                Assert.IsNull(decoded.Verify(TestKit.Catalog, out var replayed));
                Assert.AreEqual(original.EventLogHash(), replayed.EventLogHash());
                Assert.AreEqual(original.StateHash(), replayed.StateHash());
                Assert.AreEqual(text, ReplayRecord.FromMatch(replayed).Encode());
            }
        }

        [Test]
        public void Replay_WithScenario_RoundTrips()
        {
            var sc = TestKit.Scenario().Faces(1, new[] { "S", "SS", "S", "S", "D" }, null).SetUnit(SideId.Opponent, 1, Rank.Silver, 2, 1).SetCrown(SideId.Player, 7);
            var m = TestKit.Start(sc);
            m.PlayRound();
            var text = ReplayRecord.FromMatch(m).Encode();
            Assert.IsNull(ReplayRecord.Decode(text).Verify(TestKit.Catalog, out var replayed));
            Assert.AreEqual(m.EventLogHash(), replayed.EventLogHash());
        }

        [Test]
        public void Replay_DetectsTamperingAndContentMismatch()
        {
            var original = TestKit.PlayFullMatch(9001);
            var rec = ReplayRecord.FromMatch(original);
            var badHash = new ReplayRecord(rec.RulesVersion, rec.ContentHash, rec.Seed, rec.Player, rec.Opponent, rec.AllowDuplicateUnits, rec.Commands, "0000000000000000");
            StringAssert.Contains("hash mismatch", badHash.Verify(TestKit.Catalog, out _));
            var badSeed = new ReplayRecord(rec.RulesVersion, rec.ContentHash, rec.Seed + 1, rec.Player, rec.Opponent, rec.AllowDuplicateUnits, rec.Commands, rec.FinalStateHash);
            Assert.IsNotNull(badSeed.Verify(TestKit.Catalog, out _));
            var badContent = new ReplayRecord(rec.RulesVersion, "ffffffffffffffff", rec.Seed, rec.Player, rec.Opponent, rec.AllowDuplicateUnits, rec.Commands, rec.FinalStateHash);
            StringAssert.Contains("Content hash", badContent.Verify(TestKit.Catalog, out _));
        }

        // ------------------------------------------------------------ Content and configuration

        [Test]
        public void ReferenceContent_IsValid_AndHashIsStable()
        {
            CollectionAssert.IsEmpty(TestKit.Catalog.Validate());
            Assert.AreEqual(ReferenceContent.Build().ContentHash, TestKit.Catalog.ContentHash);
            Assert.AreEqual(16, TestKit.Catalog.ContentHash.Length);
            Assert.AreEqual("cbf29ce484222325", StableHash.Fnv1a64Hex(""), "FNV-1a offset basis");
        }

        [Test]
        public void ReelFaces_MatchTheSpecExactly()
        {
            var copper = TestKit.Copper;
            Assert.AreEqual("S,D,S,S+,D,H,DD+,H", string.Join(",", copper.Reels[0].Faces.Select(f => f.Code)));
            Assert.AreEqual("S+,D,SS,D+,S,H,DD,HH", string.Join(",", copper.Reels[1].Faces.Select(f => f.Code)));
            Assert.AreEqual("S+,D,D+,S,D,HH,SS,HH", string.Join(",", copper.Reels[2].Faces.Select(f => f.Code)));
            Assert.AreEqual("S,D,S+,D,HH,S,D+,HH", string.Join(",", copper.Reels[3].Faces.Select(f => f.Code)));
            Assert.AreEqual("S,D,H,-,-,S,D,-", string.Join(",", copper.Reels[4].Faces.Select(f => f.Code)));
            Assert.AreEqual("S,DD+,HHH,SS+,DD+,SS+,D,HH", string.Join(",", TestKit.Catalog.ReelSet(ReelTier.Platinum).Reels[4].Faces.Select(f => f.Code)));
        }

        [Test]
        public void OnlyStrikerAndCaster_ArePlayerFacing()
        {
            CollectionAssert.AreEquivalent(new[] { Striker, Caster }, TestKit.Catalog.Units.Where(u => u.PlayerFacing).Select(u => u.Id));
            var s = TestKit.Catalog.Unit(Striker);
            Assert.AreEqual(new[] { 3, 3, 3 }, s.Ranks.Select(r => r.EnergyCost).ToArray());
            Assert.AreEqual(new[] { 3, 5, 7 }, s.Ranks.Select(r => r.CrownDamage).ToArray());
            var c = TestKit.Catalog.Unit(Caster);
            Assert.AreEqual(new[] { 5, 4, 4 }, c.Ranks.Select(r => r.EnergyCost).ToArray());
            Assert.AreEqual(new[] { 2, 3, 5 }, c.Ranks.Select(r => r.BarrierDamage).ToArray());
        }

        [Test]
        public void InvalidContent_IsRejected()
        {
            var units = ReferenceContent.BuildUnits();
            var sets = ReferenceContent.BuildReelSets();
            var shortReel = new ReelDefinition("reel_1", sets[0].Reels[0].Faces.Take(7).ToList());
            var badSet = new ReelSetDefinition(ReelTier.Copper, new[] { shortReel, sets[0].Reels[1], sets[0].Reels[2], sets[0].Reels[3], sets[0].Reels[4] });
            var catalog = new ContentCatalog(RulesConstants.RulesVersion, units, new[] { badSet });
            Assert.IsNotEmpty(catalog.Validate());
            Assert.IsNull(Match.Start(MatchConfig.Standard(1, Striker, Caster), catalog, out var rej));
            Assert.AreEqual(RejectionCode.ConfigInvalid, rej.Code);

            units.Add(new UnitDefinition("broken", "Broken", "", "", ActionKind.Projectiles, new[] { 1 }, new[] { new UnitRankStats(0, 1, 1) }, false));
            Assert.IsNotEmpty(new ContentCatalog(RulesConstants.RulesVersion, units, sets).Validate());
            Assert.IsNotEmpty(new ContentCatalog("9.9.9", ReferenceContent.BuildUnits(), sets).Validate());
        }

        [Test]
        public void InvalidConfigurations_AreRejected()
        {
            var cat = TestKit.Catalog;
            CommandRejection Reject(MatchConfig cfg) { Assert.IsNull(Match.Start(cfg, cat, out var r)); return r; }

            Assert.AreEqual(RejectionCode.DuplicateUnitNotAllowed, Reject(MatchConfig.Standard(1, Striker, Striker)).Code);
            Assert.AreEqual(RejectionCode.UnitSelectionIncomplete, Reject(MatchConfig.Standard(1, Striker, null)).Code);
            Assert.AreEqual(RejectionCode.ConfigInvalid, Reject(MatchConfig.Standard(1, Striker, "nobody")).Code);
            Assert.AreEqual(RejectionCode.ConfigInvalid, Reject(MatchConfig.Standard(-1, Striker, Caster)).Code);
            Assert.AreEqual(RejectionCode.ConfigInvalid, Reject(MatchConfig.Standard(MatchConfig.MaxSeed + 1, Striker, Caster)).Code);
            Assert.AreEqual(RejectionCode.ConfigInvalid, Reject(new MatchConfig("0.0.9", 1,
                new SideConfig(ControllerIds.Human, ReelTier.Copper, Striker, Caster), new SideConfig(ControllerIds.AiStandard, ReelTier.Copper, Striker, Caster))).Code);
            Assert.AreEqual(RejectionCode.ConfigInvalid, Reject(new MatchConfig(RulesConstants.RulesVersion, 1,
                new SideConfig(ControllerIds.Human, ReelTier.Copper, Striker, Caster), new SideConfig(ControllerIds.AiStandard, ReelTier.Gold, Striker, Caster))).Code);
            Assert.AreEqual(RejectionCode.ConfigInvalid, Reject(new MatchConfig(RulesConstants.RulesVersion, 1,
                new SideConfig("robot", ReelTier.Copper, Striker, Caster), new SideConfig(ControllerIds.AiStandard, ReelTier.Copper, Striker, Caster))).Code);
            Assert.IsNotNull(Match.Start(MatchConfig.Standard(1, Striker, Striker, allowDuplicateUnits: true), cat, out _));
        }

        [Test]
        public void MatchStart_InitializesBothSidesCorrectly()
        {
            var m = Match.Start(MatchConfig.Standard(5, Caster, Striker), TestKit.Catalog);
            var snap = m.Snapshot();
            Assert.AreEqual(1, snap.Round);
            Assert.AreEqual(MatchPhase.Spinning, snap.Phase);
            foreach (var side in snap.Sides)
            {
                Assert.AreEqual(10, side.CrownHp);
                Assert.AreEqual(0, side.Barrier);
                Assert.AreEqual(0, side.SpinsUsed);
                foreach (var u in side.Units)
                {
                    Assert.AreEqual(Rank.Bronze, u.Rank);
                    Assert.AreEqual(0, u.Xp);
                    Assert.AreEqual(0, u.Energy);
                }
            }
            Assert.AreEqual(Caster, snap.Side(SideId.Player).Units[0].DefinitionId);
            Assert.AreEqual(Channel.A, snap.Side(SideId.Player).Units[0].Channel);
            Assert.AreEqual(Striker, snap.Side(SideId.Player).Units[1].DefinitionId);
        }

        [Test]
        public void SwappingChannels_ChangesEnergyRouting()
        {
            // Five A symbols: with Striker on A it fires; with Caster on A it only gains 3 of 5.
            var faces = new[] { "S", "SS", "S", "S", "D" };
            var ab = TestKit.Start(TestKit.Scenario().Faces(1, faces, null), pA: Striker, pB: Caster);
            ab.PlayRound();
            var ba = TestKit.Start(TestKit.Scenario().Faces(1, faces, null), pA: Caster, pB: Striker);
            ba.PlayRound();
            Assert.AreEqual(7, ab.Side(SideId.Opponent).CrownHp);
            Assert.AreEqual(10, ba.Side(SideId.Opponent).CrownHp);
            Assert.AreEqual(3, ba.Unit(SideId.Player, 0).Energy);
        }

        [Test]
        public void EventSnapshots_AreConsistentWithDeltas()
        {
            var m = TestKit.PlayFullMatch(2024);
            MatchSnapshot prev = null;
            foreach (var e in m.EventLog)
            {
                if (prev != null)
                {
                    if (e.Type == MatchEventType.CrownDamaged || e.Type == MatchEventType.CrownHealed)
                    {
                        Assert.AreEqual(e.Before, prev.Sides[e.TargetSide].CrownHp);
                        Assert.AreEqual(e.After, e.StateAfter.Sides[e.TargetSide].CrownHp);
                    }
                    if (e.Type == MatchEventType.BarrierDamaged)
                        Assert.AreEqual(e.After, e.StateAfter.Sides[e.TargetSide].Barrier);
                    if (e.Type == MatchEventType.BarrierBuilt)
                        Assert.AreEqual(e.After, e.StateAfter.Sides[e.Side].Barrier);
                    if (e.Type == MatchEventType.EnergyGranted || e.Type == MatchEventType.UnitActivated)
                        Assert.AreEqual(e.After, e.StateAfter.Sides[e.Side].Units[e.Slot].Energy);
                }
                prev = e.StateAfter;
            }
        }
    }
}
