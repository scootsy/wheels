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
        private readonly InputActionMap _world;
        private readonly InputAction _move;
        private readonly InputAction _interact;
        private readonly InputAction _sprint;
        private readonly InputAction _jump;
        private readonly InputAction _lookDelta;
        private readonly InputAction _lookStick;
        private readonly InputAction _accelerate;
        private readonly InputAction[] _lockSlots = new InputAction[5];
        private double _accelerateStart = -1;

        /// <param name="consumeShortcuts">
        /// True for the match table (Shift+Tab must not also fire Tab). The world passes false: with consumption on,
        /// the UI Navigate composite swallows WASD/arrows and the Move action reads zero.
        /// </param>
        public GameInputRouter(InputActionAsset asset, bool consumeShortcuts = true)
        {
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));
            _ui = asset.FindActionMap("UI", true);
            _match = asset.FindActionMap("Match", true);
            _accelerate = _match.FindAction("AcceleratePresentation", true);
            _world = asset.FindActionMap("WorldReserved", true);
            _move = _world.FindAction("Move", true);
            _interact = _world.FindAction("Interact", true);
            Hook(_interact, () => Interact?.Invoke());
            _move.performed += OnDevice;
            _sprint = _world.FindAction("Sprint", true);
            _jump = _world.FindAction("Jump", true);
            _lookDelta = _world.FindAction("LookDelta", true);
            _lookStick = _world.FindAction("LookStick", true);
            _jump.performed += OnDevice;
            _lookStick.performed += OnDevice;
            Hook(_world.FindAction("ToggleView", true), () => ToggleView?.Invoke());
            Hook(_ui.FindAction("Submit", true), () => Submit?.Invoke());

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
            InputSystem.settings.shortcutKeysConsumeInput = consumeShortcuts;
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
        public event Action Interact;
        public event Action Submit;

        /// <summary>World movement input (x = east, y = north), zero when the world map is disabled.</summary>
        public UnityEngine.Vector2 Move => _world.enabled ? _move.ReadValue<UnityEngine.Vector2>() : UnityEngine.Vector2.zero;
        public bool SprintHeld => _world.enabled && _sprint.IsPressed();
        public bool JumpPressedThisFrame => _world.enabled && _jump.WasPressedThisFrame();
        public bool InteractPressedThisFrame => _world.enabled && _interact.WasPressedThisFrame();
        /// <summary>Pointer look this frame, in pixels (first-person view).</summary>
        public UnityEngine.Vector2 LookDelta => _world.enabled ? _lookDelta.ReadValue<UnityEngine.Vector2>() : UnityEngine.Vector2.zero;
        /// <summary>Stick look, -1..1 (a rate: multiply by time).</summary>
        public UnityEngine.Vector2 LookStick => _world.enabled ? _lookStick.ReadValue<UnityEngine.Vector2>() : UnityEngine.Vector2.zero;
        public event Action ToggleView;

        /// <summary>Enable walking/interacting (world scene only).</summary>
        public void EnableWorld() => _world.Enable();
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
            _world.Disable();
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
            // Touch shows gamepad names: the on-screen pad's buttons are labelled like a gamepad's (D-037).
            var scheme = device is Gamepad || device is Touchscreen || device is Pen ? ControlScheme.Gamepad : ControlScheme.KeyboardMouse;
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
