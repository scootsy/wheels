using System;
using System.Collections.Generic;

namespace Tabletop.Domain
{
    /// <summary>
    /// Drives one AI side through a spin phase using only the legal command surface.
    /// It sees the match only through <paramref name="dispatch"/> results for its own commands,
    /// never through snapshots, so it cannot read the other side's current-round faces or locks.
    /// </summary>
    public static class AiTurnRunner
    {
        public static List<MatchCommand> PlayTurn(IAiPolicy policy, AiDecisionInput input, Func<MatchCommand, CommandResult> dispatch)
        {
            var issued = new List<MatchCommand>();
            var self = input.Self;
            int[] faces = new int[ReelSetDefinition.ReelCount];
            bool[] locks = new bool[ReelSetDefinition.ReelCount];
            int spinsUsed = 0;

            CommandResult Send(MatchCommand c)
            {
                var result = dispatch(c);
                if (!result.Accepted) throw new InvalidOperationException("AI issued an illegal command " + c + ": " + result.Rejection);
                issued.Add(c);
                return result;
            }

            bool committed = false;
            while (!committed)
            {
                var spin = Send(MatchCommand.Spin(self));
                spinsUsed++;
                foreach (var e in spin.Events)
                {
                    if (e.Side != (int)self) continue;
                    if (e.Type == MatchEventType.ReelsSpun) for (int r = 0; r < faces.Length; r++) faces[r] = e.Faces[r];
                    if (e.Type == MatchEventType.SpinFinalized) committed = true;
                }
                if (committed) break;

                var desired = policy.ChooseLocks(input, (int[])faces.Clone(), spinsUsed);
                int lockedCount = 0;
                for (int r = 0; r < locks.Length; r++)
                {
                    if (desired[r] != locks[r])
                    {
                        Send(MatchCommand.SetReelLock(self, r, desired[r]));
                        locks[r] = desired[r];
                    }
                    if (locks[r]) lockedCount++;
                }
                if (lockedCount == locks.Length)
                {
                    Send(MatchCommand.FinalizeSpin(self));
                    committed = true;
                }
            }
            return issued;
        }
    }
}
