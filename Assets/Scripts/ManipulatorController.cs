using System.Collections;
using UnityEngine;
using AEM.DigitalTwin.Telemetry;

namespace AEM.DigitalTwin
{
    public class ManipulatorController : MonoBehaviour
    {
        public FactoryProcessGate processGate;
        public Transform bridge;
        public Transform trolley;
        public Transform gripper;
        public Transform telescopicColumn;
        public Transform leftJaw;
        public Transform rightJaw;
        public float travelSpeed = 2.7f;
        public float liftSpeed = 1.8f;
        public float clearanceHeight = 3.45f;
        public float railHeight = 4.35f;
        private bool closed;

        public IEnumerator Transfer(Workpiece part, Transform destination)
        {
            SetClosed(false);
            yield return MoveAtHeight(clearanceHeight);
            yield return MoveAcross(part.transform.position);
            yield return MoveAtHeight(part.transform.position.y + 0.28f);
            SetClosed(true);
            yield return FactoryMotion.Delay(0.3f, processGate);
            part.transform.SetParent(gripper, true);
            yield return MoveAtHeight(clearanceHeight);
            yield return MoveAcross(destination.position);
            yield return MoveAtHeight(destination.position.y + 0.28f);
            part.transform.SetParent(null, true);
            part.transform.SetPositionAndRotation(destination.position, Quaternion.identity);
            SetClosed(false);
            yield return FactoryMotion.Delay(0.25f, processGate);
            yield return MoveAtHeight(clearanceHeight);
        }

        private IEnumerator MoveAcross(Vector3 destination)
        {
            destination.y = gripper.position.y;
            yield return FactoryMotion.Move(gripper, destination, travelSpeed, processGate);
        }

        private IEnumerator MoveAtHeight(float height)
        {
            Vector3 target = gripper.position;
            target.y = height;
            yield return FactoryMotion.Move(gripper, target, liftSpeed, processGate);
        }

        private void SetClosed(bool value) => closed = value;

        private void LateUpdate()
        {
            Vector3 position = gripper.position;
            bridge.position = new Vector3(position.x, railHeight, bridge.position.z);
            trolley.position = new Vector3(position.x, railHeight, position.z);
            float length = Mathf.Max(0.05f, railHeight - position.y);
            telescopicColumn.position = new Vector3(position.x, position.y + length * 0.5f, position.z);
            telescopicColumn.localScale = new Vector3(0.12f, length, 0.12f);
            float jawOffset = closed ? 0.30f : 0.48f;
            leftJaw.localPosition = new Vector3(-jawOffset, -0.16f, 0);
            rightJaw.localPosition = new Vector3(jawOffset, -0.16f, 0);
        }
    }
}
