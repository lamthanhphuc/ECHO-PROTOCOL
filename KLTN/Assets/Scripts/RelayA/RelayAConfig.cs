using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.RelayA
{
    [CreateAssetMenu(menuName = "ECHO Protocol/Relay A/Power Routing Config", fileName = "RelayAConfig")]
    public sealed partial class RelayAConfig : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float circuitTestSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float circuitFaultDelaySeconds = 1.2f;
        [SerializeField] private RelayACircuitScenario[] circuitScenarios;
        [SerializeField] private int defaultBoardRevision;
        private const int CurrentBoardRevision = 3;

        public float CircuitTestSeconds => circuitTestSeconds;
        public float CircuitFaultDelaySeconds => circuitFaultDelaySeconds;
        public IReadOnlyList<RelayACircuitScenario> CircuitScenarios
        {
            get { InitializeDefaultCircuitScenariosIfEmpty(); return circuitScenarios; }
        }

        public RelayACircuitScenario GetCircuitScenario(int index)
        {
            InitializeDefaultCircuitScenariosIfEmpty();
            return circuitScenarios[Mathf.Clamp(index, 0, circuitScenarios.Length - 1)];
        }

        public void InitializeDefaultCircuitScenariosIfEmpty()
        {
            bool legacyDefaults = circuitScenarios != null && circuitScenarios.Length == 4;
            string[] names = { "A1 / Split Feed", "A1 / Return Feed", "A2 / Triple Feed", "A2 / Return Feed" };
            if (legacyDefaults)
                for (int i = 0; i < names.Length; i++)
                    if (circuitScenarios[i] == null || circuitScenarios[i].Name != names[i]) legacyDefaults = false;
            if (circuitScenarios == null || circuitScenarios.Length == 0
                || (legacyDefaults && defaultBoardRevision < CurrentBoardRevision))
            {
                circuitScenarios = RelayACircuitScenarios.CreateDefaults();
                defaultBoardRevision = CurrentBoardRevision;
            }
        }

        private void OnValidate()
        {
            ValidateStabilization();
            circuitTestSeconds = Mathf.Max(0.1f, circuitTestSeconds);
            circuitFaultDelaySeconds = Mathf.Max(0.1f, circuitFaultDelaySeconds);
            InitializeDefaultCircuitScenariosIfEmpty();
        }
    }
}
