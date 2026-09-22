using System;
using System.Globalization;

namespace Tabletop.Domain
{
    public enum MatchPhase
    {
        Setup = 0,
        /// <summary>Both sides may spin/lock until each has committed (finalized).</summary>
        Spinning = 1,
        /// <summary>Both sides committed; ResolveRound is the only legal command.</summary>
        AwaitingResolution = 2,
        Ended = 3,
    }

    public enum CommandType { Spin = 0, SetReelLock = 1, FinalizeSpin = 2, ResolveRound = 3 }

    /// <summary>Immutable match command. StartMatch is the Match constructor (config + seed).</summary>
    public sealed class MatchCommand
    {
        private MatchCommand(CommandType type, SideId side, int reelIndex, bool locked)
        {
            Type = type;
            Side = side;
            ReelIndex = reelIndex;
            Locked = locked;
        }

        public CommandType Type { get; }
        public SideId Side { get; }
        public int ReelIndex { get; }
        public bool Locked { get; }

        public static MatchCommand Spin(SideId side) => new MatchCommand(CommandType.Spin, side, -1, false);
        public static MatchCommand SetReelLock(SideId side, int reelIndex, bool locked) => new MatchCommand(CommandType.SetReelLock, side, reelIndex, locked);
        public static MatchCommand FinalizeSpin(SideId side) => new MatchCommand(CommandType.FinalizeSpin, side, -1, false);
        public static MatchCommand ResolveRound() => new MatchCommand(CommandType.ResolveRound, SideId.Player, -1, false);

        /// <summary>Compact text token: S0, L0 3 1 => "L031", F1, R.</summary>
        public string Encode()
        {
            switch (Type)
            {
                case CommandType.Spin: return "S" + (int)Side;
                case CommandType.SetReelLock: return "L" + (int)Side + ReelIndex.ToString(CultureInfo.InvariantCulture) + (Locked ? "1" : "0");
                case CommandType.FinalizeSpin: return "F" + (int)Side;
                default: return "R";
            }
        }

        public static MatchCommand Decode(string token)
        {
            if (string.IsNullOrEmpty(token)) throw new FormatException("Empty command token.");
            switch (token[0])
            {
                case 'S': return Spin((SideId)(token[1] - '0'));
                case 'L': return SetReelLock((SideId)(token[1] - '0'), token[2] - '0', token[3] == '1');
                case 'F': return FinalizeSpin((SideId)(token[1] - '0'));
                case 'R': return ResolveRound();
                default: throw new FormatException("Unknown command token " + token);
            }
        }

        public override string ToString() => Encode();
    }

    public enum RejectionCode
    {
        WrongPhase,
        SideNotControlled,
        FirstSpinRequired,
        NoSpinsRemaining,
        InvalidReelIndex,
        ReelAlreadyInState,
        MatchAlreadyEnded,
        UnitSelectionIncomplete,
        DuplicateUnitNotAllowed,
        ConfigInvalid,
        SideAlreadyCommitted,
        AllReelsLocked,
        NotAllReelsLocked,
        SidesNotCommitted,
        InvalidSide,
    }

    public sealed class CommandRejection
    {
        public CommandRejection(int commandId, RejectionCode code, string message, MatchPhase phase)
        {
            CommandId = commandId;
            Code = code;
            Message = message;
            CurrentPhase = phase;
        }

        public int CommandId { get; }
        public RejectionCode Code { get; }
        /// <summary>Concise player-facing text (English placeholder for a localization key).</summary>
        public string Message { get; }
        public MatchPhase CurrentPhase { get; }

        /// <summary>Spec reason code, e.g. WRONG_PHASE.</summary>
        public string ReasonCode
        {
            get
            {
                var name = Code.ToString();
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < name.Length; i++)
                {
                    if (i > 0 && char.IsUpper(name[i])) sb.Append('_');
                    sb.Append(char.ToUpperInvariant(name[i]));
                }
                return sb.ToString();
            }
        }

        public override string ToString() => ReasonCode + ": " + Message;
    }
}
