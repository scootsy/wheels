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
            // Background strips around the 3D action lane.
            Ui.Panel("BgTop", t, Theme.Background).rectTransform.Place(0, 0, 1920, LaneY);
            Ui.Panel("BgBottom", t, Theme.Background).rectTransform.Place(0, LaneY + LaneH, 1920, 1080 - LaneY - LaneH);
            Ui.Panel("BgLeft", t, Theme.Background).rectTransform.Place(0, LaneY, LaneX, LaneH);
            Ui.Panel("BgRight", t, Theme.Background).rectTransform.Place(LaneX + LaneW, LaneY, 1920 - LaneX - LaneW, LaneH);
            foreach (var img in t.GetComponentsInChildren<Image>()) img.raycastTarget = false;

            // Enemy reels (top center) and player reels (bottom center).
            for (int r = 0; r < 5; r++)
            {
                _enemyReels[r] = new ReelView(t, r, 615 + r * 140, 8, 130, 124, false, icons);
                _enemyReels[r].Button.GetComponent<Image>().raycastTarget = false;
                int reel = r;
                _playerReels[r] = new ReelView(t, r, 561 + r * 162, 800, 150, 150, true, icons);
                _playerReels[r].Button.onClick.AddListener(() => OnReelClicked(reel));
            }
            L("EnemyReelsLabel", t, "ENEMY REELS", 16, TextAnchor.UpperRight, Theme.Enemy).rectTransform.Place(440, 10, 165, 24);
            L("PlayerReelsLabel", t, "YOUR\nREELS", 16, TextAnchor.UpperRight, Theme.Player).rectTransform.Place(440, 800, 112, 44);

            // Crown + Barrier readouts overlaid on the table view (top = enemy, bottom = you).
            for (int side = 0; side < 2; side++)
            {
                float y = side == 1 ? LaneY + 6 : LaneY + LaneH - 50;
                var chip = Ui.Panel(side == 1 ? "EnemyChip" : "PlayerChip", t, new Color(0.08f, 0.07f, 0.07f, 0.82f));
                chip.rectTransform.Place(LaneX + 6, y, 640, 44);
                chip.raycastTarget = false;
                Ui.Icon("CrownIcon", chip.transform, icons != null ? icons.crown : null, 38, Theme.Crown).rectTransform.Place(6, 3, 38, 38);
                var crownText = L(side == 1 ? "EnemyCrown" : "PlayerCrown", chip.transform, "", 26, TextAnchor.MiddleLeft, Theme.Crown, FontStyle.Bold);
                crownText.rectTransform.Place(50, 0, 330, 44);
                Ui.Icon("WallIcon", chip.transform, icons != null ? icons.wall : null, 38, Theme.Barrier).rectTransform.Place(384, 3, 38, 38);
                var wallText = L(side == 1 ? "EnemyWall" : "PlayerWall", chip.transform, "", 20, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
                wallText.rectTransform.Place(426, 0, 90, 44);
                for (int i = 0; i < 5; i++)
                {
                    var pip = Ui.Panel("WallPip" + side + i, chip.transform, Theme.Barrier);
                    pip.rectTransform.Place(516 + i * 24, 12, 20, 20);
                    pip.raycastTarget = false;
                    _barrierPips[side, i] = pip;
                }
                if (side == 1) { _enemyCrown = crownText; _enemyBarrier = wallText; }
                else { _playerCrown = crownText; _playerBarrier = wallText; }
            }

            _banner = L("Banner", t, "", 26, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            _banner.rectTransform.Place(440, 668, 1040, 52);
            _preview = L("Preview", t, "", 18, TextAnchor.UpperLeft, Theme.Text);
            _preview.rectTransform.Place(450, 722, 1030, 76);

            // Controls.
            _spinsText = L("Spins", t, "", 22, TextAnchor.MiddleRight, Theme.Text, FontStyle.Bold);
            _spinsText.rectTransform.Place(440, 962, 330, 96);
            _spinButton = Ui.Button("Spin", t, "SPIN", OnSpinClicked, 30, Theme.ButtonPrimary);
            ((RectTransform)_spinButton.transform).Place(780, 962, 360, 96);
            _speedButton = Ui.Button("Speed", t, "HOLD TO SPEED UP", null, 22, Theme.Button);
            ((RectTransform)_speedButton.transform).Place(780, 962, 360, 96);
            var trigger = _speedButton.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerDown, () => SetPointerAccelerate(true));
            AddTrigger(trigger, EventTriggerType.PointerUp, () => SetPointerAccelerate(false));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => SetPointerAccelerate(false));
            _pauseButton = Ui.Button("Pause", t, "PAUSE", OnPause, 22);
            ((RectTransform)_pauseButton.transform).Place(1160, 962, 150, 96);
            _helpButton = Ui.Button("Help", t, "HELP", OnHelp, 22);
            ((RectTransform)_helpButton.transform).Place(1325, 962, 150, 96);

            // Unit plaques.
            _unitPanels.Add(new UnitPanelView(t, 1, 0, 10, 10, 420, 262, icons));
            _unitPanels.Add(new UnitPanelView(t, 1, 1, 1490, 10, 420, 262, icons));
            _unitPanels.Add(new UnitPanelView(t, 0, 0, 10, 700, 420, 262, icons));
            _unitPanels.Add(new UnitPanelView(t, 0, 1, 1490, 700, 420, 262, icons));
            foreach (var p in _unitPanels)
            {
                var panel = p;
                panel.Button.onClick.AddListener(() => OpenInspect(panel.Side, panel.Slot, true));
                var tr = panel.Button.gameObject.AddComponent<EventTrigger>();
                AddTrigger(tr, EventTriggerType.PointerEnter, () => { if (!AnyModalOpen) Focus(panel.Button); });
                foreach (var text in panel.Button.GetComponentsInChildren<Text>(true)) { _scalableLabels.Add(text); _baseSizes.Add(text.fontSize); }
            }

            // Info columns.
            var info = Ui.Panel("RoundInfoBg", t, Theme.PanelDark);
            info.rectTransform.Place(10, 296, 420, 190);
            info.raycastTarget = false;
            _roundInfo = L("RoundInfo", info.transform, "", 20, TextAnchor.UpperLeft);
            _roundInfo.rectTransform.Fill(12);
            BuildLegend(t);
            var logBg = Ui.Panel("EventLogBg", t, Theme.PanelDark);
            logBg.rectTransform.Place(1490, 296, 420, 396);
            logBg.raycastTarget = false;
            _log = L("EventLog", logBg.transform, "", 16, TextAnchor.LowerLeft, Theme.TextDim);
            _log.rectTransform.Fill(10);
            _log.verticalOverflow = VerticalWrapMode.Truncate;
            _focusInfo = L("FocusInfo", t, "", 16, TextAnchor.UpperLeft, Theme.Focus);
            _focusInfo.rectTransform.Place(10, 968, 420, 104);
            _hints = L("Hints", t, "", 16, TextAnchor.UpperLeft, Theme.TextDim);
            _hints.rectTransform.Place(1490, 968, 420, 104);

            foreach (var r in _playerReels) foreach (var text in r.Button.GetComponentsInChildren<Text>(true)) { _scalableLabels.Add(text); _baseSizes.Add(text.fontSize); }
            RefreshPrompts();
        }

        private readonly Text[] _legendText = new Text[2];

        /// <summary>Symbol legend: what each reel symbol does for you (names follow your chosen units).</summary>
        private void BuildLegend(Transform t)
        {
            var bg = Ui.Panel("LegendBg", t, Theme.PanelDark);
            bg.rectTransform.Place(10, 492, 420, 200);
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
            if (_diorama != null) _diorama.gameObject.SetActive(board);
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
                case Winner.Player: title = "VICTORY  (+)"; _resultTitle.color = Theme.Heal; break;
                case Winner.Opponent: title = "DEFEAT  (x)"; _resultTitle.color = Theme.Damage; break;
                default: title = "TIE  (=)"; _resultTitle.color = Theme.Crown; break;
            }
            _resultTitle.text = title;
            _resultExit.SetText(InEncounter ? "RETURN TO THE VILLAGE" : "EXIT");
            var p = m.Config.Sides[0];
            var o = m.Config.Sides[1];
            _resultBody.text =
                "Opponent: " + Session.OpponentName + "\n" +
                "Final Crown:  You " + snap.Sides[0].CrownHp + "   -   Enemy " + snap.Sides[1].CrownHp + "\n" +
                "Rounds played: " + snap.Round + "\n" +
                "Your units:  A " + Catalog.Unit(p.UnitIds[0]).DisplayName + ",  B " + Catalog.Unit(p.UnitIds[1]).DisplayName + "\n" +
                "Enemy units: A " + Catalog.Unit(o.UnitIds[0]).DisplayName + ",  B " + Catalog.Unit(o.UnitIds[1]).DisplayName + "   (" + AiName(o.ControllerId) + ")\n" +
                "Reel tier: " + p.ReelTier + "     Seed: " + m.Seed;
            _resultSameSeed.gameObject.SetActive(Session.Options.DeveloperMode);
        }

        // ------------------------------------------------------------------ per-frame board

        private void RenderBoard()
        {
            var m = Session.Match;
            if (m != _renderedMatch)
            {
                _renderedMatch = m;
                _diorama.ClearFloating();
                if (m != null)
                {
                    for (int s = 0; s < 2; s++)
                        for (int u = 0; u < 2; u++) _diorama.SetUnitShape(s, u, m.UnitDefinition((SideId)s, u));
                }
            }
            if (!_boardScreen.activeSelf || m == null) return;
            var v = Presenter.Visual;
            var cur = Presenter.Current;
            var state = Session.State;

            // Reels.
            var pDefs = m.ReelDefinitions(SideId.Player);
            var eDefs = m.ReelDefinitions(SideId.Opponent);
            string nameA = m.UnitDefinition(SideId.Player, 0).DisplayName, nameB = m.UnitDefinition(SideId.Player, 1).DisplayName;
            string enemyA = m.UnitDefinition(SideId.Opponent, 0).DisplayName, enemyB = m.UnitDefinition(SideId.Opponent, 1).DisplayName;
            _legendText[0].text = "A gem: energy for your " + nameA;
            _legendText[1].text = "B gem: energy for your " + nameB;
            bool spinningNow = cur != null && cur.Type == MatchEventType.ReelsSpun && !Presenter.Applied;
            for (int r = 0; r < 5; r++)
            {
                var face = v.Face[0, r];
                if (spinningNow && cur.Side == 0 && cur.Changed[r] && !Settings.ReducedMotion)
                {
                    int f = (int)(Time.unscaledTime * 14f + r * 3) % 8;
                    _playerReels[r].SetFace(pDefs[r].Faces[f], nameA, nameB);
                }
                else _playerReels[r].SetFace(face >= 0 ? pDefs[r].Faces[face] : null, nameA, nameB);
                _playerReels[r].SetLocked(v.Locked[0, r]);
                _playerReels[r].SetHidden(false);
                bool roundStartStale = state == UxState.RoundReady && v.SpinsUsed[0] == 0;
                
                SetDim(_playerReels[r].Button, roundStartStale);
                _playerReels[r].Button.interactable = state == UxState.SpinDecision && !Session.Paused || state == UxState.RoundReady;

                var ef = v.Face[1, r];
                _enemyReels[r].SetFace(ef >= 0 ? eDefs[r].Faces[ef] : null, enemyA, enemyB);
                _enemyReels[r].SetLocked(false);
                _enemyReels[r].SetHidden(!Presenter.OpponentRevealed);
            }

            // Crowns and Barriers.
            _enemyCrown.text = CrownText("ENEMY CROWN", v.Crown[1]);
            _playerCrown.text = CrownText("YOUR CROWN", v.Crown[0]);
            _enemyBarrier.text = "WALL " + v.Barrier[1];
            _playerBarrier.text = "WALL " + v.Barrier[0];
            for (int s = 0; s < 2; s++)
                for (int i = 0; i < 5; i++)
                    _barrierPips[s, i].color = i < v.Barrier[s] ? Theme.Barrier : new Color(0.22f, 0.22f, 0.24f, 1f);

            // Units.
            foreach (var p in _unitPanels)
            {
                bool acting = cur != null && cur.Side == p.Side && cur.Slot == p.Slot && cur.Stage > 0;
                p.Render(m.UnitDefinition((SideId)p.Side, p.Slot), v.Rank[p.Side, p.Slot], v.Xp[p.Side, p.Slot], v.Energy[p.Side, p.Slot], p.Side == 0, acting);
            }

            // Controls.
            bool presentation = state == UxState.Spinning || state == UxState.AiCommit || state == UxState.Reveal || state == UxState.Resolving;
            _spinButton.gameObject.SetActive(!presentation);
            _speedButton.gameObject.SetActive(presentation);
            _spinButton.interactable = Session.CanSpin && !AnyModalOpen;
            _spinButton.SetText(state == UxState.SpinDecision ? "SPIN UNLOCKED REELS" : state == UxState.MatchResult ? "MATCH OVER" : "SPIN");
            _speedButton.SetText(Presenter.Accelerated ? "SPEED x4 (release for normal)" : "HOLD " + Input.Binding("Match", "AcceleratePresentation").ToUpperInvariant() + " TO SPEED UP");
            int used = v.SpinsUsed[0];
            _spinsText.text = "SPINS USED " + used + " / 3\n" + (3 - used) + " REMAINING";

            // Text regions.
            _banner.text = state == UxState.RoundReady ? "Round " + m.Round + ": press SPIN." :
                state == UxState.SpinDecision ? "Lock the reels you want to keep, then spin the rest." :
                Presenter.Banner;
            _preview.text = PreviewText(m, v, state);
            _roundInfo.text = RoundInfo(m, state);
            var logLines = Presenter.Log;
            _log.text = "EVENT LOG\n" + string.Join("\n", logLines.Skip(System.Math.Max(0, logLines.Count - 16)));
            _focusInfo.text = "FOCUS: " + DescribeFocus();
            UpdateNavigation(state);

            _diorama.Render(v, m, cur, Presenter.Progress, Presenter.Applied, Settings.ReducedMotion);
            if (Settings.ScreenShake && !Settings.ReducedMotion && cur != null && cur.Type == MatchEventType.CrownDamaged && Presenter.Applied) _diorama.Shake(0.05f);
            else _diorama.StopShake();
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

        private string RoundInfo(Match m, UxState state)
        {
            string phase;
            switch (state)
            {
                case UxState.RoundReady: phase = "Ready: spin the reels"; break;
                case UxState.Spinning: phase = "Reels spinning"; break;
                case UxState.SpinDecision: phase = "Choose locks or spin again"; break;
                case UxState.AiCommit: phase = "Opponent choosing..."; break;
                case UxState.Reveal: phase = "Reveal"; break;
                case UxState.Resolving: phase = "Resolving the round"; break;
                case UxState.MatchResult: phase = "Match over"; break;
                default: phase = state.ToString(); break;
            }
            string status = _statusOverride ?? Session.LastStatus;
            _statusOverride = null;
            var sb = new System.Text.StringBuilder();
            sb.Append("ROUND ").Append(m.Round).Append('\n').Append(phase).Append("\n\n");
            if (Session.Paused) sb.Append("|| PAUSED\n");
            if (Presenter.Accelerated) sb.Append(">> PRESENTATION x4\n");
            if (Session.LastRejection != null) sb.Append("! ").Append(status).Append('\n');
            else sb.Append(status).Append('\n');
            if (Session.Options.DeveloperMode)
                sb.Append("\nDEV  seed ").Append(m.Seed).Append("  tier ").Append(m.Config.Sides[0].ReelTier)
                  .Append("\n     ").Append(AiName(m.Config.Sides[1].ControllerId)).Append(m.Config.Scenario != null ? "\n     scenario: " + m.Config.Scenario.Name : "");
            return sb.ToString();
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

        private void RefreshControlHints()
        {
            if (_hints == null) return;
            string B(string map, string a) => Input.Binding(map, a);
            _hints.text = (Input.ActiveScheme == Tabletop.Input.ControlScheme.Gamepad ? "GAMEPAD" : "KEYBOARD / MOUSE") + "\n"
                + "Spin " + B("Match", "Spin") + "   Confirm/lock " + B("UI", "Submit") + "   Lock " + B("Match", "LockSlot1") + "-" + B("Match", "LockSlot5") + "\n"
                + "Inspect " + B("Match", "Inspect") + "   Help " + B("Match", "Help") + "   Pause " + B("Match", "Pause") + "\n"
                + "Next/prev " + B("Match", "FocusNext") + " / " + B("Match", "FocusPrevious") + "   Speed up: hold " + B("Match", "AcceleratePresentation");
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
