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
        public readonly Button TitleBegin, TitlePractice, TitleQuit;
        public readonly Button PauseResume, PauseHelp, PausePractice, PauseQuit;
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
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = Ui.Rect("Frame", canvasGo.transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(1920, 1080);

            // HUD
            var hudBg = Ui.Panel("HudBg", root, new Color(0, 0, 0, 0.55f));
            hudBg.rectTransform.Place(20, 20, 520, 86);
            hudBg.raycastTarget = false;
            _hud = Ui.Label("Hud", hudBg.transform, "", 24, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
            _hud.rectTransform.Fill(14);
            var ctlBg = Ui.Panel("ControlsBg", root, new Color(0, 0, 0, 0.6f));
            ctlBg.rectTransform.Place(1380, 20, 520, 86);
            ctlBg.raycastTarget = false;
            _controls = Ui.Label("Controls", ctlBg.transform, "", 21, TextAnchor.MiddleLeft, Theme.Text);
            _controls.rectTransform.Fill(12);

            // Area title
            var area = Ui.Rect("AreaTitle", root);
            area.Place(0, 150, 1920, 90);
            _areaGroup = area.gameObject.AddComponent<CanvasGroup>();
            _areaGroup.blocksRaycasts = false;
            _area = Ui.Label("Text", area, "", 56, TextAnchor.MiddleCenter, Theme.Crown, FontStyle.Bold);
            _area.rectTransform.Fill();
            _area.gameObject.AddComponent<Outline>().effectColor = Color.black;
            _areaGroup.alpha = 0;

            // Prompt
            var promptBg = Ui.Panel("Prompt", root, new Color(0, 0, 0, 0.7f));
            promptBg.rectTransform.Place(610, 900, 700, 64);
            promptBg.raycastTarget = false;
            _promptBox = promptBg.gameObject;
            _prompt = Ui.Label("Text", promptBg.transform, "", 28, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            _prompt.rectTransform.Fill(8);
            _promptBox.SetActive(false);

            // Dialogue
            var dlg = Ui.Panel("Dialogue", root, new Color(0.1f, 0.08f, 0.08f, 0.95f));
            dlg.rectTransform.Place(260, 740, 1400, 300);
            Dialogue = dlg.gameObject;
            var border = Ui.Panel("Border", dlg.transform, Theme.Crown);
            border.rectTransform.Place(0, 0, 1400, 6);
            var portraitBg = Ui.Panel("PortraitBg", dlg.transform, new Color(0.2f, 0.17f, 0.16f, 1f));
            portraitBg.rectTransform.Place(24, 30, 150, 150);
            _portrait = Ui.Icon("Portrait", portraitBg.transform, null, 130, Theme.TextDim);
            _portrait.rectTransform.Place(10, 10, 130, 130);
            _speaker = Ui.Label("Speaker", dlg.transform, "", 34, TextAnchor.UpperLeft, Theme.Crown, FontStyle.Bold);
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
            pbox.rectTransform.Place(560, 170, 800, 740);
            Ui.Label("Title", pbox.transform, "PAUSED", 48, TextAnchor.UpperCenter, Theme.Text, FontStyle.Bold).rectTransform.Place(0, 20, 800, 60);
            PauseResume = Ui.Button("Resume", pbox.transform, "RESUME", null, 26, Theme.ButtonPrimary);
            ((RectTransform)PauseResume.transform).Place(200, 100, 400, 66);
            PauseHelp = Ui.Button("Help", pbox.transform, "HOW TO PLAY", null, 26);
            ((RectTransform)PauseHelp.transform).Place(200, 180, 400, 66);
            PausePractice = Ui.Button("Practice", pbox.transform, "PRACTICE TABLE", null, 26);
            ((RectTransform)PausePractice.transform).Place(200, 260, 400, 66);
            PauseQuit = Ui.Button("Quit", pbox.transform, "QUIT GAME", null, 26);
            ((RectTransform)PauseQuit.transform).Place(200, 340, 400, 66);
            _pauseText = Ui.Label("HelpText", pbox.transform, "", 21, TextAnchor.UpperLeft, Theme.Text);
            _pauseText.rectTransform.Place(40, 430, 720, 290);
            Link(PauseResume, PauseHelp, PausePractice, PauseQuit);
            Pause.SetActive(false);

            // Title
            Title = Ui.Panel("Title", root, new Color(0.05f, 0.04f, 0.04f, 0.78f)).gameObject;
            ((RectTransform)Title.transform).Fill();
            Ui.Label("Name", Title.transform, "TABLETOP REELS", 110, TextAnchor.UpperCenter, Theme.Crown, FontStyle.Bold).rectTransform.Place(0, 150, 1920, 140);
            Ui.Label("Tagline", Title.transform, "A journey north, one table at a time.", 34, TextAnchor.UpperCenter, Theme.Text).rectTransform.Place(0, 300, 1920, 50);
            TitleBegin = Ui.Button("Begin", Title.transform, "BEGIN YOUR JOURNEY", null, 32, Theme.ButtonPrimary);
            ((RectTransform)TitleBegin.transform).Place(710, 440, 500, 84);
            TitlePractice = Ui.Button("Practice", Title.transform, "PRACTICE TABLE", null, 26);
            ((RectTransform)TitlePractice.transform).Place(760, 544, 400, 66);
            TitleQuit = Ui.Button("Quit", Title.transform, "QUIT", null, 26);
            ((RectTransform)TitleQuit.transform).Place(760, 626, 400, 66);
            Ui.Label("How", Title.transform,
                "Walk with WASD / arrows or the left stick. Talk and interact with E or (A). Menu with Esc or Start.\n"
                + "Villagers with a gem above their heads will play Reels with you. The Champion waits in Brindlecross, far to the north.",
                24, TextAnchor.UpperCenter, Theme.TextDim).rectTransform.Place(260, 760, 1400, 120);
            Link(TitleBegin, TitlePractice, TitleQuit);
            Title.SetActive(false); // shown by WorldApp only on the first visit

            // Fade (top-most)
            _fade = Ui.Panel("Fade", root, new Color(0, 0, 0, 0));
            _fade.rectTransform.Fill();
            _fade.raycastTarget = false;
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
