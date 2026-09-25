using System.Collections.Generic;
using System.Text;
using Tabletop.Application;
using Tabletop.Domain;
using Tabletop.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tabletop.World
{
    /// <summary>
    /// "Your deck" (D-027): every piece in the game (catalog order), owned or locked, with full stats for the focused one.
    /// Locked pieces say who holds them, so the next goal is always visible.
    /// </summary>
    public sealed class DeckView
    {
        public readonly GameObject Root;
        public readonly Button Close;
        public readonly List<Button> Cards = new List<Button>();
        private readonly List<string> _unitIds = new List<string>();
        private readonly List<Text> _cardStatus = new List<Text>();
        private readonly List<Image> _portraits = new List<Image>();
        private readonly Text _detailTitle;
        private readonly Text _detail;
        private readonly Text _summary;
        private readonly ContentCatalog _catalog;
        private int _shown = -1;

        public string DetailText => _detailTitle.text + "\n" + _detail.text;
        public IReadOnlyList<string> UnitOrder => _unitIds;

        public DeckView(Transform parent, ContentCatalog catalog, IconSet icons)
        {
            _catalog = catalog;
            Root = Ui.Panel("Deck", parent, new Color(0.07f, 0.05f, 0.05f, 0.94f)).gameObject;
            ((RectTransform)Root.transform).Fill();
            Ui.Label("Title", Root.transform, "YOUR DECK", 56, TextAnchor.UpperCenter, Theme.Crown, FontStyle.Bold).rectTransform.Place(0, 40, 1920, 70);
            _summary = Ui.Label("Summary", Root.transform, "", 26, TextAnchor.UpperCenter, Theme.TextDim);
            _summary.rectTransform.Place(0, 112, 1920, 40);

            var units = catalog.Units;
            float gap = 18;
            float w = Mathf.Min(270f, (1840f - (units.Count - 1) * gap) / Mathf.Max(1, units.Count)); // fit every piece on screen
            float x0 = (1920 - (units.Count * w + (units.Count - 1) * gap)) / 2f;
            for (int i = 0; i < units.Count; i++)
            {
                var def = units[i];
                var card = Ui.Button("Card_" + def.Id, Root.transform, "", null, 20, Theme.Panel);
                card.GetComponentInChildren<Text>().gameObject.SetActive(false);
                ((RectTransform)card.transform).Place(x0 + i * (w + gap), 175, w, 300);
                var portrait = Ui.Icon("Portrait", card.transform, icons != null ? icons.Unit(def.Id) : null, 130, Theme.TextDim);
                portrait.rectTransform.Place((w - 130) / 2f, 18, 130, 130);
                Ui.Label("Name", card.transform, def.DisplayName.ToUpperInvariant(), 30, TextAnchor.UpperCenter, Theme.Text, FontStyle.Bold).rectTransform.Place(0, 160, w, 40);
                Ui.Label("Role", card.transform, def.Role, 20, TextAnchor.UpperCenter, Theme.TextDim).rectTransform.Place(0, 202, w, 30);
                var status = Ui.Label("Status", card.transform, "", 22, TextAnchor.UpperCenter, Theme.Crown, FontStyle.Bold);
                status.rectTransform.Place(0, 248, w, 34);
                Cards.Add(card);
                _unitIds.Add(def.Id);
                _cardStatus.Add(status);
                _portraits.Add(portrait);
            }
            for (int i = 0; i < Cards.Count; i++)
            {
                var n = new Navigation { mode = Navigation.Mode.Explicit };
                n.selectOnLeft = i > 0 ? Cards[i - 1] : null;
                n.selectOnRight = i < Cards.Count - 1 ? Cards[i + 1] : null;
                Cards[i].navigation = n;
            }

            var detailBg = Ui.Panel("DetailBg", Root.transform, new Color(0.14f, 0.11f, 0.10f, 1f));
            detailBg.rectTransform.Place(200, 500, 1520, 440);
            _detailTitle = Ui.Label("DetailTitle", detailBg.transform, "", 36, TextAnchor.UpperLeft, Theme.Crown, FontStyle.Bold);
            _detailTitle.rectTransform.Place(32, 22, 1456, 48);
            _detail = Ui.Label("Detail", detailBg.transform, "", 25, TextAnchor.UpperLeft, Theme.Text);
            _detail.rectTransform.Place(32, 80, 1456, 350);

            Close = Ui.Button("Close", Root.transform, "CLOSE", null, 26, Theme.ButtonPrimary);
            ((RectTransform)Close.transform).Place(810, 970, 300, 70);
            foreach (var c in Cards)
            {
                var n = c.navigation;
                n.selectOnDown = Close;
                c.navigation = n;
            }
            var cn = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = Cards.Count > 0 ? Cards[0] : null };
            Close.navigation = cn;
            Root.SetActive(false);
        }

        public bool IsOpen => Root.activeSelf;

        /// <summary>Refreshes ownership from <see cref="GameFlow"/> and shows the deck, focused on the first owned piece.</summary>
        public Button Open()
        {
            Root.SetActive(true);
            int owned = 0;
            for (int i = 0; i < _unitIds.Count; i++)
            {
                bool has = GameFlow.IsUnlocked(_unitIds[i]);
                if (has) owned++;
                _cardStatus[i].text = has ? "OWNED" : "LOCKED";
                _cardStatus[i].color = has ? Theme.Heal : Theme.TextDim;
                _portraits[i].color = has ? Color.white : new Color(0.12f, 0.12f, 0.14f, 1f);
            }
            _summary.text = "You own " + owned + " of " + _unitIds.Count + " pieces. Each town's Champion holds a new one: beat them to win it.";
            _shown = -1;
            Show(0);
            return Cards.Count > 0 ? Cards[0] : Close;
        }

        public void Hide() => Root.SetActive(false);

        /// <summary>Shows details for whichever card has focus (call every frame while open).</summary>
        public void Tick(EventSystem es)
        {
            if (!IsOpen || es == null) return;
            var sel = es.currentSelectedGameObject;
            for (int i = 0; i < Cards.Count; i++)
                if (sel == Cards[i].gameObject) { Show(i); return; }
        }

        private void Show(int index)
        {
            if (index == _shown || index < 0 || index >= _unitIds.Count) return;
            _shown = index;
            _catalog.TryGetUnit(_unitIds[index], out var def);
            bool has = GameFlow.IsUnlocked(def.Id);
            _detailTitle.text = def.DisplayName.ToUpperInvariant() + "  -  " + def.Role + (has ? "" : "   (LOCKED)");
            _detail.text = Describe(def, has);
        }

        public static string Describe(UnitDefinition def, bool owned)
        {
            var sb = new StringBuilder();
            sb.Append(def.Description).Append("\n\n");
            string[] names = { "BRONZE  [I]", "SILVER  [II]", "GOLD  [III]" };
            for (int r = 0; r < def.Ranks.Count && r < names.Length; r++)
            {
                var s = def.Ranks[r];
                sb.Append(names[r]).Append("    cost ").Append(s.EnergyCost).Append(" energy    ")
                  .Append(UnitPanelView.StatsLine(def, s).Replace("\n", "    ")).Append('\n');
            }
            sb.Append("\nEvery piece starts a match at Bronze and ranks up at ").Append(RulesConstants.XpThreshold).Append(" XP.");
            if (!owned)
            {
                var source = EncounterCatalog.PrizeSource(def.Id);
                sb.Append("\n\n").Append(source != null
                    ? "Win it by beating " + source.Name + ", " + source.Title + "."
                    : "Held by a Champion in a town further north.");
            }
            return sb.ToString();
        }
    }
}
