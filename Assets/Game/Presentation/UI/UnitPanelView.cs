using Tabletop.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>Unit podium plaque (MATCH_UX_SPEC 5.2): every required statistic without opening inspection.</summary>
    public sealed class UnitPanelView
    {
        public readonly Button Button;
        public readonly int Side;
        public readonly int Slot;
        private readonly IconSet _icons;
        private readonly Image _rankBand;
        private readonly Image _portrait;
        private readonly Text _title;
        private readonly Text _rank;
        private readonly Text _xp;
        private readonly Text _energy;
        private readonly Text _stats;
        private readonly Text _ready;
        private readonly Image _readyBg;
        private readonly Image[] _energyPips = new Image[5];
        private readonly Image[] _xpPips = new Image[6];
        private readonly Image _highlight;
        private string _portraitFor;

        public UnitPanelView(Transform parent, int side, int slot, float x, float y, float w, float h, IconSet icons)
        {
            Side = side;
            Slot = slot;
            _icons = icons;
            Button = Ui.Button((side == 0 ? "Player" : "Enemy") + "Unit" + (slot == 0 ? "A" : "B"), parent, "", null, 18, Theme.Panel);
            Button.GetComponentInChildren<Text>().gameObject.SetActive(false);
            ((RectTransform)Button.transform).Place(x, y, w, h);

            _highlight = Ui.Panel("ActingHighlight", Button.transform, new Color(1f, 0.95f, 0.5f, 0.25f));
            _highlight.rectTransform.Fill();
            _highlight.raycastTarget = false;
            _highlight.gameObject.SetActive(false);

            _rankBand = Ui.Panel("RankBand", Button.transform, Theme.Rank(Rank.Bronze));
            _rankBand.rectTransform.Place(0, 0, 10, h);
            _rankBand.raycastTarget = false;

            _title = Ui.Label("Title", Button.transform, "", 22, TextAnchor.UpperLeft, Theme.Text, FontStyle.Bold);
            _title.rectTransform.Place(20, 8, w - 70, 30);
            var channel = Ui.Icon("Channel", Button.transform, icons != null ? (slot == 0 ? icons.energyA : icons.energyB) : null, 34,
                slot == 0 ? Theme.ChannelA : Theme.ChannelB);
            channel.rectTransform.Place(w - 46, 6, 34, 34);

            var portraitBg = Ui.Panel("PortraitBg", Button.transform, new Color(0.12f, 0.11f, 0.12f, 1f));
            portraitBg.rectTransform.Place(20, 44, 92, 92);
            portraitBg.raycastTarget = false;
            _portrait = Ui.Icon("Portrait", portraitBg.transform, null, 84, Theme.TextDim);
            _portrait.rectTransform.Place(4, 4, 84, 84);

            _rank = Ui.Label("Rank", Button.transform, "", 18, TextAnchor.UpperLeft, Theme.Text, FontStyle.Bold);
            _rank.rectTransform.Place(124, 44, 150, 24);
            _readyBg = Ui.Panel("ReadyBg", Button.transform, new Color(0.15f, 0.55f, 0.2f, 1f));
            _readyBg.rectTransform.Place(w - 118, 44, 108, 26);
            _readyBg.raycastTarget = false;
            _ready = Ui.Label("Ready", _readyBg.transform, "READY!", 18, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            _ready.rectTransform.Fill();

            _energy = Ui.Label("Energy", Button.transform, "", 17, TextAnchor.UpperLeft);
            _energy.rectTransform.Place(124, 74, 110, 22);
            for (int i = 0; i < 5; i++)
            {
                _energyPips[i] = Ui.Panel("EnergyPip" + i, Button.transform, slot == 0 ? Theme.ChannelA : Theme.ChannelB);
                _energyPips[i].rectTransform.Place(236 + i * 34, 74, 30, 20);
            }
            _xp = Ui.Label("Xp", Button.transform, "", 17, TextAnchor.UpperLeft);
            _xp.rectTransform.Place(124, 104, 110, 22);
            for (int i = 0; i < 6; i++)
            {
                _xpPips[i] = Ui.Panel("XpPip" + i, Button.transform, Theme.Xp);
                _xpPips[i].rectTransform.Place(236 + i * 28, 106, 22, 16);
            }

            _stats = Ui.Label("Stats", Button.transform, "", 17, TextAnchor.UpperLeft, Theme.TextDim);
            _stats.rectTransform.Place(20, 146, w - 30, h - 150);
        }

        public string Description { get; private set; } = "";

        public void Render(UnitDefinition def, Rank rank, int xp, int energy, bool sideIsPlayer, bool acting)
        {
            var stats = def.Stats(rank);
            if (_portraitFor != def.Id)
            {
                _portraitFor = def.Id;
                var sprite = _icons != null ? _icons.Unit(def.Id) : null;
                _portrait.sprite = sprite;
                _portrait.color = sprite != null ? Color.white : Theme.TextDim;
            }
            _title.text = (sideIsPlayer ? "YOUR " : "ENEMY ") + def.DisplayName.ToUpperInvariant() + (Slot == 0 ? "  (A)" : "  (B)");
            _title.color = sideIsPlayer ? Theme.Player : Theme.Enemy;
            _rankBand.color = Theme.Rank(rank);
            string glyph = rank == Rank.Bronze ? "[I]" : rank == Rank.Silver ? "[II]" : "[III]";
            _rank.text = glyph + " " + rank.ToString().ToUpperInvariant();
            _rank.color = Theme.Rank(rank);
            _energy.text = "ENERGY " + energy + "/" + stats.EnergyCost;
            for (int i = 0; i < 5; i++)
            {
                _energyPips[i].gameObject.SetActive(i < stats.EnergyCost);
                _energyPips[i].color = i < energy ? (Slot == 0 ? Theme.ChannelA : Theme.ChannelB) : new Color(0.3f, 0.3f, 0.3f, 1f);
            }
            _xp.text = "XP " + xp + "/6";
            for (int i = 0; i < 6; i++) _xpPips[i].color = i < xp ? Theme.Xp : new Color(0.3f, 0.3f, 0.3f, 1f);
            bool ready = energy >= stats.EnergyCost;
            _readyBg.gameObject.SetActive(ready);
            _stats.text = StatsLine(def, stats) + "\n" + (rank == Rank.Gold ? "At 6 XP: launches a 2-damage BOMB" : "At 6 XP: ranks up");
            _highlight.gameObject.SetActive(acting);
            Description = (sideIsPlayer ? "Player " : "Opponent ") + def.DisplayName + ", " + rank + " rank, " + xp + " of 6 XP, "
                + energy + " of " + stats.EnergyCost + " energy" + (ready ? ", ready" : "");
        }

        public static string StatsLine(UnitDefinition def, UnitRankStats s)
        {
            switch (def.Action)
            {
                case ActionKind.AssassinStrike:
                    return "DIRECT: Crown " + s.CrownDamage + ", drain " + s.Delay + " energy";
                case ActionKind.PriestBlessing:
                    return "SUPPORT: heal " + s.Heal + ", +" + s.EnergyGrant + " energy to partner";
                default:
                {
                    string h = string.Join(", ", def.Heights);
                    string line = "Crown " + s.CrownDamage + " / Wall " + s.BarrierDamage + " per shot\n"
                        + def.Heights.Count + (def.Heights.Count == 1 ? " shot" : " shots") + " at height " + h;
                    if (s.FriendlyBarrier > 0) line += ", +" + s.FriendlyBarrier + " own Wall";
                    if (s.SelfDamage > 0) line += ", self " + s.SelfDamage;
                    return line;
                }
            }
        }
    }
}
