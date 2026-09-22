using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Tabletop.Domain
{
    public enum Channel { A = 0, B = 1 }

    public enum Rank { Bronze = 0, Silver = 1, Gold = 2 }

    public enum ReelTier { Copper = 0, Bronze = 1, Silver = 2, Gold = 3, Diamond = 4, Platinum = 5 }

    /// <summary>Resolution priority group from RULES_SPEC Section 10.</summary>
    public enum ActionKind
    {
        /// <summary>Sequential projectiles (Warrior/Striker, Mage/Caster, Archer). Stage 8.</summary>
        Projectiles = 0,
        /// <summary>Projectiles then friendly Barrier (Engineer). Stage 6.</summary>
        EngineerBuild = 1,
        /// <summary>Delay closest enemy then direct Crown damage (Assassin). Stage 4.</summary>
        AssassinStrike = 2,
        /// <summary>Heal Crown and grant partner energy (Priest). Stage 5.</summary>
        PriestBlessing = 3,
        /// <summary>Self-damage (floor 1) then projectiles (Warlock). Stage 8 (O-05).</summary>
        WarlockVolley = 4,
    }

    public enum SideId { Player = 0, Opponent = 1 }

    public sealed class ReelFace
    {
        public ReelFace(int channelA, int channelB, int hammer, Channel? xpChannel)
        {
            if (channelA < 0 || channelB < 0 || hammer < 0) throw new ArgumentException("Negative symbol count.");
            ChannelA = channelA;
            ChannelB = channelB;
            Hammer = hammer;
            XpChannel = xpChannel;
        }

        public int ChannelA { get; }
        public int ChannelB { get; }
        public int Hammer { get; }
        public Channel? XpChannel { get; }
        public bool IsBlank => ChannelA == 0 && ChannelB == 0 && Hammer == 0;

        public int Count(Channel channel) => channel == Channel.A ? ChannelA : ChannelB;

        /// <summary>Spec notation, e.g. "S", "DD+", "HH", "-".</summary>
        public string Code
        {
            get
            {
                if (IsBlank) return "-";
                var s = new string('S', ChannelA) + new string('D', ChannelB) + new string('H', Hammer);
                return XpChannel.HasValue ? s + "+" : s;
            }
        }

        public override string ToString() => Code;

        /// <summary>Parses spec notation ("S", "DD+", "HHH", "-").</summary>
        public static ReelFace Parse(string code)
        {
            if (code == "-") return new ReelFace(0, 0, 0, null);
            bool xp = code.EndsWith("+", StringComparison.Ordinal);
            string body = xp ? code.Substring(0, code.Length - 1) : code;
            int a = 0, b = 0, h = 0;
            foreach (char c in body)
            {
                if (c == 'S') a++;
                else if (c == 'D') b++;
                else if (c == 'H') h++;
                else throw new FormatException("Unknown face symbol '" + c + "' in " + code);
            }
            Channel? xpChannel = null;
            if (xp)
            {
                if (a > 0 && b == 0 && h == 0) xpChannel = Channel.A;
                else if (b > 0 && a == 0 && h == 0) xpChannel = Channel.B;
                else throw new FormatException("XP face must contain exactly one channel: " + code);
            }
            return new ReelFace(a, b, h, xpChannel);
        }
    }

    public sealed class ReelDefinition
    {
        public const int FaceCount = 8;

        public ReelDefinition(string id, IList<ReelFace> faces)
        {
            Id = id;
            Faces = new ReadOnlyCollection<ReelFace>(new List<ReelFace>(faces));
        }

        public string Id { get; }
        public IReadOnlyList<ReelFace> Faces { get; }
    }

    /// <summary>Five reels: four fixed reels plus the tiered fifth reel.</summary>
    public sealed class ReelSetDefinition
    {
        public const int ReelCount = 5;

        public ReelSetDefinition(ReelTier tier, IList<ReelDefinition> reels)
        {
            Tier = tier;
            Reels = new ReadOnlyCollection<ReelDefinition>(new List<ReelDefinition>(reels));
        }

        public ReelTier Tier { get; }
        public IReadOnlyList<ReelDefinition> Reels { get; }
    }

    public sealed class UnitRankStats
    {
        public UnitRankStats(int energyCost, int crownDamage = 0, int barrierDamage = 0, int delay = 0,
            int heal = 0, int energyGrant = 0, int selfDamage = 0, int friendlyBarrier = 0)
        {
            EnergyCost = energyCost;
            CrownDamage = crownDamage;
            BarrierDamage = barrierDamage;
            Delay = delay;
            Heal = heal;
            EnergyGrant = energyGrant;
            SelfDamage = selfDamage;
            FriendlyBarrier = friendlyBarrier;
        }

        public int EnergyCost { get; }
        public int CrownDamage { get; }
        public int BarrierDamage { get; }
        public int Delay { get; }
        public int Heal { get; }
        public int EnergyGrant { get; }
        public int SelfDamage { get; }
        public int FriendlyBarrier { get; }
    }

    public sealed class UnitDefinition
    {
        public UnitDefinition(string id, string displayName, string role, string description, ActionKind action,
            IList<int> heights, IList<UnitRankStats> ranks, bool playerFacing)
        {
            Id = id;
            DisplayName = displayName;
            Role = role;
            Description = description;
            Action = action;
            Heights = new ReadOnlyCollection<int>(new List<int>(heights ?? new int[0]));
            Ranks = new ReadOnlyCollection<UnitRankStats>(new List<UnitRankStats>(ranks));
            PlayerFacing = playerFacing;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Role { get; }
        public string Description { get; }
        public ActionKind Action { get; }
        /// <summary>Projectile heights in firing order. Empty for direct/support units.</summary>
        public IReadOnlyList<int> Heights { get; }
        /// <summary>Bronze, Silver, Gold.</summary>
        public IReadOnlyList<UnitRankStats> Ranks { get; }
        /// <summary>Visible in normal (non-developer) unit selection.</summary>
        public bool PlayerFacing { get; }

        public UnitRankStats Stats(Rank rank) => Ranks[(int)rank];
    }
}
