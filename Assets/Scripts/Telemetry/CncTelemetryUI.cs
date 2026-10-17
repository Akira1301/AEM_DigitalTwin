using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AEM.DigitalTwin.Telemetry
{
    public sealed class CncTelemetryUI : MonoBehaviour
    {
        public CncTelemetryClient client;
        public CncMachineLink link;
        public FactorySimulation simulation;
        public Text sourceText, machineText, connectionText, axesText, spindleText, zeroText, timeText, warningsText, cycleText, modeButtonText;
        public Text[] axisRows;
        public Button refreshButton, modeButton;

        private void OnEnable()
        {
            if (client == null || refreshButton == null) return;
            refreshButton.onClick.AddListener(client.RefreshNow);
            modeButton.onClick.AddListener(client.ToggleSource);
        }
        private void OnDisable()
        {
            if (client == null || refreshButton == null) return;
            refreshButton.onClick.RemoveListener(client.RefreshNow);
            modeButton.onClick.RemoveListener(client.ToggleSource);
        }
        private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        private void Update()
        {
            var data = client.Latest;
            sourceText.text = client.source == TelemetrySource.Server ? "ИСТОЧНИК: HTTPS-СЕРВЕР" : "ИСТОЧНИК: ТЕСТОВОЕ ВОСПРОИЗВЕДЕНИЕ";
            sourceText.color = client.source == TelemetrySource.Server ? new Color(.2f,.85f,.76f) : new Color(1,.74f,.26f);
            modeButtonText.text = client.source == TelemetrySource.Server ? "Включить тестовые данные" : "Вернуться к серверу";
            connectionText.text = $"{(client.IsRequesting ? "Запрос..." : client.ConnectionHealthy ? "Ответ получен" : "Нет связи")}  /  HTTP {client.LastHttpCode}\n"
                + $"Успешно: {client.SuccessfulRequests}   Ошибок: {client.FailedRequests}   Следующий: {client.SecondsToNextRequest:F1} с";
            refreshButton.interactable = !client.IsRequesting;
            machineText.text = data == null ? "CNC / ожидание ответа" : data.machine_id + "  /  " + data.model;
            if (data == null)
            {
                axesText.text = "X  —\nY  —\nZ  —\nA  —\nC  —";
                spindleText.text = "Шпиндель: —\nИнструмент: —";
                zeroText.text = "Ноль детали: —";
                timeText.text = "Источник: —\nПолучено: —";
                if (axisRows != null) foreach (var row in axisRows) row.text = "Ожидание ответа";
            }
            else
            {
                var axes = new[] { data.axes.x, data.axes.y, data.axes.z, data.axes.a, data.axes.c };
                var names = new[] { "X", "Y", "Z", "A", "C" };
                var lines = new StringBuilder();
                for (int i = 0; i < axes.Length; i++)
                {
                    var a = axes[i];
                    lines.AppendLine(names[i] + "   " + N(a.current_position) + " " + a.units + (a.InRange ? "" : "   !"));
                    if (axisRows != null && i < axisRows.Length)
                    {
                        axisRows[i].text = $"{names[i]}       {N(a.min_position)}   …   {N(a.max_position)}\n       Получено: {N(a.current_position)} {a.units}   /   {(a.InRange ? "в пределах" : "выход за пределы")}";
                        axisRows[i].color = a.InRange ? new Color(.85f,.92f,.97f) : new Color(1,.51f,.36f);
                    }
                }
                axesText.text = lines.ToString().TrimEnd();
                spindleText.text = $"Шпиндель: {N(data.spindle.speed)} rpm  /  позиция: {N(data.spindle.position)}\nИнструмент: T{data.tool_changer.current_tool}  /  {data.tool_changer.position}\nСтатус сервера: {data.status}";
                zeroText.text = $"Ноль детали: X {N(data.workpiece_zero.x)} / Y {N(data.workpiece_zero.y)} / Z {N(data.workpiece_zero.z)} mm";
                timeText.text = "Источник: " + data.SourceTime.ToString("dd.MM.yyyy HH:mm:ss 'UTC'")
                    + "\nПолучено: " + client.ReceivedAt.ToString("dd.MM.yyyy HH:mm:ss 'UTC'");
            }
            var warning = new StringBuilder();
            if (!string.IsNullOrEmpty(client.LastError)) warning.AppendLine(client.LastError);
            if (data != null && !data.AxesInRange) warning.AppendLine("Координаты за пределами. Показаны исходные значения; положение 3D ограничено.");
            if (client.IsSourceStale) warning.AppendLine("Метка времени источника устарела. Новое получение не обновляет возраст измерения.");
            if (client.IsTransportStale) warning.AppendLine("Данные давно не получались. Движение остановлено.");
            if (data != null && data.spindle.speed == 0) warning.AppendLine("Шпиндель: 0 rpm. Обработка не выполняется.");
            warningsText.text = warning.Length == 0 ? "Данные приняты. Температура и вибрация в ответе сервера отсутствуют." : warning.ToString().TrimEnd();
            warningsText.color = warning.Length == 0 ? new Color(.5f,.75f,.7f) : new Color(1,.57f,.39f);
            cycleText.text = simulation == null ? "Опрос: каждые " + N(client.pollIntervalSeconds) + " с после ответа\nРазбор: JSON → параметры станка"
                : link.State + "\n" + simulation.twin.StageName + "  /  Выпущено: " + simulation.twin.CompletedParts;
        }
    }
}
