using System.Collections.Generic;

namespace Tabletop.Domain
{
    /// <summary>The twelve-stage resolution sequence from RULES_SPEC Section 10.</summary>
    public sealed partial class Match
    {
        private struct QueuedBomb
        {
            public int Side;
            public int Slot;
            public int Order;
        }

        private readonly List<QueuedBomb> _earlyBombs = new List<QueuedBomb>();
        private readonly List<QueuedBomb> _lateBombs = new List<QueuedBomb>();
        private readonly bool[,] _deferredPriestEnergy = new bool[2, 2];
        private readonly int[,] _deferredPriestAmount = new int[2, 2];
        private int _bombOrder;

        private void DoResolve()
        {
            _earlyBombs.Clear();
            _lateBombs.Clear();
            System.Array.Clear(_deferredPriestEnergy, 0, _deferredPriestEnergy.Length);
            System.Array.Clear(_deferredPriestAmount, 0, _deferredPriestAmount.Length);
            _bombOrder = 0;

            var totals = new SymbolTotals[2];
            for (int s = 0; s < 2; s++)
            {
                var faces = new List<ReelFace>();
                foreach (var r in _sides[s].Reels) faces.Add(r.Face);
                totals[s] = SymbolEvaluator.Evaluate(faces);
            }

            // 1. Panel XP
            _stage = 1;
            for (int s = 0; s < 2; s++)
                for (int u = 0; u < 2; u++)
                {
                    int xp = totals[s].Xp((Channel)u);
                    if (xp > 0) GrantXp(s, u, xp, MatchEventType.PanelXpGranted);
                }

            // 2. Hammer results
            _stage = 2;
            for (int s = 0; s < 2; s++)
            {
                int gain = totals[s].BarrierGain;
                if (gain <= 0) continue;
                var side = _sides[s];
                int before = side.Barrier;
                side.Barrier = System.Math.Min(RulesConstants.MaxBarrier, side.Barrier + gain);
                Emit(MatchEventType.BarrierBuilt, side: s, amount: side.Barrier - before, attempted: gain, before: before, after: side.Barrier,
                    note: "hammers " + totals[s].Hammer);
            }

            // 3. Energy results
            _stage = 3;
            for (int s = 0; s < 2; s++)
                for (int u = 0; u < 2; u++)
                {
                    var unit = _sides[s].Units[u];
                    int gain = totals[s].Energy(unit.Channel);
                    if (gain > 0) AddEnergy(s, u, gain, EnergySource.Reels, "symbols " + totals[s].Symbols(unit.Channel));
                }
            for (int s = 0; s < 2; s++)
                for (int u = 0; u < 2; u++)
                {
                    var unit = _sides[s].Units[u];
                    unit.ReadySource = unit.Ready ? EnergySource.Reels : EnergySource.None;
                }

            // 4. Assassin actions
            _stage = 4;
            ActGroup(ActionKind.AssassinStrike);

            // 5. Priest healing and conditional energy setup
            _stage = 5;
            ActGroup(ActionKind.PriestBlessing);

            // 6. Engineer actions
            _stage = 6;
            ActGroup(ActionKind.EngineerBuild);

            // 7. Early bombs
            _stage = 7;
            ResolveBombs(_earlyBombs);

            // 8. Remaining normal actions (O-05: Warlock resolves here)
            _stage = 8;
            for (int s = 0; s < 2; s++)
                for (int u = 0; u < 2; u++)
                {
                    var unit = _sides[s].Units[u];
                    if ((unit.Def.Action == ActionKind.Projectiles || unit.Def.Action == ActionKind.WarlockVolley) && CanActFromReels(unit))
                        Act(s, u, EnergySource.Reels);
                }

            // 9. Deferred Priest energy
            _stage = 9;
            for (int s = 0; s < 2; s++)
                for (int u = 0; u < 2; u++)
                    if (_deferredPriestEnergy[s, u])
                    {
                        _deferredPriestEnergy[s, u] = false;
                        AddEnergy(s, u, _deferredPriestAmount[s, u], EnergySource.Priest, "deferred blessing");
                    }

            // 10. Priest-enabled partner actions
            _stage = 10;
            for (int s = 0; s < 2; s++)
                for (int u = 0; u < 2; u++)
                {
                    var unit = _sides[s].Units[u];
                    if (unit.ReadySource == EnergySource.Priest && unit.Ready && !unit.ActedFromPriest)
                        Act(s, u, EnergySource.Priest);
                }

            // 11. Late bombs
            _stage = 11;
            ResolveBombs(_lateBombs);

            // 12. Final Crown check
            _stage = 12;
            bool p0 = _sides[0].Crown <= 0, p1 = _sides[1].Crown <= 0;
            Winner result = Winner.None;
            if (p0 && p1) result = Winner.Tie;
            else if (p1) result = Winner.Player;
            else if (p0) result = Winner.Opponent;
            else if (_round >= RulesConstants.MaxRounds) result = Winner.Tie;

            Emit(MatchEventType.RoundEnded, note: result == Winner.None ? "next round" : "final check");
            _stage = 0;

            if (result != Winner.None)
            {
                _winner = result;
                _phase = MatchPhase.Ended;
                Emit(MatchEventType.MatchEnded, note: result.ToString());
                return;
            }

            // Next round: keep HP, Barrier, rank, XP, energy; unlock reels and reset spins.
            _round++;
            for (int s = 0; s < 2; s++)
            {
                var side = _sides[s];
                side.SpinsUsed = 0;
                side.Committed = false;
                foreach (var r in side.Reels) r.Locked = false;
                foreach (var u in side.Units)
                {
                    u.ActedFromReels = false;
                    u.ActedFromPriest = false;
                    u.ReadySource = EnergySource.None;
                }
            }
            _phase = MatchPhase.Spinning;
            Emit(MatchEventType.RoundStarted);
        }

        private static bool CanActFromReels(UnitState unit) =>
            unit.ReadySource == EnergySource.Reels && unit.Ready && !unit.ActedFromReels;

        private void ActGroup(ActionKind kind)
        {
            for (int s = 0; s < 2; s++)
                for (int u = 0; u < 2; u++)
                {
                    var unit = _sides[s].Units[u];
                    if (unit.Def.Action == kind && CanActFromReels(unit)) Act(s, u, EnergySource.Reels);
                }
        }

        // ----------------------------------------------------------------- actions

        private void Act(int s, int u, EnergySource source)
        {
            var side = _sides[s];
            var unit = side.Units[u];
            var stats = unit.Def.Stats(unit.Rank);
            int opp = 1 - s;

            int before = unit.Energy;
            unit.Energy = 0;
            unit.ReadySource = EnergySource.None;
            if (source == EnergySource.Priest) unit.ActedFromPriest = true; else unit.ActedFromReels = true;
            Emit(MatchEventType.UnitActivated, side: s, slot: u, before: before, after: 0, amount: before, source: source,
                note: unit.Def.Id + " " + unit.Rank);

            switch (unit.Def.Action)
            {
                case ActionKind.Projectiles:
                    FireProjectiles(s, u, stats);
                    break;
                case ActionKind.EngineerBuild:
                    FireProjectiles(s, u, stats);
                    BuildBarrier(s, u, stats.FriendlyBarrier);
                    break;
                case ActionKind.WarlockVolley:
                {
                    int hp = side.Crown;
                    int loss = System.Math.Min(stats.SelfDamage, System.Math.Max(0, hp - 1));
                    side.Crown = hp - loss;
                    Emit(MatchEventType.CrownDamaged, side: s, slot: u, targetSide: s, amount: loss, attempted: stats.SelfDamage,
                        before: hp, after: side.Crown, targetIsCrown: true, note: "self");
                    FireProjectiles(s, u, stats);
                    break;
                }
                case ActionKind.AssassinStrike:
                {
                    int target = ClosestToActing(opp, out bool tie);
                    var victim = _sides[opp].Units[target];
                    int eBefore = victim.Energy;
                    int drained = System.Math.Min(stats.Delay, victim.Energy);
                    victim.Energy -= drained;
                    if (!victim.Ready) victim.ReadySource = EnergySource.None;
                    Emit(MatchEventType.EnergyDelayed, side: s, slot: u, targetSide: opp, targetSlot: target, amount: drained,
                        attempted: stats.Delay, before: eBefore, after: victim.Energy, note: tie ? "tie: Channel A targeted (O-01)" : null);
                    DamageCrownDirect(s, u, opp, stats.CrownDamage, "direct");
                    break;
                }
                case ActionKind.PriestBlessing:
                {
                    int hp = side.Crown;
                    side.Crown = System.Math.Min(RulesConstants.HardCrownCap, hp + stats.Heal);
                    Emit(MatchEventType.CrownHealed, side: s, slot: u, targetSide: s, amount: side.Crown - hp, attempted: stats.Heal,
                        before: hp, after: side.Crown, targetIsCrown: true);
                    int partner = 1 - u;
                    var p = side.Units[partner];
                    if (_stage == 5 && p.ReadySource == EnergySource.Reels && p.Ready && !p.ActedFromReels)
                    {
                        _deferredPriestEnergy[s, partner] = true;
                        _deferredPriestAmount[s, partner] = stats.EnergyGrant;
                    }
                    else
                    {
                        AddEnergy(s, partner, stats.EnergyGrant, EnergySource.Priest, "blessing");
                    }
                    break;
                }
            }

            GrantXp(s, u, RulesConstants.ActionXp, MatchEventType.ActionXpGranted);
        }

        private int ClosestToActing(int side, out bool tie)
        {
            var units = _sides[side].Units;
            int need0 = units[0].Cost - units[0].Energy;
            int need1 = units[1].Cost - units[1].Energy;
            tie = need0 == need1;
            return need1 < need0 ? 1 : 0;
        }

        private void FireProjectiles(int s, int u, UnitRankStats stats)
        {
            var unit = _sides[s].Units[u];
            int opp = 1 - s;
            var target = _sides[opp];
            for (int i = 0; i < unit.Def.Heights.Count; i++)
            {
                int height = unit.Def.Heights[i];
                bool hitsCrown = height > target.Barrier;
                Emit(MatchEventType.ProjectileResolved, side: s, slot: u, targetSide: opp, height: height, projectileIndex: i,
                    targetIsCrown: hitsCrown, before: target.Barrier, after: target.Barrier);
                if (hitsCrown)
                {
                    int hp = target.Crown;
                    target.Crown = System.Math.Max(0, hp - stats.CrownDamage);
                    Emit(MatchEventType.CrownDamaged, side: s, slot: u, targetSide: opp, amount: hp - target.Crown, attempted: stats.CrownDamage,
                        before: hp, after: target.Crown, height: height, projectileIndex: i, targetIsCrown: true);
                }
                else
                {
                    int b = target.Barrier;
                    target.Barrier = System.Math.Max(0, b - stats.BarrierDamage);
                    Emit(MatchEventType.BarrierDamaged, side: s, slot: u, targetSide: opp, amount: b - target.Barrier, attempted: stats.BarrierDamage,
                        before: b, after: target.Barrier, height: height, projectileIndex: i);
                }
            }
        }

        private void DamageCrownDirect(int s, int u, int targetSide, int damage, string note)
        {
            var target = _sides[targetSide];
            int hp = target.Crown;
            target.Crown = System.Math.Max(0, hp - damage);
            Emit(MatchEventType.CrownDamaged, side: s, slot: u, targetSide: targetSide, amount: hp - target.Crown, attempted: damage,
                before: hp, after: target.Crown, targetIsCrown: true, note: note);
        }

        private void BuildBarrier(int s, int u, int amount)
        {
            var side = _sides[s];
            int before = side.Barrier;
            side.Barrier = System.Math.Min(RulesConstants.MaxBarrier, before + amount);
            Emit(MatchEventType.BarrierBuilt, side: s, slot: u, amount: side.Barrier - before, attempted: amount, before: before, after: side.Barrier,
                note: "unit build");
        }

        private void AddEnergy(int s, int u, int amount, EnergySource source, string note)
        {
            var unit = _sides[s].Units[u];
            int before = unit.Energy;
            unit.Energy = System.Math.Min(unit.Cost, before + amount);
            if (source == EnergySource.Priest && unit.Ready) unit.ReadySource = EnergySource.Priest;
            Emit(MatchEventType.EnergyGranted, side: s, slot: u, amount: unit.Energy - before, attempted: amount, before: before, after: unit.Energy,
                source: source, note: note);
        }

        // ----------------------------------------------------------------- XP, rank, bombs

        /// <summary>One grant crosses at most one threshold; excess XP is discarded (RULES_SPEC 7.2, O-07).</summary>
        private void GrantXp(int s, int u, int amount, MatchEventType type)
        {
            var unit = _sides[s].Units[u];
            int before = unit.Xp;
            unit.Xp = System.Math.Min(RulesConstants.XpThreshold, before + amount);
            Emit(type, side: s, slot: u, amount: unit.Xp - before, attempted: amount, before: before, after: unit.Xp);
            if (unit.Xp < RulesConstants.XpThreshold) return;

            if (unit.Rank == Rank.Gold)
            {
                unit.Xp = 0;
                var bomb = new QueuedBomb { Side = s, Slot = u, Order = _bombOrder++ };
                if (_stage <= 6) _earlyBombs.Add(bomb); else _lateBombs.Add(bomb);
                Emit(MatchEventType.BombQueued, side: s, slot: u, before: RulesConstants.XpThreshold, after: 0,
                    note: _stage <= 6 ? "early bomb" : "late bomb");
                return;
            }

            var oldRank = unit.Rank;
            int energyBefore = unit.Energy;
            unit.Rank = oldRank + 1;
            unit.Xp = 0;
            // O-02: preserve absolute stored energy, clamp to the new cost.
            unit.Energy = System.Math.Min(unit.Energy, unit.Cost);
            Emit(MatchEventType.UnitRankedUp, side: s, slot: u, before: (int)oldRank, after: (int)unit.Rank,
                amount: unit.Energy - energyBefore, note: oldRank + "->" + unit.Rank);
        }

        private void ResolveBombs(List<QueuedBomb> bombs)
        {
            // Player side first, then opponent; Channel A before B; then queue order.
            bombs.Sort((a, b) =>
            {
                if (a.Side != b.Side) return a.Side.CompareTo(b.Side);
                if (a.Slot != b.Slot) return a.Slot.CompareTo(b.Slot);
                return a.Order.CompareTo(b.Order);
            });
            foreach (var bomb in bombs)
            {
                int opp = 1 - bomb.Side;
                Emit(MatchEventType.BombLaunched, side: bomb.Side, slot: bomb.Slot, targetSide: opp, height: 0, targetIsCrown: true,
                    before: _sides[opp].Crown, after: _sides[opp].Crown);
                DamageCrownDirect(bomb.Side, bomb.Slot, opp, RulesConstants.BombDamage, "bomb");
            }
            bombs.Clear();
        }
    }
}
