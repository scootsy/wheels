namespace Tabletop.Domain
{
    /// <summary>
    /// Stateless deterministic randomness. Every reel draw is a pure function of
    /// (seed, side, round, spin number, reel index), so draws are reproducible and one
    /// side's choices never change the other side's faces (IMPLEMENTATION DECISION D-013).
    /// </summary>
    public static class ReelRandom
    {
        public static ulong Mix(ulong x)
        {
            // SplitMix64 finalizer.
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }

        public static ulong DrawKey(long seed, int side, int round, int spinNumber, int reelIndex)
        {
            ulong h = Mix((ulong)seed);
            h = Mix(h ^ (ulong)(uint)side);
            h = Mix(h ^ (ulong)(uint)round);
            h = Mix(h ^ (ulong)(uint)spinNumber);
            h = Mix(h ^ (ulong)(uint)reelIndex);
            return h;
        }

        /// <summary>Uniform face index in [0, faceCount). Exact for power-of-two face counts (8).</summary>
        public static int FaceIndex(long seed, int side, int round, int spinNumber, int reelIndex, int faceCount)
        {
            return (int)(DrawKey(seed, side, round, spinNumber, reelIndex) % (ulong)faceCount);
        }

        /// <summary>Derived deterministic seed for auxiliary streams (e.g. AI rollouts, new-match seeds).</summary>
        public static long Derive(long seed, ulong salt) => (long)(Mix((ulong)seed ^ Mix(salt)) & 0x7FFFFFFFUL);
    }
}
