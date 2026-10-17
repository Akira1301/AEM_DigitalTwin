using System;
using UnityEngine;

namespace AEM.DigitalTwin
{
    public class DigitalTwinController : MonoBehaviour
    {
        public MachineController machine;
        public string productId = "AEM-00124";
        public string machineId = "CNC-01";
        public FactoryStage Stage { get; private set; } = FactoryStage.Waiting;
        public int CompletedParts { get; private set; }
        public event Action Changed;

        public float Temperature => machine.Temperature;
        public float Vibration => machine.Vibration;
        public bool IsAlarm => machine.IsOverheated;
        public string State => machine.IsCooling ? "ОХЛАЖДЕНИЕ" : IsAlarm ? "ПЕРЕГРЕВ"
            : machine.IsOverheatRequested ? "НАГРЕВ / СБОЙ" : machine.IsProcessing ? "В РАБОТЕ" : "НОРМА";
        public string StageName => Stage switch
        {
            FactoryStage.Feeding => "Подача заготовки",
            FactoryStage.Loading => "Загрузка в станок",
            FactoryStage.Machining => "Механическая обработка",
            FactoryStage.Unloading => "Выгрузка",
            FactoryStage.Transporting => "Транспортировка",
            FactoryStage.Finished => "Готовая продукция",
            _ => "Ожидание"
        };

        public void SetStage(FactoryStage stage)
        {
            Stage = stage;
            Changed?.Invoke();
        }

        public void RegisterFinishedPart()
        {
            CompletedParts++;
            Changed?.Invoke();
        }

        public void SimulateOverheat() => machine.SimulateOverheat();
        public void ResetAlarm() => machine.ResetAlarm();
    }
}
