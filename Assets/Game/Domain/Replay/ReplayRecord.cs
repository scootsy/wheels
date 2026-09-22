using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Tabletop.Domain
{
    /// <summary>
    /// Compact, text-safe replay: rules version, content hash, seed, side configurations,
    /// ordered accepted commands, and final authoritative state hash (MATCH_UX_SPEC 13).
    /// </summary>
    public sealed class ReplayRecord
    {
        public const string Magic = "TTR1";

        public ReplayRecord(string rulesVersion, string contentHash, long seed, SideConfig player, SideConfig opponent,
            bool allowDuplicateUnits, IEnumerable<MatchCommand> commands, string finalStateHash, string scenario = "")
        {
            Scenario = scenario ?? "";
            RulesVersion = rulesVersion;
            ContentHash = contentHash;
            Seed = seed;
            Player = player;
            Opponent = opponent;
            AllowDuplicateUnits = allowDuplicateUnits;
            Commands = new ReadOnlyCollection<MatchCommand>(new List<MatchCommand>(commands));
            FinalStateHash = finalStateHash;
        }

        public string RulesVersion { get; }
        public string ContentHash { get; }
        public long Seed { get; }
        public SideConfig Player { get; }
        public SideConfig Opponent { get; }
        public bool AllowDuplicateUnits { get; }
        public IReadOnlyList<MatchCommand> Commands { get; }
        public string FinalStateHash { get; }
        /// <summary>Encoded developer scenario, empty for normal matches.</summary>
        public string Scenario { get; }

        public static ReplayRecord FromMatch(Match match)
        {
            var cfg = match.Config;
            return new ReplayRecord(cfg.RulesVersion, match.Catalog.ContentHash, cfg.Seed, cfg.Sides[0], cfg.Sides[1],
                cfg.AllowDuplicateUnits, new List<MatchCommand>(match.AcceptedCommands), match.StateHash(),
                cfg.Scenario == null ? "" : cfg.Scenario.Encode());
        }

        public MatchConfig ToConfig() => new MatchConfig(RulesVersion, Seed, Player, Opponent, AllowDuplicateUnits,
            Scenario.Length > 0 ? ScenarioSetup.Decode(Scenario) : null);

        public string Encode()
        {
            var sb = new StringBuilder();
            sb.Append(Magic).Append(';').Append(RulesVersion).Append(';').Append(ContentHash).Append(';')
              .Append(Seed.ToString(CultureInfo.InvariantCulture)).Append(';').Append(Player.Encode()).Append(';')
              .Append(Opponent.Encode()).Append(';').Append(AllowDuplicateUnits ? '1' : '0').Append(';');
            for (int i = 0; i < Commands.Count; i++)
            {
                if (i > 0) sb.Append('.');
                sb.Append(Commands[i].Encode());
            }
            sb.Append(';').Append(FinalStateHash);
            if (Scenario.Length > 0) sb.Append(';').Append(Scenario);
            return sb.ToString();
        }

        public static ReplayRecord Decode(string text)
        {
            if (text == null) throw new FormatException("Empty replay.");
            var parts = text.Trim().Split(';');
            if ((parts.Length != 9 && parts.Length != 10) || parts[0] != Magic) throw new FormatException("Not a replay payload.");
            var commands = new List<MatchCommand>();
            if (parts[7].Length > 0)
                foreach (var token in parts[7].Split('.')) commands.Add(MatchCommand.Decode(token));
            return new ReplayRecord(parts[1], parts[2], long.Parse(parts[3], CultureInfo.InvariantCulture),
                SideConfig.Decode(parts[4]), SideConfig.Decode(parts[5]), parts[6] == "1", commands, parts[8],
                parts.Length == 10 ? parts[9] : "");
        }

        /// <summary>Re-executes the command log. Returns null on success, otherwise a diagnostic.</summary>
        public string Verify(ContentCatalog catalog, out Match replayed)
        {
            replayed = null;
            if (catalog.ContentHash != ContentHash) return "Content hash mismatch: replay " + ContentHash + ", local " + catalog.ContentHash;
            var match = Match.Start(ToConfig(), catalog, out var rejection);
            if (match == null) return "Invalid configuration: " + rejection;
            for (int i = 0; i < Commands.Count; i++)
            {
                var result = match.Execute(Commands[i]);
                if (!result.Accepted) return "Command " + i + " (" + Commands[i] + ") rejected: " + result.Rejection;
            }
            replayed = match;
            var hash = match.StateHash();
            return hash == FinalStateHash ? null : "Final state hash mismatch: expected " + FinalStateHash + ", got " + hash;
        }
    }
}
