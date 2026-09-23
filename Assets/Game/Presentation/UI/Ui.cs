using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>Placeholder palette. Every colored state also has a text, glyph, or shape cue.</summary>
    public static class Theme
    {
        public static readonly Color Background = new Color(0.10f, 0.09f, 0.10f, 1f);
        public static readonly Color Panel = new Color(0.18f, 0.16f, 0.17f, 0.96f);
        public static readonly Color PanelDark = new Color(0.12f, 0.11f, 0.12f, 0.98f);
        public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color Text = new Color(0.95f, 0.93f, 0.88f, 1f);
        public static readonly Color TextDim = new Color(0.70f, 0.67f, 0.62f, 1f);
        public static readonly Color ChannelA = new Color(0.95f, 0.55f, 0.20f, 1f);   // orange square
        public static readonly Color ChannelB = new Color(0.30f, 0.80f, 0.85f, 1f);   // teal diamond
        public static readonly Color Hammer = new Color(0.80f, 0.80f, 0.82f, 1f);
        public static readonly Color Xp = new Color(0.55f, 0.45f, 0.95f, 1f);
        public static readonly Color Focus = new Color(1f, 0.92f, 0.25f, 1f);
        public static readonly Color Locked = new Color(0.95f, 0.30f, 0.30f, 1f);
        public static readonly Color Button = new Color(0.30f, 0.27f, 0.26f, 1f);
        public static readonly Color ButtonPrimary = new Color(0.20f, 0.45f, 0.25f, 1f);
        public static readonly Color Disabled = new Color(0.22f, 0.21f, 0.21f, 1f);
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

    /// <summary>Tiny factory for code-built uGUI (placeholder M1 UI; no prefabs or art assets).</summary>
    public static class Ui
    {
        private static Font _font;

        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

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
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? Theme.Text;
            t.fontStyle = style;
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
