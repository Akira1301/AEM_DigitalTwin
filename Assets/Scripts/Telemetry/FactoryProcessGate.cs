using UnityEngine;

namespace AEM.DigitalTwin.Telemetry
{
    public sealed class FactoryProcessGate : MonoBehaviour
    {
        public CncMachineLink link;
        public bool CanRun => link != null && link.CanProcess;
    }
}
