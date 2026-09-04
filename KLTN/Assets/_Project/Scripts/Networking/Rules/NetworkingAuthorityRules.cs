using Fusion;

namespace EchoProtocol.Networking
{
    public enum NetworkItemState
    {
        Available = 0,
        Carried = 1,
        Dropped = 2,
        Placed = 3,
    }

    public enum NetworkPowerPuzzleState
    {
        Idle = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3,
        Resetting = 4,
    }

    public enum PowerPuzzleInputResult
    {
        AcceptedCorrect = 0,
        AcceptedIncorrect = 1,
        RejectedInvalidState = 2,
        RejectedInvalidInput = 3,
        AlreadyCompleted = 4,
    }

    public static class EnergyCoreAuthorityRules
    {
        public static bool CanPickup(
            NetworkItemState state,
            PlayerRef holder,
            bool playerExists,
            bool playerAlreadyCarriesCore)
        {
            return playerExists
                && !playerAlreadyCarriesCore
                && !holder.IsValid
                && (state == NetworkItemState.Available || state == NetworkItemState.Dropped);
        }

        public static bool CanDrop(NetworkItemState state, PlayerRef holder, PlayerRef requester)
        {
            return requester.IsRealPlayer
                && state == NetworkItemState.Carried
                && holder == requester;
        }

        public static bool CanPlace(NetworkItemState state, PlayerRef holder, PlayerRef requester)
        {
            return CanDrop(state, holder, requester);
        }
    }

    public static class EnergyCoreObjectiveRules
    {
        public static bool CanRegisterPlacement(int placedCoreCount, int requiredCoreCount)
        {
            return requiredCoreCount > 0
                && placedCoreCount >= 0
                && placedCoreCount < requiredCoreCount;
        }
    }

    public static class PowerPuzzleAuthorityRules
    {
        public static PowerPuzzleInputResult EvaluateInput(
            NetworkPowerPuzzleState state,
            int inputId,
            int stationCount,
            int expectedInputId)
        {
            if (state == NetworkPowerPuzzleState.Completed)
            {
                return PowerPuzzleInputResult.AlreadyCompleted;
            }

            if (state != NetworkPowerPuzzleState.InProgress)
            {
                return PowerPuzzleInputResult.RejectedInvalidState;
            }

            if (inputId < 0
                || inputId >= stationCount
                || expectedInputId < 0
                || expectedInputId >= stationCount)
            {
                return PowerPuzzleInputResult.RejectedInvalidInput;
            }

            return inputId == expectedInputId
                ? PowerPuzzleInputResult.AcceptedCorrect
                : PowerPuzzleInputResult.AcceptedIncorrect;
        }
    }
}
