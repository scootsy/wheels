using System.Linq;
using NUnit.Framework;
using Tabletop.Application;
using Tabletop.Domain;
using Tabletop.Infrastructure;
using UnityEngine;
using static Tabletop.Domain.ReferenceContent;

namespace Tabletop.Tests.EditMode
{
    /// <summary>Match controller, selection, preview, and authoring-to-domain mapping.</summary>
    public class ApplicationTests
    {
        private static MatchSession NewSession(long seed = 1234)
        {
            long next = seed;
            return new MatchSession(TestKit.Catalog, () => next++);
        }

        [Test]
        public void Selection_ConfirmRequiresTwoUnits_AndDuplicatesAreRejectedWithoutLosingTheValidSlot()
        {
            var sel = new UnitSelection(TestKit.Catalog);
            Assert.AreEqual(RejectionCode.UnitSelectionIncomplete, sel.Validate().Code);
            Assert.IsNull(sel.Toggle(Striker));
            Assert.AreEqual(RejectionCode.UnitSelectionIncomplete, sel.Validate().Code, "one unit is not enough");
            var dup = sel.Assign(1, Striker);
            Assert.AreEqual(RejectionCode.DuplicateUnitNotAllowed, dup.Code);
            Assert.AreEqual(Striker, sel.Slots[0], "valid slot kept");
            Assert.IsNull(sel.Slots[1]);
            Assert.IsNull(sel.Toggle(Caster));
            Assert.IsNull(sel.Validate());
            sel.Swap();
            Assert.AreEqual(Caster, sel.Slots[0]);
            Assert.AreEqual(Striker, sel.Slots[1]);
            Assert.IsNull(sel.Toggle(Caster), "selecting an assigned unit removes it");
            Assert.IsNull(sel.Slots[0]);
        }

        [Test]
        public void Selection_HidesDeveloperUnitsInNormalMode()
        {
            var sel = new UnitSelection(TestKit.Catalog);
            CollectionAssert.AreEquivalent(new[] { Striker, Caster }, sel.Available(false).Select(u => u.Id));
            Assert.AreEqual(RejectionCode.ConfigInvalid, sel.Toggle(Shade).Code);
            Assert.AreEqual(7, sel.Available(true).Count);
        }

        [Test]
        public void Session_FullFlowThroughEveryUxState()
        {
            var s = NewSession();
            var seen = new System.Collections.Generic.List<UxState>();
            s.StateChanged += st => seen.Add(st);
            s.ContinueFromSetup();
            s.Selection.Toggle(Striker);
            s.Selection.Toggle(Caster);
            Assert.IsTrue(s.ConfirmUnits());
            Assert.AreEqual(UxState.RoundReady, s.State);
            // Opponent defaults: Standard AI, Copper, Striker + Caster.
            Assert.AreEqual(ControllerIds.AiStandard, s.Match.Config.Sides[1].ControllerId);
            Assert.AreEqual(ReelTier.Copper, s.Match.Config.Sides[1].ReelTier);

            int guard = 0;
            while (s.State != UxState.MatchResult && guard++ < 2000)
            {
                switch (s.State)
                {
                    case UxState.RoundReady: Assert.IsTrue(s.RequestSpin()); break;
                    case UxState.Spinning: while (s.DequeueEvent() != null) { } s.NotifySpinPresented(); break;
                    case UxState.SpinDecision: Assert.IsTrue(s.RequestSpin()); break;
                    case UxState.AiCommit: s.RunAiCommit(); break;
                    case UxState.Reveal: while (s.DequeueEvent() != null) { } s.NotifyRevealPresented(); break;
                    case UxState.Resolving: while (s.DequeueEvent() != null) { } s.NotifyResolutionPresented(); break;
                }
            }
            Assert.AreEqual(UxState.MatchResult, s.State);
            foreach (var st in new[] { UxState.UnitSelect, UxState.RoundReady, UxState.Spinning, UxState.SpinDecision, UxState.AiCommit, UxState.Reveal, UxState.Resolving, UxState.MatchResult })
                CollectionAssert.Contains(seen, st);
            var replay = ReplayRecord.Decode(s.ReplayText);
            Assert.IsNull(replay.Verify(TestKit.Catalog, out _));
        }

        [Test]
        public void Session_RejectsCommandsInTheWrongStateOrWhilePaused()
        {
            var s = NewSession();
            s.ContinueFromSetup();
            Assert.IsFalse(s.ConfirmUnits());
            Assert.AreEqual(RejectionCode.UnitSelectionIncomplete, s.LastRejection.Code);
            s.Selection.Toggle(Striker);
            s.Selection.Toggle(Caster);
            s.ConfirmUnits();
            var hash = s.Match.StateHash();
            Assert.IsFalse(s.RequestToggleLock(0));
            Assert.AreEqual(RejectionCode.FirstSpinRequired, s.LastRejection.Code);
            Assert.AreEqual("Spin all reels before locking.", s.LastStatus);
            s.SetPaused(true);
            Assert.IsFalse(s.RequestSpin());
            Assert.AreEqual(hash, s.Match.StateHash());
            s.SetPaused(false);
            Assert.IsTrue(s.RequestSpin());
        }

        [Test]
        public void Session_RematchUsesANewSeed_AndChangeUnitsKeepsSelection()
        {
            var s = NewSession(50);
            s.ContinueFromSetup();
            s.Selection.Toggle(Caster);
            s.Selection.Toggle(Striker);
            s.ConfirmUnits();
            long first = s.Match.Seed;
            s.Rematch();
            Assert.AreNotEqual(first, s.Match.Seed);
            Assert.AreEqual(UxState.RoundReady, s.State);
            s.ChangeUnits();
            Assert.AreEqual(UxState.UnitSelect, s.State);
            Assert.AreEqual(Caster, s.Selection.Slots[0]);
        }

        [Test]
        public void Preview_MatchesFinalEvaluation_WhenNoPriorityEffectIntervenes()
        {
            for (long seed = 0; seed < 60; seed++)
            {
                var m = Match.Start(MatchConfig.Standard(seed, Striker, Caster), TestKit.Catalog);
                m.Ok(MatchCommand.Spin(SideId.Player));
                var preview = OutcomePreview.Compute(m.Snapshot().Side(SideId.Player), m.ReelDefinitions(SideId.Player), TestKit.Catalog);
                for (int r = 0; r < 5; r++) m.Ok(MatchCommand.SetReelLock(SideId.Player, r, true));
                m.Ok(MatchCommand.FinalizeSpin(SideId.Player));
                m.Ok(MatchCommand.Spin(SideId.Opponent));
                for (int r = 0; r < 5; r++) m.Ok(MatchCommand.SetReelLock(SideId.Opponent, r, true));
                m.Ok(MatchCommand.FinalizeSpin(SideId.Opponent));
                var events = m.Ok(MatchCommand.ResolveRound()).Events;
                var barrier = events.Where(e => e.Type == MatchEventType.BarrierBuilt && e.Side == 0 && e.Stage == 2).Select(e => e.After).DefaultIfEmpty(0).First();
                Assert.AreEqual(preview.BarrierAfter, barrier, "seed " + seed);
                for (int u = 0; u < 2; u++)
                {
                    // After stage 3, energy equals the preview; readiness matches when nothing earlier delays it.
                    var afterEnergy = events.Where(e => e.Type == MatchEventType.EnergyGranted && e.Side == 0 && e.Slot == u && e.Stage == 3).Select(e => e.After)
                        .DefaultIfEmpty(0).First();
                    if (preview.Units[u].EnergyGain > 0) Assert.AreEqual(preview.Units[u].EnergyAfter, afterEnergy, "seed " + seed + " unit " + u);
                    bool acted = events.Any(e => e.Type == MatchEventType.UnitActivated && e.Side == 0 && e.Slot == u);
                    Assert.AreEqual(preview.Units[u].ReadyBeforeResolution, acted, "seed " + seed + " unit " + u);
                }
            }
        }

        [Test]
        public void Preview_LockedOnly_CountsJustTheLockedReels()
        {
            for (long seed = 0; seed < 40; seed++)
            {
                var m = Match.Start(MatchConfig.Standard(seed, Striker, Caster), TestKit.Catalog);
                m.Ok(MatchCommand.Spin(SideId.Player));
                m.Ok(MatchCommand.SetReelLock(SideId.Player, 1, true));
                m.Ok(MatchCommand.SetReelLock(SideId.Player, 3, true));
                var side = m.Snapshot().Side(SideId.Player);
                var defs = m.ReelDefinitions(SideId.Player);
                var locked = OutcomePreview.Compute(side, defs, TestKit.Catalog, lockedOnly: true);
                var t = SymbolEvaluator.Evaluate(new[] { 1, 3 }.Select(r => defs[r].Faces[side.Reels[r].FaceIndex]).ToList());
                Assert.IsTrue(locked.LockedOnly);
                Assert.IsTrue(locked.HasFaces);
                for (int u = 0; u < 2; u++)
                {
                    Assert.AreEqual(t.Symbols((Channel)u), locked.Units[u].Symbols, "seed " + seed);
                    Assert.AreEqual(t.Energy((Channel)u), locked.Units[u].EnergyGain, "seed " + seed);
                }
                Assert.AreEqual(t.Hammer, locked.Hammers, "seed " + seed);

                // With all five locked it is exactly the full preview.
                foreach (int r in new[] { 0, 2, 4 }) m.Ok(MatchCommand.SetReelLock(SideId.Player, r, true));
                side = m.Snapshot().Side(SideId.Player);
                var all = OutcomePreview.Compute(side, defs, TestKit.Catalog, lockedOnly: true);
                var full = OutcomePreview.Compute(side, defs, TestKit.Catalog);
                for (int u = 0; u < 2; u++)
                {
                    Assert.AreEqual(full.Units[u].EnergyGain, all.Units[u].EnergyGain);
                    Assert.AreEqual(full.Units[u].Wasted, all.Units[u].Wasted);
                }
                Assert.AreEqual(full.BarrierAfter, all.BarrierAfter);
            }
        }

        [Test]
        public void Preview_NothingLocked_ShowsNoIncomingEnergy()
        {
            var m = Match.Start(MatchConfig.Standard(5, Striker, Caster), TestKit.Catalog);
            m.Ok(MatchCommand.Spin(SideId.Player));
            var p = OutcomePreview.Compute(m.Snapshot().Side(SideId.Player), m.ReelDefinitions(SideId.Player), TestKit.Catalog, lockedOnly: true);
            Assert.IsFalse(p.HasFaces);
            for (int u = 0; u < 2; u++) { Assert.AreEqual(0, p.Units[u].Symbols); Assert.AreEqual(0, p.Units[u].EnergyGain); Assert.AreEqual(0, p.Units[u].Wasted); }
        }

        [Test]
        public void Preview_SymbolsToNextPoint_FollowsTheThreeSymbolThreshold()
        {
            // 3 symbols give the first point, each further symbol one more (RULES_SPEC 5.2).
            Assert.AreEqual(3, OutcomePreview.SymbolsToNextPoint(0));
            Assert.AreEqual(2, OutcomePreview.SymbolsToNextPoint(1));
            Assert.AreEqual(1, OutcomePreview.SymbolsToNextPoint(2));
            Assert.AreEqual(1, OutcomePreview.SymbolsToNextPoint(3));
            Assert.AreEqual(1, OutcomePreview.SymbolsToNextPoint(6));
        }

        [Test]
        public void Session_LockingAllFive_WaitsForConfirmation()
        {
            var s = NewSession(77);
            s.ContinueFromSetup();
            s.Selection.Toggle(Striker);
            s.Selection.Toggle(Caster);
            s.ConfirmUnits();
            Assert.IsTrue(s.RequestSpin());
            while (s.DequeueEvent() != null) { }
            s.NotifySpinPresented();
            Assert.AreEqual(UxState.SpinDecision, s.State);
            for (int r = 0; r < 5; r++) Assert.IsTrue(s.RequestToggleLock(r));
            Assert.AreEqual(UxState.SpinDecision, s.State, "an accidental fifth lock must not end the turn");
            Assert.IsTrue(s.CanFinalize);
            Assert.IsTrue(s.RequestToggleLock(2));
            Assert.IsFalse(s.CanFinalize, "unlocking one goes back to spinning");
            Assert.IsTrue(s.RequestToggleLock(2));
            Assert.AreEqual(1, s.Match.Snapshot().Side(SideId.Player).SpinsUsed);
            Assert.IsTrue(s.RequestSpin(), "the lever confirms");
            Assert.AreEqual(UxState.AiCommit, s.State);
            Assert.AreEqual(1, s.Match.Snapshot().Side(SideId.Player).SpinsUsed, "confirming is not a spin");
        }

        [Test]
        public void EventNarrator_DescribesEveryEventWithItsDelta()
        {
            var m = TestKit.PlayFullMatch(777);
            foreach (var e in m.EventLog)
            {
                var text = EventNarrator.Describe(e, m);
                Assert.IsFalse(string.IsNullOrEmpty(text));
                if (e.Type == MatchEventType.CrownDamaged || e.Type == MatchEventType.BarrierDamaged || e.Type == MatchEventType.EnergyGranted)
                    StringAssert.Contains(e.Before + " -> " + e.After, text);
            }
        }

        [Test]
        public void ContentAsset_MapsToTheReferenceCatalog()
        {
            var asset = ScriptableObject.CreateInstance<ContentCatalogAsset>();
            asset.PopulateFromReference();
            var catalog = asset.ToCatalog(out var errors);
            CollectionAssert.IsEmpty(errors);
            Assert.AreEqual(TestKit.Catalog.ContentHash, catalog.ContentHash);

            asset.reelSets[0].reels[0] = "S,D,S";
            Assert.IsNull(asset.ToCatalog(out errors), "invalid authoring data refuses to build");
            Assert.IsNotEmpty(errors);
            Object.DestroyImmediate(asset);
        }

#if UNITY_EDITOR
        [Test]
        public void ProjectContentAsset_IsValidAndMatchesReference()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentCatalogAsset>("Assets/Game/Content/ContentCatalog.asset");
            Assert.IsNotNull(asset, "content asset exists");
            var catalog = asset.ToCatalog(out var errors);
            CollectionAssert.IsEmpty(errors);
            Assert.AreEqual(TestKit.Catalog.ContentHash, catalog.ContentHash);
        }

        [Test]
        public void ProjectSettings_UseInputSystemOnly()
        {
            var so = new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            Assert.AreEqual(1, so.FindProperty("activeInputHandler").intValue, "Active Input Handling = Input System Package (New)");
        }

        [Test]
        public void GameplayCode_DoesNotUseLegacyInput()
        {
            foreach (var file in System.IO.Directory.GetFiles("Assets/Game", "*.cs", System.IO.SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                var text = System.IO.File.ReadAllText(file);
                foreach (var banned in new[] { "Input.GetKey", "Input.GetButton", "Input.GetAxis", "KeyCode.", "UnityEngine.Random" })
                {
                    if (banned == "UnityEngine.Random" && !file.Replace('\\', '/').Contains("/Domain/")) continue;
                    StringAssert.DoesNotContain(banned, text, file);
                }
            }
        }
#endif
    }
}
