using Tabletop.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Tabletop.World
{
    /// <summary>
    /// Stop-gap touch controls for phones and tablets (D-032): a thumb stick and a few buttons that press the
    /// same virtual gamepad controls the real gamepad bindings already use. Gameplay still reads only abstract
    /// actions; nothing here knows about walking or talking.
    /// </summary>
    public sealed class WorldTouchPad
    {
        public readonly GameObject Root;

        /// <summary>True on phones/tablets, or anywhere a touchscreen is attached.</summary>
        public static bool Wanted => Application.isMobilePlatform || Touchscreen.current != null;

        public WorldTouchPad(Transform frame)
        {
            var root = Ui.Rect("TouchPad", frame);
            root.Fill();
            Root = root.gameObject;

            // Thumb stick, bottom-left. The knob is the on-screen control; dragging it moves the left stick.
            var stickBase = Circle("StickBase", root, new Color(0.09f, 0.05f, 0.035f, 0.55f));
            stickBase.rectTransform.Place(70, 700, 300, 300);
            stickBase.raycastTarget = false;
            var knob = Circle("StickKnob", stickBase.transform, new Color(0.93f, 0.72f, 0.36f, 0.85f));
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = knob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            knob.rectTransform.anchoredPosition = Vector2.zero;
            knob.rectTransform.sizeDelta = new Vector2(150, 150);
            AddControl<OnScreenStick>(knob.gameObject, "<Gamepad>/leftStick", s => s.movementRange = 110);

            // Buttons, bottom-right. A = talk / jump / confirm, like the gamepad.
            PadButton(root, "A", "<Gamepad>/buttonSouth", 1640, 780, 200, Theme.ButtonPrimary);
            PadButton(root, "RUN", "<Gamepad>/leftTrigger", 1440, 860, 150, Theme.Button);
            PadButton(root, "VIEW", "<Gamepad>/buttonNorth", 1690, 600, 150, Theme.Button);
            PadButton(root, "MENU", "<Gamepad>/start", 1760, 150, 130, Theme.Button);
        }

        public void SetVisible(bool visible)
        {
            if (Root.activeSelf != visible) Root.SetActive(visible);
        }

        private static void PadButton(RectTransform parent, string label, string path, float x, float y, float size, Color color)
        {
            var img = Circle("Pad" + label, parent, new Color(color.r, color.g, color.b, 0.8f));
            img.rectTransform.Place(x, y, size, size);
            var text = Ui.Label("Label", img.transform, label, size >= 200 ? 64 : 28, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            text.rectTransform.Fill();
            AddControl<OnScreenButton>(img.gameObject, path, null);
        }

        private static Image Circle(string name, Transform parent, Color color)
        {
            var img = Ui.Panel(name, parent, color);
            if (Ui.Kit != null && Ui.Kit.uiCircle != null) img.sprite = Ui.Kit.uiCircle;
            img.raycastTarget = true;
            return img;
        }

        /// <summary>The control path is set while the object is inactive so the virtual gamepad is created with it.</summary>
        private static void AddControl<T>(GameObject go, string path, System.Action<T> setup) where T : OnScreenControl
        {
            bool was = go.activeSelf;
            go.SetActive(false);
            var c = go.AddComponent<T>();
            c.controlPath = path;
            setup?.Invoke(c);
            go.SetActive(was);
        }
    }
}
