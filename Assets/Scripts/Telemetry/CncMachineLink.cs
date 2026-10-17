using System;
using UnityEngine;

namespace AEM.DigitalTwin.Telemetry
{
    [DefaultExecutionOrder(-200)]
    public sealed class CncMachineLink : MonoBehaviour
    {
        public CncTelemetryClient client;
        public MachineController machine;
        public Transform rotaryTable;
        public Transform zeroMarker;
        public Renderer toolMarker;
        public bool CanProcess => client.isActiveAndEnabled && client.ConnectionHealthy && !client.IsTransportStale
            && !client.IsSourceStale && client.Latest != null && client.Latest.AxesInRange
            && string.Equals(client.Latest.status, "running", StringComparison.OrdinalIgnoreCase)
            && client.Latest.spindle.speed > 0 && string.Equals(client.Latest.tool_changer.position, "ready", StringComparison.OrdinalIgnoreCase);
        public bool HasFault => !string.IsNullOrEmpty(client.LastError) || client.IsSourceStale || client.IsTransportStale
            || (client.Latest != null && (!client.Latest.AxesInRange
                || string.Equals(client.Latest.status, "alarm", StringComparison.OrdinalIgnoreCase)
                || string.Equals(client.Latest.status, "error", StringComparison.OrdinalIgnoreCase)));
        public int SignalState => HasFault ? 2 : CanProcess ? 1 : 0;
        public string State => client.Latest == null ? (string.IsNullOrEmpty(client.LastError) ? "ОЖИДАНИЕ ДАННЫХ" : "ОШИБКА СВЯЗИ")
            : !client.ConnectionHealthy ? "ОШИБКА СВЯЗИ"
            : !client.Latest.AxesInRange ? "ВЫХОД ЗА ПРЕДЕЛЫ"
            : client.IsSourceStale || client.IsTransportStale ? "УСТАРЕВШИЕ ДАННЫЕ"
            : CanProcess ? "РАБОТА РАЗРЕШЕНА" : "ОЖИДАНИЕ СТАНКА";
        private Vector3 spindleHome;
        private Vector3 zeroHome;
        private MaterialPropertyBlock toolProperties;

        private void Awake()
        {
            spindleHome = machine.spindleSlide.localPosition;
            zeroHome = zeroMarker.localPosition;
            toolProperties = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            var data = client.Latest;
            // Keep the last observed pose on a transport failure, but stop all motion.
            if (data == null || !client.ConnectionHealthy || client.IsTransportStale) return;
            Vector3 target = spindleHome + new Vector3(Mathf.Lerp(-.4f, .4f, data.axes.x.Normalized),
                Mathf.Lerp(-.4f, .1f, data.axes.z.Normalized), Mathf.Lerp(-.3f, .3f, data.axes.y.Normalized));
            machine.spindleSlide.localPosition = Vector3.MoveTowards(machine.spindleSlide.localPosition, target, Time.deltaTime * 1.5f);
            // A and C are angles in degrees. Clamp the tilt of this simplified fixture.
            rotaryTable.localRotation = Quaternion.Euler((float)Math.Max(-25, Math.Min(25, data.axes.a.current_position)),
                (float)(data.axes.c.current_position % 360), 0);
            zeroMarker.localPosition = zeroHome + new Vector3((float)Math.Max(-.5, Math.Min(.5, data.workpiece_zero.x / 1000)),
                (float)Math.Max(-.1, Math.Min(.1, data.workpiece_zero.z / 1000)),
                (float)Math.Max(-.5, Math.Min(.5, data.workpiece_zero.y / 1000)));
            toolProperties.SetColor("_BaseColor", Color.HSVToRGB((data.tool_changer.current_tool * .13f) % 1, .7f, 1));
            toolMarker.SetPropertyBlock(toolProperties);
        }
    }
}
