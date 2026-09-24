namespace EchoProtocol.Api.Enums;

public enum ScenarioConfigSource
{
    Fixed,
    Adaptive
}

public enum ScenarioContentType
{
    Map,
    Monster,
    ObjectiveSpawnSet,
    RouteModifier
}

public enum ScenarioResolutionMode { Fixed, Adaptive }
public enum AdaptiveSnapshotValidity { Valid, Partial, Invalid }
public enum ScenarioCandidateValidationStatus { NotEvaluated, Valid, Invalid }
public enum ScenarioDecisionStatus { Committed }
