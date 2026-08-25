namespace EchoProtocol.AI.Stalker
{
    public sealed class StalkerBlackboard
    {
        // Serialized FSM state remains on StalkerController; this keeps lightweight runtime spatial context.
        public int CurrentSpatialNodeId { get; set; } = -1;
        public int DestinationSpatialNodeId { get; set; } = -1;
        public int PreviousSpatialNodeId { get; set; } = -1;
    }
}
