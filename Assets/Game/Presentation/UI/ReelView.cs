using System.Collections.Generic;
using Tabletop.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>
    /// One reel tile. Encodes symbol family by shape, quantity by repeated marks, XP by a badge,
    /// and lock state by padlock glyph + frame + "LOCKED" text (MATCH_UX_SPEC 5.4).
    /// </summary>
    public sealed class ReelView
    {
        public readonly Button Button;
        public readonly int Index;
        private readonly RectTransform _symbols;
        private readonly Text _caption;
        private readonly Text _blank;
        private readonly GameObject _xpBadge;
        private readonly Text _xpText;
        private readonly GameObject _lockGroup;
        private readonly GameObject _lockFrame;
        private readonly Text _lockText;
        private readonly GameObject _cover;
        private readonly Image _bg;
        private readonly List<GameObject> _glyphs = new List<GameObject>();
        private string _shownCode;

        public ReelView(Transform parent, int index, float x, float y, float w, float h, bool interactive)
        {
            Index = index;
            Button = Ui.Button("Reel" + (index + 1), parent, "", null, 20, Theme.PanelDark);
            Button.GetComponentInChildren<Text>().gameObject.SetActive(false);
            ((RectTransform)Button.transform).Place(x, y, w, h);
            Button.interactable = interactive;
            _bg = Button.GetComponent<Image>();

            var number = Ui.Label("Number", Button.transform, "REEL " + (index + 1), 14, TextAnchor.UpperLeft, Theme.TextDim);
            number.rectTransform.Place(6, 4, 90, 20);

            _symbols = Ui.Rect("Symbols", Button.transform);
            _symbols.anchorMin = _symbols.anchorMax = new Vector2(0.5f, 0.5f);
            _symbols.pivot = new Vector2(0.5f, 0.5f);
            _symbols.sizeDelta = new Vector2(w - 20, h * 0.42f);
            _symbols.anchoredPosition = new Vector2(0, h * 0.06f);
            var layout = _symbols.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 4;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;

            _blank = Ui.Label("Blank", Button.transform, "BLANK", 18, TextAnchor.MiddleCenter, Theme.TextDim);
            _blank.rectTransform.Fill();

            _caption = Ui.Label("Caption", Button.transform, "", 15, TextAnchor.LowerCenter, Theme.Text);
            _caption.rectTransform.anchorMin = new Vector2(0, 0);
            _caption.rectTransform.anchorMax = new Vector2(1, 0);
            _caption.rectTransform.pivot = new Vector2(0.5f, 0);
            _caption.rectTransform.anchoredPosition = new Vector2(0, 4);
            _caption.rectTransform.sizeDelta = new Vector2(-8, 22);

            var badge = Ui.Panel("XpBadge", Button.transform, Theme.Xp);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1, 1);
            badge.rectTransform.pivot = new Vector2(1, 1);
            badge.rectTransform.anchoredPosition = new Vector2(-4, -4);
            badge.rectTransform.sizeDelta = new Vector2(46, 24);
            _xpText = Ui.Label("Text", badge.transform, "XP*", 15, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            _xpText.rectTransform.Fill();
            _xpBadge = badge.gameObject;

            // Lock cue 1: thick red frame.
            var frame = Ui.Rect("LockFrame", Button.transform);
            frame.Fill();
            foreach (var bar in new[] { (new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -6), Vector2.zero), (Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 6)),
                         (Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(6, 0)), (new Vector2(1, 0), Vector2.one, new Vector2(-6, 0), Vector2.zero) })
            {
                var img = Ui.Panel("Bar", frame, Theme.Locked);
                img.raycastTarget = false;
                img.rectTransform.anchorMin = bar.Item1;
                img.rectTransform.anchorMax = bar.Item2;
                img.rectTransform.offsetMin = bar.Item3;
                img.rectTransform.offsetMax = bar.Item4;
            }
            _lockFrame = frame.gameObject;

            // Lock cue 2: padlock glyph + text.
            var lockGroup = Ui.Rect("Padlock", Button.transform);
            lockGroup.anchorMin = lockGroup.anchorMax = new Vector2(0, 1);
            lockGroup.pivot = new Vector2(0, 1);
            lockGroup.anchoredPosition = new Vector2(6, -24);
            lockGroup.sizeDelta = new Vector2(28, 30);
            var body = Ui.Panel("Body", lockGroup, Theme.Locked).rectTransform;
            body.Place(0, 12, 28, 18);
            var shL = Ui.Panel("ShackleL", lockGroup, Theme.Locked).rectTransform;
            shL.Place(4, 0, 5, 14);
            var shR = Ui.Panel("ShackleR", lockGroup, Theme.Locked).rectTransform;
            shR.Place(19, 0, 5, 14);
            var shT = Ui.Panel("ShackleT", lockGroup, Theme.Locked).rectTransform;
            shT.Place(4, 0, 20, 5);
            _lockGroup = lockGroup.gameObject;
            _lockText = Ui.Label("LockText", Button.transform, "LOCKED", 15, TextAnchor.UpperLeft, Theme.Locked, FontStyle.Bold);
            _lockText.rectTransform.Place(38, 30, 90, 20);

            var cover = Ui.Panel("Cover", Button.transform, new Color(0.25f, 0.22f, 0.2f, 1f));
            cover.rectTransform.Fill(3);
            cover.raycastTarget = false;
            var q = Ui.Label("Q", cover.transform, "?\nHIDDEN", 22, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
            q.rectTransform.Fill();
            _cover = cover.gameObject;

            SetFace(null);
            SetLocked(false);
            SetHidden(false);
        }

        public string Description { get; private set; } = "";

        public void SetHidden(bool hidden) => _cover.SetActive(hidden);

        public void SetLocked(bool locked)
        {
            _lockFrame.SetActive(locked);
            _lockGroup.SetActive(locked);
            _lockText.gameObject.SetActive(locked);
            _bg.color = locked ? new Color(0.30f, 0.14f, 0.14f, 1f) : Theme.PanelDark;
        }

        /// <summary>Shows a face (null = no face yet).</summary>
        public void SetFace(ReelFace face, string unitAName = "left unit", string unitBName = "right unit")
        {
            string code = face == null ? "" : face.Code;
            if (code == _shownCode) return;
            _shownCode = code;
            foreach (var g in _glyphs)
            {
                g.SetActive(false); // hide immediately; Destroy is deferred to end of frame
                Object.Destroy(g);
            }
            _glyphs.Clear();
            if (face == null)
            {
                _blank.text = "--";
                _blank.gameObject.SetActive(true);
                _caption.text = "";
                _xpBadge.SetActive(false);
                Description = "Reel " + (Index + 1) + ", not spun";
                return;
            }
            _blank.text = "BLANK";
            _blank.gameObject.SetActive(face.IsBlank);
            for (int i = 0; i < face.ChannelA; i++) _glyphs.Add(Ui.Glyph("A", _symbols, GlyphShape.Square, 34).transform.parent.gameObject);
            for (int i = 0; i < face.ChannelB; i++) _glyphs.Add(Ui.Glyph("B", _symbols, GlyphShape.Diamond, 34).transform.parent.gameObject);
            for (int i = 0; i < face.Hammer; i++) _glyphs.Add(Ui.Glyph("H", _symbols, GlyphShape.Hammer, 34).transform.parent.gameObject);

            string caption;
            if (face.ChannelA > 0) caption = "A x" + face.ChannelA;
            else if (face.ChannelB > 0) caption = "B x" + face.ChannelB;
            else if (face.Hammer > 0) caption = "HAMMER x" + face.Hammer;
            else caption = "blank";
            _xpBadge.SetActive(face.XpChannel.HasValue);
            if (face.XpChannel.HasValue)
            {
                _xpText.text = "XP " + (face.XpChannel == Channel.A ? "A" : "B");
                caption += " +XP";
            }
            _caption.text = caption;
            Description = "Reel " + (Index + 1) + ", " + caption.Replace("x", "times ")
                + (face.XpChannel.HasValue ? ", grants XP to " + (face.XpChannel == Channel.A ? unitAName : unitBName) : "");
        }
    }
}
