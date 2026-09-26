using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>
    /// Palette (D-028): warm lacquered-wood darks, cream text, bronze and gold accents to match the table.
    /// Every colored state also has a text, glyph, or shape cue.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Background = new Color(0.06f, 0.03f, 0.022f, 1f);
        public static readonly Color Panel = new Color(0.14f, 0.08f, 0.058f, 0.96f);
        public static readonly Color PanelDark = new Color(0.09f, 0.05f, 0.036f, 0.97f);
        public static readonly Color Overlay = new Color(0.02f, 0.01f, 0.006f, 0.80f);
        public static readonly Color Text = new Color(0.98f, 0.94f, 0.86f, 1f);
        public static readonly Color TextDim = new Color(0.76f, 0.67f, 0.56f, 1f);
        public static readonly Color Gilt = new Color(0.93f, 0.72f, 0.36f, 1f);
        public static readonly Color Hairline = new Color(0.93f, 0.72f, 0.36f, 0.45f);
        public static readonly Color ChannelA = new Color(0.95f, 0.55f, 0.20f, 1f);   // orange square
        public static readonly Color ChannelB = new Color(0.30f, 0.80f, 0.85f, 1f);   // teal diamond
        public static readonly Color Hammer = new Color(0.80f, 0.80f, 0.82f, 1f);
        public static readonly Color Xp = new Color(0.55f, 0.45f, 0.95f, 1f);
        public static readonly Color Focus = new Color(1f, 0.84f, 0.42f, 1f);
        public static readonly Color Locked = new Color(0.95f, 0.30f, 0.30f, 1f);
        public static readonly Color Button = new Color(0.25f, 0.15f, 0.10f, 1f);
        public static readonly Color ButtonPrimary = new Color(0.66f, 0.38f, 0.13f, 1f);
        public static readonly Color Disabled = new Color(0.18f, 0.12f, 0.09f, 1f);
        public static readonly Color Crown = new Color(0.95f, 0.80f, 0.30f, 1f);
        public static readonly Color Barrier = new Color(0.55f, 0.58f, 0.65f, 1f);
        public static readonly Color Damage = new Color(1f, 0.35f, 0.30f, 1f);
        public static readonly Color Heal = new Color(0.40f, 0.95f, 0.45f, 1f);
        public static readonly Color Player = new Color(0.35f, 0.60f, 1f, 1f);
        public static readonly Color Enemy = new Color(1f, 0.45f, 0.45f, 1f);

        public static Color Rank(Tabletop.Domain.Rank r)
        {
            switch (r)
            {
                case Tabletop.Domain.Rank.Silver: return new Color(0.78f, 0.80f, 0.85f, 1f);
                case Tabletop.Domain.Rank.Gold: return new Color(0.95f, 0.78f, 0.25f, 1f);
                default: return new Color(0.72f, 0.45f, 0.25f, 1f);
            }
        }
    }

    /// <summary>Factory for the code-built uGUI. Uses the UI kit sprites (rounded panels, key caps) when available.</summary>
    public static class Ui
    {
        /// <summary>The icon set carrying the UI kit sprites; set once by the scene's composition root.</summary>
        public static IconSet Kit { get; set; }

        /// <summary>A rounded card, optionally with a gold hairline border.</summary>
        public static Image Card(string name, Transform parent, Color color, bool hairline = false)
        {
            var img = Panel(name, parent, color);
            Round(img);
            img.raycastTarget = false;
            if (hairline && Kit != null && Kit.uiFrame != null)
            {
                var f = Panel("Hairline", img.transform, Theme.Hairline);
                f.sprite = Kit.uiFrame;
                f.type = Image.Type.Sliced;
                f.raycastTarget = false;
                f.rectTransform.Fill();
            }
            return img;
        }

        /// <summary>Gives an image the rounded nine-slice panel sprite (no-op without the UI kit).</summary>
        public static void Round(Image img, float cornerScale = 1f)
        {
            if (Kit == null || Kit.uiPanel == null) return;
            img.sprite = Kit.uiPanel;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.05f, cornerScale);
        }

        /// <summary>A key cap showing a binding ("R", "Space", "A"), sized to its text.</summary>
        public static RectTransform Keycap(Transform parent, string key, int size = 18)
        {
            var img = Panel("Key", parent, new Color(0.93f, 0.86f, 0.74f, 1f));
            if (Kit != null && Kit.uiKeycap != null) { img.sprite = Kit.uiKeycap; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2f; }
            img.raycastTarget = false;
            var label = Label("Text", img.transform, key, size, TextAnchor.MiddleCenter, new Color(0.16f, 0.09f, 0.05f), FontStyle.Bold);
            label.rectTransform.Fill();
            label.rectTransform.offsetMin = new Vector2(8, 4);
            label.rectTransform.offsetMax = new Vector2(-8, 0);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = Mathf.Max(size * 1.6f, label.preferredWidth + 18f);
            img.rectTransform.sizeDelta = new Vector2(w, size * 1.7f);
            return img.rectTransform;
        }

        private static Font _font;
        private static Font _fontStrong;

        /// <summary>Inter (SIL Open Font License, Resources/Fonts/Inter-LICENSE.txt); the built-in font only if it is missing.</summary>
        public static Font Font => _font != null ? _font : (_font = Resources.Load<Font>("Fonts/Inter-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        /// <summary>Inter SemiBold: used for "bold" text instead of Unity's smeared synthetic bold.</summary>
        public static Font FontStrong => _fontStrong != null ? _fontStrong : (_fontStrong = Resources.Load<Font>("Fonts/Inter-SemiBold") ?? Font);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform rt, float x, float y, float w, float h)
        {
            // Top-left anchored placement in reference pixels (1920x1080 canvas).
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size = 22, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            bool strong = style == FontStyle.Bold || style == FontStyle.BoldAndItalic;
            t.font = strong ? FontStrong : Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? Theme.Text;
            t.fontStyle = strong ? (style == FontStyle.BoldAndItalic ? FontStyle.Italic : FontStyle.Normal) : style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, UnityAction onClick, int size = 24, Color? color = null)
        {
            var img = Panel(name, parent, color ?? Theme.Button);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.selectedColor = new Color(1.25f, 1.2f, 1.0f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.colorMultiplier = 1.5f;
            btn.colors = colors;
            Round(img);
            if (onClick != null) btn.onClick.AddListener(onClick);
            var label = Label("Label", img.transform, text, size, TextAnchor.MiddleCenter);
            label.rectTransform.Fill(6);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = size;
            FocusFrame.Attach(btn);
            return btn;
        }

        public static void SetText(this Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        /// <summary>Sprite icon of a fixed size; falls back to a flat colored square if the sprite is missing.</summary>
        public static Image Icon(string name, Transform parent, Sprite sprite, float size, Color fallback)
        {
            var img = Panel(name, parent, sprite != null ? Color.white : fallback);
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        /// <summary>Filled square (Channel A), diamond (Channel B), or bar (Hammer) glyph.</summary>
        public static Image Glyph(string name, Transform parent, GlyphShape shape, float size)
        {
            var holder = Rect(name, parent);
            holder.sizeDelta = new Vector2(size, size);
            var img = Panel("Shape", holder, Color.white);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            switch (shape)
            {
                case GlyphShape.Square:
                    img.color = Theme.ChannelA;
                    rt.sizeDelta = new Vector2(size * 0.72f, size * 0.72f);
                    break;
                case GlyphShape.Diamond:
                    img.color = Theme.ChannelB;
                    rt.sizeDelta = new Vector2(size * 0.62f, size * 0.62f);
                    rt.localRotation = Quaternion.Euler(0, 0, 45);
                    break;
                case GlyphShape.Hammer:
                {
                    img.color = Theme.Hammer;
                    rt.sizeDelta = new Vector2(size * 0.22f, size * 0.8f);
                    var head = Panel("Head", holder, Theme.Hammer).rectTransform;
                    head.anchorMin = head.anchorMax = new Vector2(0.5f, 0.5f);
                    head.pivot = new Vector2(0.5f, 0.5f);
                    head.sizeDelta = new Vector2(size * 0.75f, size * 0.28f);
                    head.anchoredPosition = new Vector2(0, size * 0.28f);
                    break;
                }
            }
            return img;
        }
    }

    public enum GlyphShape { Square, Diamond, Hammer }

    /// <summary>High-contrast focus outline shown while the element is selected (keyboard/controller/pointer).</summary>
    public sealed class FocusFrame : MonoBehaviour, UnityEngine.EventSystems.ISelectHandler, UnityEngine.EventSystems.IDeselectHandler
    {
        private GameObject _frame;

        public static void Attach(Selectable s)
        {
            var f = s.gameObject.AddComponent<FocusFrame>();
            var frame = Ui.Rect("FocusFrame", s.transform);
            frame.Fill(-7);
            if (Ui.Kit != null && Ui.Kit.uiFrame != null)
            {
                var ring = Ui.Panel("Outline", frame, Theme.Focus);
                ring.sprite = Ui.Kit.uiFrame;
                ring.type = Image.Type.Sliced;
                ring.pixelsPerUnitMultiplier = 0.6f; // thicker line
                ring.raycastTarget = false;
                ring.rectTransform.Fill();
                f._frame = frame.gameObject;
                f._frame.SetActive(false);
                return;
            }
            const float t = 5f;
            Bar(frame, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -t), Vector2.zero);
            Bar(frame, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, t));
            Bar(frame, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(t, 0));
            Bar(frame, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-t, 0), Vector2.zero);
            f._frame = frame.gameObject;
            f._frame.SetActive(false);
        }

        private static void Bar(RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var img = Ui.Panel("Bar", parent, Theme.Focus);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
        }

        public void OnSelect(UnityEngine.EventSystems.BaseEventData eventData) { if (_frame) _frame.SetActive(true); }
        public void OnDeselect(UnityEngine.EventSystems.BaseEventData eventData) { if (_frame) _frame.SetActive(false); }
        private void OnDisable() { if (_frame) _frame.SetActive(false); }
    }
}
