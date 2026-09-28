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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tabletop.World
{
    /// <summary>
    /// Composition root for World.unity: builds the world, walks the player around, runs errands and stalls, and
    /// hands challenges (with their stakes and charms) to the match table through <see cref="GameFlow"/>.
    /// Holds no match rules.
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
        [Tooltip("Pack art for the land, buildings, trees and people (D-033). Optional: placeholders without it.")]
        [SerializeField] private WorldLook look;
        [Tooltip("Music, ambience and sound effects (D-033). Optional: silent without it.")]
        [SerializeField] private SoundBank sounds;

        public static readonly Vector3 CameraOffset = new Vector3(0, 12.5f, -13f);
        public static readonly Vector3 SeatedOffset = new Vector3(0, 4.6f, -4.8f);
        public const float EyeHeight = 1.6f;
        public const float OverheadFov = 40f;
        public const float FirstPersonFov = 70f;
        /// <summary>Degrees per pixel of mouse movement / per second of full stick in first person.</summary>
        public const float MouseLookSpeed = 0.12f;
        public const float StickLookSpeed = 150f;
        /// <summary>Name tags are fully visible within the near distance and gone beyond the far one (D-027).</summary>
        public const float TagNear = 7f;
        public const float TagFar = 11f;
        public const float AutosaveSeconds = 30f;
        public const float StepLength = 2.2f;

        public WorldLayout Layout { get; private set; }
        public PlayerController Player { get; private set; }
        public WorldUi Ui { get; private set; }
        public GameInputRouter Input { get; private set; }
        public Interactable Nearest { get; private set; }
        public bool DialogueOpen => Ui.Dialogue.activeSelf;
        public bool Busy => DialogueOpen || Ui.Pause.activeSelf || Ui.Title.activeSelf || _fading || (_deck != null && _deck.IsOpen) || (_list != null && _list.IsOpen);
        public bool FirstPerson => GameFlow.FirstPersonView;
        public DeckView Deck => _deck;
        /// <summary>The on-screen stick and buttons; null where there is no touchscreen.</summary>
        public WorldTouchPad TouchPad => _touchPad;
        public ListOverlay List => _list;
        public Camera WorldCamera => worldCamera;
        public string CurrentArea { get; private set; }
        public bool Seated => _seatedAt != null;
        public Material WorldMaterial => worldMaterial;
        public WorldArtSet Art => art;
        public WorldLook LookAsset => look;
        public SoundBank Sounds => sounds;

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
        private DeckView _deck;
        private ListOverlay _list;
        private WorldTouchPad _touchPad;
        private AudioDirector _audio;
        private float _fpYaw;
        private float _fpPitch;
        private readonly List<Renderer> _playerRenderers = new List<Renderer>();
        private float _autosaveTimer;
        private float _stepDistance;
        private bool _confirmNewJourney;

        private void Awake()
        {
            // The build self-check must never touch the player's real journey.
            var selfCheckFolder = SelfCheckFolder();
            if (selfCheckFolder != null) SaveGame.DirectoryOverride = selfCheckFolder;
            if (worldMaterial == null) Debug.LogError("[Tabletop] World material not assigned; the world will render magenta in builds.");
            Presentation.Ui.Kit = icons;
            _kit = new WorldKit(worldMaterial);
            Layout = new WorldBuilder(_kit, icons, art, look).Build(transform);
            StaticBatchingUtility.Combine(Layout.StaticRoot.gameObject);
            _audio = AudioDirector.Ensure(sounds);

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
            _list = new ListOverlay(Ui.Frame);
            Ui.AddOverlay(_list.Root);
            if (WorldTouchPad.Wanted)
            {
                _touchPad = new WorldTouchPad(Ui.Frame);
                Ui.AddOverlay(_touchPad.Root);
            }
            WireUi();
            RefreshControls();

            if (worldCamera == null) worldCamera = Camera.main;
            RefreshTournament();
            ApplyMood();
            PlaceAfterLoad();
            ApplyView();
            UpdateAreaAudio(true);
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
            Player.Roam = Layout.Roam;
            var slot = art != null ? art.Person(WorldBuilder.PlayerKey) : null;
            Transform person;
            if (slot != null)
            {
                person = new GameObject("Figure").transform;
                person.SetParent(go.transform, false);
                var body = new GameObject("Body").transform;
                body.SetParent(person, false);
                float height = slot.size > 0 ? slot.size : WorldBuilder.DefaultPersonHeight;
                _kit.PlaceModel(slot, body, height, Vector2.zero, out _);
            }
            else
            {
                person = WorldBuilder.BuildPlayerFigure(_kit, look, go.transform, out var rig);
                Player.Rig = rig;
            }
            Player.Figure = person;
            // No floating "YOU": the camera already centres on the player (names fade in by distance, D-027).
            _playerRenderers.AddRange(person.GetComponentsInChildren<Renderer>(true));
        }

        /// <summary>Sky, fog, light and colour grading from the world look (D-033).</summary>
        private void ApplyMood()
        {
            if (worldCamera == null) return;
            worldCamera.farClipPlane = 320f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.56f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.82f, 0.9f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 260f;
            if (look != null && look.skybox != null)
            {
                RenderSettings.skybox = look.skybox;
                worldCamera.clearFlags = CameraClearFlags.Skybox;
            }
            else worldCamera.backgroundColor = RenderSettings.fogColor;
            if (look != null && look.postProcessing != null)
            {
                var volume = new GameObject("PostProcessing").AddComponent<Volume>();
                volume.transform.SetParent(transform, false);
                volume.isGlobal = true;
                volume.sharedProfile = look.postProcessing;
                var data = worldCamera.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }
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
            Ui.PauseJournal.onClick.AddListener(() => { MarkClick(); ClosePause(); OpenJournal(); });
            Ui.PauseView.onClick.AddListener(() => { MarkClick(); SetView(!FirstPerson); });
            Ui.PauseSound.onClick.AddListener(() => { MarkClick(); AudioDirector.Muted = !AudioDirector.Muted; RefreshSoundLabel(); });
            Ui.TitlePractice.onClick.AddListener(() => { MarkClick(); GoPractice(); });
            Ui.TitleQuit.onClick.AddListener(() => { MarkClick(); QuitGame(); });
            Ui.PauseResume.onClick.AddListener(() => { MarkClick(); ClosePause(); });
            Ui.PauseHelp.onClick.AddListener(() => { MarkClick(); Ui.SetPauseText(HelpText()); });
            Ui.PausePractice.onClick.AddListener(() => { MarkClick(); GoPractice(); });
            Ui.PauseQuit.onClick.AddListener(() => { MarkClick(); QuitGame(); });
            _deck.Close.onClick.AddListener(() => { MarkClick(); CloseDeck(); });
            Ui.Continue.onClick.AddListener(() => { if (MarkClick()) NextPage(); });
            RefreshSoundLabel();
        }

        private void RefreshSoundLabel() => Ui.PauseSound.SetText(AudioDirector.Muted ? "SOUND: OFF" : "SOUND: ON");

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
                Player.Teleport(p, p.x > WorldGround.InteriorThreshold ? 0 : 180);
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
            RefreshTournament();
            Player.Teleport(Layout.Spawn, Layout.SpawnYaw);
            _fpYaw = Layout.SpawnYaw;
            CurrentArea = WorldLayout.AreaAt(Player.transform.position);
            Ui.Title.SetActive(false);
            _closedFrame = Time.frameCount;
            ApplyView();
            SaveNow();
            Ui.ShowArea(CurrentArea);
            UpdateAreaAudio(true);
        }

        /// <summary>Resume the saved journey where the player left off.</summary>
        private void ContinueJourney()
        {
            if (!SaveGame.TryLoad(out var data)) { BeginJourney(); return; }
            SaveGame.Apply(data);
            RefreshTournament();
            var p = new Vector3(data.x, 0, data.z);
            if (!Layout.Roam.CanStand(p.x, p.z)) p = Layout.Spawn; // saves from an older layout
            Player.Teleport(p, data.yaw);
            _fpYaw = data.yaw;
            CurrentArea = WorldLayout.AreaAt(Player.transform.position);
            Ui.Title.SetActive(false);
            _closedFrame = Time.frameCount;
            ApplyView();
            Ui.ShowArea(CurrentArea);
            UpdateAreaAudio(true);
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
            if (outcome.CoinDelta > 0)
                pages.Add(("", "You won " + outcome.CoinDelta + " coins. You now have " + GameFlow.Coins + ".", icons != null ? icons.crown : null));
            else if (outcome.CoinDelta < 0)
                pages.Add(("", "You lost " + (-outcome.CoinDelta) + " coins. You have " + GameFlow.Coins + " left.", icons != null ? icons.crown : null));
            if (outcome.FavourOwed)
                pages.Add(("A FAVOUR", "You owe " + npc.DisplayName + " a favour. " + enc.FavourChore, null));
            if (enc.Tournament) TournamentPages(outcome, pages);
            else if (outcome.PlayerWon && enc.IsChampion)
            {
                pages.Add(("", "You are now the Wheels " + ChampionTitle(enc) + "! (" + GameFlow.ChampionTitles + " of " + GameFlow.ChampionTitlesNeeded + " champion titles)", icons != null ? icons.crown : null));
                if (GameFlow.TournamentQualified && GameFlow.TournamentAttempts == 0 && !GameFlow.IsGrandChampion)
                    pages.Add(("THE GRAND TOURNAMENT", "You hold every champion title in the realm. The Grand Tournament at Crownhold is open to you: take the Tourney Road north from Brindlecross, past the Champion's Hall, and speak to the Herald.", icons != null ? icons.crown : null));
            }
            else if (outcome.PlayerWon)
                pages.Add(("", "You beat " + npc.DisplayName + "! (" + GameFlow.Wins + " of " + EncounterCatalog.All.Count + " players beaten)", icons != null ? icons.xp : null));
            if (outcome.UnlockedUnit != null && ReferenceContent.Catalog.TryGetUnit(outcome.UnlockedUnit, out var won))
                pages.Add(("NEW FIGURINE", "You won the " + won.DisplayName.ToUpperInvariant() + "! It's in your deck now and you can bring it to any table. "
                    + "Open your deck with " + Input.Binding("Match", "Inspect") + ".", icons != null ? icons.Unit(won.Id) : null));
            if (outcome.PlayerWon) npc.Rig?.Play("Hit_A");
            if (outcome.CoinDelta != 0) _audio?.Sfx("sfx/coins");
            if (outcome.Tournament == TournamentResult.Won) { Player.Rig?.Play("Cheer"); Layout.Herald?.Rig?.Play("Cheer"); }
            OpenDialogue(pages, null, () => npc.FaceHome());
            _talkingTo = npc;
            if (outcome.FavourOwed) StartCoroutine(FavourFade());
        }

        /// <summary>"Champion of Lanternmere" from "the Nightjar, Champion of Duskhollow" and the like.</summary>
        public static string ChampionTitle(EncounterDefinition e)
        {
            int i = e.Title.IndexOf("Champion of", StringComparison.Ordinal);
            return i >= 0 ? e.Title.Substring(i) : "Champion of " + Areas.Town(e.Area);
        }

        /// <summary>What the Herald and the crowd say after a tournament match (D-038).</summary>
        private void TournamentPages(EncounterOutcome outcome, List<(string, string, Sprite)> pages)
        {
            var crown = icons != null ? icons.crown : null;
            var herald = Layout.Herald != null ? Layout.Herald.DisplayName : "THE HERALD";
            switch (outcome.Tournament)
            {
                case TournamentResult.Advanced:
                    var next = GameFlow.CurrentTournamentOpponent;
                    pages.Add((herald, "Round " + (GameFlow.TournamentRound - 1) + " goes to the challenger! "
                        + (next != null ? next.Name + " takes the table for " + (next.TournamentRound == EncounterCatalog.TournamentRounds ? "the final" : "round " + next.TournamentRound)
                            + ", playing the " + UnitName(next.UnitA) + " and the " + UnitName(next.UnitB) + ". Take your seat when you're ready." : ""), crown));
                    break;
                case TournamentResult.Eliminated:
                    pages.Add((herald, "The challenger is out of the tournament! Speak to me whenever you want to enter again: you'll start from the first round.", crown));
                    break;
                case TournamentResult.Won:
                    pages.Add(("GRAND CHAMPION", "You are the Grand Champion of the Realm!", crown));
                    if (GameFlow.ItemCount(ItemCatalog.PlatinumWheel) > 0)
                        pages.Add(("THE PLATINUM WHEEL", "Aldric hands you his Platinum Wheel, the finest there is. You'll bring it to every table from now on.", icons != null ? icons.uiRing : null));
                    pages.Add(("", "Word travels fast. In Hearthmoor, Gran Oddly tells everyone she taught you. In Brindlecross, Corvin Vale hangs your portrait in the hall. Up at the Outpost, Dorran Hale laughs for the first time in a year.", crown));
                    pages.Add(("", "Every table in the realm is still open to you, and you can defend your title at Crownhold whenever you like. Thank you for playing.", crown));
                    break;
                default:
                    if (outcome.Winner == Winner.Tie) pages.Add((herald, "A tie! The round is played again. Take your seat when you're ready.", crown));
                    break;
            }
        }

        private static string UnitName(string id) => ReferenceContent.Catalog.TryGetUnit(id, out var u) ? u.DisplayName : id;

        /// <summary>A short fade while the favour is done (time passes; nothing else changes).</summary>
        private IEnumerator FavourFade()
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * 3f) { Ui.FadeAlpha = t * 0.6f; yield return null; }
            yield return new WaitForSecondsRealtime(0.5f);
            for (float t = 0.6f; t > 0f; t -= Time.unscaledDeltaTime * 2f) { Ui.FadeAlpha = t; yield return null; }
            Ui.FadeAlpha = 0;
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
                Footsteps(dt);
            }
            else if (_seatedAt == null) Player.Rig?.SetMove(0f, false);
            // A connected controller replaces the on-screen pad (D-037).
            _touchPad?.SetVisible(!Busy && _seatedAt == null && !_touchPad.RealGamepadConnected);
            UpdateCamera(dt);
            UpdateOccluders();
            UpdateMood(dt);
            _kit.FaceCamera(worldCamera);
            Ui.Tick(dt);

            var area = WorldLayout.AreaAt(Player.transform.position);
            if (area != CurrentArea)
            {
                CurrentArea = area;
                Ui.ShowArea(area);
                SaveNow();
                UpdateAreaAudio(false);
            }
            if (GameFlow.TitleShown && !Busy && (_autosaveTimer += dt) >= AutosaveSeconds) SaveNow();

            Nearest = Busy || _seatedAt != null ? null : FindNearest();
            Ui.SetPrompt(Nearest != null ? "[" + Input.Binding("WorldReserved", "Interact") + "]  " + Nearest.Prompt : "");
            int titles = GameFlow.ChampionTitles;
            Ui.SetHud(CurrentArea.ToUpperInvariant() + (GameFlow.IsGrandChampion ? "   ★ GRAND CHAMPION" : titles > 0 ? "   ★ CHAMPION x" + titles : "")
                      + "\nCoins: " + GameFlow.Coins + "     Wins: " + GameFlow.Wins + " / " + EncounterCatalog.All.Count);
            var me = Player.transform.position;
            foreach (var n in Layout.Npcs)
            {
                n.RefreshTag();
                var d = n.transform.position - me;
                d.y = 0;
                n.SetTagAlpha(Mathf.InverseLerp(TagFar, TagNear, d.magnitude));
            }
            _deck.Tick(eventSystem);
            // At a champion's table the close-up camera would be covered by labels: hide them while seated.
            foreach (var chair in Layout.Chairs)
            {
                var champ = chair.Champion;
                if (champ == null) continue;
                bool here = _seatedAt == chair;
                if (champ.Tag != null) champ.Tag.Canvas.enabled = !here;
                if (champ.Marker != null && here) champ.Marker.gameObject.SetActive(false);
            }
            bool seated = _seatedAt != null;
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
            if (_seatedAt != null) { target = _seatedAt.TableFocus; offset = SeatedOffset; }
            else { target = Player.transform.position + new Vector3(0, 1f, 0); offset = CameraOffset + new Vector3(0, TerrainLift(Player.transform.position + new Vector3(0, 1f, 0)), 0); }
            float k = 1f - Mathf.Exp(-6f * dt);
            worldCamera.transform.position = Vector3.Lerp(worldCamera.transform.position, target + offset, k);
            var rot = Quaternion.LookRotation(target - (target + offset));
            worldCamera.transform.rotation = Quaternion.Slerp(worldCamera.transform.rotation, rot, k);
        }

        /// <summary>
        /// How much higher the overhead camera must sit so a hill between it and the player never hides them (D-038):
        /// the player can now wander into the hills, where the land is no longer kept low on the camera side.
        /// </summary>
        public float TerrainLift(Vector3 target)
        {
            if (target.x > WorldGround.InteriorThreshold) return 0f;
            float lift = 0f;
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                var p = target + CameraOffset * t;
                float ground = WorldGround.Terrain(p.x, p.z, Layout.Walkable) + 1.2f;
                float need = ground - p.y;
                if (need > 0f) lift = Mathf.Max(lift, need / t);
            }
            return Mathf.Min(lift, 20f);
        }

        /// <summary>
        /// Trees and buildings between the overhead camera and the player keep only their shadow, so wandering behind
        /// them never loses the player (D-038).
        /// </summary>
        private void UpdateOccluders()
        {
            if (worldCamera == null) return;
            bool active = !FirstPerson && _seatedAt == null;
            var head = Player.transform.position + new Vector3(0, 1.2f, 0);
            var cam = worldCamera.transform.position;
            foreach (var o in Layout.Occluders)
            {
                bool hide = false;
                if (active)
                {
                    var c = o.Bounds.center;
                    float dx = c.x - head.x, dz = c.z - head.z;
                    if (dx * dx + dz * dz < 30f * 30f)
                    {
                        var b = o.Bounds;
                        b.Expand(new Vector3(0.6f, 0f, 0.6f));
                        var dir = head - cam;
                        float len = dir.magnitude;
                        hide = b.IntersectRay(new Ray(cam, dir / len), out float hit) && hit < len - 0.3f && !b.Contains(head);
                    }
                }
                o.SetHidden(hide);
            }
        }

        /// <summary>Darker, closer fog in the pinewood of Duskhollow; the usual valley haze everywhere else.</summary>
        private void UpdateMood(float dt)
        {
            if (!RenderSettings.fog) return;
            bool dusk = CurrentArea == Areas.Duskhollow || CurrentArea == Areas.HollowPath;
            var colour = dusk ? new Color(0.42f, 0.48f, 0.5f) : new Color(0.74f, 0.82f, 0.9f);
            float start = dusk ? 18f : 60f, end = dusk ? 120f : 260f;
            float k = 1f - Mathf.Exp(-1.5f * dt);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, colour, k);
            RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, start, k);
            RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, end, k);
        }

        public void SnapCamera()
        {
            if (worldCamera == null) return;
            if (FirstPerson && _seatedAt == null)
            {
                worldCamera.transform.SetPositionAndRotation(EyePosition(), Quaternion.Euler(_fpPitch, _fpYaw, 0));
                return;
            }
            var target = _seatedAt != null ? _seatedAt.TableFocus : Player.transform.position + new Vector3(0, 1f, 0);
            var offset = _seatedAt != null ? SeatedOffset : CameraOffset + new Vector3(0, TerrainLift(target), 0);
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

        // ------------------------------------------------------------------ sound

        /// <summary>Music and ambience for each place (D-033).</summary>
        public static (string music, string ambience) AreaSounds(string area)
        {
            switch (area)
            {
                case Areas.Hearthmoor: return ("music/village", "amb/village_calm");
                case Areas.NorthRoad: return ("music/road", "amb/forest");
                case Areas.Brindlecross: return ("music/town", "amb/village_busy");
                case Areas.Hall: return ("music/hall", null);
                case Areas.QuarryPath: return ("music/road", "amb/wind");
                case Areas.Outpost: return ("music/outpost", "amb/wind");
                case Areas.StreamPath: return ("music/road", "amb/forest");
                case Areas.Lanternmere: return ("music/village", "amb/lake");
                case Areas.HollowPath: return ("music/road", "amb/forest");
                case Areas.Duskhollow: return ("music/hollow", "amb/forest");
                case Areas.BellRoad: return ("music/road", "amb/wind");
                case Areas.Ironbell: return ("music/outpost", "amb/wind");
                case Areas.MoorTrack: return ("music/road", "amb/wind");
                case Areas.TourneyRoad: return ("music/road", "amb/forest");
                case Areas.Crownhold: return ("music/tournament", "amb/village_busy");
                default: return ("music/village", "amb/village_calm");
            }
        }

        private void UpdateAreaAudio(bool force)
        {
            if (_audio == null || (Ui.Title.activeSelf && !force)) return;
            var (music, amb) = AreaSounds(CurrentArea);
            _audio.PlayMusic(music);
            _audio.PlayAmbience(amb);
        }

        private void Footsteps(float dt)
        {
            if (_audio == null || !Player.Grounded || Player.LastSpeed <= 0.01f) return;
            _stepDistance += Player.LastSpeed * Player.Speed * dt;
            if (_stepDistance < StepLength) return;
            _stepDistance = 0;
            var p = Player.transform.position;
            bool stone = CurrentArea == Areas.Hall || CurrentArea == Areas.Outpost || CurrentArea == Areas.Crownhold || CurrentArea == Areas.Ironbell
                         || (CurrentArea == Areas.Brindlecross && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(0, 134)) < 12f);
            _audio.Sfx(stone ? "step/stone" : "step/grass", 0.28f, 0.12f);
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
            var mode = FirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
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
            if (Ui.Title.activeSelf || Ui.Pause.activeSelf || _fading || _list.IsOpen) return;
            if (DialogueOpen) { _pendingInteract = true; return; }
            if (Time.frameCount == _closedFrame) return;
            if (Nearest != null) Nearest.Interact(this);
        }

        private void OnPause()
        {
            if (Ui.Title.activeSelf || _fading || Time.frameCount == _closedFrame) return;
            if (_deck.IsOpen) { CloseDeck(); return; }
            if (_list.IsOpen) { CloseList(); return; }
            if (Ui.Pause.activeSelf) { ClosePause(); return; }
            if (DialogueOpen) { CloseDialogue(); return; }
            OpenPause(false);
        }

        private void OnCancel()
        {
            if (Time.frameCount == _closedFrame) return;
            if (_deck.IsOpen) CloseDeck();
            else if (_list.IsOpen) CloseList();
            else if (Ui.Pause.activeSelf) ClosePause();
            else if (DialogueOpen) CloseDialogue(); // like NOT NOW (or STAND UP at a champion's table)
        }

        private void OpenPause(bool showHelp)
        {
            _returnFocus = eventSystem.currentSelectedGameObject;
            Ui.Pause.SetActive(true);
            Ui.SetPauseText(showHelp ? HelpText() : Ui.ControlsText);
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
            + "People with a golden marker over their heads play Wheels. Most play for coins: win and take their stake, lose and pay yours. "
            + "Short of coins? They'll play you for a favour instead. Villagers pay coins for errands, and stalls sell charms "
            + "(one per match) and better wheels. Each town's Champion holds a new figurine; hold all five champion titles and the "
            + "Grand Tournament at Crownhold opens to you. Wander wherever the land lets you: off the paths, over the hills.\n"
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

        /// <summary>The label for sitting down at this person's table (shows what is at stake).</summary>
        public static string PlayLabel(EncounterDefinition e)
        {
            if (e != null && e.Tournament) return e.TournamentRound == EncounterCatalog.TournamentRounds ? "PLAY THE FINAL" : "PLAY ROUND " + e.TournamentRound;
            switch (GameFlow.StakeModeFor(e))
            {
                case StakeMode.Friendly: return "PLAY (FREE)";
                case StakeMode.Coins: return "PLAY FOR " + e.Stake;
                default: return "PLAY FOR A FAVOUR";
            }
        }

        private static ErrandDefinition ErrandToHandIn(string person)
        {
            foreach (var e in ErrandCatalog.All)
            {
                var st = GameFlow.ErrandState(e.Id);
                if (e.IsDelivery && e.Receiver == person && st == ErrandStage.Active) return e;
                if (!e.IsDelivery && e.Giver == person && st == ErrandStage.Found) return e;
            }
            return null;
        }

        private static ErrandDefinition ErrandUnderWay(string giver)
        {
            foreach (var e in ErrandCatalog.All)
                if (e.Giver == giver && (GameFlow.ErrandState(e.Id) == ErrandStage.Active || GameFlow.ErrandState(e.Id) == ErrandStage.Found)) return e;
            return null;
        }

        private static ErrandDefinition ErrandOnOffer(string giver)
        {
            if (ErrandUnderWay(giver) != null) return null;
            foreach (var e in ErrandCatalog.All) if (e.Giver == giver && ErrandCatalog.Offerable(e)) return e;
            return null;
        }

        public void TalkTo(Npc npc)
        {
            _talkingTo = npc;
            npc.FaceTowards(Player.transform.position);
            var portrait = Portrait(npc);

            // Handing in an errand comes first.
            var hand = ErrandToHandIn(npc.DisplayName);
            if (hand != null)
            {
                int reward = GameFlow.CompleteErrand(hand);
                SaveNow();
                _audio?.Sfx("sfx/coins");
                npc.Rig?.Play("Cheer");
                var done = new List<(string, string, Sprite)>
                {
                    (npc.DisplayName, hand.IsDelivery ? hand.DeliverLine : hand.ThanksLine, portrait),
                    ("ERRAND DONE", hand.Title + ": +" + reward + " coins. You now have " + GameFlow.Coins + ".", icons != null ? icons.crown : null),
                };
                OpenDialogue(done, null, () => npc.FaceHome());
                return;
            }

            if (npc.Role == "herald") { HeraldTalk(npc, portrait); return; }
            if (npc.Encounter != null && npc.Encounter.Tournament)
            {
                var enc = npc.Encounter;
                var lines = new List<(string, string, Sprite)> { (npc.DisplayName, GameFlow.HasDefeated(enc.Id) ? enc.RematchLine : enc.Intro[0], portrait) };
                lines.Add((npc.DisplayName, GameFlow.TournamentRound > 0 && GameFlow.TournamentRound < enc.TournamentRound
                    ? "Win your round first. Then we'll talk."
                    : "We only play at the tournament table, and only in our round. The Herald will enter you.", portrait));
                OpenDialogue(lines, null, () => npc.FaceHome());
                return;
            }

            if (npc.ShopId != null)
            {
                var shop = ShopCatalog.Find(npc.ShopId);
                var shopPages = new List<(string, string, Sprite)> { (npc.DisplayName, shop != null ? shop.Greeting : "Have a look.", portrait) };
                var shopChoices = new List<(string, Action)>
                {
                    ("BROWSE", () => { if (MarkClick()) { CloseDialogue(false); OpenShop(npc.ShopId); } }),
                    ("NOT NOW", () => { if (MarkClick()) CloseDialogue(); }),
                };
                OpenDialogue(shopPages, shopChoices, () => npc.FaceHome());
                return;
            }

            var pages = new List<(string, string, Sprite)>();
            var underWay = ErrandUnderWay(npc.DisplayName);
            var offer = ErrandOnOffer(npc.DisplayName);
            IList<(string, Action)> choices = null;
            if (npc.Encounter != null)
            {
                var enc = npc.Encounter;
                if (GameFlow.HasDefeated(enc.Id)) pages.Add((npc.DisplayName, enc.RematchLine, portrait));
                else foreach (var l in enc.Intro) pages.Add((npc.DisplayName, l, portrait));
                if (underWay != null) pages.Add((npc.DisplayName, underWay.Reminder, portrait));
                var list = new List<(string, Action)> { (PlayLabel(enc), () => { if (MarkClick()) StartChallenge(npc); }) };
                if (offer != null) list.Add(("ANY WORK?", () => { if (MarkClick()) OfferErrand(npc, offer); }));
                list.Add(("NOT NOW", () => { if (MarkClick()) CloseDialogue(); }));
                choices = list;
            }
            else
            {
                foreach (var l in npc.Lines) pages.Add((npc.DisplayName, l, portrait));
                if (underWay != null) pages.Add((npc.DisplayName, underWay.Reminder, portrait));
                if (offer != null)
                {
                    foreach (var l in offer.Offer) pages.Add((npc.DisplayName, l, portrait));
                    choices = ErrandChoices(npc, offer);
                }
            }
            OpenDialogue(pages, choices, () => npc.FaceHome());
        }

        /// <summary>The Herald enters champions in the Grand Tournament (D-038).</summary>
        private void HeraldTalk(Npc npc, Sprite portrait)
        {
            var crown = icons != null ? icons.crown : null;
            var pages = new List<(string, string, Sprite)>();
            IList<(string, Action)> choices = null;
            if (GameFlow.TournamentRound > 0)
            {
                var opp = GameFlow.CurrentTournamentOpponent;
                pages.Add((npc.DisplayName, "You're in the tournament, champion. " + (opp.TournamentRound == EncounterCatalog.TournamentRounds ? "The final" : "Round " + opp.TournamentRound)
                    + " is waiting: " + opp.Name + " is at the table with the " + UnitName(opp.UnitA) + " and the " + UnitName(opp.UnitB) + ". Take the empty chair.", crown));
            }
            else if (!GameFlow.TournamentQualified)
            {
                pages.Add((npc.DisplayName, "Hear ye! The Grand Tournament of Crownhold: three rounds, one after another, for the title of Grand Champion of the Realm.", crown));
                var missing = new List<string>();
                foreach (var c in EncounterCatalog.TownChampions) if (!GameFlow.HasDefeated(c.Id)) missing.Add(c.Name + " (" + Areas.Town(c.Area) + ")");
                pages.Add((npc.DisplayName, "Only a champion of all five towns may enter. You hold " + GameFlow.ChampionTitles + " of " + GameFlow.ChampionTitlesNeeded
                    + " titles. Still to beat: " + string.Join(", ", missing) + ".", crown));
            }
            else
            {
                pages.Add((npc.DisplayName, GameFlow.IsGrandChampion
                    ? "The Grand Champion returns! Will you defend your title? Three rounds again, from the first."
                    : "Five titles! You may enter the Grand Tournament. Three rounds, one after another. Lose once and you start again from the first round. Win them all and you are the Grand Champion of the Realm.", crown));
                choices = new List<(string, Action)>
                {
                    ("ENTER THE TOURNAMENT", () => { if (MarkClick()) EnterTournament(npc); }),
                    ("NOT NOW", () => { if (MarkClick()) CloseDialogue(); }),
                };
            }
            OpenDialogue(pages, choices, () => npc.FaceHome());
        }

        public void EnterTournament(Npc herald)
        {
            CloseDialogue(false);
            if (!GameFlow.EnterTournament()) return;
            RefreshTournament();
            SaveNow();
            _audio?.Sfx("sfx/chips");
            var opp = GameFlow.CurrentTournamentOpponent;
            OpenDialogue(new List<(string, string, Sprite)>
            {
                (herald != null ? herald.DisplayName : "THE HERALD", "The challenger enters the Grand Tournament! Round one: " + opp.Name + ", with the " + UnitName(opp.UnitA) + " and the " + UnitName(opp.UnitB)
                    + ". Take the empty chair at the table in the ring.", icons != null ? icons.crown : null),
            }, null, () => herald?.FaceHome());
        }

        /// <summary>
        /// Seats the player's next tournament opponent at the table and sends the others back to the edge of the ring;
        /// the Grand Champion's figurines follow the current entry (D-038).
        /// </summary>
        public void RefreshTournament()
        {
            if (Layout == null || Layout.TournamentChair == null) return;
            var current = GameFlow.CurrentTournamentOpponent;
            Npc seated = null;
            foreach (var n in Layout.Npcs)
            {
                if (n.Encounter == null || !n.Encounter.Tournament) continue;
                if (n.Encounter.Id == EncounterCatalog.GrandChampion)
                    n.Encounter = EncounterCatalog.TournamentOpponent(EncounterCatalog.TournamentRounds, Mathf.Max(1, GameFlow.TournamentAttempts));
                bool due = current != null && current.Id == n.Encounter.Id;
                if (due)
                {
                    seated = n;
                    n.transform.position = Layout.TournamentSeat;
                    n.HomeYaw = 180;
                }
                else n.transform.position = n.HomePosition;
                if (n.Seated != due)
                {
                    n.Seated = due;
                    if (n.Rig != null) n.Rig.Sit(due);
                    else if (n.Figure != null) n.Figure.localPosition = n.BaseOffset = due ? new Vector3(0, -0.35f, 0) : Vector3.zero;
                }
                if (!due) n.HomeYaw = n.StandingYaw;
                n.Radius = due ? 0f : 2.4f;
                n.FaceHome();
                n.RefreshTag();
            }
            Layout.TournamentChair.Champion = seated;
        }

        private List<(string, Action)> ErrandChoices(Npc npc, ErrandDefinition errand) => new List<(string, Action)>
        {
            ("I'LL DO IT (" + errand.Reward + ")", () => { if (MarkClick()) AcceptErrand(npc, errand); }),
            ("NOT NOW", () => { if (MarkClick()) CloseDialogue(); }),
        };

        private void OfferErrand(Npc npc, ErrandDefinition errand)
        {
            var pages = new List<(string, string, Sprite)>();
            foreach (var l in errand.Offer) pages.Add((npc.DisplayName, l, Portrait(npc)));
            CloseDialogue(false);
            OpenDialogue(pages, ErrandChoices(npc, errand), () => npc.FaceHome());
        }

        public void AcceptErrand(Npc npc, ErrandDefinition errand)
        {
            GameFlow.SetErrand(errand.Id, ErrandStage.Active);
            SaveNow();
            _audio?.Sfx("sfx/page");
            CloseDialogue(false);
            string where = errand.IsDelivery ? "Take " + errand.ItemName + " to " + errand.Receiver + ". " : "Find " + errand.ItemName + ". ";
            OpenDialogue(new List<(string, string, Sprite)> { ("NEW ERRAND", errand.Title + ". " + where + errand.Reminder + " (Reward: " + errand.Reward + " coins.)", null) },
                null, () => npc.FaceHome());
        }

        public void PickUp(Pickup pickup)
        {
            var errand = pickup.Errand;
            if (errand == null || GameFlow.ErrandState(errand.Id) != ErrandStage.Active) return;
            GameFlow.SetErrand(errand.Id, ErrandStage.Found);
            SaveNow();
            Player.Rig?.Play("PickUp");
            _audio?.Sfx("sfx/pickup");
            OpenDialogue(new List<(string, string, Sprite)> { ("FOUND IT", errand.FoundLine + " Take it back to " + errand.Giver + ".", icons != null ? icons.xp : null) }, null, null);
        }

        public void ReadSign(Sign sign)
        {
            OpenDialogue(new List<(string, string, Sprite)> { (sign.Title, sign.Text, null) }, null, null);
        }

        public void UseDoor(Door door)
        {
            _audio?.Sfx("sfx/door");
            StartCoroutine(FadeThen(() =>
            {
                Player.Teleport(door.Target, door.TargetYaw);
                SnapCamera();
            }));
        }

        public void SitAt(Chair chair)
        {
            _seatedAt = chair;
            var toTable = chair.TableFocus - chair.SeatPosition;
            toTable.y = 0;
            Player.SetSeated(true, chair.SeatPosition, toTable.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toTable).eulerAngles.y : 0);
            var champ = chair.Champion;
            _talkingTo = champ;
            var enc = champ.Encounter;
            var portrait = Portrait(champ);
            var pages = new List<(string, string, Sprite)>();
            if (enc.Tournament)
            {
                foreach (var l in enc.Intro) pages.Add((champ.DisplayName, l, portrait));
                pages.Insert(0, ("GRAND TOURNAMENT", (enc.TournamentRound == EncounterCatalog.TournamentRounds ? "THE FINAL" : "ROUND " + enc.TournamentRound + " OF " + EncounterCatalog.TournamentRounds)
                    + ": " + enc.Name + ", with the " + UnitName(enc.UnitA) + " and the " + UnitName(enc.UnitB) + ", on a " + enc.Tier + " wheel.", icons != null ? icons.crown : null));
            }
            else if (GameFlow.HasDefeated(enc.Id)) pages.Add((champ.DisplayName, enc.RematchLine, portrait));
            else
            {
                foreach (var l in enc.Intro) pages.Add((champ.DisplayName, l, portrait));
                var town = Areas.Town(enc.Area);
                int beaten = 0;
                foreach (var e in EncounterCatalog.All)
                    if (!e.IsChampion && Areas.Town(e.Area) == town && GameFlow.HasDefeated(e.Id)) beaten++;
                if (beaten > 0) pages.Insert(1, (champ.DisplayName, "You've already beaten " + beaten + (beaten == 1 ? " player" : " players") + " on the way here. Let's see if it was luck.", portrait));
            }
            var choices = new List<(string, Action)>
            {
                (PlayLabel(enc), () => { if (MarkClick()) StartChallenge(champ); }),
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

        /// <summary>Sit down to play: straight away, or first choose a charm if the player owns any.</summary>
        public void StartChallenge(Npc npc)
        {
            var charms = GameFlow.OwnedCharms();
            if (charms.Count == 0) { Challenge(npc); return; }
            CloseDialogue(false);
            var rows = new List<ListOverlay.Row>();
            foreach (var c in charms)
            {
                var id = c.Id;
                rows.Add(new ListOverlay.Row { Name = c.Name, Detail = c.Description, Tag = "x" + GameFlow.ItemCount(id), Action = "BRING IT", OnPress = () => Challenge(npc, id) });
            }
            rows.Add(new ListOverlay.Row { Name = "No charm", Detail = "Keep your charms for another table.", Action = "JUST PLAY", OnPress = () => Challenge(npc, null) });
            var enc = npc.Encounter;
            var mode = GameFlow.StakeModeFor(enc);
            string stake = enc.Tournament ? "round " + enc.TournamentRound + " of the Grand Tournament"
                : mode == StakeMode.Coins ? enc.Stake + " coins a side" : mode == StakeMode.Friendly ? "a friendly game" : "a favour";
            Select(_list.Show("BRING A CHARM?", "Against " + enc.Name + ", for " + stake + ". One charm per match; it is used up when the match ends.",
                rows, "Coins: " + GameFlow.Coins, "NOT NOW", () =>
                {
                    _closedFrame = Time.frameCount;
                    if (_seatedAt != null && _talkingTo == _seatedAt.Champion) StandUp();
                }));
        }

        /// <summary>Start a match against this person: remember where we are and move to the table scene.</summary>
        public void Challenge(Npc npc) => Challenge(npc, null);

        public void Challenge(Npc npc, string charm)
        {
            CloseDialogue(false);
            _list.Hide(false);
            var pos = Player.transform.position;
            if (_seatedAt != null) pos = _seatedAt.transform.position;
            GameFlow.TitleShown = true;
            SaveNow();
            GameFlow.BeginEncounter(npc.Encounter, pos.x, pos.z, GameFlow.StakeModeFor(npc.Encounter), charm);
            _audio?.Sfx("sfx/chips");
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

        // ------------------------------------------------------------------ stalls and the journal

        public void OpenShop(string shopId)
        {
            var shop = ShopCatalog.Find(shopId);
            if (shop == null) return;
            var rows = new List<ListOverlay.Row>();
            foreach (var id in shop.Items)
            {
                var item = ItemCatalog.Find(id);
                if (item == null) continue;
                bool ownedWheel = item.Kind == ItemKind.Wheel && GameFlow.ItemCount(id) > 0;
                bool worse = item.Kind == ItemKind.Wheel && !ownedWheel && item.Wheel <= GameFlow.PlayerWheel;
                string owned = item.Kind == ItemKind.Charm ? (GameFlow.ItemCount(id) > 0 ? "   (you have " + GameFlow.ItemCount(id) + ")" : "") : ownedWheel ? "   (yours)" : "";
                var itemId = id;
                rows.Add(new ListOverlay.Row
                {
                    Name = item.Name + owned,
                    Detail = item.Description,
                    Tag = item.Price + " coins",
                    Action = ownedWheel ? "OWNED" : "BUY",
                    Enabled = !ownedWheel && !worse && GameFlow.CanAfford(item.Price),
                    OnPress = () => Buy(shopId, itemId),
                    Icon = icons != null ? (item.Kind == ItemKind.Wheel ? icons.uiRing : icons.xp) : null,
                });
            }
            Select(_list.Show(shop.Name.ToUpperInvariant(), shop.Keeper + ": \"" + shop.Greeting + "\"", rows,
                "Coins: " + GameFlow.Coins + "     Your wheel: " + GameFlow.PlayerWheel, "LEAVE", () => _closedFrame = Time.frameCount));
        }

        private void Buy(string shopId, string itemId)
        {
            var err = GameFlow.Buy(itemId);
            if (err == null)
            {
                _audio?.Sfx("sfx/coins");
                SaveNow();
            }
            var shop = ShopCatalog.Find(shopId);
            int index = 0;
            for (int i = 0; i < shop.Items.Count; i++) if (shop.Items[i] == itemId) index = i;
            OpenShop(shopId);
            var buttons = _list.RowButtons;
            if (buttons.Count > 0) Select(buttons[Mathf.Min(index, buttons.Count - 1)]);
        }

        public void OpenJournal()
        {
            var rows = new List<ListOverlay.Row>();
            int done = 0;
            foreach (var e in ErrandCatalog.All)
            {
                var st = GameFlow.ErrandState(e.Id);
                if (st == ErrandStage.Done) { done++; continue; }
                if (st == ErrandStage.NotStarted) continue;
                string detail = st == ErrandStage.Found ? "Found it. Take it back to " + e.Giver + "."
                    : (e.IsDelivery ? "Take " + e.ItemName + " to " + e.Receiver + ". " : "") + e.Reminder;
                rows.Add(new ListOverlay.Row { Name = e.Title + "  (" + e.Area + ")", Detail = detail, Tag = e.Reward + " coins" });
            }
            if (rows.Count == 0) rows.Add(new ListOverlay.Row { Name = "No errands under way", Detail = "People with work for you mention it when you talk to them." });
            foreach (var c in GameFlow.OwnedCharms())
                if (rows.Count < ListOverlay.MaxRows - 1)
                    rows.Add(new ListOverlay.Row { Name = c.Name, Detail = c.Description, Tag = "x" + GameFlow.ItemCount(c.Id), Icon = icons != null ? icons.xp : null });
            rows.Add(new ListOverlay.Row { Name = "Your fifth wheel: " + GameFlow.PlayerWheel, Detail = "You always bring your best wheel to the table. Opponents bring their own.", Icon = icons != null ? icons.uiRing : null });
            Select(_list.Show("ERRANDS AND SATCHEL", "Errands done: " + done + " of " + ErrandCatalog.All.Count, rows,
                "Coins: " + GameFlow.Coins, "CLOSE", () => _closedFrame = Time.frameCount));
        }

        public void CloseList()
        {
            _list.Hide();
            _closedFrame = Time.frameCount;
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
            GameObject modal = Ui.Title.activeSelf ? Ui.Title : _deck.IsOpen ? _deck.Root : _list.IsOpen ? _list.Root : Ui.Pause.activeSelf ? Ui.Pause : DialogueOpen ? Ui.Dialogue : null;
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
            foreach (var (name, pos) in new[]
                     {
                         ("world_2_bridge", new Vector3(-5.5f, 0, 55f)), ("world_2b_camp", new Vector3(-10.5f, 0, 39.5f)), ("world_3_brindlecross", new Vector3(0, 0, 124f)),
                         ("world_3b_inn", new Vector3(10f, 0, 136f)), ("world_4_hall_outside", new Vector3(0, 0, 152f)), ("world_7_quarry_path", new Vector3(44f, 0, 131f)),
                         ("world_8_outpost", new Vector3(78f, 0, 166f)), ("world_8b_quarry", new Vector3(84f, 0, 196f)), ("world_8c_ledge", new Vector3(66f, 0, 170f)),
                         ("world_9_stream_path", new Vector3(-30f, 0, 73f)), ("world_9b_lanternmere", new Vector3(-84f, 0, 100f)), ("world_9c_pier", new Vector3(-85f, 0, 88f)),
                         ("world_10_duskhollow", new Vector3(-60f, 0, 206f)), ("world_10b_stones", new Vector3(-62f, 0, 219f)),
                         ("world_11_bell_road", new Vector3(40f, 0, 83f)), ("world_11b_ironbell", new Vector3(92f, 0, 74f)), ("world_11c_moor", new Vector3(90f, 0, 50f)),
                         ("world_12_tourney_road", new Vector3(21f, 0, 184f)), ("world_12b_crownhold", new Vector3(0f, 0, 226f)),
                         ("world_13_wander_hills", new Vector3(-30f, 0, 100f)), ("world_13b_wander_woods", new Vector3(-32f, 0, 42f)),
                     })
            {
                Player.Teleport(pos, 0);
                CurrentArea = WorldLayout.AreaAt(Player.transform.position);
                SnapCamera();
                yield return new WaitForSeconds(0.8f);
                if (!Layout.Roam.CanStand(pos.x, pos.z)) Debug.LogWarning("[Tabletop] SelfCheck spot " + name + " is not walkable");
                yield return Capture(folder, name);
            }
            Player.Teleport(new Vector3(0, 0, 118f), 0);
            SetFirstPersonYaw(0);
            SetView(true);
            yield return new WaitForSeconds(0.6f);
            yield return Capture(folder, "world_3c_first_person");
            SetView(false);
            Player.Teleport(new Vector3(0, 0, 124f), 0);
            TalkTo(Layout.Find(EncounterCatalog.Mira));
            yield return new WaitForSeconds(0.3f);
            yield return Capture(folder, "world_5_dialogue");
            CloseDialogue();
            OpenShop(ShopCatalog.AdasStall);
            yield return new WaitForSeconds(0.3f);
            yield return Capture(folder, "world_5c_shop");
            CloseList();
            GameFlow.SetErrand("hollis_loaf", ErrandStage.Active);
            OpenJournal();
            yield return new WaitForSeconds(0.3f);
            yield return Capture(folder, "world_5d_journal");
            CloseList();
            GameFlow.SetErrand("hollis_loaf", ErrandStage.NotStarted);
            OpenDeck();
            yield return new WaitForSeconds(0.3f);
            yield return Capture(folder, "world_5b_deck");
            CloseDeck();
            Player.Teleport(new Vector3(0, 0, 157f), 0);
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
                foreach (var m in r.sharedMaterials)
                    if (m == null || m.shader == null || !m.shader.isSupported || m.shader.name.StartsWith("Hidden/InternalErrorShader")) { unsupported++; break; }
            }
            int models = Layout.Root.Find("Models") != null ? Layout.Root.Find("Models").childCount : 0;
            int rigged = 0;
            foreach (var n in Layout.Npcs) if (n.Rig != null) rigged++;
            Debug.Log("[Tabletop] SelfCheck world renderers=" + total + " unsupportedShader=" + unsupported + " packModels=" + models
                      + " animatedPeople=" + rigged + "/" + Layout.Npcs.Count + " player=" + (Player.Rig != null ? "animated" : "placeholder")
                      + " terrain=" + (Layout.Terrain != null) + " sounds=" + (sounds != null ? sounds.entries.Count : 0)
                      + " fps=" + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("F0"));
        }
    }
}
