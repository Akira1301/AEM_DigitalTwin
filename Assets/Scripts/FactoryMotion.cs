using System.Collections;
using UnityEngine;
using AEM.DigitalTwin.Telemetry;

namespace AEM.DigitalTwin
{
    public static class FactoryMotion
    {
        public static IEnumerator Move(Transform target, Vector3 destination, float speed, FactoryProcessGate gate = null)
        {
            while ((target.position - destination).sqrMagnitude > 0.00001f)
            {
                if (gate == null || gate.CanRun)
                    target.position = Vector3.MoveTowards(target.position, destination, speed * Time.deltaTime);
                yield return null;
            }
            target.position = destination;
        }

        public static IEnumerator Delay(float seconds, FactoryProcessGate gate = null)
        {
            float elapsed = 0;
            while (elapsed < seconds || (gate != null && !gate.CanRun))
            {
                if (gate == null || gate.CanRun) elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
