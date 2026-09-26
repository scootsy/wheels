using System;
using System.Collections.Generic;
using Tabletop.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Tabletop.World
{
    /// <summary>Screen UI for exploring: prompt, dialogue, area title, HUD, title screen, pause menu, fade.</summary>
    public sealed class WorldUi
    {
        public readonly GameObject Title;
        public readonly GameObject Pause;
        public readonly GameObject Dialogue;
        public readonly Button TitleContinue, TitleBegin, TitlePractice, TitleQuit;
        public readonly Button PauseResume, PauseDeck, PauseView, PauseHelp, PausePractice, PauseQuit;
        public readonly Transform Frame;
        public readonly Button Continue;
        public readonly List<Button> Choices = new List<Button>();
        private readonly Text _prompt;
        private readonly GameObject _promptBox;
        private readonly Text _area;
        private readonly CanvasGroup _areaGroup;
        private readonly Text _hud;
        private readonly Text _controls;
        private readonly Text _speaker;
        private readonly Text _line;
        private readonly Image _portrait;
        private readonly Image _fade;
        private readonly Text _pauseText;
        private float _areaTimer;

        public float FadeAlpha
        {
            get => _fade.color.a;
            set
            {
                _fade.color = new Color(0, 0, 0, Mathf.Clamp01(value));
                _fade.raycastTarget = value > 0.01f;
            }
        }

        public string PromptText => _promptBox.activeSelf ? _prompt.text : "";
        public string DialogueSpeaker => _speaker.text;
        public string DialogueLine => _line.text;
        public string AreaText => _area.text;

        public WorldUi(Transform parent)
        {
            var canvasGo = new GameObject("WorldCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<FrameFitter>();
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = Ui.Rect("Frame", canvasGo.transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(1920, 1080);
            Frame = root;

            // HUD
            var hudBg = Ui.Card("HudBg", root, new Color(0.09f, 0.05f, 0.035f, 0.86f), true);
            hudBg.rectTransform.Place(20, 20, 520, 86);
            hudBg.raycastTarget = false;
            _hud = Ui.Label("Hud", hudBg.transform, "", 24, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
            _hud.rectTransform.Fill(14);
            var ctlBg = Ui.Card("ControlsBg", root, new Color(0.09f, 0.05f, 0.035f, 0.86f), true);
            ctlBg.rectTransform.Place(1300, 20, 600, 112);
            ctlBg.raycastTarget = false;
            _controls = Ui.Label("Controls", ctlBg.transform, "", 21, TextAnchor.MiddleLeft, Theme.TextDim);
            _controls.rectTransform.Fill(12);

            // Area title
            var area = Ui.Rect("AreaTitle", root);
            area.Place(0, 150, 1920, 90);
            _areaGroup = area.gameObject.AddComponent<CanvasGroup>();
            _areaGroup.blocksRaycasts = false;
            _area = Ui.Label("Text", area, "", 60, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            _area.rectTransform.Fill();
            var areaShadow = _area.gameObject.AddComponent<Shadow>();
            areaShadow.effectColor = new Color(0.05f, 0.02f, 0.01f, 0.85f);
            areaShadow.effectDistance = new Vector2(0, -4);
            _areaGroup.alpha = 0;

            // Prompt
            var promptBg = Ui.Card("Prompt", root, new Color(0.09f, 0.05f, 0.035f, 0.92f), true);
            promptBg.rectTransform.Place(610, 900, 700, 64);
            promptBg.raycastTarget = false;
            _promptBox = promptBg.gameObject;
            _prompt = Ui.Label("Text", promptBg.transform, "", 28, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            _prompt.rectTransform.Fill(8);
            _promptBox.SetActive(false);

            // Dialogue
            var dlg = Ui.Card("Dialogue", root, new Color(0.09f, 0.05f, 0.035f, 0.96f), true);
            dlg.raycastTarget = true;
            dlg.rectTransform.Place(260, 740, 1400, 300);
            Dialogue = dlg.gameObject;
            var portraitBg = Ui.Card("PortraitBg", dlg.transform, new Color(0.18f, 0.11f, 0.07f, 1f), true);
            portraitBg.rectTransform.Place(24, 30, 150, 150);
            _portrait = Ui.Icon("Portrait", portraitBg.transform, null, 130, Theme.TextDim);
            _portrait.rectTransform.Place(10, 10, 130, 130);
            _speaker = Ui.Label("Speaker", dlg.transform, "", 34, TextAnchor.UpperLeft, Theme.Gilt, FontStyle.Bold);
            _speaker.rectTransform.Place(200, 22, 800, 44);
            _line = Ui.Label("Line", dlg.transform, "", 30, TextAnchor.UpperLeft, Theme.Text);
            _line.rectTransform.Place(200, 76, 860, 200);
            Continue = Ui.Button("Continue", dlg.transform, "CONTINUE", null, 24, Theme.ButtonPrimary);
            ((RectTransform)Continue.transform).Place(1100, 206, 270, 64);
            for (int i = 0; i < 3; i++)
            {
                var b = Ui.Button("Choice" + i, dlg.transform, "", null, 24, i == 0 ? Theme.ButtonPrimary : Theme.Button);
                ((RectTransform)b.transform).Place(1080, 30 + i * 84, 300, 70);
                Choices.Add(b);
            }
            Dialogue.SetActive(false);

            // Pause
            Pause = Ui.Panel("Pause", root, Theme.Overlay).gameObject;
            ((RectTransform)Pause.transform).Fill();
            var pbox = Ui.Panel("Box", Pause.transform, Theme.Panel);
            pbox.rectTransform.Place(560, 90, 800, 900);
            Ui.Label("Title", pbox.transform, "PAUSED", 48, TextAnchor.UpperCenter, Theme.Text, FontStyle.Bold).rectTransform.Place(0, 20, 800, 60);
            PauseResume = Ui.Button("Resume", pbox.transform, "RESUME", null, 26, Theme.ButtonPrimary);
            PauseDeck = Ui.Button("Deck", pbox.transform, "YOUR DECK", null, 26);
            PauseView = Ui.Button("View", pbox.transform, "VIEW: OVERHEAD", null, 26);
            PauseHelp = Ui.Button("Help", pbox.transform, "HOW TO PLAY", null, 26);
            PausePractice = Ui.Button("Practice", pbox.transform, "PRACTICE TABLE", null, 26);
            PauseQuit = Ui.Button("Quit", pbox.transform, "SAVE AND QUIT", null, 26);
            var pauseButtons = new[] { PauseResume, PauseDeck, PauseView, PauseHelp, PausePractice, PauseQuit };
            for (int i = 0; i < pauseButtons.Length; i++) ((RectTransform)pauseButtons[i].transform).Place(200, 96 + i * 76, 400, 64);
            _pauseText = Ui.Label("HelpText", pbox.transform, "", 21, TextAnchor.UpperLeft, Theme.Text);
            _pauseText.rectTransform.Place(40, 560, 720, 320);
            Link(pauseButtons);
            Pause.SetActive(false);

            // Title
            Title = Ui.Panel("Title", root, new Color(0.05f, 0.04f, 0.04f, 0.78f)).gameObject;
            ((RectTransform)Title.transform).Fill();
            Ui.Label("Name", Title.transform, "TABLETOP REELS", 110, TextAnchor.UpperCenter, Theme.Crown, FontStyle.Bold).rectTransform.Place(0, 150, 1920, 140);
            Ui.Label("Tagline", Title.transform, "A journey north, one table at a time.", 34, TextAnchor.UpperCenter, Theme.Text).rectTransform.Place(0, 300, 1920, 50);
            TitleContinue = Ui.Button("Continue", Title.transform, "CONTINUE JOURNEY", null, 32, Theme.ButtonPrimary);
            ((RectTransform)TitleContinue.transform).Place(710, 410, 500, 84);
            TitleBegin = Ui.Button("Begin", Title.transform, "BEGIN YOUR JOURNEY", null, 30, Theme.ButtonPrimary);
            ((RectTransform)TitleBegin.transform).Place(710, 510, 500, 76);
            TitlePractice = Ui.Button("Practice", Title.transform, "PRACTICE TABLE", null, 26);
            ((RectTransform)TitlePractice.transform).Place(760, 602, 400, 66);
            TitleQuit = Ui.Button("Quit", Title.transform, "QUIT", null, 26);
            ((RectTransform)TitleQuit.transform).Place(760, 684, 400, 66);
            Ui.Label("How", Title.transform,
                "Walk: WASD / arrows or left stick.  Sprint: Shift or LT.  Jump: Space or (A).  Talk: E or (A).  Switch view: V or (Y).  Deck: I or (X).  Menu: Esc or Start.\n"
                + "Villagers with a gem above their heads will play Reels with you. The Champion waits in Brindlecross, far to the north. Your journey saves itself.",
                23, TextAnchor.UpperCenter, Theme.TextDim).rectTransform.Place(160, 790, 1600, 120);
            Link(TitleContinue, TitleBegin, TitlePractice, TitleQuit);
            Title.SetActive(false); // shown by WorldApp only on the first visit

            // Fade (top-most)
            _fade = Ui.Panel("Fade", root, new Color(0, 0, 0, 0));
            _fade.rectTransform.Fill();
            _fade.raycastTarget = false;
        }

        /// <summary>Re-links the title buttons when CONTINUE is shown or hidden (only active buttons are reachable).</summary>
        public void LinkTitle(bool hasSave)
        {
            TitleContinue.gameObject.SetActive(hasSave);
            if (hasSave) Link(TitleContinue, TitleBegin, TitlePractice, TitleQuit);
            else Link(TitleBegin, TitlePractice, TitleQuit);
        }

        /// <summary>Adds a full-screen panel above the menus but below the fade.</summary>
        public void AddOverlay(GameObject overlay)
        {
            overlay.transform.SetParent(Frame, false);
            overlay.transform.SetSiblingIndex(_fade.transform.GetSiblingIndex());
        }

        private static void Link(params Selectable[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var n = new Navigation { mode = Navigation.Mode.Explicit };
                n.selectOnUp = i > 0 ? items[i - 1] : null;
                n.selectOnDown = i < items.Length - 1 ? items[i + 1] : null;
                items[i].navigation = n;
            }
        }

        public void SetPrompt(string text)
        {
            _promptBox.SetActive(!string.IsNullOrEmpty(text));
            _prompt.text = text ?? "";
        }

        public void ShowArea(string name)
        {
            _area.text = name.ToUpperInvariant();
            _areaTimer = 3f;
        }

        public void HideArea() => _areaTimer = 0;
        public void SetHud(string text) => _hud.text = text;
        public void SetControls(string text) => _controls.text = text;
        public void SetPauseText(string text) => _pauseText.text = text;

        public void Tick(float dt)
        {
            if (_areaTimer > 0)
            {
                _areaTimer -= dt;
                _areaGroup.alpha = Mathf.Clamp01(Mathf.Min(_areaTimer, 3f - _areaTimer) * 2f);
            }
            else _areaGroup.alpha = 0;
        }

        /// <summary>Shows one dialogue page. Choices (label, action) appear instead of CONTINUE when given.</summary>
        public void ShowPage(string speaker, string line, Sprite portrait, IList<(string label, Action act)> choices)
        {
            Dialogue.SetActive(true);
            _speaker.text = speaker;
            _line.text = line;
            _portrait.sprite = portrait;
            _portrait.color = portrait != null ? Color.white : new Color(0, 0, 0, 0);
            bool hasChoices = choices != null && choices.Count > 0;
            Continue.gameObject.SetActive(!hasChoices);
            for (int i = 0; i < Choices.Count; i++)
            {
                bool on = hasChoices && i < choices.Count;
                Choices[i].gameObject.SetActive(on);
                Choices[i].onClick.RemoveAllListeners();
                if (on)
                {
                    Choices[i].SetText(choices[i].label);
                    var act = choices[i].act;
                    Choices[i].onClick.AddListener(() => act());
                }
            }
            if (hasChoices)
            {
                var active = new List<Selectable>();
                for (int i = 0; i < choices.Count && i < Choices.Count; i++) active.Add(Choices[i]);
                Link(active.ToArray());
            }
        }
    }
}
