using System;
using System.Collections.Generic;
using System.IO;
using AEM.DigitalTwin;
using AEM.DigitalTwin.Telemetry;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class LabVerification
{
    private const string Key = "AEM.LabVerification";
    private static DateTime began = DateTime.UtcNow;
    private static readonly List<string> results = new List<string>();
    private static int phase;
    private static DateTimeOffset firstReceipt;
    private static DateTime phaseTime;
    private static float progress;
    private static Quaternion toolRotation;
    private static Vector3 agvPosition, gripperPosition;
    private static bool done;

    static LabVerification() { if (SessionState.GetBool(Key,false)) Attach(); }

    public static void Run()
    {
        Directory.CreateDirectory("Logs");
        ParserChecks();
        SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Scenes/Lab2Telemetry.unity");
        Attach();
        EditorApplication.isPlaying=true;
    }
    private static void Attach()
    {
        began=DateTime.UtcNow;
        EditorApplication.update-=Tick; EditorApplication.update+=Tick;
        Application.logMessageReceived-=OnLog; Application.logMessageReceived+=OnLog;
    }
    private static void Require(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Pass(string message) { results.Add("PASS: "+message); Debug.Log("LAB_TEST: "+message); }
    private static void OnLog(string message,string stack,LogType type)
    {
        if (!done && (type==LogType.Exception || type==LogType.Error || type==LogType.Assert)) Finish(false,message+"\n"+stack);
    }
    private static void ParserChecks()
    {
        string valid=CncTelemetryClient.CreateReplayJson(0);
        var data=CncTelemetryParser.Parse(valid);
        Require(data.machine_id=="CNC-001" && data.AxesInRange,"Valid parser sample failed.");
        foreach (string invalid in new[] { "{", "{}", "{\"cnc_machine\":null}", valid.Replace("\"rpm\"","\"invalid\"") })
        {
            bool rejected=false;
            try { CncTelemetryParser.Parse(invalid); } catch { rejected=true; }
            Require(rejected,"Malformed/missing-field sample accepted.");
        }
        var missing=JObject.Parse(valid);
        ((JObject)missing["cnc_machine"]["axes"]["x"]).Remove("current_position");
        bool missingRejected=false;
        try { CncTelemetryParser.Parse(missing.ToString()); } catch { missingRejected=true; }
        Require(missingRejected,"Missing coordinate interpreted as zero.");
        var outOfRange=JObject.Parse(valid);
        outOfRange["cnc_machine"]["axes"]["x"]["current_position"]=1700.8;
        var parsed=CncTelemetryParser.Parse(outOfRange.ToString());
        Require(!parsed.AxesInRange && parsed.axes.x.current_position==1700.8 && parsed.axes.x.Normalized==1,"Raw coordinates not preserved/clamped safely.");
        File.WriteAllText("Logs/LabParserValidation.txt","PASS: valid schema; malformed JSON; missing root; null root; incorrect units; missing coordinate; preservation of out-of-range raw data and clamped display.\n");
        Debug.Log("LAB_PARSER_SUCCESS");
    }
    private static string Sample(string status="running",double rpm=240,bool old=false,bool outside=false)
    {
        var json=JObject.Parse(CncTelemetryClient.CreateReplayJson(5));
        json["cnc_machine"]["status"]=status;
        json["cnc_machine"]["spindle"]["speed"]=rpm;
        if (old) json["cnc_machine"]["timestamp"]="2024-01-15T14:30:25Z";
        if (outside) json["cnc_machine"]["axes"]["z"]["current_position"]=51110.7;
        return json.ToString();
    }
    private static void Tick()
    {
        if (done) return;
        if ((DateTime.UtcNow-began).TotalSeconds>125) { Finish(false,"Timeout, phase="+phase); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            var client=Object.FindAnyObjectByType<CncTelemetryClient>();
            var ui=Object.FindAnyObjectByType<CncTelemetryUI>();
            if (client==null || ui==null) return;
            Time.timeScale=8;
            switch (phase)
            {
                case 0:
                    if (client.SuccessfulRequests==1 && firstReceipt==default) firstReceipt=client.ReceivedAt;
                    if (client.SuccessfulRequests<2) break;
                    Require(client.LastHttpCode==200 && client.ConnectionHealthy,"Live HTTP failed.");
                    Require((client.ReceivedAt-firstReceipt).TotalSeconds>=4.8,"Polling uses scaled time.");
                    File.WriteAllText("Logs/ServerData.json",client.RawJson);
                    Pass("LR2: two real HTTPS 200 responses, JSON parsed, 5-second real-time polling at timeScale 8.");
                    phaseTime=DateTime.UtcNow; phase=1; break;
                case 1:
                    if ((DateTime.UtcNow-phaseTime).TotalSeconds<.2) break;
                    Require(ui.axesText.text.Contains("1700.8") && ui.warningsText.text.Contains("Метка времени"),"Live values/age warning absent from UI.");
                    FactoryPreview.Capture(Path.GetFullPath("Logs/Lab2-live.png"));
                    Pass("LR2: raw X/Z values, source timestamp, receipt timestamp and warnings displayed.");
                    SceneManager.LoadScene("FactoryConnected"); phase=2; break;
                case 2:
                    if (SceneManager.GetActiveScene().name!="FactoryConnected" || client.SuccessfulRequests<1) break;
                    var link=Object.FindAnyObjectByType<CncMachineLink>();
                    Require(!link.CanProcess,"Invalid/stale live data allowed production.");
                    phaseTime=DateTime.UtcNow; phase=3; break;
                case 3:
                    if ((DateTime.UtcNow-phaseTime).TotalSeconds<.3) break;
                    var liveSimulation=Object.FindAnyObjectByType<FactorySimulation>();
                    Require(liveSimulation.ActivePart==null,"Live bad data started conveyor.");
                    Require(liveSimulation.machine.signalLamp.sharedMaterial==liveSimulation.machine.alarmMaterial,"Live fault lamp not red.");
                    FactoryPreview.Capture(Path.GetFullPath("Logs/Lab3-live.png"));
                    Pass("LR3: current live out-of-range and stale data stop the line; alarm lamp red.");
                    client.pollIntervalSeconds=5;
                    ui.modeButton.onClick.Invoke();
                    phase=4; break;
                case 4:
                    var simulation=Object.FindAnyObjectByType<FactorySimulation>();
                    if (!simulation.machine.IsProcessing || simulation.machine.Progress<.25f) break;
                    Require(client.source==TelemetrySource.Replay && ui.sourceText.text.Contains("ТЕСТОВОЕ"),"Replay not explicitly marked.");
                    Require(simulation.processGate.CanRun,"Valid data did not allow production.");
                    FactoryPreview.Capture(Path.GetFullPath("Logs/Lab3-replay.png"));
                    client.pollIntervalSeconds=3600;
                    client.AcceptJson(Sample("stopped"),0);
                    progress=simulation.machine.Progress; toolRotation=simulation.machine.tool.localRotation;
                    gripperPosition=simulation.manipulator.gripper.position; agvPosition=simulation.agv.transform.position;
                    phaseTime=DateTime.UtcNow; phase=5; break;
                case 5:
                    var sim=Object.FindAnyObjectByType<FactorySimulation>();
                    Require(Mathf.Abs(sim.machine.Progress-progress)<.0001f,"Process advanced after stopped status.");
                    Require(Quaternion.Angle(sim.machine.tool.localRotation,toolRotation)<.01f,"Spindle kept rotating.");
                    Require(Vector3.Distance(sim.agv.transform.position,agvPosition)<.001f && Vector3.Distance(sim.manipulator.gripper.position,gripperPosition)<.001f,"Line moved while stopped.");
                    if ((DateTime.UtcNow-phaseTime).TotalSeconds<.4) break;
                    Pass("LR3: stopped status freezes machining progress, spindle and line without losing the part.");
                    client.AcceptJson(Sample("alarm"),0); Require(!sim.processGate.CanRun,"Alarm allowed motion.");
                    client.AcceptJson(Sample(rpm:0),0); Require(!sim.processGate.CanRun,"0 rpm allowed machining.");
                    client.AcceptJson(Sample(old:true),0); Require(!sim.processGate.CanRun,"Stale source allowed machining.");
                    client.AcceptJson(Sample(outside:true),0); Require(!sim.processGate.CanRun,"Out-of-range axis allowed machining.");
                    var last=client.Latest;
                    Require(!client.AcceptJson("{}",0) && client.Latest==last && !sim.processGate.CanRun,"Bad JSON did not preserve last good data and stop line.");
                    client.AcceptJson(Sample(),0);
                    Require(sim.processGate.CanRun,"Recovery did not reopen the gate.");
                    Pass("LR3: alarm, zero rpm, stale timestamp, out-of-range axes and bad JSON block motion; valid sample recovers.");
                    phase=6; break;
                case 6:
                    var completed=Object.FindAnyObjectByType<FactorySimulation>();
                    if (completed.twin.CompletedParts<1) break;
                    Require(completed.storageSlots[0].GetComponentInChildren<Workpiece>()?.IsFinished==true,"Finished part not delivered.");
                    Pass("LR3: full conveyor/manipulator/CNC/AGV cycle completes after recovery, counter=1.");
                    // Check a real transport failure, then switch back through the UI.
                    client.endpoint="http://127.0.0.1:1/unavailable";
                    client.SetSource(TelemetrySource.Server);
                    phase=7; break;
                case 7:
                    if (client.IsRequesting || string.IsNullOrEmpty(client.LastError)) break;
                    Require(!client.ConnectionHealthy && !Object.FindAnyObjectByType<FactoryProcessGate>().CanRun,"Network failure not fail-closed.");
                    Pass("LR3: failed HTTP request stops the process; polling remains available.");
                    client.endpoint="https://karasevv.com/test/mt_data.json";
                    ui.modeButton.onClick.Invoke();
                    phase=8; break;
                case 8:
                    if (client.Latest==null || !client.ConnectionHealthy) break;
                    Require(client.source==TelemetrySource.Replay && Object.FindAnyObjectByType<FactoryProcessGate>().CanRun,"Source switch recovery failed.");
                    Pass("UI source switch recovers after transport error.");
                    Finish(true,"All laboratory checks passed."); break;
            }
        }
        catch (Exception error) { Finish(false,error.ToString()); }
    }
    private static void Finish(bool success,string message)
    {
        if (done) return; done=true; SessionState.SetBool(Key,false);
        File.WriteAllText("Logs/LabValidation.txt",string.Join("\n",results)+"\n"+message);
        Debug.Log((success ? "LAB_TEST_SUCCESS: " : "LAB_TEST_FAILED: ")+message);
        EditorApplication.Exit(success?0:1);
    }
}
