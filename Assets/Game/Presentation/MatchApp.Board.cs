using System.Collections.Generic;
using System.Linq;
using Tabletop.Application;
using Tabletop.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>Board screen: layout, per-frame rendering from the presenter's visual state, and focus rules.</summary>
    public sealed partial class MatchApp
    {
        private readonly ReelView[] _playerReels = new ReelView[5];
        private readonly ReelView[] _enemyReels = new ReelView[5];
        private readonly List<UnitPanelView> _unitPanels = new List<UnitPanelView>();
        private Button _spinButton, _speedButton, _pauseButton, _helpButton;
        private Text _enemyCrown, _playerCrown, _enemyBarrier, _playerBarrier, _banner, _preview, _roundInfo, _log, _hints, _focusInfo, _spinsText;
        private readonly Image[,] _barrierPips = new Image[2, 5];
        private Selectable _pendingFocus;
        private Match _renderedMatch;

        public IReadOnlyList<ReelView> PlayerReels => _playerReels;
        public IReadOnlyList<ReelView> EnemyReels => _enemyReels;
        public Button SpinButton => _spinButton;
        public Button SpeedButton => _speedButton;
        public Text BannerText => _banner;
        public Text PreviewLabel => _preview;
        public Text RoundInfoText => _roundInfo;
        public Text HintsText => _hints;
        public IReadOnlyList<UnitPanelView> UnitPanels => _unitPanels;

        private void BuildBoardScreen()
        {
            _boardScreen = Screen("BoardScreen");
            var t = _boardScreen.transform;
            // D-028: the table is the interface. Everything the player needs is a part on the board; the uGUI layer
            // only carries invisible hit areas laid over those parts (for focus, clicks and screen readers), a
            // compact prompt bar, a toast for feedback, and two small menu buttons.
            for (int r = 0; r < 5; r++)
            {
                _enemyReels[r] = new ReelView(t, r, 0, 0, 110, 110, false, icons);
                _enemyReels[r].Button.GetComponent<Image>().raycastTarget = false;
                _enemyReels[r].SetOverlay(true);
                _enemyReels[r].SetCaptionOnTop(true);
                int reel = r;
                _playerReels[r] = new ReelView(t, r, 0, 0, 110, 110, true, icons);
                _playerReels[r].Button.onClick.AddListener(() => OnReelClicked(reel));
                _playerReels[r].SetOverlay(true);
            }

            // Legacy readouts, kept (hidden) for their generated text: the table now shows all of this.
            var legacy = Ui.Rect("LegacyReadouts", t);
            legacy.Fill();
            for (int side = 0; side < 2; side++)
            {
                var crownText = L(side == 1 ? "EnemyCrown" : "PlayerCrown", legacy, "", 20);
                var wallText = L(side == 1 ? "EnemyWall" : "PlayerWall", legacy, "", 20);
                for (int i = 0; i < 5; i++) _barrierPips[side, i] = Ui.Panel("WallPip" + side + i, legacy, Theme.Barrier);
                if (side == 1) { _enemyCrown = crownText; _enemyBarrier = wallText; }
                else { _playerCrown = crownText; _playerBarrier = wallText; }
            }
            _banner = L("Banner", legacy, "", 20);
            _preview = L("Preview", legacy, "", 16);
            _spinsText = L("Spins", legacy, "", 20);
            _log = L("EventLog", legacy, "", 14);
            _focusInfo = L("FocusInfo", legacy, "", 14);
            BuildLegend(legacy);
            legacy.gameObject.SetActive(false);

            // The SPIN lever: invisible buttons over the 3D lever (the lever's own plate shows what it will do).
            _spinButton = Ui.Button("Spin", t, "SPIN", OnSpinClicked, 30, Theme.ButtonPrimary);
            MakeOverlay(_spinButton);
            _speedButton = Ui.Button("Speed", t, "HOLD TO SPEED UP", null, 20, Theme.Button);
            MakeOverlay(_speedButton);
            var trigger = _speedButton.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerDown, () => SetPointerAccelerate(true));
            AddTrigger(trigger, EventTriggerType.PointerUp, () => SetPointerAccelerate(false));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => SetPointerAccelerate(false));

            // Menu and help: two small round buttons in the top-right corner.
            _pauseButton = Ui.Button("Pause", t, "II", OnPause, 22, new Color(0.14f, 0.08f, 0.05f, 0.92f));
            ((RectTransform)_pauseButton.transform).Place(1848, 14, 58, 58);
            _helpButton = Ui.Button("Help", t, "?", OnHelp, 26, new Color(0.14f, 0.08f, 0.05f, 0.92f));
            ((RectTransform)_helpButton.transform).Place(1782, 14, 58, 58);
            foreach (var b in new[] { _pauseButton, _helpButton })
            {
                var img = b.GetComponent<Image>();
                if (icons != null && icons.uiCircle != null) { img.sprite = icons.uiCircle; img.type = Image.Type.Simple; }
                b.GetComponentInChildren<Text>().color = Theme.Gilt;
            }

            // Unit hit areas over each podium (focus, Inspect, click-to-inspect). The podium shows the unit itself.
            _unitPanels.Add(new UnitPanelView(t, 1, 0, 0, 0, 420, 262, icons));
            _unitPanels.Add(new UnitPanelView(t, 1, 1, 0, 0, 420, 262, icons));
            _unitPanels.Add(new UnitPanelView(t, 0, 0, 0, 0, 420, 262, icons));
            _unitPanels.Add(new UnitPanelView(t, 0, 1, 0, 0, 420, 262, icons));
            foreach (var p in _unitPanels)
            {
                var panel = p;
                panel.SetOverlay(true);
                panel.Button.onClick.AddListener(() => OpenInspect(panel.Side, panel.Slot, true));
                var tr = panel.Button.gameObject.AddComponent<EventTrigger>();
                AddTrigger(tr, EventTriggerType.PointerEnter, () => { if (!AnyModalOpen) Focus(panel.Button); });
            }

            // Toast (feedback such as "Spin before locking") and the prompt bar, bottom-left.
            _toast = Ui.Card("Toast", t, new Color(0.1f, 0.05f, 0.035f, 0.94f), true);
            _toast.rectTransform.Place(24, 956, 620, 50);
            _toastGroup = _toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _roundInfo = L("ToastText", _toast.transform, "", 20, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
            _roundInfo.rectTransform.Fill(16);
            _promptBar = Ui.Rect("Prompts", t);
            _promptBar.Place(24, 1016, 1100, 46);
            _hints = L("PromptText", _promptBar, "", 1, TextAnchor.MiddleLeft, new Color(0, 0, 0, 0)); // plain-text copy for tests and screen readers

            RefreshPrompts();
        }

        private Image _toast;
        private CanvasGroup _toastGroup;
        private float _toastTime;
        private string _toastShown = "";
        private RectTransform _promptBar;
        private string _promptKey = "";
        private readonly TableFrame _frame = new TableFrame();
        private readonly Vector3[] _corners = new Vector3[4];

        /// <summary>A button that sits invisibly over a table part: no fill, no label, but the focus outline shows.</summary>
        private static void MakeOverlay(Button b)
        {
            var img = b.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            var colors = b.colors;
            colors.normalColor = colors.highlightedColor = colors.selectedColor = colors.pressedColor = colors.disabledColor = new Color(1, 1, 1, 0);
            b.colors = colors;
            var label = b.GetComponentInChildren<Text>();
            if (label != null) label.enabled = false;
        }

        private readonly Text[] _legendText = new Text[2];

        /// <summary>Symbol legend: what each reel symbol does for you (names follow your chosen units).</summary>
        private void BuildLegend(Transform t)
        {
            var bg = Ui.Panel("LegendBg", t, new Color(0.07f, 0.03f, 0.02f, 0.84f));
            bg.rectTransform.Place(8, 338, 420, 200);
            bg.rectTransform.localScale = Vector3.one * (262f / 420f);
            bg.raycastTarget = false;
            L("LegendTitle", bg.transform, "WHAT THE SYMBOLS DO", 17, TextAnchor.UpperLeft, Theme.Focus, FontStyle.Bold).rectTransform.Place(12, 6, 396, 22);
            var rows = new[]
            {
                (icons != null ? icons.energyA : null, Theme.ChannelA, "A gem: energy for your A unit"),
                (icons != null ? icons.energyB : null, Theme.ChannelB, "B gem: energy for your B unit"),
                (icons != null ? icons.hammer : null, Theme.Hammer, "Hammer: builds your wall"),
                (icons != null ? icons.xp : null, Theme.Xp, "Star: +1 XP to that unit"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                Ui.Icon("LegendIcon" + i, bg.transform, rows[i].Item1, 30, rows[i].Item2).rectTransform.Place(12, 32 + i * 34, 30, 30);
                var text = L("LegendText" + i, bg.transform, rows[i].Item3, 17, TextAnchor.MiddleLeft);
                text.rectTransform.Place(50, 32 + i * 34, 360, 30);
                if (i < 2) _legendText[i] = text;
            }
            L("LegendRule", bg.transform, "Count matching symbols: 3 = 1 point, then +1 for each extra.", 15, TextAnchor.UpperLeft, Theme.TextDim)
                .rectTransform.Place(12, 170, 400, 26);
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        // ------------------------------------------------------------------ screens

        private void ShowScreenFor(UxState state)
        {
            bool board = state != UxState.MatchSetup && state != UxState.UnitSelect;
            _setupScreen.SetActive(state == UxState.MatchSetup);
            _selectScreen.SetActive(state == UxState.UnitSelect);
            _boardScreen.SetActive(board);
            if (boardCamera != null) boardCamera.enabled = board;
            if (_table != null) _table.gameObject.SetActive(board);
            if (state == UxState.UnitSelect) RebuildUnitCards();
            bool result = state == UxState.MatchResult;
            if (result && !_resultOverlay.activeSelf)
            {
                RenderResult();
                _resultReplay.text = "";
                _resultOverlay.SetActive(true);
                _resultOverlay.transform.SetAsLastSibling();
                _pendingFocus = _resultRematch;
            }
            else if (!result && _resultOverlay.activeSelf)
            {
                _resultOverlay.SetActive(false);
            }
            if (!board)
            {
                foreach (var o in new[] { _inspectOverlay, _helpOverlay, _pauseOverlay, _confirmOverlay }) o.SetActive(false);
                _overlayStack.Clear();
            }
        }

        private void RenderResult()
        {
            var m = Session.Match;
            var snap = m.Snapshot();
            string title;
            switch (m.Winner)
            {
                case Winner.Player: title = "VICTORY"; _resultTitle.color = Theme.Heal; break;
                case Winner.Opponent: title = "DEFEAT"; _resultTitle.color = Theme.Damage; break;
                default: title = "TIE"; _resultTitle.color = Theme.Crown; break;
            }
            _resultTitle.text = title;
            _resultExit.SetText(InEncounter ? "RETURN TO THE VILLAGE" : "EXIT");
            var p = m.Config.Sides[0];
            var o = m.Config.Sides[1];
            _resultSub.text = ("vs " + Session.OpponentName + "   \u00b7   " + snap.Round + (snap.Round == 1 ? " ROUND" : " ROUNDS")).ToUpperInvariant();
            for (int side = 0; side < 2; side++)
            {
                var cfg = m.Config.Sides[side];
                _resultScore[side].text = snap.Sides[side].CrownHp.ToString();
                _resultScore[side].color = snap.Sides[side].CrownHp <= 0 ? Theme.Damage : Theme.Text;
                _resultTeam[side].text = Catalog.Unit(cfg.UnitIds[0]).DisplayName + "  +  " + Catalog.Unit(cfg.UnitIds[1]).DisplayName;
                bool won = (side == 0 && m.Winner == Winner.Player) || (side == 1 && m.Winner == Winner.Opponent);
                _resultCards[side].color = won ? new Color(0.2f, 0.13f, 0.05f, 0.97f) : new Color(0.09f, 0.05f, 0.035f, 0.95f);
            }
            _resultBody.text = p.ReelTier + " reels   \u00b7   " + AiName(o.ControllerId) + "   \u00b7   seed " + m.Seed;
            _resultSameSeed.gameObject.SetActive(Session.Options.DeveloperMode);
        }

        // ------------------------------------------------------------------ per-frame board

        private void RenderBoard()
        {
            var m = Session.Match;
            if (m != _renderedMatch)
            {
                _renderedMatch = m;
                _table.ClearFloating();
                if (m != null)
                {
                    for (int s = 0; s < 2; s++)
                    {
                        _table.SetReels(s, m.ReelDefinitions((SideId)s));
                        for (int u = 0; u < 2; u++) _table.SetUnitShape(s, u, m.UnitDefinition((SideId)s, u));
                    }
                }
            }
            if (!_boardScreen.activeSelf || m == null) return;
            var v = Presenter.Visual;
            var cur = Presenter.Current;
            var state = Session.State;

            // Reel hit areas carry focus, lock cues and the accessible description of each face.
            var pDefs = m.ReelDefinitions(SideId.Player);
            var eDefs = m.ReelDefinitions(SideId.Opponent);
            string nameA = m.UnitDefinition(SideId.Player, 0).DisplayName, nameB = m.UnitDefinition(SideId.Player, 1).DisplayName;
            string enemyA = m.UnitDefinition(SideId.Opponent, 0).DisplayName, enemyB = m.UnitDefinition(SideId.Opponent, 1).DisplayName;
            _legendText[0].text = "A gem: energy for your " + nameA;
            _legendText[1].text = "B gem: energy for your " + nameB;
            for (int r = 0; r < 5; r++)
            {
                var face = v.Face[0, r];
                _playerReels[r].SetFace(face >= 0 ? pDefs[r].Faces[face] : null, nameA, nameB);
                _playerReels[r].SetLocked(v.Locked[0, r]);
                _playerReels[r].SetHidden(false);
                _playerReels[r].Button.interactable = state == UxState.SpinDecision && !Session.Paused || state == UxState.RoundReady;
                var ef = v.Face[1, r];
                _enemyReels[r].SetFace(ef >= 0 ? eDefs[r].Faces[ef] : null, enemyA, enemyB);
                _enemyReels[r].SetLocked(false);
                _enemyReels[r].SetHidden(!Presenter.OpponentRevealed);
            }

            // Legacy text readouts (hidden): still generated for accessibility/tests.
            _enemyCrown.text = CrownText("ENEMY CROWN", v.Crown[1]);
            _playerCrown.text = CrownText("YOUR CROWN", v.Crown[0]);
            _enemyBarrier.text = "WALL " + v.Barrier[1];
            _playerBarrier.text = "WALL " + v.Barrier[0];
            foreach (var p in _unitPanels)
            {
                bool acting = cur != null && cur.Side == p.Side && cur.Slot == p.Slot && cur.Stage > 0;
                p.Render(m.UnitDefinition((SideId)p.Side, p.Slot), v.Rank[p.Side, p.Slot], v.Xp[p.Side, p.Slot], v.Energy[p.Side, p.Slot], p.Side == 0, acting);
            }
            _banner.text = Presenter.Banner;
            _preview.text = PreviewText(m, v, state);
            _focusInfo.text = "FOCUS: " + DescribeFocus();
            var logLines = Presenter.Log;
            _log.text = string.Join("\n", logLines.Skip(System.Math.Max(0, logLines.Count - 16)));

            // Controls.
            bool presentation = state == UxState.Spinning || state == UxState.AiCommit || state == UxState.Reveal || state == UxState.Resolving;
            _spinButton.gameObject.SetActive(!presentation);
            _speedButton.gameObject.SetActive(presentation);
            _spinButton.interactable = Session.CanSpinOrConfirm && !AnyModalOpen;
            _spinButton.SetText(Session.CanFinalize ? "LOCK IN" : state == UxState.SpinDecision ? "SPIN UNLOCKED REELS" : state == UxState.MatchResult ? "MATCH OVER" : "SPIN");
            _speedButton.SetText(Presenter.Accelerated ? "SPEED x4" : "HOLD TO SPEED UP");
            _spinsText.text = "SPINS USED " + v.SpinsUsed[0] + " / 3";
            UpdateNavigation(state);

            // The table.
            var focused = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            var f = _frame;
            f.Visual = v;
            f.Match = m;
            f.Current = cur;
            f.Progress = Presenter.Progress;
            f.Applied = Presenter.Applied;
            f.ReducedMotion = Settings.ReducedMotion;
            f.OpponentRevealed = Presenter.OpponentRevealed;
            f.Phase = Presenter.Phase;
            f.PhaseSerial = Presenter.PhaseSerial;
            f.State = state;
            f.Preview = (state == UxState.SpinDecision || state == UxState.Spinning) && v.SpinsUsed[0] > 0
                ? OutcomePreview.Compute(m.Snapshot().Side(SideId.Player), pDefs, Catalog, lockedOnly: true) : null;
            f.ActionOrder = Presenter.ActionOrder;
            f.ActionsStarted = Presenter.ActionsStarted;
            f.FocusSide = f.FocusSlot = -1;
            foreach (var p in _unitPanels) if (p.Button.gameObject == focused) { f.FocusSide = p.Side; f.FocusSlot = p.Slot; }
            f.LeverFocused = focused == _spinButton.gameObject || focused == _speedButton.gameObject;
            f.CanSpin = Session.CanSpin && !AnyModalOpen;
            f.CanConfirm = Session.CanFinalize && !AnyModalOpen;
            f.SpinsUsed = v.SpinsUsed[0];
            f.Round = System.Math.Max(1, v.Round);
            _table.Render(f);
            if (Settings.ScreenShake && !Settings.ReducedMotion && cur != null && cur.Type == MatchEventType.CrownDamaged && Presenter.Applied) _table.Shake(0.05f);
            else _table.StopShake();
            PlaceOverlays();
            RenderToast(state);
            RefreshControlHints();
        }

        /// <summary>Lays the invisible hit areas over the drums, podiums and lever as seen on screen.</summary>
        private void PlaceOverlays()
        {
            if (boardCamera == null) return;
            for (int side = 0; side < 2; side++)
                for (int r = 0; r < 5; r++)
                {
                    _table.ReelFaceCorners(side, r, _corners);
                    var rect = ScreenRect(_corners);
                    var view = side == 0 ? _playerReels[r] : _enemyReels[r];
                    view.Rect.Place(rect.x, rect.y, rect.width, rect.height);
                }
            foreach (var p in _unitPanels)
            {
                _table.PodiumCorners(p.Side, p.Slot, _corners);
                var rect = ScreenRect(_corners);
                ((RectTransform)p.Button.transform).Place(rect.x, rect.y, rect.width, rect.height);
            }
            _table.LeverCorners(_corners);
            var lever = ScreenRect(_corners);
            ((RectTransform)_spinButton.transform).Place(lever.x, lever.y, lever.width, lever.height);
            ((RectTransform)_speedButton.transform).Place(lever.x, lever.y, lever.width, lever.height);
        }

        /// <summary>Screen rectangle (1920x1080 frame, top-left origin) around world points.</summary>
        private Rect ScreenRect(Vector3[] points)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var c in points)
            {
                var sp = boardCamera.WorldToScreenPoint(c);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var local);
                float fx = local.x + 960f, fy = 540f - local.y;
                minX = Mathf.Min(minX, fx); maxX = Mathf.Max(maxX, fx);
                minY = Mathf.Min(minY, fy); maxY = Mathf.Max(maxY, fy);
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>Short feedback: rejections, and the few statuses the table can't show by itself.</summary>
        private void RenderToast(UxState state)
        {
            const string finalize = "All five reels locked. Pull the lever to lock in, or unlock one to keep spinning.";
            string msg = _statusOverride;
            _statusOverride = null;
            // Each rejection is shown once (it stays in LastRejection until the next command).
            if (msg == null && Session.LastRejection != null && !ReferenceEquals(Session.LastRejection, _toastRejection))
            {
                _toastRejection = Session.LastRejection;
                msg = Session.LastStatus;
            }
            bool sticky = Session.CanFinalize && !Session.Paused;
            if (msg != null) ShowToast(msg, 3.2f);
            else if (sticky && _toastShown != finalize) ShowToast(finalize, float.MaxValue);
            else if (!sticky && _toastShown == finalize) _toastTime = 0f;
            if (_toastTime < float.MaxValue) _toastTime -= Time.unscaledDeltaTime;
            if (_toastTime <= 0f) _toastShown = "";
            _toastGroup.alpha = Mathf.Clamp01(_toastTime * 4f);
        }

        private CommandRejection _toastRejection;

        private void ShowToast(string msg, float seconds)
        {
            _toastShown = msg;
            _roundInfo.text = msg;
            _toastTime = seconds;
        }

        private static void SetDim(Selectable s, bool dim)
        {
            var cg = s.GetComponent<CanvasGroup>();
            if (cg == null) cg = s.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = dim ? 0.45f : 1f;
        }

        private static string CrownText(string label, int hp)
        {
            string s = label + "  " + hp + " / 10";
            if (hp > 10) s += "  (+" + (hp - 10) + " OVERHEAL)";
            if (hp <= 0) s += "  - BROKEN";
            return s;
        }

        private string PreviewText(Match m, VisualState v, UxState state)
        {
            if (state == UxState.SpinDecision)
            {
                var p = OutcomePreview.Compute(m.Snapshot().Side(SideId.Player), m.ReelDefinitions(SideId.Player), Catalog);
                return "PREVIEW (if these faces were final):  " + p.BarrierText() + "\n" + p.UnitText(0) + "\n" + p.UnitText(1);
            }
            if (Presenter.OpponentRevealed && (state == UxState.Reveal || state == UxState.Resolving))
                return "FINAL TOTALS   YOU: " + Totals(m, v, 0) + "\nENEMY: " + Totals(m, v, 1);
            if (state == UxState.RoundReady) return "Your reels are unlocked. The first spin rolls all five.";
            return "";
        }

        private static string Totals(Match m, VisualState v, int side)
        {
            var defs = m.ReelDefinitions((SideId)side);
            var faces = new List<ReelFace>();
            for (int r = 0; r < 5; r++) if (v.Face[side, r] >= 0) faces.Add(defs[r].Faces[v.Face[side, r]]);
            var t = SymbolEvaluator.Evaluate(faces);
            return "A x" + t.ChannelA + " -> +" + t.Energy(Channel.A) + " energy,  B x" + t.ChannelB + " -> +" + t.Energy(Channel.B)
                + " energy,  Hammers x" + t.Hammer + " -> +" + t.BarrierGain + " Wall,  XP A+" + t.XpA + " B+" + t.XpB;
        }

        /// <summary>Key-cap prompts for what can be done right now (rebuilt only when that changes).</summary>
        private void RefreshControlHints()
        {
            if (_promptBar == null) return;
            var st = Session.State;
            bool presenting = st == UxState.Spinning || st == UxState.AiCommit || st == UxState.Reveal || st == UxState.Resolving;
            bool pad = Input.ActiveScheme == Tabletop.Input.ControlScheme.Gamepad;
            string B(string map, string a) => Input.Binding(map, a);
            var items = new List<(string key, string label)>();
            if (presenting)
            {
                items.Add(("Hold " + B("Match", "AcceleratePresentation"), "Fast-forward"));
                items.Add((B("Match", "FocusNext"), "Skip step"));
            }
            else if (st == UxState.SpinDecision)
            {
                items.Add((pad ? B("UI", "Submit") : B("Match", "LockSlot1") + "-" + B("Match", "LockSlot5"), "Lock reel"));
                items.Add((B("Match", "Spin"), Session.CanFinalize ? "Lock in" : "Spin again"));
                items.Add((B("Match", "Inspect"), "Inspect"));
            }
            else if (st == UxState.RoundReady)
            {
                items.Add((B("Match", "Spin"), "Spin"));
                items.Add((B("Match", "Inspect"), "Inspect"));
            }
            items.Add((B("Match", "Help"), "Help"));
            items.Add((B("Match", "Pause"), "Menu"));
            string key = (pad ? "pad|" : "kb|") + string.Join("|", items.Select(i => i.key + "=" + i.label));
            if (key == _promptKey) return;
            _promptKey = key;
            for (int i = _promptBar.childCount - 1; i >= 0; i--)
            {
                var c = _promptBar.GetChild(i);
                if (c != _hints.transform) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
            }
            float x = 0;
            foreach (var (k, label) in items)
            {
                var cap = Ui.Keycap(_promptBar, k, 17);
                float w = cap.sizeDelta.x, h = cap.sizeDelta.y;
                cap.Place(x, (46 - h) / 2f, w, h);
                var t = L("Label", _promptBar, label, 19, TextAnchor.MiddleLeft, Theme.Text);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                var outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, 0.8f);
                float lw = t.preferredWidth;
                t.rectTransform.Place(x + w + 8, 0, lw + 4, 46);
                x += w + 8 + lw + 28;
            }
            _hints.text = (pad ? "GAMEPAD  " : "KEYBOARD  ") + string.Join("   ", items.Select(i => i.key + " " + i.label));
        }

        // ------------------------------------------------------------------ focus

        public void Focus(Selectable s)
        {
            if (s == null || eventSystem == null) return;
            if (!s.gameObject.activeInHierarchy || !s.IsInteractable()) { _pendingFocus = s; return; }
            eventSystem.SetSelectedGameObject(s.gameObject);
        }

        private Selectable FirstUnlockedReel()
        {
            var v = Presenter.Visual;
            for (int r = 0; r < 5; r++) if (!v.Locked[0, r]) return _playerReels[r].Button;
            return _playerReels[0].Button;
        }

        private Selectable DefaultFocus()
        {
            switch (Session.State)
            {
                case UxState.MatchSetup: return _setupContinue;
                case UxState.UnitSelect: return _cardButtons.Count > 0 ? _cardButtons[0] : (Selectable)_selectBack;
                case UxState.RoundReady: return _spinButton;
                case UxState.SpinDecision: return FirstUnlockedReel();
                case UxState.MatchResult: return _resultRematch;
                default: return _speedButton;
            }
        }

        private GameObject TopOverlay()
        {
            if (_errorOverlay.activeSelf) return _errorOverlay;
            if (_overlayStack.Count > 0) return _overlayStack.Peek().overlay;
            if (_resultOverlay.activeSelf) return _resultOverlay;
            return null;
        }

        /// <summary>Focus never disappears: restore a legal target whenever selection is lost or becomes illegal.</summary>
        private void EnsureFocus()
        {
            if (eventSystem == null) return;
            var top = TopOverlay();
            var current = eventSystem.currentSelectedGameObject;
            if (top != null)
            {
                // Modal overlays trap focus.
                if (current == null || !current.activeInHierarchy || !current.transform.IsChildOf(top.transform) || !IsInteractable(current))
                {
                    var first = top.GetComponentsInChildren<Selectable>().FirstOrDefault(x => x.IsInteractable());
                    if (_pendingFocus != null && _pendingFocus.transform.IsChildOf(top.transform) && _pendingFocus.IsInteractable()) first = _pendingFocus;
                    if (first != null) eventSystem.SetSelectedGameObject(first.gameObject);
                    _pendingFocus = null;
                }
                return;
            }
            if (_pendingFocus != null && _pendingFocus.gameObject.activeInHierarchy && _pendingFocus.IsInteractable())
            {
                eventSystem.SetSelectedGameObject(_pendingFocus.gameObject);
                _pendingFocus = null;
                return;
            }
            if (current == null || !current.activeInHierarchy || !IsInteractable(current))
            {
                var d = DefaultFocus();
                if (d != null && d.gameObject.activeInHierarchy && d.IsInteractable()) eventSystem.SetSelectedGameObject(d.gameObject);
                else
                {
                    // Nearest legal action on the visible screen.
                    var any = FocusRing().FirstOrDefault(x => x.gameObject.activeInHierarchy && x.IsInteractable());
                    if (any != null) eventSystem.SetSelectedGameObject(any.gameObject);
                }
            }
        }

        private static bool IsInteractable(GameObject go)
        {
            var s = go.GetComponent<Selectable>();
            return s != null && s.IsInteractable();
        }

        private List<Selectable> FocusRing()
        {
            var top = TopOverlay();
            if (top != null) return top.GetComponentsInChildren<Selectable>().ToList();
            switch (Session.State)
            {
                case UxState.MatchSetup: return _setupScreen.GetComponentsInChildren<Selectable>().ToList();
                case UxState.UnitSelect: return _selectScreen.GetComponentsInChildren<Selectable>().ToList();
                default:
                {
                    var ring = new List<Selectable>();
                    ring.AddRange(_playerReels.Select(r => (Selectable)r.Button));
                    ring.Add(_spinButton.gameObject.activeSelf ? _spinButton : _speedButton);
                    ring.Add(_pauseButton);
                    ring.Add(_helpButton);
                    ring.AddRange(_unitPanels.Select(p => (Selectable)p.Button));
                    return ring;
                }
            }
        }

        public void CycleFocus(int dir)
        {
            var ring = FocusRing().Where(s => s.gameObject.activeInHierarchy && s.IsInteractable()).ToList();
            if (ring.Count == 0) return;
            var current = eventSystem.currentSelectedGameObject;
            int idx = ring.FindIndex(s => s.gameObject == current);
            int next = idx < 0 ? 0 : ((idx + dir) % ring.Count + ring.Count) % ring.Count;
            eventSystem.SetSelectedGameObject(ring[next].gameObject);
        }

        private void UpdateNavigation(UxState state)
        {
            var primary = _spinButton.gameObject.activeSelf ? (Selectable)_spinButton : _speedButton;
            var pA = _unitPanels[2].Button;
            var pB = _unitPanels[3].Button;
            for (int r = 0; r < 5; r++)
            {
                Selectable left = r > 0 ? _playerReels[r - 1].Button : (WrapReelNavigation ? _playerReels[4].Button : null);
                Selectable right = r < 4 ? _playerReels[r + 1].Button : (WrapReelNavigation ? _playerReels[0].Button : null);
                SetNav(_playerReels[r].Button, left: left, right: right, down: primary, up: r <= 2 ? pA : pB);
            }
            SetNav(_spinButton, up: FirstUnlockedReel(), right: _pauseButton);
            SetNav(_speedButton, up: FirstUnlockedReel(), right: _pauseButton);
            SetNav(_pauseButton, left: primary, right: _helpButton, up: _playerReels[4].Button);
            SetNav(_helpButton, left: _pauseButton, up: _playerReels[4].Button, right: pB);
            SetNav(pA, down: _playerReels[0].Button, up: _unitPanels[0].Button, right: _playerReels[0].Button);
            SetNav(pB, down: _playerReels[4].Button, up: _unitPanels[1].Button, left: _playerReels[4].Button);
            SetNav(_unitPanels[0].Button, down: pA, right: _unitPanels[1].Button);
            SetNav(_unitPanels[1].Button, down: pB, left: _unitPanels[0].Button);
        }

        private static void SetNav(Selectable s, Selectable up = null, Selectable down = null, Selectable left = null, Selectable right = null)
        {
            var n = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
            s.navigation = n;
        }

        private static void LinkVertical(params Selectable[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var n = items[i].navigation;
                n.mode = Navigation.Mode.Explicit;
                n.selectOnUp = i > 0 ? items[i - 1] : null;
                n.selectOnDown = i < items.Length - 1 ? items[i + 1] : null;
                items[i].navigation = n;
            }
        }
    }
}
