using System;
using System.Collections.Generic;
using System.IO;
using AEM.DigitalTwin;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run without -quit: this test exits Unity itself when Play Mode checks finish.
[InitializeOnLoad]
public static class FactorySmokeTest
{
    private const string ActiveKey = "AEM.FactorySmokeTest.Active";
    private static readonly HashSet<FactoryStage> stages = new HashSet<FactoryStage>();
    private static readonly List<string> checks = new List<string>();
    private static DateTime started = DateTime.UtcNow;
    private static DateTime alarmAt;
    private static DateTime doneAt;
    private static int phase;
    private static float frozenProgress;
    private static Quaternion frozenTool;
    private static bool completed;

    static FactorySmokeTest()
    {
        if (SessionState.GetBool(ActiveKey, false)) Attach();
    }

    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Factory.unity");
        Attach();
        EditorApplication.isPlaying = true;
    }

    private static void Attach()
    {
        started = DateTime.UtcNow;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (!completed && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
            Finish(false, message + "\n" + stack);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Pass(string message)
    {
        checks.Add("PASS: " + message);
        Debug.Log("FACTORY_TEST: " + message);
    }

    private static void Tick()
    {
        if (completed) return;
        if ((DateTime.UtcNow - started).TotalSeconds > 100)
        {
            Finish(false, "Timed out after 100 seconds, phase=" + phase);
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            var simulation = Object.FindAnyObjectByType<FactorySimulation>();
            var ui = Object.FindAnyObjectByType<DigitalTwinUI>();
            if (simulation == null || ui == null) return;
            var twin = simulation.twin;
            var machine = simulation.machine;
            Time.timeScale = 8;
            stages.Add(twin.Stage);
            if (machine.IsProcessing && !machine.IsOverheated && !machine.IsCooling)
                Require(machine.Vibration >= 1.199f && machine.Vibration <= 1.901f, "Vibration out of range.");
            switch (phase)
            {
                case 0:
                    if (!machine.IsProcessing || machine.Progress < .3f) break;
                    Require(simulation.ActivePart != null && !simulation.ActivePart.IsFinished, "Raw blank missing at CNC.");
                    Require(Vector3.Distance(simulation.ActivePart.transform.position, machine.bedPoint.position) < .01f, "Part not on CNC bed.");
                    Require(ui.overheatButton.interactable, "Overheat button not enabled.");
                    Require(machine.signalLamp.sharedMaterial == machine.workingMaterial, "Working lamp is not amber.");
                    Pass("Conveyor and manipulator deliver blank to CNC; UI button and amber lamp active.");
                    ui.overheatButton.onClick.Invoke();
                    phase = 1;
                    break;
                case 1:
                    if (!machine.IsOverheated) break;
                    Require(machine.Temperature > 80, "Fault threshold not reached.");
                    Require(machine.signalLamp.sharedMaterial == machine.alarmMaterial, "Alarm lamp is not red.");
                    frozenProgress = machine.Progress;
                    frozenTool = machine.tool.localRotation;
                    alarmAt = DateTime.UtcNow;
                    phase = 2;
                    break;
                case 2:
                    Require(Mathf.Abs(machine.Progress - frozenProgress) < .00001f, "Machining advanced during fault.");
                    Require(Quaternion.Angle(frozenTool, machine.tool.localRotation) < .01f, "Spindle moved during fault.");
                    Require(twin.CompletedParts == 0 && !simulation.ActivePart.IsFinished, "Part completed during fault.");
                    if ((DateTime.UtcNow - alarmAt).TotalSeconds < .65) break;
                    Require(twin.State == "ПЕРЕГРЕВ" && ui.stateText.text == "ПЕРЕГРЕВ", "Overheat status not shown.");
                    Require(ui.recommendationText.text.Contains("Проверьте оборудование"), "Recommendation missing.");
                    Require(ui.resetButton.interactable, "Reset button not enabled.");
                    Capture("Factory-Overheat.png");
                    Pass("Above 80 C: progress and spindle stop, red lamp, OVERHEAT status and recommendation visible.");
                    ui.resetButton.onClick.Invoke();
                    Require(machine.IsCooling, "Reset did not start cooling.");
                    phase = 3;
                    break;
                case 3:
                    if (machine.IsCooling) break;
                    Require(!machine.IsOverheated && machine.ResetCount == 1, "Alarm did not reset.");
                    Pass("Reset button cools machine and clears alarm.");
                    phase = 4;
                    break;
                case 4:
                    if (twin.CompletedParts < 1) break;
                    Require(simulation.agv.Cargo == null, "AGV cargo not unloaded.");
                    Require(simulation.storageSlots[0].childCount == 1, "Finished product not stored.");
                    Require(simulation.storageSlots[0].GetComponentInChildren<Workpiece>().IsFinished, "Stored part was not machined.");
                    Pass("Processing resumes; finished part delivered by AGV; count increments once.");
                    phase = 5;
                    break;
                case 5:
                    if (!machine.IsProcessing || machine.Progress < .94f) break;
                    Require(machine.Temperature >= 65f && machine.Temperature <= 70f, "Normal temperature not within 65-70 C.");
                    Capture("Factory-Normal.png");
                    Pass("Second cycle starts automatically; normal temperature reaches 65-70 C.");
                    phase = 6;
                    break;
                case 6:
                    if (twin.CompletedParts < 2) break;
                    Require(stages.Count == 7, "Not all 7 manufacturing stages visited.");
                    Require(machine.signalLamp.sharedMaterial == machine.readyMaterial, "Ready lamp is not green.");
                    Require(simulation.storageSlots[1].childCount == 1, "Second finished product missing.");
                    Pass("All 7 stages observed; 2 completed parts stored; green ready lamp restored.");
                    doneAt = DateTime.UtcNow;
                    phase = 7;
                    break;
                case 7:
                    if ((DateTime.UtcNow - doneAt).TotalSeconds > 1) Finish(true, "All runtime checks passed.");
                    break;
            }
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Capture(string filename)
    {
        FactoryPreview.Capture(Path.GetFullPath("Logs/" + filename));
    }

    private static void Finish(bool success, string message)
    {
        if (completed) return;
        completed = true;
        SessionState.SetBool(ActiveKey, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/FactoryValidation.txt", string.Join("\n", checks) + "\n" + message);
        Debug.Log((success ? "FACTORY_TEST_SUCCESS: " : "FACTORY_TEST_FAILED: ") + message);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
