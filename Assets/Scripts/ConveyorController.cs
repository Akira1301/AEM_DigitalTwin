using System.Collections;
using UnityEngine;
using AEM.DigitalTwin.Telemetry;

namespace AEM.DigitalTwin
{
    public class ConveyorController : MonoBehaviour
    {
        public FactoryProcessGate processGate;
        public Transform spawnPoint;
        public Transform pickupPoint;
        public Transform[] rollers;
        public float feedSpeed = 1.05f;
        public bool IsRunning { get; private set; }

        public IEnumerator Feed(Workpiece workpiece)
        {
            workpiece.transform.position = spawnPoint.position;
            IsRunning = true;
            yield return FactoryMotion.Move(workpiece.transform, pickupPoint.position, feedSpeed, processGate);
            IsRunning = false;
        }

        private void Update()
        {
            if (!IsRunning || (processGate != null && !processGate.CanRun)) return;
            foreach (Transform roller in rollers)
                roller.Rotate(Vector3.up, -feedSpeed * 480f * Time.deltaTime, Space.Self);
        }

        private void OnDisable() => IsRunning = false;
    }
}
