using System;

namespace EchoProtocol.AI.Common
{
    public static class DeterministicTieBreak
    {
        public static int ComparePrimaryThenStableKey<TStableKey>(
            int primaryComparison,
            TStableKey leftStableKey,
            TStableKey rightStableKey)
            where TStableKey : struct, IComparable<TStableKey>
        {
            return primaryComparison != 0
                ? primaryComparison
                : leftStableKey.CompareTo(rightStableKey);
        }
    }
}
