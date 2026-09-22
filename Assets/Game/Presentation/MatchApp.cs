using System.Collections.Generic;
using System.Text;
using Tabletop.Application;
using Tabletop.Domain;
using Tabletop.Infrastructure;
using Tabletop.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>
    /// Composition root for MatchPrototype.unity. Wires Input System -> session (commands) and
    /// session/presenter -> views. Holds no rules state; everything authoritative is in the Match.
    /// </summary>
    public sealed partial class MatchApp : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private ContentCatalogAsset content;
        [SerializeField] private Camera boardCamera;
        [SerializeField] private EventSystem eventSystem;

        public MatchSession Session { get; private set; }
        public MatchPresenter Presenter { get; private set; }
        public PresentationSettings Settings { get; } = new PresentationSettings();
        public GameInputRouter Input { get; private set; }
        public string ConfigError { get; private set; }
        public string LastCopiedReplay { get; private set; } = "";
        public bool WrapReelNavigation { get; set; }

        private InputActionAsset _actionsInstance;
        private BoardDiorama _diorama;
        private Canvas _canvas;
        private readonly List<Text> _scalableLabels = new List<Text>();
        private readonly List<int> _baseSizes = new List<int>();
        private bool _pointerAccelerate;
        private float _pointerAccelerateStart;
        private int _lastOverlayCloseFrame = -1;
        private int _lastOverlayOpenFrame = -1;
        private UxState _lastRenderedState = (UxState)(-1);

        // Lane rectangle in 1920x1080 reference pixels (the 3D diorama shows through here).
        private const float LaneX = 440, LaneY = 212, LaneW = 1040, LaneH = 386;

        public ContentCatalog Catalog { get; private set; }

        private void Awake()
        {
            UnityEngine.Application.targetFrameRate = 60;
            IReadOnlyList<string> errors = new string[0];
            var catalog = content != null ? content.ToCatalog(out errors) : null;
            if (catalog == null)
            {
                ConfigError = content == null ? "No content catalog asset assigned." : string.Join("\n", errors);
                Debug.LogError("[Tabletop] Content invalid; the match refuses to start: " + ConfigError);
                Catalog = ReferenceContent.Catalog; // only used to render the error screen
            }
            else
            {
                Catalog = catalog;
            }

            var rng = new System.Random();
            Session = new MatchSession(Catalog, () => rng.Next(0, int.MaxValue));
            Presenter = new MatchPresenter(Session, Settings);
            Session.StateChanged += OnStateChanged;

            // Private instance so scene reloads and tests never accumulate subscriptions on the asset.
            _actionsInstance = Instantiate(inputActions);
            Input = new GameInputRouter(_actionsInstance);
            Input.Spin += OnSpin;
            Input.LockSlot += OnLockSlot;
            Input.FocusNext += () => CycleFocus(1);
            Input.FocusPrevious += () => CycleFocus(-1);
            Input.Inspect += OnInspect;
            Input.Help += OnHelp;
            Input.Pause += OnPause;
            Input.Cancel += OnCancel;
            Input.SchemeChanged += _ => RefreshPrompts();
            ConfigureEventSystem();

            _diorama = new GameObject("Board").AddComponent<BoardDiorama>();
            _diorama.transform.SetParent(transform, false);
            _diorama.Build();
            ConfigureCamera();

            BuildCanvas();
            BuildSetupScreen();
            BuildUnitSelectScreen();
            BuildBoardScreen();
            BuildOverlays();
            ApplyUiScale(1f);
            RefreshPrompts();
            ShowScreenFor(Session.State);
            Focus(_setupContinue);
        }

        private void OnEnable() => Input?.Enable();
        private void OnDisable() => Input?.Disable();

        private void OnDestroy()
        {
            if (Session != null) Session.StateChanged -= OnStateChanged;
            Input?.Dispose();
            if (_actionsInstance != null) Destroy(_actionsInstance);
        }

        private void ConfigureEventSystem()
        {
            if (eventSystem == null) eventSystem = EventSystem.current;
            if (eventSystem == null) eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            module.actionsAsset = _actionsInstance;
            module.move = InputActionReference.Create(_actionsInstance.FindAction("UI/Navigate", true));
            module.submit = InputActionReference.Create(_actionsInstance.FindAction("UI/Submit", true));
            module.cancel = InputActionReference.Create(_actionsInstance.FindAction("UI/Cancel", true));
            module.point = InputActionReference.Create(_actionsInstance.FindAction("UI/Point", true));
            module.leftClick = InputActionReference.Create(_actionsInstance.FindAction("UI/Click", true));
            module.scrollWheel = InputActionReference.Create(_actionsInstance.FindAction("UI/ScrollWheel", true));
            // Clicking empty space must never make focus disappear.
            module.deselectOnBackgroundClick = false;
            eventSystem.sendNavigationEvents = true;
        }

        private void ConfigureCamera()
        {
            if (boardCamera == null) boardCamera = Camera.main;
            if (boardCamera == null) return;
            boardCamera.rect = new Rect(LaneX / 1920f, 1f - (LaneY + LaneH) / 1080f, LaneW / 1920f, LaneH / 1080f);
            boardCamera.clearFlags = CameraClearFlags.SolidColor;
            boardCamera.backgroundColor = new Color(0.16f, 0.12f, 0.10f);
            boardCamera.transform.position = new Vector3(0, 7.8f, -6.2f);
            boardCamera.transform.LookAt(new Vector3(0, 0.4f, 0.15f));
            boardCamera.fieldOfView = 34f;
        }

        // ------------------------------------------------------------------ frame loop

        private void Update()
        {
            float held = Input.AccelerateHeldSeconds;
            if (_pointerAccelerate) held = Mathf.Max(held, Time.unscaledTime - _pointerAccelerateStart);
            bool overlayFrozen = _helpOverlay.activeSelf || _pauseOverlay.activeSelf;
            if (!overlayFrozen) Presenter.Tick(Time.unscaledDeltaTime, held);
            if (Session.FatalError != null && !_errorOverlay.activeSelf) ShowError();
            if (Session.State != _lastRenderedState)
            {
                _lastRenderedState = Session.State;
                ShowScreenFor(Session.State);
            }
            RenderBoard();
            RenderSetupAndSelect();
            EnsureFocus();
        }

        private void OnStateChanged(UxState state)
        {
            if (state == UxState.RoundReady && Presenter.Current == null && Session.PresentationQueue.Count == 0 && _pendingFocus == null)
                _pendingFocus = _spinButton;
            switch (state)
            {
                case UxState.UnitSelect: _pendingFocus = _cardButtons.Count > 0 ? _cardButtons[0] : null; break;
                case UxState.RoundReady: _pendingFocus = _spinButton; break;
                case UxState.SpinDecision: _pendingFocus = FirstUnlockedReel(); break;
                case UxState.Spinning:
                case UxState.AiCommit:
                case UxState.Reveal:
                case UxState.Resolving:
                    _pendingFocus = _speedButton;
                    break;
                case UxState.MatchResult: _pendingFocus = _resultRematch; break;
                case UxState.MatchSetup: _pendingFocus = _setupContinue; break;
            }
        }

        // ------------------------------------------------------------------ input intents

        private void OnSpin()
        {
            if (AnyModalOpen) return;
            if (Session.State == UxState.RoundReady || Session.State == UxState.SpinDecision) Session.RequestSpin();
        }

        private void OnLockSlot(int reel)
        {
            if (AnyModalOpen || Session.Match == null) return;
            if (Session.State == UxState.RoundReady || Session.State == UxState.SpinDecision) Session.RequestToggleLock(reel);
        }

        private void OnSpinClicked()
        {
            if (!Session.RequestSpin()) Focus(_spinButton);
        }

        private void OnReelClicked(int reel)
        {
            Session.RequestToggleLock(reel);
            // Focus stays on the attempted control (recoverable rejection or success).
            if (Session.State == UxState.SpinDecision) Focus(_playerReels[reel].Button);
        }

        private void OnInspect()
        {
            if (_helpOverlay.activeSelf || _pauseOverlay.activeSelf || _resultOverlay.activeSelf) return;
            if (_inspectOverlay.activeSelf) { CloseOverlay(_inspectOverlay); return; }
            var focused = eventSystem.currentSelectedGameObject;
            foreach (var p in _unitPanels)
            {
                if (p.Button.gameObject == focused) { OpenInspect(p.Side, p.Slot, true); return; }
            }
            for (int i = 0; i < _cardButtons.Count; i++)
            {
                if (_cardButtons[i].gameObject == focused) { OpenInspectDefinition(_cardUnits[i], true); return; }
            }
            SetStatus("Focus a unit (Tab / bumpers) to inspect it.");
        }

        private void OnHelp()
        {
            if (_helpOverlay.activeSelf) { CloseOverlay(_helpOverlay); return; }
            if (_resultOverlay.activeSelf || _confirmOverlay.activeSelf) return;
            OpenOverlay(_helpOverlay, _helpClose);
        }

        private void OnPause()
        {
            // Pause and Cancel share Escape: handle only one overlay change per frame.
            if (Time.frameCount == _lastOverlayCloseFrame || Time.frameCount == _lastOverlayOpenFrame) return;
            if (CloseTopOverlay()) return;
            if (Session.Match != null && Session.State != UxState.MatchResult)
            {
                Session.SetPaused(true);
                OpenOverlay(_pauseOverlay, _pauseResume);
            }
            else if (Session.State == UxState.UnitSelect)
            {
                Session.BackToSetup();
            }
        }

        private void OnCancel()
        {
            if (Time.frameCount == _lastOverlayCloseFrame || Time.frameCount == _lastOverlayOpenFrame) return;
            CloseTopOverlay();
        }

        private bool CloseTopOverlay()
        {
            if (_confirmOverlay.activeSelf) { CloseOverlay(_confirmOverlay); return true; }
            if (_inspectOverlay.activeSelf) { CloseOverlay(_inspectOverlay); return true; }
            if (_helpOverlay.activeSelf) { CloseOverlay(_helpOverlay); return true; }
            if (_pauseOverlay.activeSelf) { ResumeFromPause(); return true; }
            return false;
        }

        private void ResumeFromPause()
        {
            CloseOverlay(_pauseOverlay);
            Session.SetPaused(false);
        }

        /// <summary>Test/automation hook: enable developer mode with a fixed seed and optional forced scenario.</summary>
        public void ConfigureDeveloperForTests(long? seed, string scenario)
        {
            if (!_devPanel.activeSelf) ToggleDev();
            _seedField.text = seed.HasValue ? seed.Value.ToString() : "";
            Session.Options.FixedSeed = seed;
            Session.Options.ScenarioName = scenario;
        }

        public void SetPointerAccelerate(bool held)
        {
            _pointerAccelerate = held;
            _pointerAccelerateStart = Time.unscaledTime;
        }

        // ------------------------------------------------------------------ helpers

        private Text L(string name, Transform parent, string text, int size = 22, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var t = Ui.Label(name, parent, text, size, anchor, color, style);
            _scalableLabels.Add(t);
            _baseSizes.Add(size);
            return t;
        }

        /// <summary>UI scale grows text up to its box (best-fit), so 150% never overlaps at 1280x720 (D-015).</summary>
        public void ApplyUiScale(float scale)
        {
            Settings.UiScale = scale;
            for (int i = 0; i < _scalableLabels.Count; i++)
            {
                var t = _scalableLabels[i];
                if (t == null) continue;
                int size = Mathf.RoundToInt(_baseSizes[i] * scale);
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = Mathf.Min(10, size);
                t.resizeTextMaxSize = size;
                t.fontSize = size;
            }
        }

        private void SetStatus(string s) => _statusOverride = s;
        private string _statusOverride;

        public static string Signed(int v) => v >= 0 ? "+" + v : v.ToString();

        public string DescribeFocus()
        {
            var go = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (go == null) return "";
            foreach (var r in _playerReels)
                if (r.Button.gameObject == go)
                    return r.Description.Replace("Reel " + (r.Index + 1) + ",", "Reel " + (r.Index + 1) + ", " + (Presenter.Visual.Locked[0, r.Index] ? "locked," : "unlocked,"));
            foreach (var p in _unitPanels) if (p.Button.gameObject == go) return p.Description;
            if (go == _spinButton.gameObject && Session.Match != null)
                return "Spin unlocked reels, " + (RulesConstants.SpinsPerRound - Presenter.Visual.SpinsUsed[0]) + " spins remaining.";
            var t = go.GetComponentInChildren<Text>();
            return t != null ? t.text.Replace("\n", " ") : go.name;
        }

        internal static string Join(IEnumerable<string> lines)
        {
            var sb = new StringBuilder();
            foreach (var l in lines) sb.AppendLine(l);
            return sb.ToString();
        }
    }
}
