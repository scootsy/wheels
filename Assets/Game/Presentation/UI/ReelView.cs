using System.Collections.Generic;
using Tabletop.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>
    /// One reel tile. Symbol family by icon and shape, quantity by repeated icons, XP by a star badge,
    /// who the face feeds by a caption, and lock state by padlock + frame + "LOCKED" text (MATCH_UX_SPEC 5.4).
    /// </summary>
    public sealed class ReelView
    {
        public readonly Button Button;
        public readonly int Index;
        private readonly IconSet _icons;
        private readonly RectTransform _symbols;
        private readonly Text _caption;
        private readonly Text _count;
        private readonly GameObject _xpBadge;
        private readonly GameObject _lockGroup;
        private readonly GameObject _lockFrame;
        private readonly GameObject _cover;
        private readonly Image _bg;
        private readonly List<GameObject> _glyphs = new List<GameObject>();
        private readonly float _iconSize;
        private string _shownKey;
        private bool _overlay;
        private readonly Text _number;

        public ReelView(Transform parent, int index, float x, float y, float w, float h, bool interactive, IconSet icons)
        {
            Index = index;
            _icons = icons;
            _iconSize = Mathf.Min(56f, (w - 24) / 3f);
            Button = Ui.Button("Reel" + (index + 1), parent, "", null, 20, Theme.PanelDark);
            Button.GetComponentInChildren<Text>().gameObject.SetActive(false);
            ((RectTransform)Button.transform).Place(x, y, w, h);
            Button.interactable = interactive;
            _bg = Button.GetComponent<Image>();

            var number = Ui.Label("Number", Button.transform, (index + 1).ToString(), 16, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Bold);
            number.rectTransform.Place(8, 4, 30, 22);
            _number = number;

            _symbols = Ui.Rect("Symbols", Button.transform);
            _symbols.anchorMin = _symbols.anchorMax = new Vector2(0.5f, 0.5f);
            _symbols.pivot = new Vector2(0.5f, 0.5f);
            _symbols.sizeDelta = new Vector2(w - 12, _iconSize);
            _symbols.anchoredPosition = new Vector2(0, h * 0.08f);
            var layout = _symbols.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 2;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;

            _count = Ui.Label("Count", Button.transform, "", 15, TextAnchor.UpperRight, Theme.Text, FontStyle.Bold);
            _count.rectTransform.Place(w - 44, 4, 38, 22);

            _caption = Ui.Label("Caption", Button.transform, "", 15, TextAnchor.LowerCenter, Theme.Text, FontStyle.Bold);
            _caption.rectTransform.anchorMin = new Vector2(0, 0);
            _caption.rectTransform.anchorMax = new Vector2(1, 0);
            _caption.rectTransform.pivot = new Vector2(0.5f, 0);
            _caption.rectTransform.anchoredPosition = new Vector2(0, 4);
            _caption.rectTransform.sizeDelta = new Vector2(-6, 40);

            var badge = Ui.Rect("XpBadge", Button.transform);
            badge.anchorMin = badge.anchorMax = new Vector2(1, 1);
            badge.pivot = new Vector2(1, 1);
            badge.anchoredPosition = new Vector2(-2, -22);
            badge.sizeDelta = new Vector2(34, 34);
            Ui.Icon("Star", badge, icons != null ? icons.xp : null, 34, Theme.Xp).rectTransform.Fill();
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

            // Lock cue 2: padlock icon + text.
            var lockGroup = Ui.Rect("Padlock", Button.transform);
            lockGroup.anchorMin = lockGroup.anchorMax = new Vector2(0.5f, 1);
            lockGroup.pivot = new Vector2(0.5f, 0.5f);
            lockGroup.anchoredPosition = new Vector2(4, -16);
            lockGroup.sizeDelta = new Vector2(100, 30);
            var pad = Ui.Icon("Icon", lockGroup, icons != null ? icons.padlock : null, 30, Theme.Locked);
            pad.rectTransform.Place(0, 0, 30, 30);
            var lockText = Ui.Label("LockText", lockGroup, "LOCKED", 16, TextAnchor.MiddleLeft, Theme.Locked, FontStyle.Bold);
            lockText.rectTransform.Place(32, 0, 70, 30);
            var outline = lockText.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            _lockGroup = lockGroup.gameObject;

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

        public RectTransform Rect => (RectTransform)Button.transform;

        /// <summary>
        /// Overlay mode (D-027): the tile sits invisibly over a 3D reel drum on the table, which shows the symbols.
        /// It keeps what the drum can't say: focus, the lock cues, and who each face feeds.
        /// </summary>
        public void SetOverlay(bool overlay)
        {
            _overlay = overlay;
            _symbols.gameObject.SetActive(!overlay);
            _xpBadge.SetActive(false);
            _count.gameObject.SetActive(!overlay);
            _number.color = overlay ? new Color(1f, 0.9f, 0.7f, 0.8f) : Theme.TextDim;
            var outline = _caption.GetComponent<Outline>();
            if (overlay && outline == null) { outline = _caption.gameObject.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(2, -2); }
            SetLocked(_lockFrame.activeSelf);
            SetHidden(IsHidden);
            _shownKey = null;
        }

        /// <summary>True while this reel's result must not be shown (the opponent's, before the reveal).</summary>
        public bool IsHidden { get; private set; }

        /// <summary>Puts the caption above the tile instead of below (the opponent's tiles, so it clears their crown).</summary>
        public void SetCaptionOnTop(bool top)
        {
            var rt = _caption.rectTransform;
            rt.anchorMin = new Vector2(0, top ? 1 : 0);
            rt.anchorMax = new Vector2(1, top ? 1 : 0);
            rt.pivot = new Vector2(0.5f, top ? 1 : 0);
            rt.anchoredPosition = new Vector2(0, top ? -2 : 4);
            _caption.alignment = top ? TextAnchor.UpperCenter : TextAnchor.LowerCenter;
        }

        public void SetHidden(bool hidden)
        {
            IsHidden = hidden;
            _cover.SetActive(hidden && !_overlay); // in overlay mode the table's shutter covers the drum
        }

        public void SetLocked(bool locked)
        {
            _lockFrame.SetActive(locked);
            _lockGroup.SetActive(locked);
            _count.enabled = !locked; // the LOCKED tab takes the top row
            // Overlay tiles stay clear (the drum shows through) but still catch clicks.
            _bg.color = _overlay ? new Color(0, 0, 0, locked ? 0.12f : 0f) : locked ? new Color(0.30f, 0.14f, 0.14f, 1f) : Theme.PanelDark;
        }

        private void AddIcons(Sprite sprite, Color fallback, int count, GlyphShape shape)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject g = sprite != null
                    ? Ui.Icon("Icon", _symbols, sprite, _iconSize, fallback).gameObject
                    : Ui.Glyph("Glyph", _symbols, shape, _iconSize).transform.parent.gameObject;
                _glyphs.Add(g);
            }
        }

        /// <summary>Shows a face (null = no face yet). Unit names say who each channel feeds.</summary>
        public void SetFace(ReelFace face, string unitAName = "left unit", string unitBName = "right unit")
        {
            string key = (face == null ? "" : face.Code) + "|" + unitAName + "|" + unitBName;
            if (key == _shownKey) return;
            _shownKey = key;
            foreach (var g in _glyphs)
            {
                g.SetActive(false); // hide immediately; Destroy is deferred to end of frame
                Object.Destroy(g);
            }
            _glyphs.Clear();
            _count.text = "";
            if (face == null)
            {
                _caption.text = "";
                _xpBadge.SetActive(false);
                Description = "Reel " + (Index + 1) + ", not spun";
                return;
            }

            string caption;
            if (face.ChannelA > 0)
            {
                AddIcons(_icons != null ? _icons.energyA : null, Theme.ChannelA, face.ChannelA, GlyphShape.Square);
                caption = "A  -> " + unitAName.ToUpperInvariant();
                _count.text = "x" + face.ChannelA;
            }
            else if (face.ChannelB > 0)
            {
                AddIcons(_icons != null ? _icons.energyB : null, Theme.ChannelB, face.ChannelB, GlyphShape.Diamond);
                caption = "B  -> " + unitBName.ToUpperInvariant();
                _count.text = "x" + face.ChannelB;
            }
            else if (face.Hammer > 0)
            {
                AddIcons(_icons != null ? _icons.hammer : null, Theme.Hammer, face.Hammer, GlyphShape.Hammer);
                caption = "HAMMER -> WALL";
                _count.text = "x" + face.Hammer;
            }
            else
            {
                AddIcons(_icons != null ? _icons.blank : null, Theme.TextDim, 1, GlyphShape.Square);
                caption = "BLANK";
            }
            _xpBadge.SetActive(face.XpChannel.HasValue && !_overlay);
            if (face.XpChannel.HasValue) caption += "\n+1 XP";
            _caption.text = caption;
            Description = "Reel " + (Index + 1) + ", " + caption.Replace("\n", ", ").Replace("->", "feeds") + (_count.text.Length > 0 ? " (" + _count.text + ")" : "");
        }
    }
}
