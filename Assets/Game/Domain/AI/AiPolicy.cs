using System;
using System.Collections.Generic;

namespace Tabletop.Domain
{
    /// <summary>A lock decision after a spin. All five locked means "finalize now".</summary>
    public interface IAiPolicy
    {
        string ProfileId { get; }
        /// <param name="faces">Own current face indices (from own ReelsSpun events).</param>
        /// <param name="spinsUsed">Own spins used this round (1 or 2 when asked).</param>
        bool[] ChooseLocks(AiDecisionInput input, int[] faces, int spinsUsed);
    }

    /// <summary>
    /// Tunable weights in hundredths (integer math keeps AI choices bit-identical across platforms).
    /// Profiles change weights and search, never odds or legality.
    /// </summary>
    public sealed class AiWeights
    {
        public int UsefulEnergy = 100;
        public int WastedEnergy = 50;
        public int ReadyBonus = 200;
        public int CrownDamage = 150;
        public int BarrierDamage = 30;
        public int Xp = 80;
        public int XpNearRank = 80;
        public int RankUp = 300;
        public int Bomb = 400;
        public int UsefulBarrier = 60;
        public int BarrierThreat = 80;
        public int WastedBarrier = 30;
        public int Lethal = 10000;
        public int NearlyReady = 50;
        public int RerollOption = 35;
    }

    public static class AiProfiles
    {
        public static IAiPolicy Create(string controllerId)
        {
            switch (controllerId)
            {
                case ControllerIds.AiLearner: return new LearnerAiPolicy();
                case ControllerIds.AiExpert: return new EvaluatingAiPolicy(ControllerIds.AiExpert, new AiWeights { Lethal = 25000, BarrierThreat = 120, WastedEnergy = 80, XpNearRank = 120 });
                default: return new EvaluatingAiPolicy(ControllerIds.AiStandard, new AiWeights());
            }
        }
    }

    /// <summary>Learner: locks whatever feeds the channel with the most printed symbols. Low lookahead; tolerates waste.</summary>
    public sealed class LearnerAiPolicy : IAiPolicy
    {
        public string ProfileId => ControllerIds.AiLearner;

        public bool[] ChooseLocks(AiDecisionInput input, int[] faces, int spinsUsed)
        {
            var list = new List<ReelFace>();
            for (int r = 0; r < faces.Length; r++) list.Add(input.OwnReels[r].Faces[faces[r]]);
            var t = SymbolEvaluator.Evaluate(list);
            Channel focus = t.ChannelA >= t.ChannelB ? Channel.A : Channel.B;
            var locks = new bool[faces.Length];
            for (int r = 0; r < faces.Length; r++)
            {
                var f = list[r];
                locks[r] = f.Count(focus) > 0 || f.XpChannel.HasValue || (t.Hammer >= 3 && f.Hammer > 0);
            }
            return locks;
        }
    }

    /// <summary>
    /// Standard/Expert: scores a final result with the RULES_SPEC 14.2 utility and picks the lock
    /// mask with the best exact one-step expected value over all rerolled face combinations.
    /// Fully deterministic; no randomness is consumed.
    /// </summary>
    public sealed class EvaluatingAiPolicy : IAiPolicy
    {
        private readonly AiWeights _w;

        public EvaluatingAiPolicy(string profileId, AiWeights weights)
        {
            ProfileId = profileId;
            _w = weights;
        }

        public string ProfileId { get; }

        public bool[] ChooseLocks(AiDecisionInput input, int[] faces, int spinsUsed)
        {
            int n = faces.Length;
            var current = new ReelFace[n];
            for (int r = 0; r < n; r++) current[r] = input.OwnReels[r].Faces[faces[r]];
            int allMask = (1 << n) - 1;

            // Utility depends only on symbol totals, so memoize per decision.
            _cache.Clear();
            // Compare expected values as exact fractions sum/count to stay integer-only.
            int bestMask = allMask;
            long bestSum = Cached(input, current);
            long bestCount = 1;
            // With two spins left, rerolling keeps an extra option; value it slightly (fixed, deterministic).
            int optionBonus = spinsUsed <= 1 ? _w.RerollOption : 0;
            var scratch = new ReelFace[n];
            for (int mask = allMask - 1; mask >= 0; mask--)
            {
                long sum = 0, count = 0;
                var free = new List<int>();
                for (int r = 0; r < n; r++)
                {
                    if ((mask & (1 << r)) != 0) scratch[r] = current[r];
                    else free.Add(r);
                }
                Enumerate(input, free, 0, scratch, ref sum, ref count);
                sum += optionBonus * count;
                if (sum * bestCount > bestSum * count)
                {
                    bestSum = sum;
                    bestCount = count;
                    bestMask = mask;
                }
            }
            var locks = new bool[n];
            for (int r = 0; r < n; r++) locks[r] = (bestMask & (1 << r)) != 0;
            return locks;
        }

        private void Enumerate(AiDecisionInput input, List<int> free, int depth, ReelFace[] scratch, ref long sum, ref long count)
        {
            if (depth == free.Count)
            {
                sum += Cached(input, scratch);
                count++;
                return;
            }
            int reel = free[depth];
            foreach (var face in input.OwnReels[reel].Faces)
            {
                scratch[reel] = face;
                Enumerate(input, free, depth + 1, scratch, ref sum, ref count);
            }
        }

        private readonly Dictionary<int, int> _cache = new Dictionary<int, int>();

        private int Cached(AiDecisionInput input, ReelFace[] faces)
        {
            var t = new SymbolTotals();
            for (int i = 0; i < faces.Length; i++)
            {
                var f = faces[i];
                t.ChannelA += f.ChannelA;
                t.ChannelB += f.ChannelB;
                t.Hammer += f.Hammer;
                if (f.XpChannel == Channel.A) t.XpA++;
                else if (f.XpChannel == Channel.B) t.XpB++;
            }
            int key = (((t.ChannelA * 32 + t.ChannelB) * 32 + t.Hammer) * 8 + t.XpA) * 8 + t.XpB;
            if (!_cache.TryGetValue(key, out int value))
            {
                value = Utility(input, t);
                _cache[key] = value;
            }
            return value;
        }

        /// <summary>RULES_SPEC 14.2 utility of a final face set, from the public round-start state.</summary>
        public int Utility(AiDecisionInput input, IList<ReelFace> faces) => Utility(input, SymbolEvaluator.Evaluate(faces));

        private int Utility(AiDecisionInput input, SymbolTotals t)
        {
            var own = input.Own;
            var opp = input.Opponent;
            int score = 0;
            int expectedCrownDamage = 0;
            int bombs = 0;

            for (int u = 0; u < 2; u++)
            {
                var unit = own.Units[u];
                var ch = (Channel)u;
                var def = unit.Definition;
                var rank = unit.Rank;

                int xpGain = t.Xp(ch);
                if (xpGain > 0)
                {
                    score += _w.Xp * xpGain + (unit.Xp >= 3 ? _w.XpNearRank : 0);
                    if (unit.Xp + xpGain >= RulesConstants.XpThreshold)
                    {
                        if (rank == Rank.Gold) { score += _w.Bomb; bombs++; }
                        else { score += _w.RankUp; rank = rank + 1; }
                    }
                }

                int cost = def.Stats(rank).EnergyCost;
                int energy = Math.Min(unit.Energy, cost);
                int gain = t.Energy(ch);
                int newEnergy = Math.Min(cost, energy + gain);
                score += _w.UsefulEnergy * (newEnergy - energy);
                score -= _w.WastedEnergy * (energy + gain - newEnergy);

                if (newEnergy >= cost)
                {
                    var stats = def.Stats(rank);
                    int crown, barrier;
                    EstimateDamage(def, stats, opp.Barrier, out crown, out barrier);
                    expectedCrownDamage += crown;
                    score += _w.ReadyBonus + _w.CrownDamage * crown + _w.BarrierDamage * barrier;
                    if (def.Action == ActionKind.PriestBlessing) score += 100 * stats.Heal + 50 * stats.EnergyGrant;
                    if (def.Action == ActionKind.EngineerBuild) score += _w.UsefulBarrier * Math.Min(stats.FriendlyBarrier, RulesConstants.MaxBarrier - own.Barrier);
                }
                else if (gain > 0 && cost - newEnergy == 1)
                {
                    score += _w.NearlyReady; // nearly ready next round
                }
            }

            int barrierGain = t.BarrierGain;
            if (barrierGain > 0)
            {
                int useful = Math.Min(barrierGain, RulesConstants.MaxBarrier - own.Barrier);
                int threat = 0;
                foreach (var enemy in opp.Units)
                {
                    int lowest = int.MaxValue;
                    foreach (var h in enemy.Definition.Heights) lowest = Math.Min(lowest, h);
                    if (lowest == int.MaxValue) continue; // direct/support: Barrier does not help
                    int proximity = enemy.EnergyNeeded <= 2 ? 2 : 1; // halves
                    if (lowest <= own.Barrier + useful) threat += proximity;
                }
                score += useful * (_w.UsefulBarrier + _w.BarrierThreat * threat / 2);
                score -= _w.WastedBarrier * (barrierGain - useful);
            }

            if (expectedCrownDamage + bombs * RulesConstants.BombDamage >= opp.Crown && opp.Crown > 0) score += _w.Lethal;
            return score;
        }

        private static void EstimateDamage(UnitDefinition def, UnitRankStats stats, int enemyBarrier, out int crown, out int barrier)
        {
            crown = 0;
            barrier = 0;
            if (def.Action == ActionKind.AssassinStrike) { crown = stats.CrownDamage; return; }
            int b = enemyBarrier;
            foreach (var h in def.Heights)
            {
                if (h > b) crown += stats.CrownDamage;
                else
                {
                    int d = Math.Min(b, stats.BarrierDamage);
                    barrier += d;
                    b -= d;
                }
            }
        }
    }
}
