using System;
using System.Collections.Generic;
using Tabletop.Application;
using Tabletop.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>Setup, unit selection, and modal overlays.</summary>
    public sealed partial class MatchApp
    {
        private GameObject _setupScreen, _selectScreen, _boardScreen;
        private Button _setupContinue, _setupDev, _setupQuit;
        private GameObject _devPanel;
        private InputField _seedField;
        private Button _devTier, _devAi, _devOppA, _devOppB, _devScenario, _devVerify;
        private Text _setupOpponent, _setupStatus, _devStatus;

        private readonly List<Button> _cardButtons = new List<Button>();
        private readonly List<string> _cardUnits = new List<string>();
        private readonly List<Text> _cardStatus = new List<Text>();
        private Button _slotA, _slotB, _swap, _confirm, _selectBack;
        private Text _selectOpponent, _selectStatus;

        private GameObject _inspectOverlay, _helpOverlay, _pauseOverlay, _confirmOverlay, _resultOverlay, _errorOverlay;
        private Text _inspectText, _helpText, _confirmText, _resultTitle, _resultBody, _resultReplay, _errorText, _resultSub;
        private readonly Image[] _resultCards = new Image[2];
        private readonly Text[] _resultScore = new Text[2];
        private readonly Text[] _resultTeam = new Text[2];
        private Button _inspectClose, _helpClose, _pauseResume, _pauseReduced, _pauseShake, _pauseScale, _pauseWrap, _pauseSkip, _pauseRestart, _pauseExit;
        private Button _confirmYes, _confirmNo, _resultRematch, _resultChange, _resultCopy, _resultExit, _resultSameSeed, _errorCopy, _errorExit;
        private Action _confirmAction;
        private readonly Stack<(GameObject overlay, GameObject opener)> _overlayStack = new Stack<(GameObject, GameObject)>();
        private int _scenarioIndex = -1;

        public GameObject SetupScreen => _setupScreen;
        public GameObject SelectScreen => _selectScreen;
        public GameObject BoardScreen => _boardScreen;
        public GameObject ResultOverlay => _resultOverlay;
        public GameObject PauseOverlay => _pauseOverlay;
        public GameObject HelpOverlay => _helpOverlay;
        public GameObject InspectOverlay => _inspectOverlay;
        public GameObject ErrorOverlay => _errorOverlay;
        public GameObject ConfirmOverlay => _confirmOverlay;
        public Button SetupContinueButton => _setupContinue;
        public Button ConfirmUnitsButton => _confirm;
        public Button SelectBackButton => _selectBack;
        public IReadOnlyList<Button> UnitCardButtons => _cardButtons;
        public Button ResultRematchButton => _resultRematch;
        public Button ResultChangeUnitsButton => _resultChange;
        public Button ResultCopyButton => _resultCopy;
        public Button ResultExitButton => _resultExit;
        public Text ResultTitle => _resultTitle;

        private void BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<FrameFitter>();
            go.AddComponent<GraphicRaycaster>();

            // Frame: a centered 1920x1080 area; wider/taller screens get decorative margins.
            _root = Ui.Rect("Frame", go.transform);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(1920, 1080);
        }

        private RectTransform _root;

        private GameObject Screen(string name)
        {
            var rt = Ui.Rect(name, _root);
            rt.Fill();
            return rt.gameObject;
        }

        // ------------------------------------------------------------------ setup

        private void BuildSetupScreen()
        {
            _setupScreen = Screen("SetupScreen");
            var bg = Ui.Panel("Bg", _setupScreen.transform, Theme.Background);
            bg.rectTransform.Fill();
            L("Title", _setupScreen.transform, "WHEELS", 64, TextAnchor.UpperCenter, Theme.Crown, FontStyle.Bold).rectTransform.Place(0, 40, 1920, 80);
            L("Subtitle", _setupScreen.transform, "First playable (M1)  -  placeholder art  -  rules " + RulesConstants.RulesVersion, 24, TextAnchor.UpperCenter, Theme.TextDim)
                .rectTransform.Place(0, 118, 1920, 34);
            var card = Ui.Panel("OpponentCard", _setupScreen.transform, Theme.Panel);
            card.rectTransform.Place(360, 180, 1200, 380);
            _setupOpponent = L("Opponent", card.transform, "", 26, TextAnchor.UpperLeft);
            _setupOpponent.rectTransform.Fill(24);

            _setupContinue = Ui.Button("Continue", _setupScreen.transform, "CONTINUE", () => { Session.Options.DeveloperMode = _devPanel.activeSelf; Session.ContinueFromSetup(); }, 32, Theme.ButtonPrimary);
            ((RectTransform)_setupContinue.transform).Place(760, 590, 400, 80);
            _setupDev = Ui.Button("DevMode", _setupScreen.transform, "DEVELOPER MODE: OFF", ToggleDev, 20);
            ((RectTransform)_setupDev.transform).Place(760, 690, 400, 56);
            _setupQuit = Ui.Button("Quit", _setupScreen.transform, "QUIT", QuitApp, 22);
            ((RectTransform)_setupQuit.transform).Place(760, 766, 400, 56);
            _setupStatus = L("Status", _setupScreen.transform, "", 22, TextAnchor.UpperCenter, Theme.Damage);
            _setupStatus.rectTransform.Place(0, 1030, 1920, 40);

            _devPanel = Ui.Panel("DeveloperPanel", _setupScreen.transform, Theme.PanelDark).gameObject;
            ((RectTransform)_devPanel.transform).Place(1200, 590, 700, 430);
            L("DevTitle", _devPanel.transform, "DEVELOPER SETTINGS", 22, TextAnchor.UpperLeft, Theme.Focus, FontStyle.Bold).rectTransform.Place(16, 10, 600, 30);
            L("SeedLabel", _devPanel.transform, "Seed (blank = random):", 18).rectTransform.Place(16, 48, 230, 40);
            var seedBg = Ui.Panel("Seed", _devPanel.transform, Color.white);
            seedBg.rectTransform.Place(250, 48, 430, 40);
            _seedField = seedBg.gameObject.AddComponent<InputField>();
            var seedText = Ui.Label("Text", seedBg.transform, "", 20, TextAnchor.MiddleLeft, Color.black);
            seedText.rectTransform.Fill(6);
            seedText.supportRichText = false;
            _seedField.textComponent = seedText;
            _seedField.contentType = InputField.ContentType.IntegerNumber;
            FocusFrame.Attach(_seedField);
            _devTier = DevButton("Tier", 96, CycleTier);
            _devAi = DevButton("AI", 146, CycleAi);
            _devOppA = DevButton("OppA", 196, () => CycleOpponent(0));
            _devOppB = DevButton("OppB", 246, () => CycleOpponent(1));
            _devScenario = DevButton("Scenario", 296, CycleScenario);
            _devVerify = DevButton("Verify", 346, VerifyClipboardReplay);
            _devVerify.SetText("VERIFY REPLAY FROM CLIPBOARD");
            _devStatus = L("DevStatus", _devPanel.transform, "", 16, TextAnchor.UpperLeft, Theme.TextDim);
            _devStatus.rectTransform.Place(16, 392, 670, 36);
            _devPanel.SetActive(false);
            LinkVertical(_setupContinue, _setupDev, _setupQuit);
        }

        private Button DevButton(string name, float y, UnityEngine.Events.UnityAction a)
        {
            var b = Ui.Button(name, _devPanel.transform, name, a, 18);
            ((RectTransform)b.transform).Place(16, y, 664, 42);
            return b;
        }

        private void ToggleDev()
        {
            bool on = !_devPanel.activeSelf;
            _devPanel.SetActive(on);
            Session.Options.DeveloperMode = on;
            _setupDev.SetText(on ? "DEVELOPER MODE: ON" : "DEVELOPER MODE: OFF");
            if (on) LinkVertical(_setupContinue, _setupDev, _setupQuit, _devTier, _devAi, _devOppA, _devOppB, _devScenario, _devVerify);
            else LinkVertical(_setupContinue, _setupDev, _setupQuit);
        }

        private void CycleTier() => Session.Options.Tier = (ReelTier)(((int)Session.Options.Tier + 1) % 6);

        private void CycleAi()
        {
            var ids = new[] { ControllerIds.AiLearner, ControllerIds.AiStandard, ControllerIds.AiExpert };
            Session.Options.AiProfile = ids[(Array.IndexOf(ids, Session.Options.AiProfile) + 1) % ids.Length];
        }

        private void CycleOpponent(int slot)
        {
            var units = Catalog.Units;
            string current = slot == 0 ? Session.Options.OpponentA : Session.Options.OpponentB;
            int idx = 0;
            for (int i = 0; i < units.Count; i++) if (units[i].Id == current) idx = i;
            string next = units[(idx + 1) % units.Count].Id;
            if (slot == 0) Session.Options.OpponentA = next; else Session.Options.OpponentB = next;
        }

        private void CycleScenario()
        {
            _scenarioIndex++;
            if (_scenarioIndex >= ScenarioLibrary.Names.Length) _scenarioIndex = -1;
            Session.Options.ScenarioName = _scenarioIndex < 0 ? null : ScenarioLibrary.Names[_scenarioIndex];
        }

        private void VerifyClipboardReplay()
        {
            string text = GUIUtility.systemCopyBuffer;
            try
            {
                var rec = ReplayRecord.Decode(text);
                var err = rec.Verify(Catalog, out var replayed);
                _devStatus.text = err == null
                    ? "REPLAY VERIFIED: seed " + rec.Seed + ", " + rec.Commands.Count + " commands, final hash " + replayed.StateHash()
                    : "REPLAY FAILED: " + err;
                Debug.Log("[Tabletop] Replay verification: " + _devStatus.text);
            }
            catch (Exception ex)
            {
                _devStatus.text = "Clipboard does not hold a replay: " + ex.Message;
            }
        }

        private void QuitApp()
        {
            // Came from the world (practice table): go back to the village instead of quitting.
            if (GameFlow.TitleShown && UnityEngine.Application.CanStreamedLevelBeLoaded(WorldSceneName))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(WorldSceneName);
                return;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private static string UnitSummary(UnitDefinition d, Rank rank = Rank.Bronze)
        {
            var s = d.Stats(rank);
            return d.DisplayName + " - " + d.Role + "\n   Cost " + s.EnergyCost + " energy. " + UnitPanelView.StatsLine(d, s).Replace("\n", ". ") + "\n   " + d.Description;
        }

        private void RenderSetupAndSelect()
        {
            if (_setupScreen.activeSelf)
            {
                var o = Session.Options;
                var (oppA, oppB, ai) = EffectiveOpponent();
                _setupOpponent.text = "OPPONENT: " + Session.OpponentName + "  (" + AiName(ai) + ")\n\n"
                    + "Their figurines (visible before you choose yours):\n"
                    + "  A / LEFT:  " + UnitSummary(Catalog.Unit(oppA)) + "\n\n"
                    + "  B / RIGHT: " + UnitSummary(Catalog.Unit(oppB)) + "\n\n"
                    + "Goal: reduce the enemy Crown from 10 to 0. Three spins per round; lock wheels to keep them.";
                _setupStatus.text = ConfigError != null ? "CONTENT ERROR - match cannot start:\n" + ConfigError : Session.LastStatus;
                _setupContinue.interactable = ConfigError == null;
                _setupQuit.SetText(GameFlow.TitleShown ? "BACK TO THE VILLAGE" : "QUIT");
                if (_devPanel.activeSelf)
                {
                    _devTier.SetText("WHEEL TIER: " + o.Tier.ToString().ToUpperInvariant() + "  (both sides)");
                    _devAi.SetText("AI PROFILE: " + AiName(o.AiProfile));
                    _devOppA.SetText("OPPONENT A: " + Catalog.Unit(o.OpponentA).DisplayName);
                    _devOppB.SetText("OPPONENT B: " + Catalog.Unit(o.OpponentB).DisplayName);
                    _devScenario.SetText("FORCED SCENARIO: " + (o.ScenarioName ?? "none"));
                    o.FixedSeed = long.TryParse(_seedField.text, out var seed) && seed >= 0 && seed <= MatchConfig.MaxSeed ? seed : (long?)null;
                }
            }
            if (_selectScreen.activeSelf)
            {
                var sel = Session.Selection;
                var o = Session.Options;
                var opp = EffectiveOpponent();
                _selectOpponent.text = "OPPONENT: " + Session.OpponentName.ToUpperInvariant() + " (" + AiName(opp.ai) + "):   A / LEFT  "
                    + Catalog.Unit(opp.a).DisplayName + "     B / RIGHT  " + Catalog.Unit(opp.b).DisplayName;
                _selectBack.SetText(InEncounter ? "LEAVE TABLE" : "BACK");
                for (int i = 0; i < _cardButtons.Count; i++)
                {
                    int slot = sel.SlotOf(_cardUnits[i]);
                    _cardStatus[i].text = slot < 0 ? "not assigned" : "ASSIGNED TO " + UnitSelection.SlotName(slot).ToUpperInvariant();
                    _cardStatus[i].color = slot < 0 ? Theme.TextDim : (slot == 0 ? Theme.ChannelA : Theme.ChannelB);
                }
                _slotA.SetText("[A] SLOT A / LEFT:  " + (sel.Slots[0] != null ? Catalog.Unit(sel.Slots[0]).DisplayName : "(empty)") + (sel.Slots[0] != null ? "   - press to clear" : ""));
                _slotB.SetText("<B> SLOT B / RIGHT:  " + (sel.Slots[1] != null ? Catalog.Unit(sel.Slots[1]).DisplayName : "(empty)") + (sel.Slots[1] != null ? "   - press to clear" : ""));
                _confirm.interactable = sel.IsComplete && sel.Validate() == null;
                _confirm.SetText(_confirm.interactable ? "CONFIRM AND START" : "CONFIRM (choose two figurines)");
                LinkBottomRow();
                _selectStatus.text = Session.LastRejection != null ? Session.LastStatus : sel.LastMessage;
            }
        }

        public static string AiName(string id)
        {
            switch (id)
            {
                case ControllerIds.AiLearner: return "Learner AI";
                case ControllerIds.AiExpert: return "Expert AI";
                default: return "Standard AI";
            }
        }

        // ------------------------------------------------------------------ unit selection

        private void BuildUnitSelectScreen()
        {
            _selectScreen = Screen("UnitSelectScreen");
            Ui.Panel("Bg", _selectScreen.transform, Theme.Background).rectTransform.Fill();
            L("Title", _selectScreen.transform, "CHOOSE YOUR TWO UNITS", 44, TextAnchor.UpperCenter, Theme.Text, FontStyle.Bold).rectTransform.Place(0, 24, 1920, 60);
            _selectOpponent = L("Opponent", _selectScreen.transform, "", 26, TextAnchor.UpperCenter, Theme.Enemy);
            _selectOpponent.rectTransform.Place(0, 92, 1920, 40);
            L("Hint", _selectScreen.transform, "Select a figurine to put it in the first empty slot (select again to remove). Use PUT IN A / B to replace a slot. Inspect (I / X) shows every rank.",
                18, TextAnchor.UpperCenter, Theme.TextDim).rectTransform.Place(160, 136, 1600, 30);
            _selectStatus = L("Status", _selectScreen.transform, "", 22, TextAnchor.UpperCenter, Theme.Focus);
            _selectStatus.rectTransform.Place(0, 1000, 1920, 40);

            _slotA = Ui.Button("SlotA", _selectScreen.transform, "", () => Session.Selection.Remove(0), 22);
            ((RectTransform)_slotA.transform).Place(260, 760, 680, 70);
            _slotB = Ui.Button("SlotB", _selectScreen.transform, "", () => Session.Selection.Remove(1), 22);
            ((RectTransform)_slotB.transform).Place(980, 760, 680, 70);
            Ui.Glyph("GlyphA", _slotA.transform, GlyphShape.Square, 36).transform.parent.GetComponent<RectTransform>().Place(12, 17, 36, 36);
            Ui.Glyph("GlyphB", _slotB.transform, GlyphShape.Diamond, 36).transform.parent.GetComponent<RectTransform>().Place(12, 17, 36, 36);
            _swap = Ui.Button("Swap", _selectScreen.transform, "SWAP A <-> B", () => Session.Selection.Swap(), 22);
            ((RectTransform)_swap.transform).Place(560, 860, 260, 64);
            _confirm = Ui.Button("Confirm", _selectScreen.transform, "CONFIRM", () => Session.ConfirmUnits(), 26, Theme.ButtonPrimary);
            ((RectTransform)_confirm.transform).Place(840, 860, 420, 64);
            _selectBack = Ui.Button("Back", _selectScreen.transform, "BACK", () => { if (InEncounter) ReturnToWorld(); else Session.BackToSetup(); }, 22);
            ((RectTransform)_selectBack.transform).Place(1280, 860, 200, 64);
        }

        /// <summary>(Re)build unit cards for the current developer-mode setting.</summary>
        private void RebuildUnitCards()
        {
            foreach (var b in _cardButtons) Destroy(b.transform.parent.gameObject);
            _cardButtons.Clear();
            _cardUnits.Clear();
            _cardStatus.Clear();
            var units = Session.SelectableUnits(Session.Options.DeveloperMode);
            float cardW = units.Count <= 2 ? 620 : units.Count == 3 ? 520 : 250, gap = 20;
            float total = units.Count * cardW + (units.Count - 1) * gap;
            float x0 = (1920 - total) / 2f;
            var putButtons = new List<Button>();
            for (int i = 0; i < units.Count; i++)
            {
                var def = units[i];
                var holder = Ui.Rect("Card_" + def.Id, _selectScreen.transform);
                holder.Place(x0 + i * (cardW + gap), 180, cardW, 560);
                var card = Ui.Button("Select", holder, "", () => Session.Selection.Toggle(def.Id), 20, Theme.Panel);
                card.GetComponentInChildren<Text>().gameObject.SetActive(false);
                ((RectTransform)card.transform).Place(0, 0, cardW, 470);
                var s = def.Stats(Rank.Bronze);
                float portrait = cardW >= 400 ? 120 : 64;
                Ui.Icon("Portrait", card.transform, icons != null ? icons.Unit(def.Id) : null, portrait, Theme.TextDim)
                    .rectTransform.Place(cardW - portrait - 16, 14, portrait, portrait);
                L("Name", card.transform, def.DisplayName.ToUpperInvariant(), 34, TextAnchor.UpperLeft, Theme.Text, FontStyle.Bold).rectTransform.Place(20, 16, cardW - 40, 44);
                L("Role", card.transform, def.Role + (Session.Options.DeveloperMode && !def.PlayerFacing ? "  [DEV]" : ""), 20, TextAnchor.UpperLeft, Theme.TextDim).rectTransform.Place(20, 62, cardW - 40, 28);
                L("Stats", card.transform,
                    "Starting rank: BRONZE [I]\nActivation cost: " + s.EnergyCost + " energy\n" + UnitPanelView.StatsLine(def, s)
                    + "\n\n" + def.Description, 21, TextAnchor.UpperLeft).rectTransform.Place(20, 100, cardW - 40, 300);
                var status = L("Status", card.transform, "", 22, TextAnchor.LowerLeft, Theme.TextDim, FontStyle.Bold);
                status.rectTransform.Place(20, 410, cardW - 40, 44);
                var putA = Ui.Button("PutA", holder, "PUT IN A", () => { Session.Selection.Assign(0, def.Id); }, 18);
                ((RectTransform)putA.transform).Place(0, 480, cardW / 2 - 6, 56);
                var putB = Ui.Button("PutB", holder, "PUT IN B", () => { Session.Selection.Assign(1, def.Id); }, 18);
                ((RectTransform)putB.transform).Place(cardW / 2 + 6, 480, cardW / 2 - 6, 56);
                _cardButtons.Add(card);
                _cardUnits.Add(def.Id);
                _cardStatus.Add(status);
                putButtons.Add(putA);
                putButtons.Add(putB);
                LinkVertical(card, putA);
                var nav = putB.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = card;
                nav.selectOnLeft = putA;
                putB.navigation = nav;
                var navA = putA.navigation;
                navA.selectOnRight = putB;
                putA.navigation = navA;
            }
            // Horizontal links between cards and down to slots / buttons.
            for (int i = 0; i < _cardButtons.Count; i++)
            {
                var n = _cardButtons[i].navigation;
                n.mode = Navigation.Mode.Explicit;
                n.selectOnLeft = i > 0 ? _cardButtons[i - 1] : null;
                n.selectOnRight = i < _cardButtons.Count - 1 ? _cardButtons[i + 1] : null;
                n.selectOnDown = putButtons[i * 2];
                _cardButtons[i].navigation = n;
                var pa = putButtons[i * 2].navigation;
                pa.selectOnDown = _slotA;
                putButtons[i * 2].navigation = pa;
                var pb = putButtons[i * 2 + 1].navigation;
                pb.selectOnDown = _slotB;
                pb.selectOnRight = i < _cardButtons.Count - 1 ? putButtons[i * 2 + 2] : null;
                putButtons[i * 2 + 1].navigation = pb;
                if (i > 0) { var pl = putButtons[i * 2].navigation; pl.selectOnLeft = putButtons[i * 2 - 1]; putButtons[i * 2].navigation = pl; }
            }
            SetNav(_slotA, up: putButtons[0], right: _slotB, down: _swap);
            SetNav(_slotB, up: putButtons[putButtons.Count - 1], left: _slotA, down: _confirm);
            LinkBottomRow();
            ApplyUiScale(Settings.UiScale);
        }

        /// <summary>
        /// SWAP, CONFIRM, BACK. A greyed-out CONFIRM cannot hold focus, so the controller path steps over it to BACK
        /// (D-037); otherwise BACK / LEAVE TABLE could never be reached with a controller.
        /// </summary>
        private void LinkBottomRow()
        {
            bool canConfirm = _confirm.interactable;
            var slotBNav = _slotB.navigation;
            slotBNav.selectOnDown = canConfirm ? (Selectable)_confirm : _selectBack;
            _slotB.navigation = slotBNav;
            SetNav(_swap, up: _slotA, right: canConfirm ? (Selectable)_confirm : _selectBack);
            SetNav(_confirm, up: _slotB, left: _swap, right: _selectBack);
            SetNav(_selectBack, up: _slotB, left: canConfirm ? (Selectable)_confirm : _swap);
        }

        // ------------------------------------------------------------------ overlays

        private GameObject Overlay(string name, float w, float h, out RectTransform box)
        {
            var o = Ui.Panel(name, _root, Theme.Overlay);
            o.rectTransform.Fill();
            box = Ui.Panel("Box", o.transform, Theme.Panel).rectTransform;
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(w, h);
            o.gameObject.SetActive(false);
            return o.gameObject;
        }

        private Button BoxButton(RectTransform box, string name, string text, float x, float y, float w, float h, UnityEngine.Events.UnityAction a, Color? c = null)
        {
            var b = Ui.Button(name, box, text, a, 24, c);
            ((RectTransform)b.transform).Place(x, y, w, h);
            return b;
        }

        private void BuildOverlays()
        {
            _inspectOverlay = Overlay("InspectOverlay", 900, 620, out var ib);
            _inspectText = L("Text", ib, "", 22, TextAnchor.UpperLeft);
            _inspectText.rectTransform.Place(24, 20, 852, 500);
            _inspectClose = BoxButton(ib, "Close", "CLOSE", 340, 540, 220, 60, () => CloseOverlay(_inspectOverlay));

            _helpOverlay = Overlay("HelpOverlay", 1500, 960, out var hb);
            _helpText = L("Text", hb, "", 20, TextAnchor.UpperLeft);
            _helpText.rectTransform.Place(28, 20, 1444, 850);
            _helpClose = BoxButton(hb, "Close", "CLOSE HELP", 630, 884, 240, 60, () => CloseOverlay(_helpOverlay));

            _pauseOverlay = Overlay("PauseOverlay", 700, 800, out var pb);
            L("Title", pb, "PAUSED", 40, TextAnchor.UpperCenter, Theme.Text, FontStyle.Bold).rectTransform.Place(0, 16, 700, 56);
            _pauseResume = BoxButton(pb, "Resume", "RESUME", 150, 84, 400, 60, ResumeFromPause, Theme.ButtonPrimary);
            _pauseReduced = BoxButton(pb, "Reduced", "", 150, 156, 400, 56, () => Settings.ReducedMotion = !Settings.ReducedMotion);
            _pauseShake = BoxButton(pb, "Shake", "", 150, 222, 400, 56, () => Settings.ScreenShake = !Settings.ScreenShake);
            _pauseScale = BoxButton(pb, "Scale", "", 150, 288, 400, 56, CycleUiScale);
            _pauseWrap = BoxButton(pb, "Wrap", "", 150, 354, 400, 56, () => WrapReelNavigation = !WrapReelNavigation);
            _pauseSkip = BoxButton(pb, "Skip", "DEV: SKIP ALL PRESENTATION", 150, 420, 400, 56, () => Presenter.SkipAll());
            var help = BoxButton(pb, "Help", "HELP", 150, 486, 400, 56, () => OpenOverlay(_helpOverlay, _helpClose));
            _pauseRestart = BoxButton(pb, "Restart", "RESTART MATCH", 150, 584, 400, 60,
                () => Confirm("Restart with the same setup and a NEW seed?\nThe current match will be discarded.", () => { ResumeFromPause(); Session.Rematch(); }));
            _pauseExit = BoxButton(pb, "Exit", "EXIT MATCH", 150, 656, 400, 60,
                () => Confirm(InEncounter ? "Leave the table and return to the village?\nThis match will not count." : "Exit to setup? The current match will be discarded.",
                    () => { ResumeFromPause(); if (InEncounter) ReturnToWorld(); else Session.ExitMatch(); }));
            LinkVertical(_pauseResume, _pauseReduced, _pauseShake, _pauseScale, _pauseWrap, _pauseSkip, help, _pauseRestart, _pauseExit);

            _confirmOverlay = Overlay("ConfirmOverlay", 760, 320, out var cb);
            _confirmText = L("Text", cb, "", 26, TextAnchor.MiddleCenter);
            _confirmText.rectTransform.Place(20, 20, 720, 170);
            _confirmYes = BoxButton(cb, "Yes", "CONFIRM", 110, 220, 250, 64, () => { var a = _confirmAction; CloseOverlay(_confirmOverlay); a?.Invoke(); }, Theme.ButtonPrimary);
            _confirmNo = BoxButton(cb, "No", "CANCEL", 400, 220, 250, 64, () => CloseOverlay(_confirmOverlay));
            SetNav(_confirmYes, right: _confirmNo);
            SetNav(_confirmNo, left: _confirmYes);

            _resultOverlay = Overlay("ResultOverlay", 1100, 700, out var rb);
            // Result: the outcome word, who and how long, then the two final crowns as a scoreboard (D-028).
            _resultTitle = L("Title", rb, "", 84, TextAnchor.UpperCenter, Theme.Crown, FontStyle.Bold);
            _resultTitle.rectTransform.Place(0, 26, 1100, 104);
            _resultSub = L("Sub", rb, "", 24, TextAnchor.UpperCenter, Theme.TextDim, FontStyle.Bold);
            _resultSub.rectTransform.Place(0, 130, 1100, 36);
            for (int side = 0; side < 2; side++)
            {
                var card = Ui.Card(side == 0 ? "YouCard" : "EnemyCard", rb, new Color(0.09f, 0.05f, 0.035f, 0.95f), true);
                card.rectTransform.Place(side == 0 ? 90 : 590, 186, 420, 196);
                _resultCards[side] = card;
                L("Who", card.transform, side == 0 ? "YOU" : "OPPONENT", 22, TextAnchor.UpperCenter, Theme.TextDim, FontStyle.Bold).rectTransform.Place(0, 14, 420, 30);
                var crown = Ui.Icon("Crown", card.transform, icons != null ? icons.crown : null, 60, Theme.Crown);
                crown.rectTransform.Place(96, 62, 60, 60);
                _resultScore[side] = L("Crown", card.transform, "", 88, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
                _resultScore[side].rectTransform.Place(172, 44, 200, 96);
                _resultTeam[side] = L("Team", card.transform, "", 20, TextAnchor.UpperCenter, Theme.TextDim);
                _resultTeam[side].rectTransform.Place(10, 150, 400, 30);
            }
            L("Versus", rb, "-", 60, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold).rectTransform.Place(510, 230, 80, 90);
            _resultBody = L("Body", rb, "", 18, TextAnchor.UpperCenter, Theme.TextDim);
            _resultBody.rectTransform.Place(40, 652, 1020, 30);
            _resultRematch = BoxButton(rb, "Rematch", "REMATCH", 90, 420, 420, 64, () => Session.Rematch(), Theme.ButtonPrimary);
            _resultChange = BoxButton(rb, "ChangeUnits", "CHANGE UNITS", 590, 420, 420, 64, () => { Session.ChangeUnits(); });
            _resultCopy = BoxButton(rb, "CopyReplay", "COPY REPLAY", 90, 500, 420, 64, CopyReplay);
            _resultExit = BoxButton(rb, "Exit", "EXIT", 590, 500, 420, 64, () => { if (InEncounter) ReturnToWorld(); else Session.ExitMatch(); });
            _resultSameSeed = BoxButton(rb, "SameSeed", "DEV: REPLAY SAME SEED", 90, 584, 420, 56, () => Session.ReplaySameSeed());
            _resultReplay = L("Replay", rb, "", 14, TextAnchor.UpperLeft, Theme.TextDim);
            _resultReplay.rectTransform.Place(590, 580, 420, 64);
            SetNav(_resultRematch, right: _resultChange, down: _resultCopy);
            SetNav(_resultChange, left: _resultRematch, down: _resultExit);
            SetNav(_resultCopy, up: _resultRematch, right: _resultExit, down: _resultSameSeed);
            SetNav(_resultExit, up: _resultChange, left: _resultCopy);
            SetNav(_resultSameSeed, up: _resultCopy);

            _errorOverlay = Overlay("ErrorOverlay", 1300, 700, out var eb);
            L("Title", eb, "MATCH STOPPED (development diagnostic)", 34, TextAnchor.UpperCenter, Theme.Damage, FontStyle.Bold).rectTransform.Place(0, 20, 1300, 50);
            _errorText = L("Text", eb, "", 18, TextAnchor.UpperLeft);
            _errorText.rectTransform.Place(30, 80, 1240, 500);
            _errorCopy = BoxButton(eb, "Copy", "COPY REPLAY", 300, 610, 320, 64, CopyReplay);
            _errorExit = BoxButton(eb, "Exit", "RETURN TO SETUP", 680, 610, 320, 64, () => { _errorOverlay.SetActive(false); _overlayStack.Clear(); Session.ExitMatch(); });
            SetNav(_errorCopy, right: _errorExit);
            SetNav(_errorExit, left: _errorCopy);
        }

        private void CycleUiScale()
        {
            float next = Settings.UiScale < 1.1f ? 1.25f : Settings.UiScale < 1.4f ? 1.5f : 1f;
            ApplyUiScale(next);
        }

        public void CopyReplay()
        {
            LastCopiedReplay = Session.ReplayText;
            GUIUtility.systemCopyBuffer = LastCopiedReplay;
            _resultReplay.text = "Copied " + LastCopiedReplay.Length + " characters:\n" + (LastCopiedReplay.Length > 300 ? LastCopiedReplay.Substring(0, 300) + "..." : LastCopiedReplay);
            Debug.Log("[Tabletop] Replay copied: " + LastCopiedReplay);
        }

        private void Confirm(string message, Action onYes)
        {
            _confirmText.text = message;
            _confirmAction = onYes;
            OpenOverlay(_confirmOverlay, _confirmNo);
        }

        public void OpenOverlay(GameObject overlay, Selectable first)
        {
            var opener = eventSystem.currentSelectedGameObject;
            _lastOverlayOpenFrame = Time.frameCount;
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
            _overlayStack.Push((overlay, opener));
            Focus(first);
        }

        public void CloseOverlay(GameObject overlay)
        {
            if (!overlay.activeSelf) return;
            overlay.SetActive(false);
            _lastOverlayCloseFrame = Time.frameCount;
            GameObject opener = null;
            var keep = new Stack<(GameObject, GameObject)>();
            while (_overlayStack.Count > 0)
            {
                var top = _overlayStack.Pop();
                if (top.overlay == overlay) { opener = top.opener; break; }
                keep.Push(top);
            }
            while (keep.Count > 0) _overlayStack.Push(keep.Pop());
            if (opener != null && opener.activeInHierarchy) eventSystem.SetSelectedGameObject(opener);
            else _pendingFocus = null;
        }

        public bool AnyModalOpen => _inspectOverlay.activeSelf || _helpOverlay.activeSelf || _pauseOverlay.activeSelf
            || _confirmOverlay.activeSelf || _errorOverlay.activeSelf || (_resultOverlay.activeSelf && Session.State == UxState.MatchResult);

        public void OpenInspect(int side, int slot, bool full)
        {
            if (Session.Match == null) return;
            var def = Session.Match.UnitDefinition((SideId)side, slot);
            var v = Presenter.Visual;
            var rank = v.Rank[side, slot];
            string text = (side == 0 ? "YOUR " : "ENEMY ") + def.DisplayName.ToUpperInvariant() + "  (" + (slot == 0 ? "A / LEFT" : "B / RIGHT") + ")\n"
                + def.Role + "\n\n" + def.Description + "\n\nCurrent: " + rank.ToString().ToUpperInvariant() + ", XP " + v.Xp[side, slot] + "/6, energy "
                + v.Energy[side, slot] + "/" + def.Stats(rank).EnergyCost + "\n\n" + AllRanks(def);
            _inspectText.text = text;
            OpenOverlay(_inspectOverlay, _inspectClose);
        }

        public void OpenInspectDefinition(string unitId, bool full)
        {
            var def = Catalog.Unit(unitId);
            _inspectText.text = def.DisplayName.ToUpperInvariant() + "\n" + def.Role + "\n\n" + def.Description + "\n\n" + AllRanks(def);
            OpenOverlay(_inspectOverlay, _inspectClose);
        }

        private static string AllRanks(UnitDefinition def)
        {
            var sb = new System.Text.StringBuilder("ALL RANKS\n");
            foreach (Rank r in new[] { Rank.Bronze, Rank.Silver, Rank.Gold })
                sb.Append(r.ToString().ToUpperInvariant()).Append(": cost ").Append(def.Stats(r).EnergyCost).Append(". ")
                  .Append(UnitPanelView.StatsLine(def, def.Stats(r)).Replace("\n", ". ")).Append('\n');
            sb.Append("Ranks up at 6 XP (XP faces +1 each, acting +2). At Gold, 6 XP launches a 2-damage bomb that ignores the Bulwark.");
            return sb.ToString();
        }

        private void ShowError()
        {
            _errorText.text = Session.FatalError;
            Debug.LogError("[Tabletop] " + Session.FatalError);
            _errorOverlay.SetActive(true);
            _errorOverlay.transform.SetAsLastSibling();
            Focus(_errorCopy);
        }

        private void RefreshHelp()
        {
            if (_helpText == null) return;
            string B(string map, string action) => Input.Binding(map, action);
            _helpText.text =
                "HOW A ROUND WORKS\n" +
                "- You get up to 3 spins of 5 wheels. The first spin rolls all five.\n" +
                "- After a spin, lock wheels to keep them; locked wheels do not move. You may unlock them again after spin 2.\n" +
                "- The third spin is final. Locking all five wheels also makes the result final immediately.\n" +
                "- The enemy spins after you, using their own wheels. You cannot see their wheels until both sides are done.\n\n" +
                "SYMBOLS  (orange SQUARE = your left figurine, teal DIAMOND = your right figurine, bar = HAMMER)\n" +
                "- Count every printed symbol of a kind, then subtract 2: 3 symbols = 1, 4 = 2, 5 = 3, 6 = 4.\n" +
                "- Squares give energy to your left figurine, diamonds to your right figurine, hammers build your Bulwark (max 5).\n" +
                "- An XP badge gives exactly +1 XP to that figurine, even with fewer than 3 symbols.\n\n" +
                "FIGURINES, CROWN, BULWARK\n" +
                "- A figurine acts when its energy reaches its cost; extra energy is wasted. Acting gives +2 XP.\n" +
                "- 6 XP ranks Bronze -> Silver -> Gold. At Gold, 6 XP launches a 2-damage BOMB that ignores the Bulwark.\n" +
                "- A shot hits the Crown only if its height is GREATER than the Bulwark; otherwise it damages the Bulwark (no spill-over).\n" +
                "- Crowns start at 10, the normal cap (healing can push one to 12: its counter turns green). The match is checked only after the whole round: 0 HP = defeat, both 0 = tie.\n\n" +
                "CONTROLS (" + (Input.ActiveScheme == Tabletop.Input.ControlScheme.Gamepad ? "gamepad" : "keyboard / mouse") + ")\n" +
                "- Move focus: " + B("UI", "Navigate") + "     Confirm / toggle lock on focused wheel: " + B("UI", "Submit") + "\n" +
                "- Spin: " + B("Match", "Spin") + " (or focus SPIN and confirm)     Lock wheels directly: " + B("Match", "LockSlot1") + "-" + B("Match", "LockSlot5") + "\n" +
                "- Next / previous element: " + B("Match", "FocusNext") + " / " + B("Match", "FocusPrevious") + " (while the round plays out, " + B("Match", "FocusNext") + " skips to the next step)     Inspect focused unit: " + B("Match", "Inspect") + "\n" +
                "- Locking all five wheels does not end your turn: pull the lever (" + B("Match", "Spin") + ") to lock in.\n" +
                "- Hold to speed up animations: " + B("Match", "AcceleratePresentation") + "     Help: " + B("Match", "Help") + "     Pause / close: " + B("Match", "Pause") + "\n" +
                "- Mouse: click wheels to lock, click buttons, hover a figurine for details. There is no timer.";
        }

        private void RefreshPrompts()
        {
            RefreshHelp();
            RefreshControlHints();
        }
    }
}
