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
        private readonly Image _rankBand;
        private readonly Text _title;
        private readonly Text _rank;
        private readonly Text _xp;
        private readonly Text _energy;
        private readonly Text _stats;
        private readonly Text _ready;
        private readonly Image[] _energyPips = new Image[5];
        private readonly Image[] _xpPips = new Image[6];
        private readonly Image _highlight;

        public UnitPanelView(Transform parent, int side, int slot, float x, float y, float w, float h)
        {
            Side = side;
            Slot = slot;
            Button = Ui.Button((side == 0 ? "Player" : "Enemy") + "Unit" + (slot == 0 ? "A" : "B"), parent, "", null, 18, Theme.Panel);
            Button.GetComponentInChildren<Text>().gameObject.SetActive(false);
            ((RectTransform)Button.transform).Place(x, y, w, h);

            _highlight = Ui.Panel("ActingHighlight", Button.transform, new Color(1f, 0.95f, 0.5f, 0.25f));
            _highlight.rectTransform.Fill();
            _highlight.raycastTarget = false;
            _highlight.gameObject.SetActive(false);

            _rankBand = Ui.Panel("RankBand", Button.transform, Theme.Rank(Rank.Bronze));
            _rankBand.rectTransform.Place(0, 0, 12, h);
            _rankBand.raycastTarget = false;

            var glyph = Ui.Glyph("Channel", Button.transform, slot == 0 ? GlyphShape.Square : GlyphShape.Diamond, 30);
            ((RectTransform)glyph.transform.parent).Place(20, 8, 30, 30);

            _title = Ui.Label("Title", Button.transform, "", 22, TextAnchor.UpperLeft, Theme.Text, FontStyle.Bold);
            _title.rectTransform.Place(56, 8, w - 60, 30);
            _rank = Ui.Label("Rank", Button.transform, "", 18, TextAnchor.UpperLeft, Theme.Text, FontStyle.Bold);
            _rank.rectTransform.Place(20, 42, 150, 24);
            _ready = Ui.Label("Ready", Button.transform, "", 18, TextAnchor.UpperRight, Theme.Heal, FontStyle.Bold);
            _ready.rectTransform.Place(w - 170, 42, 160, 24);

            _xp = Ui.Label("Xp", Button.transform, "", 18, TextAnchor.UpperLeft);
            _xp.rectTransform.Place(20, 70, 130, 24);
            for (int i = 0; i < 6; i++)
            {
                _xpPips[i] = Ui.Panel("XpPip" + i, Button.transform, Theme.Xp);
                _xpPips[i].rectTransform.Place(150 + i * 26, 74, 20, 16);
            }

            _energy = Ui.Label("Energy", Button.transform, "", 18, TextAnchor.UpperLeft);
            _energy.rectTransform.Place(20, 98, 130, 24);
            for (int i = 0; i < 5; i++)
            {
                _energyPips[i] = Ui.Panel("EnergyPip" + i, Button.transform, slot == 0 ? Theme.ChannelA : Theme.ChannelB);
                _energyPips[i].rectTransform.Place(150 + i * 32, 100, 26, 18);
            }

            _stats = Ui.Label("Stats", Button.transform, "", 17, TextAnchor.UpperLeft, Theme.TextDim);
            _stats.rectTransform.Place(20, 126, w - 30, h - 130);
        }

        public string Description { get; private set; } = "";

        public void Render(UnitDefinition def, Rank rank, int xp, int energy, bool sideIsPlayer, bool acting)
        {
            var stats = def.Stats(rank);
            string ch = Slot == 0 ? "A / LEFT" : "B / RIGHT";
            _title.text = (sideIsPlayer ? "YOUR " : "ENEMY ") + def.DisplayName.ToUpperInvariant() + "  (" + ch + ")";
            _title.color = sideIsPlayer ? Theme.Text : Theme.Enemy;
            _rankBand.color = Theme.Rank(rank);
            string glyph = rank == Rank.Bronze ? "[I]" : rank == Rank.Silver ? "[II]" : "[III]";
            _rank.text = glyph + " " + rank.ToString().ToUpperInvariant();
            _rank.color = Theme.Rank(rank);
            _xp.text = "XP " + xp + " / 6";
            for (int i = 0; i < 6; i++) _xpPips[i].color = i < xp ? Theme.Xp : new Color(0.3f, 0.3f, 0.3f, 1f);
            _energy.text = "ENERGY " + energy + " / " + stats.EnergyCost;
            for (int i = 0; i < 5; i++)
            {
                _energyPips[i].gameObject.SetActive(i < stats.EnergyCost);
                _energyPips[i].color = i < energy ? (Slot == 0 ? Theme.ChannelA : Theme.ChannelB) : new Color(0.3f, 0.3f, 0.3f, 1f);
            }
            bool ready = energy >= stats.EnergyCost;
            _ready.text = ready ? "READY >>" : "";
            _stats.text = StatsLine(def, stats) + "\n" + (rank == Rank.Gold ? "Gold: 6 XP launches a 2-dmg BOMB" : "6 XP ranks up");
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
                    string line = "Crown " + s.CrownDamage + " / Barrier " + s.BarrierDamage + " per shot\n"
                        + def.Heights.Count + (def.Heights.Count == 1 ? " shot" : " shots") + " at height " + h;
                    if (s.FriendlyBarrier > 0) line += ", +" + s.FriendlyBarrier + " own Barrier";
                    if (s.SelfDamage > 0) line += ", self " + s.SelfDamage;
                    return line;
                }
            }
        }
    }
}
