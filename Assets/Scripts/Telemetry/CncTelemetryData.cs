using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace AEM.DigitalTwin.Telemetry
{
    [Serializable]
    public sealed class CncEnvelope
    {
        [JsonProperty(Required = Required.Always)] public CncMachineData cnc_machine;
    }

    [Serializable]
    public sealed class CncMachineData
    {
        [JsonProperty(Required = Required.Always)] public string machine_id;
        [JsonProperty(Required = Required.Always)] public string model;
        [JsonProperty(Required = Required.Always)] public CncAxes axes;
        [JsonProperty(Required = Required.Always)] public CncSpindle spindle;
        [JsonProperty(Required = Required.Always)] public CncToolChanger tool_changer;
        [JsonProperty(Required = Required.Always)] public CncZero workpiece_zero;
        [JsonProperty(Required = Required.Always)] public string timestamp;
        [JsonProperty(Required = Required.Always)] public string status;
        [JsonIgnore] public DateTimeOffset SourceTime => DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        [JsonIgnore] public bool AxesInRange => axes.x.InRange && axes.y.InRange && axes.z.InRange && axes.a.InRange && axes.c.InRange;
    }

    [Serializable]
    public sealed class CncAxes
    {
        [JsonProperty(Required = Required.Always)] public CncAxis x;
        [JsonProperty(Required = Required.Always)] public CncAxis y;
        [JsonProperty(Required = Required.Always)] public CncAxis z;
        [JsonProperty(Required = Required.Always)] public CncAxis a;
        [JsonProperty(Required = Required.Always)] public CncAxis c;
        public IEnumerable<CncAxis> All() { yield return x; yield return y; yield return z; yield return a; yield return c; }
    }

    [Serializable]
    public sealed class CncAxis
    {
        [JsonProperty(Required = Required.Always)] public double min_position;
        [JsonProperty(Required = Required.Always)] public double max_position;
        [JsonProperty(Required = Required.Always)] public double current_position;
        [JsonProperty(Required = Required.Always)] public string units;
        [JsonIgnore] public bool InRange => current_position >= min_position && current_position <= max_position;
        [JsonIgnore] public float Normalized => (float)Math.Max(0, Math.Min(1, (current_position - min_position) / (max_position - min_position)));
    }

    [Serializable]
    public sealed class CncSpindle
    {
        [JsonProperty(Required = Required.Always)] public double position;
        [JsonProperty(Required = Required.Always)] public double speed;
        [JsonProperty(Required = Required.Always)] public string units;
    }

    [Serializable]
    public sealed class CncToolChanger
    {
        [JsonProperty(Required = Required.Always)] public int current_tool;
        [JsonProperty(Required = Required.Always)] public string position;
    }

    [Serializable]
    public sealed class CncZero
    {
        [JsonProperty(Required = Required.Always)] public double x;
        [JsonProperty(Required = Required.Always)] public double y;
        [JsonProperty(Required = Required.Always)] public double z;
    }

    public static class CncTelemetryParser
    {
        public static CncMachineData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 262144)
                throw new FormatException("Пустой или слишком большой ответ JSON.");
            var envelope = JsonConvert.DeserializeObject<CncEnvelope>(json, new JsonSerializerSettings
            {
                MaxDepth = 16, DateParseHandling = DateParseHandling.None,
                TypeNameHandling = TypeNameHandling.None, CheckAdditionalContent = true
            });
            var data = envelope?.cnc_machine ?? throw new FormatException("Нет объекта cnc_machine.");
            if (string.IsNullOrWhiteSpace(data.machine_id) || string.IsNullOrWhiteSpace(data.model)
                || string.IsNullOrWhiteSpace(data.status)) throw new FormatException("Не заполнены идентификатор, модель или статус.");
            foreach (var axis in data.axes.All())
            {
                if (!Finite(axis.min_position) || !Finite(axis.max_position) || !Finite(axis.current_position)
                    || axis.max_position <= axis.min_position) throw new FormatException("Некорректные границы оси.");
            }
            if (data.axes.x.units != "mm" || data.axes.y.units != "mm" || data.axes.z.units != "mm"
                || data.axes.a.units != "degrees" || data.axes.c.units != "degrees" || data.spindle.units != "rpm")
                throw new FormatException("Неизвестные единицы измерения.");
            if (!Finite(data.spindle.position) || !Finite(data.spindle.speed) || data.spindle.speed < 0 || data.spindle.speed > 100000
                || !Finite(data.workpiece_zero.x) || !Finite(data.workpiece_zero.y) || !Finite(data.workpiece_zero.z)
                || data.tool_changer.current_tool < 0 || string.IsNullOrWhiteSpace(data.tool_changer.position))
                throw new FormatException("Некорректные параметры шпинделя, инструмента или нуля детали.");
            if (!DateTimeOffset.TryParse(data.timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _)
                || !(data.timestamp.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                    || System.Text.RegularExpressions.Regex.IsMatch(data.timestamp, @"[+-]\d{2}:\d{2}$")))
                throw new FormatException("Некорректная метка времени или отсутствует часовой пояс.");
            return data;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
