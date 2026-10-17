using System;
using System.Collections;
using UnityEngine;
using AEM.DigitalTwin.Telemetry;

namespace AEM.DigitalTwin
{
    public class MachineController : MonoBehaviour
    {
        public CncMachineLink telemetryLink;
        public const float AmbientTemperature = 25f;
        public const float OverheatThreshold = 80f;
        public Transform bedPoint;
        public Transform tool;
        public Transform spindleSlide;
        public Renderer signalLamp;
        public Light signalLight;
        public Material readyMaterial;
        public Material workingMaterial;
        public Material alarmMaterial;
        public float machiningSeconds = 7f;
        public float spindleRpm = 240f;

        public float Temperature { get; private set; } = AmbientTemperature;
        public float Vibration { get; private set; }
        public float Progress { get; private set; }
        public bool IsProcessing { get; private set; }
        public bool IsOverheated { get; private set; }
        public bool IsOverheatRequested { get; private set; }
        public bool IsCooling { get; private set; }
        public int ResetCount { get; private set; }
        public event Action StateChanged;

        private float elapsed;
        private int lampState = -1;
        private Vector3 spindleHome;

        private void Awake()
        {
            spindleHome = spindleSlide.localPosition;
            UpdateSignal();
        }

        public IEnumerator Process(Workpiece part)
        {
            elapsed = 0;
            Progress = 0;
            IsProcessing = true;
            Vibration = 1.55f;
            while (elapsed < machiningSeconds)
            {
                // A requested fault must reach the threshold even at the end of a cut.
                if (!IsOverheated && !IsCooling && !IsOverheatRequested && (telemetryLink == null || telemetryLink.CanProcess))
                {
                    elapsed = Mathf.Min(machiningSeconds, elapsed + Time.deltaTime);
                    Progress = elapsed / machiningSeconds;
                }
                yield return null;
            }
            part.MarkFinished();
            IsProcessing = false;
            Vibration = 0;
            spindleSlide.localPosition = spindleHome;
            UpdateSignal();
        }

        public void SimulateOverheat()
        {
            if (telemetryLink != null || !IsProcessing || IsOverheated || IsCooling) return;
            IsOverheatRequested = true;
            StateChanged?.Invoke();
        }

        public void ResetAlarm()
        {
            if (!IsOverheated || IsCooling) return;
            IsOverheatRequested = false;
            IsCooling = true;
            ResetCount++;
            StateChanged?.Invoke();
        }

        private void Update()
        {
            if (telemetryLink != null)
            {
                // The remote schema does not contain temperature or vibration.
                // These local model values are not shown as measured telemetry.
                Vibration = 0;
                if (telemetryLink.CanProcess)
                    tool.Rotate(Vector3.up, (float)telemetryLink.client.Latest.spindle.speed * 6f * Time.deltaTime, Space.Self);
                UpdateSignal();
                return;
            }
            float dt = Time.deltaTime;
            if (IsCooling)
            {
                Temperature = Mathf.MoveTowards(Temperature, AmbientTemperature, 22f * dt);
                if (Temperature <= AmbientTemperature + 0.01f)
                {
                    IsCooling = false;
                    IsOverheated = false;
                    StateChanged?.Invoke();
                }
            }
            else if (IsOverheated)
            {
                Temperature = Mathf.MoveTowards(Temperature, 88f, 2f * dt);
            }
            else if (IsProcessing)
            {
                Temperature = IsOverheatRequested
                    ? Mathf.MoveTowards(Temperature, 92f, 17f * dt)
                    : Mathf.Lerp(AmbientTemperature, 68f, Progress);
                if (Temperature > OverheatThreshold)
                {
                    IsOverheated = true;
                    StateChanged?.Invoke();
                }
            }
            else
            {
                Temperature = Mathf.MoveTowards(Temperature, AmbientTemperature, 9f * dt);
            }

            bool cutting = IsProcessing && !IsOverheated && !IsCooling;
            Vibration = cutting ? 1.55f + 0.35f * Mathf.Sin(Time.time * 6.5f) : 0;
            if (cutting)
            {
                tool.Rotate(Vector3.up, spindleRpm * 6f * dt, Space.Self);
                spindleSlide.localPosition = spindleHome + new Vector3(0.11f * Mathf.Sin(elapsed * 2.3f), -0.42f, 0);
            }
            UpdateSignal();
        }

        private void UpdateSignal()
        {
            int state = telemetryLink != null ? telemetryLink.SignalState : IsOverheated ? 2 : IsProcessing ? 1 : 0;
            if (state == lampState) return;
            lampState = state;
            signalLamp.sharedMaterial = state == 2 ? alarmMaterial : state == 1 ? workingMaterial : readyMaterial;
            signalLight.color = state == 2 ? Color.red : state == 1 ? new Color(1f, 0.65f, 0.06f) : Color.green;
        }
    }
}
