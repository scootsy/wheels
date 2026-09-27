using System;
using System.Collections.Generic;
using Tabletop.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Tabletop.World
{
    /// <summary>
    /// One full-screen list panel (D-033), reused for a stall's wares, choosing a charm before a match, and the
    /// errand journal: a title, a line of context, rows with an optional button, and a close button.
    /// </summary>
    public sealed class ListOverlay
    {
        public sealed class Row
        {
            public string Name;
            public string Detail;
            /// <summary>Small text at the right (a price, a count).</summary>
            public string Tag;
            /// <summary>Button text, or null for a row without a button.</summary>
            public string Action;
            public bool Enabled = true;
            public Action OnPress;
            public Sprite Icon;
        }

        public const int MaxRows = 9;

        public readonly GameObject Root;
        public readonly Button Close;
        private readonly Text _title;
        private readonly Text _subtitle;
        private readonly Text _footer;
        private readonly List<(GameObject go, Text name, Text detail, Text tag, Button button, Image icon)> _rows =
            new List<(GameObject, Text, Text, Text, Button, Image)>();
        private Action _onClose;

        public bool IsOpen => Root.activeSelf;
        public IReadOnlyList<Button> RowButtons
        {
            get
            {
                var list = new List<Button>();
                foreach (var r in _rows) if (r.go.activeSelf && r.button.gameObject.activeSelf) list.Add(r.button);
                return list;
            }
        }
        public string Title => _title.text;
        public string Subtitle => _subtitle.text;
        public string Footer => _footer.text;

        public ListOverlay(Transform frame)
        {
            var root = Ui.Panel("ListOverlay", frame, Theme.Overlay);
            root.rectTransform.Fill();
            Root = root.gameObject;
            var box = Ui.Card("Box", root.transform, Theme.Panel, true);
            box.rectTransform.Place(300, 60, 1320, 960);
            _title = Ui.Label("Title", box.transform, "", 46, TextAnchor.UpperCenter, Theme.Gilt, FontStyle.Bold);
            _title.rectTransform.Place(0, 22, 1320, 60);
            _subtitle = Ui.Label("Subtitle", box.transform, "", 24, TextAnchor.UpperCenter, Theme.TextDim);
            _subtitle.rectTransform.Place(60, 88, 1200, 70);
            for (int i = 0; i < MaxRows; i++)
            {
                var row = Ui.Card("Row" + i, box.transform, new Color(0.2f, 0.12f, 0.08f, 0.9f), false);
                row.rectTransform.Place(40, 166 + i * 76, 1240, 68);
                row.raycastTarget = false;
                var icon = Ui.Icon("Icon", row.transform, null, 48, new Color(0, 0, 0, 0));
                icon.rectTransform.Place(12, 10, 48, 48);
                var name = Ui.Label("Name", row.transform, "", 26, TextAnchor.UpperLeft, Theme.Text, FontStyle.Bold);
                name.rectTransform.Place(72, 6, 560, 32);
                var detail = Ui.Label("Detail", row.transform, "", 19, TextAnchor.UpperLeft, Theme.TextDim);
                detail.rectTransform.Place(72, 36, 760, 28);
                var tag = Ui.Label("Tag", row.transform, "", 24, TextAnchor.MiddleRight, Theme.Crown, FontStyle.Bold);
                tag.rectTransform.Place(840, 0, 150, 68);
                var button = Ui.Button("Action", row.transform, "", null, 22, Theme.Button);
                ((RectTransform)button.transform).Place(1010, 8, 216, 52);
                _rows.Add((row.gameObject, name, detail, tag, button, icon));
            }
            _footer = Ui.Label("Footer", box.transform, "", 24, TextAnchor.MiddleLeft, Theme.Crown, FontStyle.Bold);
            _footer.rectTransform.Place(60, 866, 800, 64);
            Close = Ui.Button("Close", box.transform, "CLOSE", null, 24);
            ((RectTransform)Close.transform).Place(1000, 866, 260, 64);
            Close.onClick.AddListener(() => Hide(true));
            Root.SetActive(false);
        }

        /// <summary>Shows (or refreshes) the list. Returns the control that should take focus.</summary>
        public Selectable Show(string title, string subtitle, IList<Row> rows, string footer, string closeLabel = "CLOSE", Action onClose = null)
        {
            Root.SetActive(true);
            _title.text = title;
            _subtitle.text = subtitle ?? "";
            _footer.text = footer ?? "";
            Close.SetText(closeLabel);
            _onClose = onClose;
            var nav = new List<Selectable>();
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                bool on = rows != null && i < rows.Count;
                r.go.SetActive(on);
                if (!on) continue;
                var data = rows[i];
                r.name.text = data.Name;
                r.detail.text = data.Detail ?? "";
                r.tag.text = data.Tag ?? "";
                r.icon.sprite = data.Icon;
                r.icon.color = data.Icon != null ? Color.white : new Color(0, 0, 0, 0);
                bool hasButton = !string.IsNullOrEmpty(data.Action);
                r.button.gameObject.SetActive(hasButton);
                r.button.onClick.RemoveAllListeners();
                if (hasButton)
                {
                    r.button.SetText(data.Action);
                    r.button.interactable = data.Enabled;
                    var act = data.OnPress;
                    r.button.onClick.AddListener(() => act?.Invoke());
                    r.button.onClick.AddListener(() => Ui.ClickSound?.Invoke());
                    if (data.Enabled) nav.Add(r.button);
                }
            }
            nav.Add(Close);
            for (int i = 0; i < nav.Count; i++)
            {
                var n = new Navigation { mode = Navigation.Mode.Explicit };
                n.selectOnUp = i > 0 ? nav[i - 1] : null;
                n.selectOnDown = i < nav.Count - 1 ? nav[i + 1] : null;
                nav[i].navigation = n;
            }
            return nav[0];
        }

        public void Hide() => Hide(true);

        /// <summary>Closes the list; <paramref name="runCallback"/> false when the close is part of moving on (e.g. to the table).</summary>
        public void Hide(bool runCallback)
        {
            if (!Root.activeSelf) return;
            Root.SetActive(false);
            var cb = _onClose;
            _onClose = null;
            if (runCallback) cb?.Invoke();
        }
    }
}
