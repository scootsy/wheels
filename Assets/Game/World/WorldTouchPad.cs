using System.Collections.Generic;
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
    /// actions; nothing here knows about walking or talking. D-040: pushing the move stick nearly all the way runs
    /// (no RUN button), and a second stick on the right looks around in first person.
    /// </summary>
    public sealed class WorldTouchPad
    {
        public readonly GameObject Root;
        private readonly List<OnScreenControl> _controls = new List<OnScreenControl>();
        private readonly GameObject _lookBase;
        private readonly OnScreenStick _lookStick;

        /// <summary>The move stick's knob, for tests that drag it.</summary>
        public RectTransform StickKnob => (RectTransform)_controls[0].transform;
        /// <summary>The look stick's knob (first person only).</summary>
        public RectTransform LookKnob => (RectTransform)_lookStick.transform;
        public bool Visible => Root.activeSelf;
        public bool LookVisible => Root.activeSelf && _lookBase.activeSelf;

        /// <summary>True on phones/tablets, or anywhere a touchscreen is attached.</summary>
        public static bool Wanted => UnityEngine.Application.isMobilePlatform || Touchscreen.current != null;

        public WorldTouchPad(Transform frame)
        {
            var root = Ui.Rect("TouchPad", frame);
            root.Fill();
            Root = root.gameObject;

            // Thumb stick, bottom-left. The knob is the on-screen control; dragging it moves the left stick.
            // Pushed nearly to the rim (GameInputRouter.RunThreshold) it runs.
            _controls.Add(Stick(root, "Stick", "<Gamepad>/leftStick", 70, null, out _));

            // Look stick, bottom-right beside A: the right stick, shown only in first person.
            _lookStick = Stick(root, "Look", "<Gamepad>/rightStick", 1320, "LOOK", out _lookBase);
            _controls.Add(_lookStick);
            _lookBase.SetActive(false);

            // Buttons, bottom-right. A = talk / jump / confirm, like the gamepad.
            _controls.Add(PadButton(root, "A", "<Gamepad>/buttonSouth", 1640, 780, 200, Theme.ButtonPrimary));
            _controls.Add(PadButton(root, "VIEW", "<Gamepad>/buttonNorth", 1690, 600, 150, Theme.Button));
            _controls.Add(PadButton(root, "MENU", "<Gamepad>/start", 1760, 150, 130, Theme.Button));
        }

        /// <summary>A thumb stick: a dark base with a gold knob that drags <paramref name="path"/>.</summary>
        private static OnScreenStick Stick(RectTransform parent, string name, string path, float x, string label, out GameObject stickBase)
        {
            var bg = Circle(name + "Base", parent, new Color(0.09f, 0.05f, 0.035f, 0.55f));
            bg.rectTransform.Place(x, 700, 300, 300);
            bg.raycastTarget = false;
            stickBase = bg.gameObject;
            if (label != null)
            {
                var text = Ui.Label("Label", bg.transform, label, 26, TextAnchor.UpperCenter, Theme.TextDim, FontStyle.Bold);
                text.rectTransform.Place(0, -36, 300, 34);
                text.raycastTarget = false;
            }
            var knob = Circle(name + "Knob", bg.transform, new Color(0.93f, 0.72f, 0.36f, 0.85f));
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = knob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            knob.rectTransform.anchoredPosition = Vector2.zero;
            knob.rectTransform.sizeDelta = new Vector2(150, 150);
            return AddControl<OnScreenStick>(knob.gameObject, path, s => s.movementRange = 110);
        }

        /// <summary>The look stick is for first person; the overhead camera has nothing to look with.</summary>
        public void SetLookVisible(bool visible)
        {
            if (_lookBase.activeSelf != visible) _lookBase.SetActive(visible);
        }

        /// <summary>
        /// True when a real controller is connected. The pad's own buttons press a virtual gamepad, which must not
        /// count, or the pad would hide itself as soon as it appeared.
        /// </summary>
        public bool RealGamepadConnected
        {
            get
            {
                foreach (var device in InputSystem.devices)
                {
                    if (!(device is Gamepad) || !device.added) continue;
                    bool ours = false;
                    foreach (var c in _controls)
                        if (c != null && c.control != null && c.control.device == device) { ours = true; break; }
                    if (!ours) return true;
                }
                return false;
            }
        }

        public void SetVisible(bool visible)
        {
            if (Root.activeSelf != visible) Root.SetActive(visible);
        }

        private static OnScreenControl PadButton(RectTransform parent, string label, string path, float x, float y, float size, Color color)
        {
            var img = Circle("Pad" + label, parent, new Color(color.r, color.g, color.b, 0.8f));
            img.rectTransform.Place(x, y, size, size);
            var text = Ui.Label("Label", img.transform, label, size >= 200 ? 64 : 28, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            text.rectTransform.Fill();
            return AddControl<OnScreenButton>(img.gameObject, path, null);
        }

        private static Image Circle(string name, Transform parent, Color color)
        {
            var img = Ui.Panel(name, parent, color);
            if (Ui.Kit != null && Ui.Kit.uiCircle != null) img.sprite = Ui.Kit.uiCircle;
            img.raycastTarget = true;
            return img;
        }

        /// <summary>The control path is set while the object is inactive so the virtual gamepad is created with it.</summary>
        private static T AddControl<T>(GameObject go, string path, System.Action<T> setup) where T : OnScreenControl
        {
            bool was = go.activeSelf;
            go.SetActive(false);
            var c = go.AddComponent<T>();
            c.controlPath = path;
            setup?.Invoke(c);
            go.SetActive(was);
            return c;
        }
    }
}
