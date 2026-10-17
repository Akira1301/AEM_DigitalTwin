using System;
using System.Collections;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace AEM.DigitalTwin.Telemetry
{
    public enum TelemetrySource { Server, Replay }

    public sealed class CncTelemetryClient : MonoBehaviour
    {
        public string endpoint = "https://karasevv.com/test/mt_data.json";
        [Min(.2f)] public float pollIntervalSeconds = 5;
        [Range(1, 30)] public int timeoutSeconds = 8;
        [Min(1)] public float maxSourceAgeSeconds = 120;
        public TelemetrySource source;
        public CncMachineData Latest { get; private set; }
        public string RawJson { get; private set; } = "";
        public string LastError { get; private set; } = "";
        public DateTimeOffset ReceivedAt { get; private set; }
        public bool IsRequesting { get; private set; }
        public bool ConnectionHealthy { get; private set; }
        public int SuccessfulRequests { get; private set; }
        public int FailedRequests { get; private set; }
        public long LastHttpCode { get; private set; }
        public double LastDurationMs { get; private set; }
        public double SecondsToNextRequest => Math.Max(0, nextRequest - Time.realtimeSinceStartupAsDouble);
        public bool IsSourceStale => Latest != null && ((DateTimeOffset.UtcNow - Latest.SourceTime).TotalSeconds > maxSourceAgeSeconds
            || (Latest.SourceTime - DateTimeOffset.UtcNow).TotalSeconds > 30);
        public bool IsTransportStale => Latest != null && (DateTimeOffset.UtcNow - ReceivedAt).TotalSeconds > Math.Max(15, pollIntervalSeconds * 3 + timeoutSeconds);
        public event Action Changed;
        private double nextRequest;
        private UnityWebRequest activeRequest;
        private int revision;

        private void OnEnable() { nextRequest = 0; StartCoroutine(Poll()); }
        private void OnDisable()
        {
            revision++;
            StopAllCoroutines();
            if (activeRequest != null) { activeRequest.Abort(); activeRequest.Dispose(); activeRequest = null; }
            IsRequesting = false;
            ConnectionHealthy = false;
        }
        public void RefreshNow() => nextRequest = 0;

        public void ToggleSource() => SetSource(source == TelemetrySource.Server ? TelemetrySource.Replay : TelemetrySource.Server);

        public void SetSource(TelemetrySource value)
        {
            revision++;
            source = value;
            Latest = null;
            RawJson = "";
            LastError = "";
            ConnectionHealthy = false;
            nextRequest = 0;
            activeRequest?.Abort();
            Changed?.Invoke();
        }

        private IEnumerator Poll()
        {
            while (true)
            {
                if (Time.realtimeSinceStartupAsDouble >= nextRequest)
                {
                    int requestRevision = revision;
                    double began = Time.realtimeSinceStartupAsDouble;
                    IsRequesting = true;
                    if (source == TelemetrySource.Replay)
                    {
                        AcceptJson(CreateReplayJson(Time.realtimeSinceStartup), 0);
                        yield return null;
                    }
                    else
                    {
                        using (var request = UnityWebRequest.Get(endpoint))
                        {
                            activeRequest = request;
                            request.timeout = timeoutSeconds;
                            request.SetRequestHeader("Accept", "application/json");
                            request.SetRequestHeader("Cache-Control", "no-cache");
                            yield return request.SendWebRequest();
                            if (requestRevision == revision)
                            {
                                LastHttpCode = request.responseCode;
                                if (request.result == UnityWebRequest.Result.Success) AcceptJson(request.downloadHandler.text, request.responseCode);
                                else Reject("Ошибка запроса: " + request.error);
                            }
                            activeRequest = null;
                        }
                    }
                    LastDurationMs = (Time.realtimeSinceStartupAsDouble - began) * 1000;
                    IsRequesting = false;
                    if (requestRevision == revision) nextRequest = Time.realtimeSinceStartupAsDouble + Math.Max(.2f, pollIntervalSeconds);
                    Changed?.Invoke();
                }
                yield return null;
            }
        }

        // Shared parsing path for HTTP, replay and deterministic validation.
        public bool AcceptJson(string json, long httpCode = 200)
        {
            try
            {
                var parsed = CncTelemetryParser.Parse(json);
                Latest = parsed;
                RawJson = json;
                LastHttpCode = httpCode;
                ReceivedAt = DateTimeOffset.UtcNow;
                LastError = "";
                ConnectionHealthy = true;
                SuccessfulRequests++;
                Changed?.Invoke();
                Debug.Log($"CNC_DATA_OK source={source} id={parsed.machine_id} status={parsed.status} x={parsed.axes.x.current_position.ToString(CultureInfo.InvariantCulture)} rpm={parsed.spindle.speed.ToString(CultureInfo.InvariantCulture)} timestamp={parsed.timestamp}");
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is OverflowException)
            {
                Reject("Ошибка JSON: " + exception.Message);
                return false;
            }
        }

        private void Reject(string message)
        {
            LastError = message;
            ConnectionHealthy = false;
            FailedRequests++;
            Changed?.Invoke();
            Debug.LogWarning("CNC_DATA_REJECTED: " + message);
        }

        public static string CreateReplayJson(float time)
        {
            CncAxis Axis(double min, double max, double current, string units) => new CncAxis
                { min_position = min, max_position = max, current_position = current, units = units };
            var machine = new CncMachineData
            {
                machine_id = "CNC-001", model = "DMG MORI NVX 7777",
                axes = new CncAxes
                {
                    x = Axis(0, 1111, 555 + 250 * Math.Sin(time * .3), "mm"),
                    y = Axis(0, 700, 350 + 180 * Math.Cos(time * .25), "mm"),
                    z = Axis(0, 700, 350 + 120 * Math.Sin(time * .2), "mm"),
                    a = Axis(-90, 90, 12 * Math.Sin(time * .2), "degrees"),
                    c = Axis(-360, 360, 45 + 30 * Math.Sin(time * .15), "degrees")
                },
                spindle = new CncSpindle { position = 0, speed = 240, units = "rpm" },
                tool_changer = new CncToolChanger { current_tool = 5, position = "ready" },
                workpiece_zero = new CncZero(), timestamp = DateTimeOffset.UtcNow.ToString("O"), status = "running"
            };
            return JsonConvert.SerializeObject(new CncEnvelope { cnc_machine = machine }, Formatting.Indented);
        }
    }
}
