using Fusion;

namespace EchoProtocol.RelayB
{
    public struct RelayBDecodeTelemetry : INetworkStruct
    {
        public int Round, Attempts, Current, Feedback;
        public int Code0, Code1, Code2, Code3, Code4;
        public int Result0, Result1, Result2, Result3, Result4;
        public byte Phase;
        public float Elapsed;

        public static RelayBDecodeTelemetry From(RelayBDecodeSnapshot state) => new RelayBDecodeTelemetry
        {
            Round = state.Round, Attempts = state.Attempts, Current = state.Current, Feedback = state.Feedback,
            Phase = (byte)state.Phase, Elapsed = state.Elapsed,
            Code0 = state.Codes[0], Code1 = state.Codes[1], Code2 = state.Codes[2], Code3 = state.Codes[3], Code4 = state.Codes[4],
            Result0 = state.Results[0], Result1 = state.Results[1], Result2 = state.Results[2], Result3 = state.Results[3], Result4 = state.Results[4]
        };
        public RelayBDecodeSnapshot Snapshot => new RelayBDecodeSnapshot(Round, Attempts, Current, Feedback,
            (RelayBDecodePhase)Phase, Elapsed, new[] { Code0, Code1, Code2, Code3, Code4 },
            new[] { Result0, Result1, Result2, Result3, Result4 });
    }
}
