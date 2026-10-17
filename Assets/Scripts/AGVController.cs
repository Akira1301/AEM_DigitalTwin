using System.Collections;
using UnityEngine;
using AEM.DigitalTwin.Telemetry;

namespace AEM.DigitalTwin
{
    public class AGVController : MonoBehaviour
    {
        public FactoryProcessGate processGate;
        public Transform loadingStation;
        public Transform deliveryStation;
        public Transform cargoPoint;
        public Transform[] wheels;
        public float speed = 1.65f;
        public bool IsMoving { get; private set; }
        public Workpiece Cargo { get; private set; }

        public void Load(Workpiece part)
        {
            Cargo = part;
            part.transform.SetParent(cargoPoint, false);
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
        }

        public IEnumerator Deliver(Transform storagePoint)
        {
            yield return DriveTo(deliveryStation.position);
            yield return FactoryMotion.Delay(0.7f, processGate);
            // Simplified automatic offload to a finished-goods pallet.
            yield return FactoryMotion.Move(Cargo.transform, storagePoint.position, 1.2f, processGate);
            Cargo.transform.SetParent(storagePoint, true);
            Cargo = null;
        }

        public IEnumerator ReturnToStation() => DriveTo(loadingStation.position);

        private IEnumerator DriveTo(Vector3 destination)
        {
            IsMoving = true;
            yield return FactoryMotion.Move(transform, destination, speed, processGate);
            IsMoving = false;
        }

        private void Update()
        {
            if (!IsMoving || (processGate != null && !processGate.CanRun)) return;
            foreach (Transform wheel in wheels)
                wheel.Rotate(Vector3.up, speed * 230f * Time.deltaTime, Space.Self);
        }

        private void OnDisable() => IsMoving = false;
    }
}
