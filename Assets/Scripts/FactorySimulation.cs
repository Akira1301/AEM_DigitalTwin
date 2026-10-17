using System.Collections;
using UnityEngine;
using AEM.DigitalTwin.Telemetry;

namespace AEM.DigitalTwin
{
    public class FactorySimulation : MonoBehaviour
    {
        public FactoryProcessGate processGate;
        public ConveyorController conveyor;
        public ManipulatorController manipulator;
        public MachineController machine;
        public AGVController agv;
        public DigitalTwinController twin;
        public Workpiece workpiecePrefab;
        public Transform[] storageSlots;
        public float cyclePause = 2f;
        public Workpiece ActivePart { get; private set; }
        private readonly Workpiece[] storedParts = new Workpiece[12];

        private IEnumerator Start()
        {
            while (true)
            {
                twin.SetStage(FactoryStage.Waiting);
                yield return FactoryMotion.Delay(cyclePause, processGate);
                ActivePart = Instantiate(workpiecePrefab, conveyor.spawnPoint.position, Quaternion.identity);
                ActivePart.name = "Изделие " + twin.productId;
                twin.SetStage(FactoryStage.Feeding);
                yield return conveyor.Feed(ActivePart);
                twin.SetStage(FactoryStage.Loading);
                yield return manipulator.Transfer(ActivePart, machine.bedPoint);
                twin.SetStage(FactoryStage.Machining);
                yield return machine.Process(ActivePart);
                twin.SetStage(FactoryStage.Unloading);
                yield return manipulator.Transfer(ActivePart, agv.cargoPoint);
                agv.Load(ActivePart);
                twin.SetStage(FactoryStage.Transporting);
                int slot = twin.CompletedParts % storageSlots.Length;
                if (storedParts[slot] != null) Destroy(storedParts[slot].gameObject);
                yield return agv.Deliver(storageSlots[slot]);
                storedParts[slot] = ActivePart;
                ActivePart = null;
                twin.RegisterFinishedPart();
                twin.SetStage(FactoryStage.Finished);
                yield return FactoryMotion.Delay(cyclePause, processGate);
                yield return agv.ReturnToStation();
            }
        }
    }
}
