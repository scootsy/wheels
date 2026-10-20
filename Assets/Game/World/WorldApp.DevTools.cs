using System;
using System.Collections.Generic;
using System.Linq;
using Tabletop.Application;
using Tabletop.Domain;
using UnityEngine;

namespace Tabletop.World
{
    /// <summary>
    /// Hidden developer tools for testing builds (D-040). Tap PAUSED five times in the pause menu to unlock DEV TOOLS
    /// for the rest of the session. The pages set coins, charms, fifth wheels, figurines and champion wins, and switch
    /// the god wheels on or off. Every change goes through <see cref="GameFlow"/> and is saved straight away.
    /// </summary>
    public sealed partial class WorldApp
    {
        public const int DevUnlockTaps = 5;

        public enum DevPage { Main, Charms, Wheels, Figurines, Champions }

        private int _devTaps;

        /// <summary>The dev page on screen, or null when the dev tools are closed.</summary>
        public DevPage? OpenDevPage { get; private set; }

        private void OnPauseTitleTapped()
        {
            if (GameFlow.DevToolsUnlocked || ++_devTaps < DevUnlockTaps) return;
            GameFlow.DevToolsUnlocked = true;
            Ui.ShowPauseDev(true);
            Select(Ui.PauseDev);
            _audio?.Sfx("sfx/coins");
        }

        public void OpenDevTools() => ShowDevPage(DevPage.Main, 0);

        /// <summary>Shows one page of the dev tools with the <paramref name="focus"/>-th button selected.</summary>
        public void ShowDevPage(DevPage page, int focus)
        {
            var rows = new List<ListOverlay.Row>();
            int buttons = 0;

            // A row whose button changes the journey, then saves and redraws this page with the same button focused.
            void Change(string name, string detail, string action, Action change, Sprite icon = null, string tag = null)
            {
                int index = buttons++;
                rows.Add(new ListOverlay.Row
                {
                    Name = name, Detail = detail, Action = action, Tag = tag, Icon = icon,
                    OnPress = () => { change(); AfterDevChange(); ShowDevPage(page, index); },
                });
            }

            void Open(string name, string detail, DevPage target, Sprite icon = null)
            {
                buttons++;
                rows.Add(new ListOverlay.Row { Name = name, Detail = detail, Action = "OPEN", Icon = icon, OnPress = () => ShowDevPage(target, 0) });
            }

            void Info(string name, string detail, string tag = null, Sprite icon = null) =>
                rows.Add(new ListOverlay.Row { Name = name, Detail = detail, Tag = tag, Icon = icon });

            var ring = icons != null ? icons.uiRing : null;
            var star = icons != null ? icons.xp : null;
            var crown = icons != null ? icons.crown : null;
            string title = "DEV TOOLS";
            string subtitle;
            switch (page)
            {
                case DevPage.Charms:
                    title = "DEV TOOLS: CHARMS";
                    subtitle = "Add charms to your satchel. You choose one when you sit down at a table.";
                    foreach (var item in ItemCatalog.All.Where(i => i.Kind == ItemKind.Charm))
                    {
                        var id = item.Id;
                        Change(item.Name, item.Description, "+1", () => GameFlow.DevSetItemCount(id, GameFlow.ItemCount(id) + 1), star, "x" + GameFlow.ItemCount(id));
                    }
                    Change("Clear every charm", "Empty the satchel of charms.", "CLEAR",
                        () => { foreach (var c in ItemCatalog.All.Where(i => i.Kind == ItemKind.Charm)) GameFlow.DevSetItemCount(c.Id, 0); });
                    break;

                case DevPage.Wheels:
                    title = "DEV TOOLS: FIFTH WHEELS";
                    subtitle = "You always bring your best fifth wheel. Now bringing: " + GameFlow.PlayerWheel
                               + (GameFlow.GodWheels ? " (but the god wheels are on)." : ".");
                    foreach (var item in ItemCatalog.All.Where(i => i.Kind == ItemKind.Wheel))
                    {
                        var id = item.Id;
                        bool owned = GameFlow.ItemCount(id) > 0;
                        Change(item.Name + (owned ? "   (yours)" : ""), item.Description, owned ? "TAKE" : "GIVE",
                            () => GameFlow.DevSetItemCount(id, owned ? 0 : 1), ring);
                    }
                    break;

                case DevPage.Figurines:
                    title = "DEV TOOLS: FIGURINES";
                    subtitle = "Figurines you own can be chosen at every table and show in your deck.";
                    foreach (var unit in ReferenceContent.Catalog.Units)
                    {
                        var id = unit.Id;
                        var icon = icons != null ? icons.Unit(id) : null;
                        if (EncounterCatalog.StartingUnits.Contains(id)) { Info(unit.DisplayName, "Your starting figurine; always yours.", "STARTER", icon); continue; }
                        bool owned = GameFlow.IsUnlocked(id);
                        Change(unit.DisplayName + (owned ? "   (yours)" : ""), unit.Description, owned ? "TAKE" : "GIVE", () => GameFlow.DevSetUnlocked(id, !owned), icon);
                    }
                    break;

                case DevPage.Champions:
                    title = "DEV TOOLS: CHAMPIONS";
                    subtitle = "Champion titles: " + GameFlow.ChampionTitles + " of " + GameFlow.ChampionTitlesNeeded
                               + ". Beating a champion here also gives their figurine. Hold all five to enter the Grand Tournament.";
                    foreach (var champ in EncounterCatalog.TownChampions)
                    {
                        var id = champ.Id;
                        bool beaten = GameFlow.HasDefeated(id);
                        string prize = champ.PrizeUnit != null && ReferenceContent.Catalog.TryGetUnit(champ.PrizeUnit, out var u) ? "Holds the " + u.DisplayName + ". " : "";
                        Change(champ.Name + (beaten ? "   (beaten)" : ""), prize + Areas.Town(champ.Area) + ".", beaten ? "UNDO" : "BEAT",
                            () => GameFlow.DevSetDefeated(id, !beaten), crown);
                    }
                    Change("Beat every town champion", "All five titles and their figurines: the Herald will enter you.", "BEAT ALL",
                        () => { foreach (var c in EncounterCatalog.TownChampions) GameFlow.DevSetDefeated(c.Id, true); });
                    Change("Forget every win", "Nobody beaten, no titles. Figurines and items stay.", "CLEAR",
                        () => { foreach (var id in GameFlow.Defeated.ToList()) GameFlow.DevSetDefeated(id, false); });
                    if (GameFlow.TournamentRound > 0)
                        Change("Leave the Grand Tournament", "You're due to play round " + GameFlow.TournamentRound + ".", "LEAVE", GameFlow.DevLeaveTournament, crown);
                    break;

                default:
                    subtitle = "Testing overrides. Changes save with the journey straight away.";
                    Change("God wheels: " + (GameFlow.GodWheels ? "ON" : "OFF"),
                        "All five of your wheels overpowered at every table. Only you: opponents keep their own.",
                        GameFlow.GodWheels ? "TURN OFF" : "TURN ON", () => GameFlow.GodWheels = !GameFlow.GodWheels, ring);
                    Change("Add 100 coins", null, "+100", () => GameFlow.AddCoins(100), crown);
                    Change("Add 1,000 coins", null, "+1000", () => GameFlow.AddCoins(1000), crown);
                    Change("Empty the purse", "Coins to 0, to test playing for a favour.", "SET TO 0", () => GameFlow.DevSetCoins(0), crown);
                    Open("Charms", "Add or clear charms.", DevPage.Charms, star);
                    Open("Fifth wheels", "Give or take the Bronze to Platinum wheels.", DevPage.Wheels, ring);
                    Open("Figurines", "Give or take any figurine.", DevPage.Figurines, icons != null ? icons.Unit(ReferenceContent.Striker) : null);
                    Open("Champions and the tournament", "Mark champions beaten, or forget every win.", DevPage.Champions, crown);
                    Change("Give everything", "Every figurine and wheel, 9 of each charm and 9,999 coins.", "GIVE ALL", DevGiveEverything);
                    break;
            }

            OpenDevPage = page;
            bool main = page == DevPage.Main;
            var first = _list.Show(title, subtitle, rows,
                "Coins: " + GameFlow.Coins + "     Wheel: " + (GameFlow.GodWheels ? "GOD" : GameFlow.PlayerWheel.ToString())
                + "     Figurines: " + GameFlow.UnlockedUnits.Count + " / " + ReferenceContent.Catalog.Units.Count,
                main ? "CLOSE" : "BACK",
                main ? (Action)(() => { OpenDevPage = null; _closedFrame = Time.frameCount; }) : () => ShowDevPage(DevPage.Main, MainIndex(page)));
            var all = _list.RowButtons;
            Select(focus > 0 && focus < all.Count ? all[focus] : first);
        }

        /// <summary>The button on the main page that opens <paramref name="page"/>, so BACK returns to it.</summary>
        private static int MainIndex(DevPage page) => page == DevPage.Charms ? 4 : page == DevPage.Wheels ? 5 : page == DevPage.Figurines ? 6 : 7;

        private static void DevGiveEverything()
        {
            foreach (var unit in ReferenceContent.Catalog.Units) GameFlow.DevSetUnlocked(unit.Id, true);
            foreach (var item in ItemCatalog.All) GameFlow.DevSetItemCount(item.Id, item.Kind == ItemKind.Wheel ? 1 : 9);
            GameFlow.DevSetCoins(9999);
        }

        private void AfterDevChange()
        {
            RefreshTournament(); // champion wins open or close the tournament
            SaveNow();
            _audio?.Sfx("sfx/coins", 0.5f);
        }
    }
}
