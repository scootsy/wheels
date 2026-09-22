using System;
using UnityEngine.InputSystem;

namespace Tabletop.Input
{
    public enum ControlScheme { KeyboardMouse, Gamepad }

    /// <summary>
    /// Converts Input System actions from GameInput.inputactions into abstract intents.
    /// Gameplay code never sees keys or buttons; device bindings live only in the asset.
    /// </summary>
    public sealed class GameInputRouter : IDisposable
    {
        private readonly InputActionAsset _asset;
        private readonly InputActionMap _ui;
        private readonly InputActionMap _match;
        private readonly InputAction _accelerate;
        private readonly InputAction[] _lockSlots = new InputAction[5];
        private double _accelerateStart = -1;

        public GameInputRouter(InputActionAsset asset)
        {
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));
            _ui = asset.FindActionMap("UI", true);
            _match = asset.FindActionMap("Match", true);
            _accelerate = _match.FindAction("AcceleratePresentation", true);

            Hook(_match.FindAction("Spin", true), () => Spin?.Invoke());
            for (int i = 0; i < 5; i++)
            {
                int slot = i;
                _lockSlots[i] = _match.FindAction("LockSlot" + (i + 1), true);
                Hook(_lockSlots[i], () => LockSlot?.Invoke(slot));
            }
            Hook(_match.FindAction("FocusNext", true), () => FocusNext?.Invoke());
            Hook(_match.FindAction("FocusPrevious", true), () => FocusPrevious?.Invoke());
            Hook(_match.FindAction("Inspect", true), () => Inspect?.Invoke());
            Hook(_match.FindAction("Help", true), () => Help?.Invoke());
            Hook(_match.FindAction("Pause", true), () => Pause?.Invoke());
            Hook(_ui.FindAction("Cancel", true), () => Cancel?.Invoke());
            _accelerate.started += OnDevice;
            _accelerate.canceled += OnDevice;
            foreach (var a in _ui.actions) a.performed += OnDevice;

            // Shift+Tab must not also trigger the plain Tab binding.
            InputSystem.settings.shortcutKeysConsumeInput = true;
        }

        public InputActionAsset Asset => _asset;
        public InputActionMap UiMap => _ui;
        public ControlScheme ActiveScheme { get; private set; } = ControlScheme.KeyboardMouse;

        public event Action Spin;
        public event Action<int> LockSlot;
        public event Action FocusNext;
        public event Action FocusPrevious;
        public event Action Inspect;
        public event Action Help;
        public event Action Pause;
        public event Action Cancel;
        public event Action<ControlScheme> SchemeChanged;

        /// <summary>Seconds the accelerate action has been continuously held (0 when released).</summary>
        public float AccelerateHeldSeconds
        {
            get
            {
                if (!_accelerate.IsPressed()) { _accelerateStart = -1; return 0f; }
                if (_accelerateStart < 0) _accelerateStart = UnityEngine.Time.unscaledTimeAsDouble;
                return (float)(UnityEngine.Time.unscaledTimeAsDouble - _accelerateStart);
            }
        }

        public void Enable()
        {
            _ui.Enable();
            _match.Enable();
        }

        public void Disable()
        {
            _match.Disable();
            _ui.Disable();
        }

        /// <summary>Human-readable binding for the active scheme, e.g. "Enter" or "A".</summary>
        public string Binding(string map, string action)
        {
            var a = _asset.FindActionMap(map, true).FindAction(action, true);
            string group = ActiveScheme == ControlScheme.Gamepad ? "Gamepad" : "KeyboardMouse";
            var s = a.GetBindingDisplayString(InputBinding.MaskByGroup(group));
            return string.IsNullOrEmpty(s) ? "-" : s;
        }

        private void Hook(InputAction action, Action handler)
        {
            action.performed += ctx =>
            {
                OnDevice(ctx);
                handler();
            };
        }

        private void OnDevice(InputAction.CallbackContext ctx)
        {
            var device = ctx.control?.device;
            if (device == null) return;
            var scheme = device is Gamepad ? ControlScheme.Gamepad : ControlScheme.KeyboardMouse;
            if (scheme == ActiveScheme) return;
            ActiveScheme = scheme;
            SchemeChanged?.Invoke(scheme);
        }

        public void Dispose()
        {
            Disable();
        }
    }
}
