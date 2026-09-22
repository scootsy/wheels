using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Tabletop.Domain
{
    /// <summary>
    /// Developer/test-only forced scenario: starting values and forced reel faces for particular spins.
    /// It is part of the match configuration and replay payload, so forced matches stay reproducible.
    /// Normal play never sets a scenario.
    /// </summary>
    public sealed class ScenarioSetup
    {
        public sealed class UnitStart
        {
            public Rank Rank;
            public int Xp;
            public int Energy;
        }

        private readonly Dictionary<string, int[]> _forcedFaces = new Dictionary<string, int[]>(StringComparer.Ordinal);

        public int?[] Crown { get; } = new int?[2];
        public int?[] Barrier { get; } = new int?[2];
        public UnitStart[,] Units { get; } = new UnitStart[2, 2];
        public string Name { get; set; } = "";

        private static string Key(int side, int round, int spin) => side + "," + round + "," + spin;

        /// <summary>Force the faces produced by a spin (unlocked reels only take the forced value).</summary>
        public ScenarioSetup Force(SideId side, int round, int spin, params int[] faceIndices)
        {
            if (faceIndices.Length != ReelSetDefinition.ReelCount) throw new ArgumentException("Five face indices required.");
            _forcedFaces[Key((int)side, round, spin)] = (int[])faceIndices.Clone();
            return this;
        }

        /// <summary>Force faces by spec notation, e.g. ("S","D","SS","HH","-"), resolving indices against the reel set.</summary>
        public ScenarioSetup ForceCodes(SideId side, int round, int spin, ReelSetDefinition reels, params string[] codes)
        {
            var idx = new int[ReelSetDefinition.ReelCount];
            for (int r = 0; r < idx.Length; r++)
            {
                idx[r] = -1;
                for (int f = 0; f < reels.Reels[r].Faces.Count; f++)
                    if (reels.Reels[r].Faces[f].Code == codes[r]) { idx[r] = f; break; }
                if (idx[r] < 0) throw new ArgumentException("Reel " + (r + 1) + " has no face " + codes[r]);
            }
            return Force(side, round, spin, idx);
        }

        public ScenarioSetup SetUnit(SideId side, int slot, Rank rank, int xp, int energy)
        {
            Units[(int)side, slot] = new UnitStart { Rank = rank, Xp = xp, Energy = energy };
            return this;
        }

        public ScenarioSetup SetCrown(SideId side, int hp) { Crown[(int)side] = hp; return this; }
        public ScenarioSetup SetBarrier(SideId side, int barrier) { Barrier[(int)side] = barrier; return this; }

        public bool TryGetForcedFaces(int side, int round, int spin, out int[] faces) => _forcedFaces.TryGetValue(Key(side, round, spin), out faces);

        public string Validate()
        {
            for (int s = 0; s < 2; s++)
            {
                if (Crown[s].HasValue && (Crown[s] < 0 || Crown[s] > RulesConstants.HardCrownCap)) return "Scenario crown out of range";
                if (Barrier[s].HasValue && (Barrier[s] < 0 || Barrier[s] > RulesConstants.MaxBarrier)) return "Scenario barrier out of range";
                for (int u = 0; u < 2; u++)
                {
                    var us = Units[s, u];
                    if (us == null) continue;
                    if (us.Xp < 0 || us.Xp >= RulesConstants.XpThreshold || us.Energy < 0) return "Scenario unit value out of range";
                }
            }
            foreach (var kv in _forcedFaces)
                foreach (var f in kv.Value)
                    if (f < 0 || f >= ReelDefinition.FaceCount) return "Scenario face index out of range";
            return null;
        }

        /// <summary>Compact text form: segments separated by '/'.</summary>
        public string Encode()
        {
            var parts = new List<string>();
            for (int s = 0; s < 2; s++)
            {
                if (Crown[s].HasValue) parts.Add("c" + s + "=" + Crown[s].Value.ToString(CultureInfo.InvariantCulture));
                if (Barrier[s].HasValue) parts.Add("b" + s + "=" + Barrier[s].Value.ToString(CultureInfo.InvariantCulture));
                for (int u = 0; u < 2; u++)
                {
                    var us = Units[s, u];
                    if (us != null) parts.Add("u" + s + u + "=" + (int)us.Rank + "," + us.Xp + "," + us.Energy);
                }
            }
            var keys = new List<string>(_forcedFaces.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys)
            {
                var sb = new StringBuilder("f").Append(k).Append('=');
                foreach (var f in _forcedFaces[k]) sb.Append(f);
                parts.Add(sb.ToString());
            }
            return string.Join("/", parts.ToArray());
        }

        public static ScenarioSetup Decode(string text)
        {
            var sc = new ScenarioSetup();
            if (string.IsNullOrEmpty(text)) return sc;
            foreach (var part in text.Split('/'))
            {
                var eq = part.IndexOf('=');
                if (eq < 2) throw new FormatException("Bad scenario segment " + part);
                string key = part.Substring(0, eq), value = part.Substring(eq + 1);
                switch (key[0])
                {
                    case 'c': sc.Crown[key[1] - '0'] = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case 'b': sc.Barrier[key[1] - '0'] = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case 'u':
                    {
                        var v = value.Split(',');
                        sc.Units[key[1] - '0', key[2] - '0'] = new UnitStart
                        {
                            Rank = (Rank)int.Parse(v[0], CultureInfo.InvariantCulture),
                            Xp = int.Parse(v[1], CultureInfo.InvariantCulture),
                            Energy = int.Parse(v[2], CultureInfo.InvariantCulture),
                        };
                        break;
                    }
                    case 'f':
                    {
                        var k = key.Substring(1).Split(',');
                        var faces = new int[value.Length];
                        for (int i = 0; i < value.Length; i++) faces[i] = value[i] - '0';
                        sc.Force((SideId)int.Parse(k[0], CultureInfo.InvariantCulture), int.Parse(k[1], CultureInfo.InvariantCulture),
                            int.Parse(k[2], CultureInfo.InvariantCulture), faces);
                        break;
                    }
                    default: throw new FormatException("Bad scenario segment " + part);
                }
            }
            return sc;
        }
    }
}
