using System.Collections.Generic;
using Tabletop.Application;
using Tabletop.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>Everything the table needs to draw one frame (filled by MatchApp from the presenter and session).</summary>
    public sealed class TableFrame
    {
        public VisualState Visual;
        public Match Match;
        public MatchEvent Current;
        public float Progress;
        public bool Applied;
        public bool ReducedMotion;
        public bool OpponentRevealed;
        public BoardPhase Phase;
        public int PhaseSerial;
        public UxState State;
        /// <summary>The player's preview counting LOCKED reels only (while deciding), or null.</summary>
        public OutcomePreview Preview;
        public IReadOnlyList<(int side, int slot)> ActionOrder;
        public int ActionsStarted;
        public int FocusSide = -1;
        public int FocusSlot = -1;
        public bool LeverFocused;
        public bool CanSpin;
        public bool CanConfirm;
        public int SpinsUsed;
        public int Round;
    }

    /// <summary>
    /// The table's instruments (D-028). The lore is that the board is a purely mechanical device, so everything the
    /// player needs is a part on the table:
    /// - nameplates: rank, XP, stats, the locked-gem tally with the exact energy result (and any waste), action order;
    /// - the step sign in the plaza that flips to each step of the round, with the step order beneath it;
    /// - the SPIN lever and its three spin lamps, and the round counter;
    /// - symbols that fly from the reels to where they apply (stars to XP, gems to podiums, hammers to the wall).
    /// </summary>
    public sealed partial class MechanicalTable
    {
        private sealed class Nameplate
        {
            public Image RankBadge;
            public Text RankNumeral;
            public Text Name;
            public readonly Image[] Xp = new Image[6];
            public Text CrownStat;
            public Text WallStat;
            public Image ChannelIcon;
            public readonly Image[] Tally = new Image[7];
            public readonly Text[] TallyPlus = new Text[7];
            public Text Result;
            public Image Token;
            public Text TokenText;
            public Image Ready;
        }

        private sealed class Token
        {
            public Transform Root;
            public Image Image;
        }

        private static readonly Color PlateInk = new Color(0.98f, 0.93f, 0.82f);
        private static readonly Color PlateDim = new Color(0.72f, 0.6f, 0.46f);
        private static readonly Color Waste = new Color(1f, 0.33f, 0.28f);
        private static readonly Color Go = new Color(0.36f, 0.86f, 0.44f);

        private readonly Nameplate[,] _plates = new Nameplate[2, 2];
        private readonly Transform[,] _focusRings = new Transform[2, 2];
        private Transform _sign;
        private Text _signTitle;
        private Text _signSub;
        private float _signFlip = 1f;
        private string _signShownTitle = "";
        private string _signWantTitle = "";
        private string _signWantSub = "";
        private readonly Image[] _stepPlates = new Image[4];
        private readonly Text[] _stepLabels = new Text[4];
        private Transform _leverArm;
        private float _leverPull;
        private readonly Renderer[] _spinLamps = new Renderer[3];
        private Text _leverLabel;
        private Text _roundText;
        private readonly List<Token> _tokens = new List<Token>();
        private int _lastSpinSerial = -1;

        public const float NameplateWidth = 3.3f;
        public const float NameplateDepth = 1.25f;
        public static Vector3 NameplatePos(int side, int slot) => UnitPos(side, slot) + new Vector3(slot == 0 ? -0.35f : 0.35f, 0.07f, -1.92f);
        /// <summary>The step sign: a fixed bronze bed, a flipping leaf (title) and the four-step strip along its front.</summary>
        public static readonly Vector3 SignPos = new Vector3(0, 0.1f, 0.12f);
        public const float SignWidth = 4.1f;
        public const float SignDepth = 1.6f;
        public static readonly Vector3 LeverPos = new Vector3(7.8f, 0, -4.05f);
        public static readonly Vector3 RoundDialPos = new Vector3(-7.75f, 0, -4.05f);

        // ------------------------------------------------------------------ build

        private void BuildInstruments()
        {
            BuildSign();
            BuildLever();
            BuildRoundDial();
            for (int i = 0; i < 16; i++) _tokens.Add(NewToken());
        }

        /// <summary>A flat canvas lying on the table, readable from the camera (text "up" points away from it).</summary>
        private RectTransform FlatCanvas(string name, Transform parent, Vector3 localPos, float worldWidth, float pxWidth, float pxHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(pxWidth, pxHeight);
            rt.localScale = Vector3.one * (worldWidth / pxWidth);
            return rt;
        }

        /// <summary>A bronze plate with a gold rim, lying on the table.</summary>
        private Transform Plate(string name, Transform parent, Vector3 localPos, float w, float d)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            Prim(PrimitiveType.Cube, "Slab", root, Vector3.zero, new Vector3(w, 0.1f, d), BronzeDark, _bronze);
            foreach (var sz in new[] { -1, 1 })
                Prim(PrimitiveType.Cube, "RimZ", root, new Vector3(0, 0.04f, sz * (d / 2f - 0.03f)), new Vector3(w, 0.1f, 0.06f), Gold, _gold);
            foreach (var sx in new[] { -1, 1 })
                Prim(PrimitiveType.Cube, "RimX", root, new Vector3(sx * (w / 2f - 0.03f), 0.04f, 0), new Vector3(0.06f, 0.1f, d), Gold, _gold);
            return root;
        }

        private void BuildNameplate(int side, int slot)
        {
            var plate = Plate("Nameplate" + side + slot, _root, NameplatePos(side, slot), NameplateWidth, NameplateDepth);
            const float pxW = 580f, pxH = 220f;
            var rt = FlatCanvas("Face", plate, new Vector3(0, 0.1f, 0), NameplateWidth - 0.12f, pxW, pxH);
            var n = new Nameplate();
            var inlay = Ui.Card("Inlay", rt, new Color(0.07f, 0.035f, 0.025f, 0.94f));
            inlay.rectTransform.Fill(4);

            n.RankBadge = Ui.Icon("Rank", rt, _icons != null ? _icons.uiDiamond : null, 64, Theme.Rank(Rank.Bronze));
            n.RankBadge.rectTransform.Place(12, 6, 64, 64);
            n.RankNumeral = Ui.Label("Numeral", n.RankBadge.transform, "I", 26, TextAnchor.MiddleCenter, new Color(0.12f, 0.06f, 0.03f), FontStyle.Bold);
            n.RankNumeral.rectTransform.Fill();
            n.Name = Ui.Label("Name", rt, "", 50, TextAnchor.MiddleLeft, PlateInk, FontStyle.Bold);
            n.Name.rectTransform.Place(88, 2, 400, 70);
            n.Name.supportRichText = true;
            n.Token = Ui.Icon("Token", rt, _icons != null ? _icons.uiCircle : null, 64, Gold);
            n.Token.rectTransform.Place(pxW - 78, 6, 64, 64);
            n.TokenText = Ui.Label("Text", n.Token.transform, "", 34, TextAnchor.MiddleCenter, new Color(0.14f, 0.07f, 0.03f), FontStyle.Bold);
            n.TokenText.rectTransform.Fill(3);
            n.TokenText.resizeTextForBestFit = true;
            n.TokenText.resizeTextMinSize = 12;
            n.TokenText.resizeTextMaxSize = 34;
            n.Ready = Ui.Card("Ready", rt, Go);
            n.Ready.rectTransform.Place(pxW - 150, 12, 136, 52);
            Ui.Label("Text", n.Ready.transform, "READY", 28, TextAnchor.MiddleCenter, new Color(0.04f, 0.12f, 0.05f), FontStyle.Bold).rectTransform.Fill();
            n.Ready.gameObject.SetActive(false);

            Ui.Label("XpLabel", rt, "XP", 24, TextAnchor.MiddleLeft, PlateDim, FontStyle.Bold).rectTransform.Place(16, 78, 44, 36);
            for (int i = 0; i < 6; i++)
            {
                n.Xp[i] = Ui.Icon("Xp" + i, rt, _icons != null ? _icons.uiDiamond : null, 30, Theme.Xp);
                n.Xp[i].rectTransform.Place(62 + i * 34, 81, 30, 30);
            }
            var crownIcon = Ui.Icon("CrownIcon", rt, _icons != null ? _icons.crown : null, 38, Theme.Crown);
            crownIcon.rectTransform.Place(290, 76, 38, 38);
            n.CrownStat = Ui.Label("Crown", rt, "", 34, TextAnchor.MiddleLeft, PlateInk, FontStyle.Bold);
            n.CrownStat.rectTransform.Place(332, 74, 70, 42);
            var wallIcon = Ui.Icon("WallIcon", rt, _icons != null ? _icons.wall : null, 38, Theme.Barrier);
            wallIcon.rectTransform.Place(410, 76, 38, 38);
            n.WallStat = Ui.Label("Wall", rt, "", 34, TextAnchor.MiddleLeft, PlateInk, FontStyle.Bold);
            n.WallStat.rectTransform.Place(452, 74, 120, 42);

            var divider = Ui.Panel("Divider", rt, new Color(0.93f, 0.72f, 0.36f, 0.3f));
            divider.rectTransform.Place(14, 122, pxW - 28, 2);
            n.ChannelIcon = Ui.Icon("Channel", rt, _icons != null ? (slot == 0 ? _icons.energyA : _icons.energyB) : null, 46, slot == 0 ? Theme.ChannelA : Theme.ChannelB);
            n.ChannelIcon.rectTransform.Place(12, 148, 46, 46);
            for (int i = 0; i < 7; i++)
            {
                // The first two symbols only prime the count: a point needs three (RULES_SPEC 5.2).
                float x = 64 + i * 34 + (i >= 2 ? 10 : 0);
                var cell = Ui.Card("Tally" + i, rt, new Color(0.2f, 0.11f, 0.07f, 1f));
                cell.rectTransform.Place(x, 144, 30, 54);
                n.Tally[i] = cell;
                n.TallyPlus[i] = Ui.Label("Plus", cell.transform, i >= 2 ? "+1" : "", 14, TextAnchor.MiddleCenter, new Color(0.1f, 0.05f, 0.03f), FontStyle.Bold);
                n.TallyPlus[i].rectTransform.Fill();
            }
            n.Result = Ui.Label("Result", rt, "", 32, TextAnchor.MiddleRight, PlateInk, FontStyle.Bold);
            n.Result.rectTransform.Place(314, 138, pxW - 326, 66);
            n.Result.supportRichText = true;
            n.Result.horizontalOverflow = HorizontalWrapMode.Overflow;
            _plates[side, slot] = n;

            var ring = MeshPart("FocusRing" + side + slot, _root, MeshFactory.Arc(1.3f, 1.42f, 0, 360, 0.05f, 48), UnitPos(side, slot) + new Vector3(0, 0.02f, 0), Vector3.one, Theme.Focus, _gloss);
            ring.SetActive(false);
            _focusRings[side, slot] = ring.transform;
        }

        private void BuildSign()
        {
            var bed = Plate("StepSignBed", _root, SignPos, SignWidth, SignDepth);
            // The leaf tips over on its hinge line to change (only the title part moves).
            const float leafDepth = 1.02f;
            float leafZ = SignDepth / 2f - 0.06f - leafDepth / 2f;
            _sign = new GameObject("StepSignLeaf").transform;
            _sign.SetParent(bed, false);
            _sign.localPosition = new Vector3(0, 0.1f, leafZ);
            var rt = FlatCanvas("Face", _sign, Vector3.zero, SignWidth - 0.16f, 790f, 208f);
            Ui.Card("Inlay", rt, new Color(0.06f, 0.03f, 0.02f, 0.96f)).rectTransform.Fill(4);
            _signTitle = Ui.Label("Title", rt, "", 80, TextAnchor.MiddleCenter, PlateInk, FontStyle.Bold);
            _signTitle.rectTransform.Place(10, 6, 770, 120);
            _signTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            _signSub = Ui.Label("Sub", rt, "", 32, TextAnchor.MiddleCenter, PlateDim, FontStyle.Bold);
            _signSub.rectTransform.Place(10, 130, 770, 60);
            _signSub.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Step strip: the order a round resolves in, lit in turn so the sequence is never a guess.
            float stripDepth = SignDepth - 0.12f - leafDepth - 0.06f;
            float stripZ = -SignDepth / 2f + 0.06f + stripDepth / 2f;
            var strip = FlatCanvas("Steps", bed, new Vector3(0, 0.1f, stripZ), SignWidth - 0.16f, 790f, 790f * stripDepth / (SignWidth - 0.16f));
            float h = strip.sizeDelta.y;
            string[] steps = { "XP", "WALL", "ENERGY", "ACTIONS" };
            const float gap = 8f;
            float w = (790f - gap * 5f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var plate = Ui.Card("Step" + i, strip, new Color(0.12f, 0.06f, 0.04f, 0.95f), true);
                plate.rectTransform.Place(gap + i * (w + gap), 4, w, h - 8);
                _stepPlates[i] = plate;
                _stepLabels[i] = Ui.Label("Label", plate.transform, (i + 1) + "  " + steps[i], 28, TextAnchor.MiddleCenter, PlateDim, FontStyle.Bold);
                _stepLabels[i].rectTransform.Fill();
                _stepLabels[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        private void BuildLever()
        {
            var lever = new GameObject("SpinLever").transform;
            lever.SetParent(_root, false);
            lever.localPosition = LeverPos;
            Prim(PrimitiveType.Cylinder, "Base", lever, new Vector3(0, 0.1f, 0), new Vector3(1.1f, 0.1f, 1.1f), BronzeDark, _bronze);
            MeshPart("BaseRim", lever, MeshFactory.Arc(0.5f, 0.58f, 0, 360, 0.24f, 32), Vector3.zero, Vector3.one, Gold, _gold);
            Prim(PrimitiveType.Cube, "Housing", lever, new Vector3(0, 0.38f, 0), new Vector3(0.5f, 0.42f, 0.62f), Bronze, _bronze);
            _leverArm = new GameObject("Arm").transform;
            _leverArm.SetParent(lever, false);
            _leverArm.localPosition = new Vector3(0, 0.55f, 0);
            Prim(PrimitiveType.Cylinder, "Shaft", _leverArm, new Vector3(0, 0.6f, 0), new Vector3(0.12f, 0.6f, 0.12f), Gold, _gold);
            Prim(PrimitiveType.Sphere, "Knob", _leverArm, new Vector3(0, 1.25f, 0), Vector3.one * 0.38f, Ruby, _gloss);
            for (int i = 0; i < 3; i++)
            {
                var at = new Vector3(-1.05f + i * 0.5f, 0.08f, -1.0f);
                MeshPart("LampRim", lever, MeshFactory.Arc(0.14f, 0.2f, 0, 360, 0.08f, 20), at, Vector3.one, Gold, _gold);
                _spinLamps[i] = Prim(PrimitiveType.Sphere, "Lamp" + i, lever, at + new Vector3(0, 0.1f, 0), Vector3.one * 0.26f, Gold, _gloss).GetComponent<Renderer>();
            }
            var rt = FlatCanvas("Label", lever, new Vector3(-0.45f, 0.08f, -1.55f), 1.8f, 380f, 80f);
            Ui.Card("Plate", rt, new Color(0.1f, 0.05f, 0.03f, 0.95f), true).rectTransform.Fill();
            _leverLabel = Ui.Label("Text", rt, "SPIN", 44, TextAnchor.MiddleCenter, PlateInk, FontStyle.Bold);
            _leverLabel.rectTransform.Fill();
            _leverLabel.resizeTextForBestFit = true;
            _leverLabel.resizeTextMinSize = 18;
            _leverLabel.resizeTextMaxSize = 44;
        }

        private void BuildRoundDial()
        {
            var dial = Plate("RoundDial", _root, RoundDialPos + new Vector3(0, 0.07f, -0.35f), 1.4f, 1.25f);
            var rt = FlatCanvas("Face", dial, new Vector3(0, 0.1f, 0), 1.28f, 260f, 230f);
            Ui.Card("Inlay", rt, new Color(0.06f, 0.03f, 0.02f, 0.95f)).rectTransform.Fill(4);
            Ui.Label("Caption", rt, "ROUND", 34, TextAnchor.MiddleCenter, PlateDim, FontStyle.Bold).rectTransform.Place(0, 12, 260, 50);
            _roundText = Ui.Label("Number", rt, "1", 120, TextAnchor.MiddleCenter, PlateInk, FontStyle.Bold);
            _roundText.rectTransform.Place(0, 58, 260, 160);
        }

        private Token NewToken()
        {
            var go = new GameObject("Token", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            canvas.sortingOrder = 5;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(100, 100);
            rt.localScale = Vector3.one * 0.0065f;
            var img = Ui.Icon("Icon", go.transform, null, 100, Color.white);
            img.rectTransform.Fill();
            _billboards.Add(go.transform);
            go.SetActive(false);
            return new Token { Root = go.transform, Image = img };
        }

        // ------------------------------------------------------------------ geometry for the interface overlays

        /// <summary>Screen-overlay anchor points around a podium and its piece.</summary>
        public void PodiumCorners(int side, int slot, Vector3[] corners)
        {
            var p = UnitPos(side, slot);
            corners[0] = p + new Vector3(-1.25f, 0, -1.25f);
            corners[1] = p + new Vector3(1.25f, 0, -1.25f);
            corners[2] = p + new Vector3(-1.25f, 2.4f, 1.25f);
            corners[3] = p + new Vector3(1.25f, 2.4f, 1.25f);
            for (int i = 0; i < 4; i++) corners[i] = transform.TransformPoint(corners[i]);
        }

        /// <summary>Screen-overlay anchor points around the lever, its lamps and its label.</summary>
        public void LeverCorners(Vector3[] corners)
        {
            var p = LeverPos;
            corners[0] = p + new Vector3(-1.4f, 0, -1.9f);
            corners[1] = p + new Vector3(0.7f, 0, -1.9f);
            corners[2] = p + new Vector3(-1.4f, 1.9f, 0.6f);
            corners[3] = p + new Vector3(0.7f, 1.9f, 0.6f);
            for (int i = 0; i < 4; i++) corners[i] = transform.TransformPoint(corners[i]);
        }

        // ------------------------------------------------------------------ per frame

        private void RenderInstruments(TableFrame f, float dt)
        {
            RenderSign(f, dt);
            RenderLever(f, dt);
            _roundText.text = Mathf.Max(1, f.Round).ToString();
            RenderTokens(f);
            for (int side = 0; side < 2; side++)
                for (int slot = 0; slot < 2; slot++)
                {
                    bool focus = f.FocusSide == side && f.FocusSlot == slot;
                    var ring = _focusRings[side, slot];
                    ring.gameObject.SetActive(focus);
                    if (focus) ring.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(Time.unscaledTime * 5f));
                }
        }

        private static readonly BoardPhase[] StepOrder = { BoardPhase.Xp, BoardPhase.Wall, BoardPhase.Energy, BoardPhase.Actions };

        private void RenderSign(TableFrame f, float dt)
        {
            string title, sub;
            bool resolving = f.State == UxState.Reveal || f.State == UxState.Resolving;
            switch (f.State)
            {
                case UxState.RoundReady: title = "ROUND " + f.Round; sub = "PULL THE LEVER TO SPIN"; break;
                case UxState.Spinning: title = "SPINNING"; sub = "SPIN " + Mathf.Max(1, f.SpinsUsed) + " OF 3"; break;
                case UxState.SpinDecision:
                    title = f.CanConfirm ? "LOCK IT IN?" : "LOCK OR SPIN";
                    sub = f.CanConfirm ? "ALL FIVE LOCKED  -  PULL TO CONFIRM" : (3 - f.SpinsUsed) + (3 - f.SpinsUsed == 1 ? " SPIN LEFT" : " SPINS LEFT");
                    break;
                case UxState.AiCommit: title = "OPPONENT"; sub = "CHOOSING THEIR REELS"; break;
                case UxState.MatchResult: title = "MATCH OVER"; sub = ""; break;
                default:
                    switch (f.Phase)
                    {
                        case BoardPhase.Reveal: title = "REVEAL"; sub = "BOTH SIDES SHOW THEIR REELS"; break;
                        case BoardPhase.Xp: title = "XP"; sub = "STARS GO TO THEIR PIECES"; break;
                        case BoardPhase.Wall: title = "WALL"; sub = "HAMMERS BUILD THE WALL"; break;
                        case BoardPhase.Energy: title = "ENERGY"; sub = "GEMS CHARGE THEIR PIECES"; break;
                        case BoardPhase.Actions:
                            title = "ACTIONS";
                            int n = f.ActionOrder != null ? f.ActionOrder.Count : 0;
                            sub = n == 0 ? "NO ONE IS READY" : "ACTION " + Mathf.Clamp(f.ActionsStarted, 1, n) + " OF " + n;
                            break;
                        case BoardPhase.RoundEnd: title = "ROUND OVER"; sub = ""; break;
                        default: title = "REVEAL"; sub = ""; break;
                    }
                    break;
            }
            // Mechanical flip: the sign tips onto its edge, changes, and tips back.
            if (title != _signWantTitle) { _signWantTitle = title; if (f.ReducedMotion) _signFlip = 0.5f; else if (_signFlip >= 1f) _signFlip = 0f; }
            _signWantSub = sub;
            if (_signFlip < 1f)
            {
                _signFlip = Mathf.Min(1f, _signFlip + dt * 4f);
                if (_signFlip >= 0.5f && _signShownTitle != _signWantTitle) { _signShownTitle = _signWantTitle; _signTitle.text = _signShownTitle; }
            }
            float angle = f.ReducedMotion ? 0f : (_signFlip < 0.5f ? _signFlip * 180f : (_signFlip - 1f) * 180f);
            _sign.localRotation = Quaternion.Euler(angle, 0, 0);
            if (_signShownTitle == _signWantTitle) _signSub.text = _signWantSub;

            // Step order: lit for the current step, dimmed once done, dark until reached.
            int current = System.Array.IndexOf(StepOrder, f.Phase);
            for (int i = 0; i < 4; i++)
            {
                bool now = resolving && i == current;
                bool done = resolving && current >= 0 && i < current || f.Phase == BoardPhase.RoundEnd && f.State == UxState.Resolving;
                float pulse = now ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
                _stepPlates[i].color = now ? Color.Lerp(new Color(0.45f, 0.28f, 0.08f), Gold, pulse) : done ? new Color(0.22f, 0.14f, 0.07f, 0.95f) : new Color(0.12f, 0.06f, 0.04f, 0.95f);
                _stepLabels[i].color = now ? new Color(0.12f, 0.06f, 0.02f) : done ? Gold * 0.9f : PlateDim * 0.8f;
            }
        }

        private void RenderLever(TableFrame f, float dt)
        {
            // Pull once for each player spin, as the reels start turning.
            bool spinStart = f.Current != null && f.Current.Type == MatchEventType.ReelsSpun && f.Current.Side == 0;
            if (spinStart && f.Current.Sequence != _lastSpinSerial && !f.ReducedMotion) { _lastSpinSerial = f.Current.Sequence; _leverPull = 1f; }
            _leverPull = Mathf.MoveTowards(_leverPull, 0f, dt * 2.2f);
            float pulled = Mathf.Sin(Mathf.Clamp01(_leverPull) * Mathf.PI);
            _leverArm.localRotation = Quaternion.Euler(18f - 70f * pulled, 0, 0);
            int left = Mathf.Clamp(3 - f.SpinsUsed, 0, 3);
            for (int i = 0; i < 3; i++) SetColor(_spinLamps[i], i < left ? Gold : new Color(0.16f, 0.09f, 0.05f));
            bool presenting = f.State == UxState.Spinning || f.State == UxState.AiCommit || f.State == UxState.Reveal || f.State == UxState.Resolving;
            _leverLabel.text = f.CanConfirm ? "LOCK IN" : f.CanSpin ? (f.SpinsUsed == 0 ? "SPIN" : "SPIN AGAIN") : presenting ? "HOLD TO SPEED UP" : "...";
            _leverLabel.color = f.CanConfirm ? Go : f.CanSpin ? PlateInk : PlateDim;
        }

        /// <summary>Symbols fly from the reel faces that produced them to where they apply.</summary>
        private void RenderTokens(TableFrame f)
        {
            int used = 0;
            var e = f.Current;
            if (e != null && !f.ReducedMotion && f.Progress < 1f && f.Match != null && f.OpponentRevealed)
            {
                bool xp = e.Type == MatchEventType.PanelXpGranted;
                bool energy = e.Type == MatchEventType.EnergyGranted && e.Source == EnergySource.Reels;
                bool hammer = e.Type == MatchEventType.BarrierBuilt && e.Stage == 2;
                if (xp || energy || hammer)
                {
                    var defs = f.Match.ReelDefinitions((SideId)e.Side);
                    Vector3 target = hammer ? CrownPos(e.Side) + new Vector3(0, 0.8f, e.Side == 0 ? WallRadius : -WallRadius)
                        : xp ? NameplatePos(e.Side, e.Slot) + new Vector3(-0.2f, 0.4f, 0.05f)
                        : UnitPos(e.Side, e.Slot) + new Vector3(0, 0.6f, -0.9f);
                    int k = 0;
                    for (int r = 0; r < 5; r++)
                    {
                        int fi = f.Visual.Face[e.Side, r];
                        if (fi < 0) continue;
                        var face = defs[r].Faces[fi];
                        int count = xp ? (face.XpChannel.HasValue && (int)face.XpChannel.Value == e.Slot ? 1 : 0)
                            : hammer ? face.Hammer
                            : e.Slot == 0 ? face.ChannelA : face.ChannelB;
                        var from = _drums[e.Side, r].position + new Vector3(0, DrumApothem + 0.2f, 0);
                        for (int c = 0; c < count && used < _tokens.Count; c++, k++)
                        {
                            var tok = _tokens[used++];
                            tok.Image.sprite = _icons == null ? null : xp ? _icons.xp : hammer ? _icons.hammer : e.Slot == 0 ? _icons.energyA : _icons.energyB;
                            float t = Mathf.Clamp01(f.Progress * 2.1f - k * 0.07f);
                            float ease = t * t * (3f - 2f * t);
                            var p = Vector3.Lerp(from + new Vector3((c - (count - 1) / 2f) * 0.3f, 0, 0), target, ease);
                            p.y += Mathf.Sin(ease * Mathf.PI) * 1.6f;
                            tok.Root.position = transform.TransformPoint(p);
                            float pop = t >= 1f ? Mathf.Clamp01(1f - (f.Progress * 2.1f - k * 0.07f - 1f) * 3f) : 1f;
                            tok.Root.localScale = Vector3.one * 0.0065f * (0.8f + 0.4f * Mathf.Sin(ease * Mathf.PI)) * pop;
                            tok.Root.gameObject.SetActive(pop > 0.02f);
                        }
                    }
                }
            }
            for (int i = used; i < _tokens.Count; i++) if (_tokens[i].Root.gameObject.activeSelf) _tokens[i].Root.gameObject.SetActive(false);
        }

        /// <summary>Nameplate: rank, XP, stats; while you decide, the locked-gem tally and exactly what it will give.</summary>
        private void RenderNameplate(TableFrame f, int side, int slot, UnitDefinition def, Rank rank, int xp, int energy, int cost, OutcomePreview.UnitLine preview)
        {
            var n = _plates[side, slot];
            n.RankBadge.color = FigureFinish(rank);
            n.RankNumeral.text = rank == Rank.Gold ? "III" : rank == Rank.Silver ? "II" : "I";
            n.Name.text = def.DisplayName.ToUpperInvariant();
            n.Name.color = PlateInk;
            int xpIn = preview != null ? preview.XpFaces : 0;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
            for (int i = 0; i < 6; i++)
                n.Xp[i].color = i < xp ? Theme.Xp : i < xp + xpIn ? Color.Lerp(new Color(0.25f, 0.2f, 0.45f), Color.white, pulse * 0.6f) : new Color(0.22f, 0.15f, 0.2f);
            var s = def.Stats(rank);
            n.CrownStat.text = def.Action == ActionKind.PriestBlessing ? "+" + s.Heal : s.CrownDamage.ToString();
            n.WallStat.text = def.Action == ActionKind.AssassinStrike ? "-" + s.Delay + " EN" : s.BarrierDamage.ToString();

            var channel = slot == 0 ? Theme.ChannelA : Theme.ChannelB;
            if (preview != null)
            {
                // Locked gems, the two that only prime the count, and each one after that worth +1 energy.
                int sym = preview.Symbols;
                for (int i = 0; i < 7; i++)
                {
                    bool has = i < sym;
                    n.Tally[i].color = !has ? new Color(0.2f, 0.11f, 0.07f, 1f) : i < 2 ? new Color(channel.r * 0.55f, channel.g * 0.55f, channel.b * 0.55f, 1f) : channel;
                    n.TallyPlus[i].enabled = has && i >= 2;
                }
                string result;
                if (sym == 0) result = "<color=#B89A76>3 GEMS = +1</color>";
                else if (preview.EnergyGain == 0) result = "<color=#B89A76>" + OutcomePreview.SymbolsToNextPoint(sym) + " MORE = +1</color>";
                else result = "+" + preview.EnergyGain + " ENERGY";
                if (preview.Wasted > 0) result = "<color=#FF5548>+" + preview.EnergyGain + "  (" + preview.Wasted + " WASTED)</color>";
                n.Result.text = result;
                n.Result.color = preview.Wasted > 0 ? Waste : channel;
            }
            else
            {
                for (int i = 0; i < 7; i++) { n.Tally[i].color = new Color(0.14f, 0.08f, 0.05f, 1f); n.TallyPlus[i].enabled = false; }
                n.Result.text = "<color=#B89A76>ENERGY</color>  " + energy + " / " + cost;
                n.Result.color = PlateInk;
            }

            // Order token: during the actions step, the order pieces act in; before it, who is ready.
            int order = -1;
            if (f.ActionOrder != null && (f.Phase == BoardPhase.Actions || f.Phase == BoardPhase.RoundEnd))
                for (int i = 0; i < f.ActionOrder.Count; i++)
                    if (f.ActionOrder[i].side == side && f.ActionOrder[i].slot == slot) { order = i; break; }
            bool willAct = preview != null ? preview.ReadyBeforeResolution : energy >= cost;
            bool ready = order < 0 && willAct && f.Phase != BoardPhase.Actions;
            n.Ready.gameObject.SetActive(ready);
            if (ready) n.Ready.color = Color.Lerp(Go * 0.75f, Go, pulse);
            if (order >= 0)
            {
                bool active = order == f.ActionsStarted - 1 && f.Phase == BoardPhase.Actions;
                bool done = order < f.ActionsStarted - 1 || f.Phase == BoardPhase.RoundEnd;
                n.Token.gameObject.SetActive(true);
                n.Token.color = active ? Color.Lerp(Gold, Color.white, 0.3f * pulse) : done ? new Color(0.3f, 0.2f, 0.12f) : new Color(0.92f, 0.82f, 0.62f);
                n.TokenText.text = (order + 1).ToString();
                n.TokenText.color = done ? PlateDim : new Color(0.14f, 0.07f, 0.03f);
            }
            else n.Token.gameObject.SetActive(false);
        }
    }
}
