using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tabletop.Application;
using Tabletop.Domain;
using Tabletop.Input;
using Tabletop.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tabletop.World
{
    /// <summary>
    /// Composition root for World.unity: builds the placeholder world, walks the player around, and hands
    /// challenges to the match table through <see cref="GameFlow"/>. Holds no match rules.
    /// </summary>
    public sealed class WorldApp : MonoBehaviour
    {
        public const string MatchSceneName = "MatchPrototype";

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Material worldMaterial;
        [SerializeField] private IconSet icons;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private EventSystem eventSystem;
        [Tooltip("Imported models that replace placeholder people and buildings (D-026). Optional.")]
        [SerializeField] private WorldArtSet art;

        public static readonly Vector3 CameraOffset = new Vector3(0, 17f, -17f);
        public static readonly Vector3 SeatedOffset = new Vector3(0, 4.6f, -4.8f);
        public const float EyeHeight = 1.6f;
        public const float OverheadFov = 42f;
        public const float FirstPersonFov = 70f;
        /// <summary>Degrees per pixel of mouse movement / per second of full stick in first person.</summary>
        public const float MouseLookSpeed = 0.12f;
        public const float StickLookSpeed = 150f;
        /// <summary>Name tags are fully visible within the near distance and gone beyond the far one (D-027).</summary>
        public const float TagNear = 7f;
        public const float TagFar = 11f;
        public const float AutosaveSeconds = 30f;

        public WorldLayout Layout { get; private set; }
        public PlayerController Player { get; private set; }
        public WorldUi Ui { get; private set; }
        public GameInputRouter Input { get; private set; }
        public Interactable Nearest { get; private set; }
        public bool DialogueOpen => Ui.Dialogue.activeSelf;
        public bool Busy => DialogueOpen || Ui.Pause.activeSelf || Ui.Title.activeSelf || _fading || (_deck != null && _deck.IsOpen);
        public bool FirstPerson => GameFlow.FirstPersonView;
        public DeckView Deck => _deck;
        public Camera WorldCamera => worldCamera;
        public string CurrentArea { get; private set; }
        public bool Seated => _seatedAt != null;
        public Material WorldMaterial => worldMaterial;
        public WorldArtSet Art => art;

        private InputActionAsset _actions;
        private WorldKit _kit;
        private bool _fading;
        private int _dialogueOpenedFrame = -1;
        private int _buttonClickedFrame = -1;
        private int _closedFrame = -1;
        private bool _pendingInteract;
        private GameObject _returnFocus;
        private Npc _talkingTo;
        private Chair _seatedAt;
        private List<(string speaker, string line, Sprite portrait)> _pages;
        private IList<(string, Action)> _finalChoices;
        private Action _onDialogueClosed;
        private int _page;
        private Text _playerTag;
        private DeckView _deck;
        private float _fpYaw;
        private float _fpPitch;
        private readonly List<Renderer> _playerRenderers = new List<Renderer>();
        private float _autosaveTimer;
        private bool _confirmNewJourney;

        private void Awake()
        {
            // The build self-check must never touch the player's real journey.
            var selfCheckFolder = SelfCheckFolder();
            if (selfCheckFolder != null) SaveGame.DirectoryOverride = selfCheckFolder;
            if (worldMaterial == null) Debug.LogError("[Tabletop] World material not assigned; the world will render magenta in builds.");
            _kit = new WorldKit(worldMaterial);
            Layout = new WorldBuilder(_kit, icons, art).Build(transform);
            StaticBatchingUtility.Combine(Layout.StaticRoot.gameObject);

            BuildPlayer();
            _actions = Instantiate(inputActions);
            Input = new GameInputRouter(_actions, consumeShortcuts: false);
            Input.Interact += OnInteract;
            Input.Pause += OnPause;
            Input.Cancel += OnCancel;
            Input.Help += () => { if (!Busy) OpenPause(true); };
            Input.Inspect += OnDeckKey;
            Input.ToggleView += OnToggleView;
            Input.SchemeChanged += _ => RefreshControls();
            ConfigureEventSystem();
            Ui = new WorldUi(transform);
            _deck = new DeckView(Ui.Frame, ReferenceContent.Catalog, icons);
            Ui.AddOverlay(_deck.Root);
            WireUi();
            RefreshControls();

            if (worldCamera == null) worldCamera = Camera.main;
            PlaceAfterLoad();
            ApplyView();
        }

        private void OnEnable()
        {
            Input?.Enable();
            Input?.EnableWorld();
        }

        private void OnDisable() => Input?.Disable();

        private void OnDestroy()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            Input?.Dispose();
            if (_actions != null) Destroy(_actions);
        }

        private void BuildPlayer()
        {
            var go = new GameObject("Player");
            go.transform.SetParent(transform, false);
            go.AddComponent<CharacterController>();
            Player = go.AddComponent<PlayerController>();
            Player.Walkable = Layout.Walkable;
            var look = new PersonLook
            {
                Cloth = new Color(0.25f, 0.42f, 0.78f), Accent = new Color(0.55f, 0.35f, 0.2f),
                Hair = new Color(0.25f, 0.16f, 0.1f), Hat = HatKind.None,
            };
            var slot = art != null ? art.Person(WorldBuilder.PlayerKey) : null;
            Transform person;
            float height = 1.8f;
            if (slot != null)
            {
                person = new GameObject("Figure").transform;
                person.SetParent(go.transform, false);
                var body = new GameObject("Body").transform;
                body.SetParent(person, false);
                height = slot.size > 0 ? slot.size : WorldBuilder.DefaultPersonHeight;
                _kit.PlaceModel(slot, body, height, Vector2.zero, out _);
            }
            else
            {
                person = WorldPieces.Person(_kit, go.transform, "Figure", Vector3.zero, 0, look);
                _kit.Box("Satchel", person.Find("Body"), new Vector3(0.3f, 0.85f, -0.15f), new Vector3(0.2f, 0.3f, 0.3f), new Color(0.5f, 0.32f, 0.18f));
                _kit.Box("Cape", person.Find("Body"), new Vector3(0, 1.0f, -0.24f), new Vector3(0.5f, 0.8f, 0.06f), new Color(0.75f, 0.2f, 0.22f));
            }
            Player.Figure = person;
            // No floating "YOU": the camera already centres on the player (names fade in by distance, D-027).
            _playerRenderers.AddRange(person.GetComponentsInChildren<Renderer>(true));
        }

        private void ConfigureEventSystem()
        {
            if (eventSystem == null) eventSystem = EventSystem.current;
            if (eventSystem == null) eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            module.actionsAsset = _actions;
            module.move = InputActionReference.Create(_actions.FindAction("UI/Navigate", true));
            module.submit = InputActionReference.Create(_actions.FindAction("UI/Submit", true));
            module.cancel = InputActionReference.Create(_actions.FindAction("UI/Cancel", true));
            module.point = InputActionReference.Create(_actions.FindAction("UI/Point", true));
            module.leftClick = InputActionReference.Create(_actions.FindAction("UI/Click", true));
            module.scrollWheel = InputActionReference.Create(_actions.FindAction("UI/ScrollWheel", true));
            module.deselectOnBackgroundClick = false;
        }

        private void WireUi()
        {
            Ui.TitleContinue.onClick.AddListener(() => { MarkClick(); ContinueJourney(); });
            Ui.TitleBegin.onClick.AddListener(() => { MarkClick(); OnNewJourneyPressed(); });
            Ui.PauseDeck.onClick.AddListener(() => { MarkClick(); ClosePause(); OpenDeck(); });
            Ui.PauseView.onClick.AddListener(() => { MarkClick(); SetView(!FirstPerson); });
            Ui.TitlePractice.onClick.AddListener(() => { MarkClick(); GoPractice(); });
            Ui.TitleQuit.onClick.AddListener(() => { MarkClick(); QuitGame(); });
            Ui.PauseResume.onClick.AddListener(() => { MarkClick(); ClosePause(); });
            Ui.PauseHelp.onClick.AddListener(() => { MarkClick(); Ui.SetPauseText(HelpText()); });
            Ui.PausePractice.onClick.AddListener(() => { MarkClick(); GoPractice(); });
            Ui.PauseQuit.onClick.AddListener(() => { MarkClick(); QuitGame(); });
            _deck.Close.onClick.AddListener(() => { MarkClick(); CloseDeck(); });
            Ui.Continue.onClick.AddListener(() => { if (MarkClick()) NextPage(); });
        }

        /// <summary>Records that a UI button fired this frame; returns false if the dialogue opened this same frame.</summary>
        private bool MarkClick()
        {
            _buttonClickedFrame = Time.frameCount;
            return Time.frameCount != _dialogueOpenedFrame;
        }

        // ------------------------------------------------------------------ arrival / return

        private void PlaceAfterLoad()
        {
            if (GameFlow.HasReturnPoint)
            {
                var p = new Vector3(GameFlow.ReturnX, 0, GameFlow.ReturnZ);
                Player.Teleport(p, p.x > 150 ? 0 : 180);
                _fpYaw = Player.transform.eulerAngles.y;
                GameFlow.ConsumeReturnPoint();
            }
            else
            {
                Player.Teleport(Layout.Spawn, Layout.SpawnYaw);
            }
            CurrentArea = WorldLayout.AreaAt(Player.transform.position);
            if (!GameFlow.TitleShown)
            {
                bool hasSave = SaveGame.Exists;
                Ui.LinkTitle(hasSave);
                Ui.TitleBegin.SetText(hasSave ? "NEW JOURNEY" : "BEGIN YOUR JOURNEY");
                Ui.Title.SetActive(true);
                Select(hasSave ? Ui.TitleContinue : Ui.TitleBegin);
                return;
            }
            var outcome = GameFlow.ConsumeOutcome();
            SaveNow(); // back from a table: the result is part of the journey now
            if (outcome != null) ShowOutcome(outcome);
            else Ui.ShowArea(CurrentArea);
        }

        private void OnNewJourneyPressed()
        {
            // Starting over replaces the saved journey, so ask for a second press first.
            if (SaveGame.Exists && !_confirmNewJourney)
            {
                _confirmNewJourney = true;
                Ui.TitleBegin.SetText("START OVER? PRESS AGAIN");
                return;
            }
            BeginJourney();
        }

        /// <summary>A fresh journey from Hearthmoor (replaces any saved one).</summary>
        private void BeginJourney()
        {
            GameFlow.Reset();
            GameFlow.TitleShown = true;
            Player.Teleport(Layout.Spawn, Layout.SpawnYaw);
            _fpYaw = Layout.SpawnYaw;
            CurrentArea = WorldLayout.AreaAt(Player.transform.position);
            Ui.Title.SetActive(false);
            _closedFrame = Time.frameCount;
            ApplyView();
            SaveNow();
            Ui.ShowArea(CurrentArea);
        }

        /// <summary>Resume the saved journey where the player left off.</summary>
        private void ContinueJourney()
        {
            if (!SaveGame.TryLoad(out var data)) { BeginJourney(); return; }
            SaveGame.Apply(data);
            var p = new Vector3(data.x, 0, data.z);
            if (!Layout.Walkable.Contains(p.x, p.z)) p = Layout.Spawn; // saves from an older layout
            Player.Teleport(p, data.yaw);
            _fpYaw = data.yaw;
            CurrentArea = WorldLayout.AreaAt(Player.transform.position);
            Ui.Title.SetActive(false);
            _closedFrame = Time.frameCount;
            ApplyView();
            Ui.ShowArea(CurrentArea);
        }

        /// <summary>Writes the journey to disk (no-op before the journey has started).</summary>
        public bool SaveNow()
        {
            if (!GameFlow.TitleShown || Player == null) return false;
            _autosaveTimer = 0;
            var pos = _seatedAt != null ? _seatedAt.transform.position : Player.transform.position;
            return SaveGame.Save(pos, FirstPerson ? _fpYaw : Player.transform.eulerAngles.y);
        }

        private void ShowOutcome(EncounterOutcome outcome)
        {
            var npc = Layout.Find(outcome.EncounterId);
            if (npc == null) return;
            npc.FaceTowards(Player.transform.position);
            var enc = npc.Encounter;
            var pages = new List<(string, string, Sprite)> { (npc.DisplayName, outcome.PlayerWon ? enc.WinLine : enc.LoseLine, Portrait(npc)) };
            if (outcome.PlayerWon && enc.IsChampion)
                pages.Add(("", "You are now the Reels Champion of Brindlecross!", icons != null ? icons.crown : null));
            else if (outcome.PlayerWon)
                pages.Add(("", "You beat " + npc.DisplayName + "! (" + GameFlow.Wins + " of " + EncounterCatalog.All.Count + " players beaten)", icons != null ? icons.xp : null));
            if (outcome.UnlockedUnit != null && ReferenceContent.Catalog.TryGetUnit(outcome.UnlockedUnit, out var won))
                pages.Add(("NEW PIECE", "You won the " + won.DisplayName.ToUpperInvariant() + "! It's in your deck now and you can bring it to any table. "
                    + "Open your deck with " + Input.Binding("Match", "Inspect") + ".", icons != null ? icons.Unit(won.Id) : null));
            OpenDialogue(pages, null, () => npc.FaceHome());
            _talkingTo = npc;
        }

        // ------------------------------------------------------------------ frame loop

        private void Update()
        {
            float dt = Time.deltaTime;
            if (FirstPerson && !Busy && _seatedAt == null) Look(dt);
            if (!Busy && _seatedAt == null)
            {
                var move = Input.Move;
                if (FirstPerson) move = RotateByYaw(move, _fpYaw);
                // Gamepad A both jumps and talks: next to someone it talks.
                bool jump = Input.JumpPressedThisFrame && Time.frameCount != _closedFrame
                            && !(Nearest != null && Input.InteractPressedThisFrame);
                Player.Step(move, dt, Input.SprintHeld, jump, FirstPerson ? _fpYaw : (float?)null);
            }
            UpdateCamera(dt);
            _kit.FaceCamera(worldCamera);
            Ui.Tick(dt);

            var area = WorldLayout.AreaAt(Player.transform.position);
            if (area != CurrentArea)
            {
                CurrentArea = area;
                Ui.ShowArea(area);
                SaveNow();
            }
            if (GameFlow.TitleShown && !Busy && (_autosaveTimer += dt) >= AutosaveSeconds) SaveNow();

            Nearest = Busy || _seatedAt != null ? null : FindNearest();
            Ui.SetPrompt(Nearest != null ? "[" + Input.Binding("WorldReserved", "Interact") + "]  " + Nearest.Prompt : "");
            Ui.SetHud(CurrentArea.ToUpperInvariant() + "\nReels wins: " + GameFlow.Wins + " / " + EncounterCatalog.All.Count
                      + (GameFlow.HasDefeated(EncounterCatalog.Champion) ? "   CHAMPION!" : ""));
            var me = Player.transform.position;
            foreach (var n in Layout.Npcs)
            {
                n.RefreshTag();
                var d = n.transform.position - me;
                d.y = 0;
                n.SetTagAlpha(Mathf.InverseLerp(TagFar, TagNear, d.magnitude));
            }
            _deck.Tick(eventSystem);
            // At the champion's table the close-up camera would be covered by labels: hide them while seated.
            bool seated = _seatedAt != null;
            if (_playerTag != null) _playerTag.enabled = !seated;
            if (Layout.ChampionChair != null)
            {
                var champ = Layout.ChampionChair.Champion;
                if (champ.Tag != null) champ.Tag.enabled = !seated;
                if (champ.Marker != null && seated) champ.Marker.gameObject.SetActive(false);
            }
            if (seated) Ui.HideArea();
            EnsureFocus();
            // First person steers with the mouse: capture it while exploring, free it for menus.
            bool capture = FirstPerson && !Busy && !seated;
            UnityEngine.Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            UnityEngine.Cursor.visible = !capture;
        }

        private void LateUpdate()
        {
            // Interact advances dialogue only when no UI button already handled this press (gamepad A is both).
            if (_pendingInteract)
            {
                _pendingInteract = false;
                if (DialogueOpen && _buttonClickedFrame != Time.frameCount && _dialogueOpenedFrame != Time.frameCount)
                {
                    var sel = eventSystem.currentSelectedGameObject;
                    var b = sel != null ? sel.GetComponent<Button>() : null;
                    if (b != null && b.transform.IsChildOf(Ui.Dialogue.transform)) b.onClick.Invoke();
                }
            }
        }

        private Interactable FindNearest()
        {
            Interactable best = null;
            float bestD = float.MaxValue;
            var p = Player.transform.position;
            var facing = Quaternion.Euler(0, _fpYaw, 0) * Vector3.forward;
            foreach (var i in Layout.Interactables)
            {
                if (i == null || !i.Available || i.Radius <= 0) continue;
                var d = i.transform.position - p;
                d.y = 0;
                float dist = d.magnitude;
                // In first person you talk to what you're looking at, not whoever is behind you.
                if (FirstPerson && dist > 0.8f && Vector3.Angle(facing, d) > 65f) continue;
                if (dist <= i.Radius && dist < bestD) { best = i; bestD = dist; }
            }
            return best;
        }

        private void UpdateCamera(float dt)
        {
            if (worldCamera == null) return;
            if (FirstPerson && _seatedAt == null)
            {
                worldCamera.transform.SetPositionAndRotation(EyePosition(), Quaternion.Euler(_fpPitch, _fpYaw, 0));
                return;
            }
            Vector3 target, offset;
            if (_seatedAt != null) { target = Layout.TableFocus; offset = SeatedOffset; }
            else { target = Player.transform.position + new Vector3(0, 1f, 0); offset = CameraOffset; }
            float k = 1f - Mathf.Exp(-6f * dt);
            worldCamera.transform.position = Vector3.Lerp(worldCamera.transform.position, target + offset, k);
            var look = Quaternion.LookRotation(target - (target + offset));
            worldCamera.transform.rotation = Quaternion.Slerp(worldCamera.transform.rotation, look, k);
        }

        public void SnapCamera()
        {
            if (worldCamera == null) return;
            if (FirstPerson && _seatedAt == null)
            {
                worldCamera.transform.SetPositionAndRotation(EyePosition(), Quaternion.Euler(_fpPitch, _fpYaw, 0));
                return;
            }
            var target = _seatedAt != null ? Layout.TableFocus : Player.transform.position + new Vector3(0, 1f, 0);
            var offset = _seatedAt != null ? SeatedOffset : CameraOffset;
            worldCamera.transform.position = target + offset;
            worldCamera.transform.rotation = Quaternion.LookRotation(-offset);
        }

        private Vector3 EyePosition() => Player.transform.position + new Vector3(0, EyeHeight, 0);

        private void Look(float dt)
        {
            var mouse = Input.LookDelta * MouseLookSpeed;
            var stick = Input.LookStick * StickLookSpeed * dt;
            _fpYaw = Mathf.Repeat(_fpYaw + mouse.x + stick.x, 360f);
            _fpPitch = Mathf.Clamp(_fpPitch - mouse.y - stick.y * 0.7f, -70f, 70f);
        }

        /// <summary>Turns stick/keys input (x right, y forward) into world directions for a camera facing <paramref name="yaw"/>.</summary>
        public static Vector2 RotateByYaw(Vector2 input, float yaw)
        {
            float r = yaw * Mathf.Deg2Rad, c = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector2(input.x * c + input.y * sn, -input.x * sn + input.y * c);
        }

        // ------------------------------------------------------------------ view (overhead / first person)

        private void OnToggleView()
        {
            if (Busy || _seatedAt != null) return;
            SetView(!FirstPerson);
        }

        /// <summary>Switches between the overhead camera and first person, and remembers the choice in the save.</summary>
        public void SetView(bool firstPerson)
        {
            if (firstPerson && !FirstPerson)
            {
                _fpYaw = Player.transform.eulerAngles.y;
                _fpPitch = 5f;
            }
            GameFlow.FirstPersonView = firstPerson;
            ApplyView();
            SaveNow();
        }

        private void ApplyView()
        {
            if (worldCamera != null) worldCamera.fieldOfView = FirstPerson ? FirstPersonFov : OverheadFov;
            // Your own body would fill the lens in first person; keep its shadow.
            var mode = FirstPerson ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            foreach (var r in _playerRenderers) if (r != null) r.shadowCastingMode = mode;
            if (Ui != null) Ui.PauseView.SetText(FirstPerson ? "VIEW: FIRST PERSON" : "VIEW: OVERHEAD");
            SnapCamera();
        }

        /// <summary>Test hook: face a direction in first person.</summary>
        public void SetFirstPersonYaw(float yaw) => _fpYaw = yaw;

        // ------------------------------------------------------------------ deck

        private void OnDeckKey()
        {
            if (_deck.IsOpen) { CloseDeck(); return; }
            if (Busy || _seatedAt != null || !GameFlow.TitleShown) return;
            OpenDeck();
        }

        public void OpenDeck()
        {
            Select(_deck.Open());
        }

        public void CloseDeck()
        {
            if (!_deck.IsOpen) return;
            _deck.Hide();
            _closedFrame = Time.frameCount;
        }

        // ------------------------------------------------------------------ input intents

        private void OnInteract()
        {
            if (Ui.Title.activeSelf || Ui.Pause.activeSelf || _fading) return;
            if (DialogueOpen) { _pendingInteract = true; return; }
            if (Time.frameCount == _closedFrame) return;
            if (Nearest != null) Nearest.Interact(this);
        }

        private void OnPause()
        {
            if (Ui.Title.activeSelf || _fading || Time.frameCount == _closedFrame) return;
            if (_deck.IsOpen) { CloseDeck(); return; }
            if (Ui.Pause.activeSelf) { ClosePause(); return; }
            if (DialogueOpen) { CloseDialogue(); return; }
            OpenPause(false);
        }

        private void OnCancel()
        {
            if (Time.frameCount == _closedFrame) return;
            if (_deck.IsOpen) CloseDeck();
            else if (Ui.Pause.activeSelf) ClosePause();
            else if (DialogueOpen && _finalChoices == null) CloseDialogue();
        }

        private void OpenPause(bool showHelp)
        {
            _returnFocus = eventSystem.currentSelectedGameObject;
            Ui.Pause.SetActive(true);
            Ui.SetPauseText(showHelp ? HelpText() : "");
            Select(Ui.PauseResume);
        }

        private void ClosePause()
        {
            Ui.Pause.SetActive(false);
            _closedFrame = Time.frameCount;
        }

        private string HelpText() =>
            "Walk: " + Input.Binding("WorldReserved", "Move") + "   Sprint: " + Input.Binding("WorldReserved", "Sprint")
            + "   Jump: " + Input.Binding("WorldReserved", "Jump") + "\nTalk / use: " + Input.Binding("WorldReserved", "Interact")
            + "   View: " + Input.Binding("WorldReserved", "ToggleView") + "   Deck: " + Input.Binding("Match", "Inspect")
            + "   Menu: " + Input.Binding("Match", "Pause") + "\n\n"
            + "People with a gem over their heads play Reels. Talk to them and choose CHALLENGE to sit down at their table. "
            + "Everyone in town plays the Striker and the Caster; the Champion in the great hall has a Ranger, and beating him wins it.\n"
            + "Your journey saves itself as you go.";

        private void RefreshControls()
        {
            if (Ui == null) return;
            Ui.SetControls("Walk: " + Input.Binding("WorldReserved", "Move") + "   Sprint: " + Input.Binding("WorldReserved", "Sprint")
                           + "\nJump: " + Input.Binding("WorldReserved", "Jump") + "   Talk: " + Input.Binding("WorldReserved", "Interact")
                           + "   Deck: " + Input.Binding("Match", "Inspect")
                           + "\nView: " + Input.Binding("WorldReserved", "ToggleView")
                           + "    Menu: " + Input.Binding("Match", "Pause"));
        }

        // ------------------------------------------------------------------ interactions

        public void TalkTo(Npc npc)
        {
            _talkingTo = npc;
            npc.FaceTowards(Player.transform.position);
            var portrait = Portrait(npc);
            var pages = new List<(string, string, Sprite)>();
            IList<(string, Action)> choices = null;
            if (npc.Encounter != null)
            {
                var enc = npc.Encounter;
                if (GameFlow.HasDefeated(enc.Id)) pages.Add((npc.DisplayName, enc.RematchLine, portrait));
                else foreach (var l in enc.Intro) pages.Add((npc.DisplayName, l, portrait));
                choices = new List<(string, Action)>
                {
                    ("CHALLENGE", () => { if (MarkClick()) Challenge(npc); }),
                    ("NOT NOW", () => { if (MarkClick()) CloseDialogue(); }),
                };
            }
            else
            {
                foreach (var l in npc.Lines) pages.Add((npc.DisplayName, l, portrait));
            }
            OpenDialogue(pages, choices, () => npc.FaceHome());
        }

        public void ReadSign(Sign sign)
        {
            OpenDialogue(new List<(string, string, Sprite)> { (sign.Title, sign.Text, null) }, null, null);
        }

        public void UseDoor(Door door)
        {
            StartCoroutine(FadeThen(() =>
            {
                Player.Teleport(door.Target, door.TargetYaw);
                SnapCamera();
            }));
        }

        public void SitAt(Chair chair)
        {
            _seatedAt = chair;
            Player.SetSeated(true, chair.SeatPosition, 0);
            var champ = chair.Champion;
            _talkingTo = champ;
            var enc = champ.Encounter;
            var portrait = Portrait(champ);
            var pages = new List<(string, string, Sprite)>();
            if (GameFlow.HasDefeated(enc.Id)) pages.Add((champ.DisplayName, enc.RematchLine, portrait));
            else
            {
                foreach (var l in enc.Intro) pages.Add((champ.DisplayName, l, portrait));
                int beaten = 0;
                foreach (var e in EncounterCatalog.All) if (!e.IsChampion && GameFlow.HasDefeated(e.Id)) beaten++;
                if (beaten > 0) pages.Insert(1, (champ.DisplayName, "You've already beaten " + beaten + (beaten == 1 ? " player" : " players") + " on the way here. Let's see if it was luck.", portrait));
            }
            var choices = new List<(string, Action)>
            {
                ("CHALLENGE", () => { if (MarkClick()) Challenge(champ); }),
                ("STAND UP", () => { if (MarkClick()) { CloseDialogue(); StandUp(); } }),
            };
            OpenDialogue(pages, choices, null);
        }

        public void StandUp()
        {
            if (_seatedAt == null) return;
            var chair = _seatedAt;
            _seatedAt = null;
            Player.SetSeated(false, chair.SeatPosition, 0);
            Player.Teleport(chair.transform.position, 180);
        }

        /// <summary>Start a match against this person: remember where we are and move to the table scene.</summary>
        public void Challenge(Npc npc)
        {
            CloseDialogue(false);
            var pos = Player.transform.position;
            if (_seatedAt != null) pos = _seatedAt.transform.position;
            GameFlow.TitleShown = true;
            SaveNow();
            GameFlow.BeginEncounter(npc.Encounter, pos.x, pos.z);
            StartCoroutine(FadeThen(() => SceneManager.LoadScene(MatchSceneName), keepBlack: true));
        }

        private void GoPractice()
        {
            GameFlow.TitleShown = true;
            var pos = Player.transform.position;
            GameFlow.BeginEncounter(null, pos.x, pos.z);
            StartCoroutine(FadeThen(() => SceneManager.LoadScene(MatchSceneName), keepBlack: true));
        }

        private void QuitGame()
        {
            SaveNow();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private Sprite Portrait(Npc npc)
        {
            if (icons == null || npc.Encounter == null) return null;
            return icons.Unit(npc.Encounter.UnitA);
        }

        // ------------------------------------------------------------------ dialogue

        private void OpenDialogue(List<(string, string, Sprite)> pages, IList<(string, Action)> finalChoices, Action onClosed)
        {
            if (pages.Count == 0) return;
            _pages = pages;
            _finalChoices = finalChoices;
            _onDialogueClosed = onClosed;
            _page = 0;
            _dialogueOpenedFrame = Time.frameCount;
            ShowCurrentPage();
        }

        private void ShowCurrentPage()
        {
            var p = _pages[_page];
            bool last = _page == _pages.Count - 1;
            Ui.ShowPage(p.speaker, p.line, p.portrait, last ? _finalChoices : null);
            Select(last && _finalChoices != null && _finalChoices.Count > 0 ? Ui.Choices[0] : Ui.Continue);
        }

        private void NextPage()
        {
            if (_page < _pages.Count - 1)
            {
                _page++;
                ShowCurrentPage();
            }
            else CloseDialogue();
        }

        public void CloseDialogue(bool runCallback = true)
        {
            if (!DialogueOpen) return;
            Ui.Dialogue.SetActive(false);
            _closedFrame = Time.frameCount;
            var cb = _onDialogueClosed;
            _onDialogueClosed = null;
            _finalChoices = null;
            if (runCallback) cb?.Invoke();
            if (_seatedAt != null && runCallback && _talkingTo == _seatedAt.Champion) StandUp();
        }

        // ------------------------------------------------------------------ focus / fade

        private void Select(Selectable s)
        {
            if (s != null && eventSystem != null) eventSystem.SetSelectedGameObject(s.gameObject);
        }

        private void EnsureFocus()
        {
            if (eventSystem == null) return;
            GameObject modal = Ui.Title.activeSelf ? Ui.Title : _deck.IsOpen ? _deck.Root : Ui.Pause.activeSelf ? Ui.Pause : DialogueOpen ? Ui.Dialogue : null;
            if (modal == null) return;
            var cur = eventSystem.currentSelectedGameObject;
            if (cur == null || !cur.activeInHierarchy || !cur.transform.IsChildOf(modal.transform))
            {
                var first = modal.GetComponentsInChildren<Selectable>().FirstOrDefault(x => x.IsInteractable() && x.gameObject.activeInHierarchy);
                if (first != null) eventSystem.SetSelectedGameObject(first.gameObject);
            }
        }

        private IEnumerator FadeThen(Action midpoint, bool keepBlack = false)
        {
            _fading = true;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * 4f) { Ui.FadeAlpha = t; yield return null; }
            Ui.FadeAlpha = 1;
            midpoint();
            if (keepBlack) yield break;
            yield return null;
            for (float t = 1; t > 0f; t -= Time.unscaledDeltaTime * 4f) { Ui.FadeAlpha = t; yield return null; }
            Ui.FadeAlpha = 0;
            _fading = false;
        }

        // ------------------------------------------------------------------ build self-check

        private void Start()
        {
            var folder = SelfCheckFolder();
            if (folder != null) StartCoroutine(SelfCheck(folder));
        }

        private void OnApplicationQuit() => SaveNow();

        /// <summary>The folder passed with -tabletopSelfCheck, or null in normal play.</summary>
        private static string SelfCheckFolder()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-tabletopSelfCheck") return args[i + 1];
            return null;
        }

        /// <summary>Test/automation hook: jump straight into exploring (skip the title).</summary>
        public void SkipTitleForTests()
        {
            if (Ui.Title.activeSelf) BeginJourney();
        }

        private IEnumerator SelfCheck(string folder)
        {
            if (GameFlow.Wins > 0 || GameFlow.HasReturnPoint) yield break; // only on the first world visit
            Directory.CreateDirectory(folder);
            yield return new WaitForSeconds(1f);
            yield return Capture(folder, "world_0_title");
            BeginJourney();
            yield return new WaitForSeconds(1f);
            LogWorldDiagnostics();
            yield return Capture(folder, "world_1_hearthmoor");
            foreach (var (name, pos) in new[] { ("world_2_bridge", new Vector3(-5.5f, 0, 57f)), ("world_2b_camp", new Vector3(-10.5f, 0, 39.5f)), ("world_3_brindlecross", new Vector3(0, 0, 124f)),
                                                  ("world_3b_inn", new Vector3(10f, 0, 136f)), ("world_4_hall_outside", new Vector3(0, 0, 152f)) })
            {
                Player.Teleport(pos, 0);
                SnapCamera();
                yield return new WaitForSeconds(0.6f);
                yield return Capture(folder, name);
            }
            Player.Teleport(new Vector3(0, 0, 118f), 0);
            SetFirstPersonYaw(0);
            SetView(true);
            yield return new WaitForSeconds(0.6f);
            yield return Capture(folder, "world_3c_first_person");
            SetView(false);
            TalkTo(Layout.Find(EncounterCatalog.Mira));
            yield return new WaitForSeconds(0.3f);
            yield return Capture(folder, "world_5_dialogue");
            CloseDialogue();
            OpenDeck();
            yield return new WaitForSeconds(0.3f);
            yield return Capture(folder, "world_5b_deck");
            CloseDeck();
            UseDoor(Layout.HallDoor);
            yield return new WaitForSeconds(1.2f);
            SitAt(Layout.ChampionChair);
            yield return new WaitForSeconds(1.2f);
            yield return Capture(folder, "world_6_champion_table");
            Challenge(Layout.ChampionChair.Champion); // the match scene's own self-check continues from here
        }

        private static IEnumerator Capture(string folder, string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
            yield return null;
            Debug.Log("[Tabletop] SelfCheck captured " + name);
        }

        private void LogWorldDiagnostics()
        {
            int total = 0, unsupported = 0;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                total++;
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || !m.shader.isSupported || !m.shader.name.StartsWith("Universal Render Pipeline")) unsupported++;
            }
            int models = Layout.Root.Find("Models") != null ? Layout.Root.Find("Models").childCount : 0;
            int modelPeople = 0;
            foreach (var n in Layout.Npcs) if (n.Figure != null && n.Figure.Find("Model") != null) modelPeople++;
            Debug.Log("[Tabletop] SelfCheck world renderers=" + total + " unsupportedShader=" + unsupported
                      + " modelBuildings=" + models + " modelPeople=" + modelPeople);
        }
    }
}
