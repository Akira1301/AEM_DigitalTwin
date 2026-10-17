using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace AEM.DigitalTwin
{
    public class DigitalTwinUI : MonoBehaviour
    {
        public DigitalTwinController twin;
        public Text productText;
        public Text machineText;
        public Text stageText;
        public Text temperatureText;
        public Text vibrationText;
        public Text stateText;
        public Text countText;
        public Text recommendationText;
        public Text progressText;
        public Text faultButtonText;
        public Image temperatureFill;
        public Image processingFill;
        public Image stateDot;
        public Image[] stageMarkers;
        public Button overheatButton;
        public Button resetButton;
        private static readonly Color Teal = new Color(0.22f, 0.88f, 0.75f);
        private static readonly Color Amber = new Color(1f, 0.7f, 0.24f);
        private static readonly Color Red = new Color(1f, 0.29f, 0.28f);
        private static readonly Color Muted = new Color(0.22f, 0.28f, 0.35f);

        private void OnEnable()
        {
            overheatButton.onClick.AddListener(twin.SimulateOverheat);
            resetButton.onClick.AddListener(twin.ResetAlarm);
        }

        private void OnDisable()
        {
            overheatButton.onClick.RemoveListener(twin.SimulateOverheat);
            resetButton.onClick.RemoveListener(twin.ResetAlarm);
        }

        private void Update()
        {
            MachineController machine = twin.machine;
            Color statusColor = twin.IsAlarm ? Red : machine.IsProcessing ? Amber : Teal;
            productText.text = "Изделие: " + twin.productId;
            machineText.text = "СТАНОК   /   " + twin.machineId;
            stageText.text = twin.StageName;
            temperatureText.text = twin.Temperature.ToString("F1", CultureInfo.InvariantCulture) + " °C";
            vibrationText.text = twin.Vibration.ToString("F2", CultureInfo.InvariantCulture) + " мм/с";
            stateText.text = twin.State;
            stateText.color = statusColor;
            stateDot.color = statusColor;
            temperatureFill.color = twin.IsAlarm ? Red : Amber;
            temperatureFill.fillAmount = Mathf.InverseLerp(0, 100, twin.Temperature);
            processingFill.fillAmount = machine.Progress;
            countText.text = twin.CompletedParts.ToString("D3");
            progressText.text = "ОБРАБОТКА  " + Mathf.RoundToInt(machine.Progress * 100f) + "%";
            overheatButton.interactable = machine.IsProcessing && !machine.IsOverheated
                && !machine.IsCooling && !machine.IsOverheatRequested;
            resetButton.interactable = machine.IsOverheated && !machine.IsCooling;
            faultButtonText.text = machine.IsOverheatRequested ? "Перегрев запрошен" : "Симулировать перегрев";
            recommendationText.text = machine.IsCooling
                ? "Охлаждение оборудования. Обработка продолжится после снижения температуры до 25 °C."
                : twin.IsAlarm
                    ? "Проверьте оборудование: систему охлаждения и инструмент. Затем сбросьте аварию."
                    : machine.IsOverheatRequested
                        ? "Симулируется отказ охлаждения. Остановка при температуре выше 80 °C."
                        : machine.IsProcessing
                            ? "Датчики обновляются в реальном времени. Можно симулировать перегрев станка."
                            : "Цикл работает автоматически. Кнопка перегрева доступна во время обработки.";
            recommendationText.color = twin.IsAlarm ? new Color(1f, 0.65f, 0.6f) : new Color(0.65f, 0.73f, 0.81f);
            for (int i = 0; i < stageMarkers.Length; i++)
                stageMarkers[i].color = i == (int)twin.Stage ? statusColor : Muted;
        }
    }
}
