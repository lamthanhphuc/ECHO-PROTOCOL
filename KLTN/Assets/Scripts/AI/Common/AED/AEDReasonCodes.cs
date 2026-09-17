namespace EchoProtocol.AI.Common.AED
{
    public static class AEDReasonCodes
    {
        public const string AdaptiveApplied =
            "ADAPTIVE_APPLIED";

        public const string AdaptiveNoChange =
            "ADAPTIVE_NO_CHANGE";

        public const string FixedFallback =
            "FIXED_FALLBACK";

        public const string InputIncomplete =
            "INPUT_INCOMPLETE";

        public const string InputInvalid =
            "INPUT_INVALID";

        public const string StaleInput =
            "STALE_INPUT";

        public const string AedUnavailable =
            "AED_UNAVAILABLE";

        public const string AedTimeout =
            "AED_TIMEOUT";

        public const string PolicyConfigInvalid =
            "POLICY_CONFIG_INVALID";

        public const string PolicyKeyNotActive =
            "POLICY_KEY_NOT_ACTIVE";

        public const string ParameterRegistryInvalid =
            "PARAMETER_REGISTRY_INVALID";

        public const string RegisteredValueRejected =
            "REGISTERED_VALUE_REJECTED";

        public const string BoundRejected =
            "BOUND_REJECTED";

        public const string TimingRejected =
            "TIMING_REJECTED";

        public const string PressureRuleRejected =
            "PRESSURE_RULE_REJECTED";

        public const string ScenarioInvalid =
            "SCENARIO_INVALID";

        public const string RouteInvalid =
            "ROUTE_INVALID";

        public const string SpawnInvalid =
            "SPAWN_INVALID";

        public const string UnsupportedVersion =
            "UNSUPPORTED_VERSION";

        public const string StaleBaseConfig =
            "STALE_BASE_CONFIG";

        public const string DecisionWindowClosed =
            "DECISION_WINDOW_CLOSED";

        public const string DecisionIdentityConflict =
            "DECISION_IDENTITY_CONFLICT";

        public const string FallbackConfigInvalid =
            "FALLBACK_CONFIG_INVALID";

        public static bool IsControlled(
            string reasonCode)
        {
            switch (reasonCode)
            {
                case AdaptiveApplied:
                case AdaptiveNoChange:
                case FixedFallback:
                case InputIncomplete:
                case InputInvalid:
                case StaleInput:
                case AedUnavailable:
                case AedTimeout:
                case PolicyConfigInvalid:
                case PolicyKeyNotActive:
                case ParameterRegistryInvalid:
                case RegisteredValueRejected:
                case BoundRejected:
                case TimingRejected:
                case PressureRuleRejected:
                case ScenarioInvalid:
                case RouteInvalid:
                case SpawnInvalid:
                case UnsupportedVersion:
                case StaleBaseConfig:
                case DecisionWindowClosed:
                case DecisionIdentityConflict:
                case FallbackConfigInvalid:
                    return true;

                default:
                    return false;
            }
        }
    }

    public static class AEDGuardCodes
    {
        public const string DuplicateDecisionNoOp =
            "DUPLICATE_DECISION_NO_OP";
    }
}
