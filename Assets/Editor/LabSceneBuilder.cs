using System;
using System.Collections.Generic;
using AEM.DigitalTwin;
using AEM.DigitalTwin.Telemetry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class LabSceneBuilder
{
    private static Font font;
    private static readonly Color Ink = new Color(.035f,.055f,.08f);
    private static readonly Color Accent = new Color(.2f,.85f,.76f);

    [MenuItem("AEM/Создать лабораторные 2 и 3")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildLab2();
        BuildLab3();
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (string path in new[] { "Assets/Scenes/Lab2Telemetry.unity", "Assets/Scenes/FactoryConnected.unity" })
            if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("LAB_SCENES_BUILD_SUCCESS: Lab2Telemetry.unity and FactoryConnected.unity");
    }

    private static void BuildLab2()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Ink;
        camera.rect = new Rect(0,0,.7f,1);
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var client = new GameObject("CNC telemetry client").AddComponent<CncTelemetryClient>();
        BuildDashboard(client, null, null, true);
        EnsureEventSystem();
        if (!EditorSceneManager.SaveScene(scene, "Assets/Scenes/Lab2Telemetry.unity")) throw new InvalidOperationException("Failed to save Lab2 scene");
    }

    private static void BuildLab3()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Factory.unity", OpenSceneMode.Single);
        var simulation = Object.FindAnyObjectByType<FactorySimulation>();
        var originalUI = Object.FindAnyObjectByType<DigitalTwinUI>();
        Object.DestroyImmediate(originalUI.gameObject);
        var client = new GameObject("CNC telemetry client").AddComponent<CncTelemetryClient>();
        var link = client.gameObject.AddComponent<CncMachineLink>();
        link.client = client;
        link.machine = simulation.machine;
        simulation.machine.telemetryLink = link;
        var gate = client.gameObject.AddComponent<FactoryProcessGate>();
        gate.link = link;
        simulation.processGate = gate;
        simulation.conveyor.processGate = gate;
        simulation.manipulator.processGate = gate;
        simulation.agv.processGate = gate;
        link.rotaryTable = new GameObject("A C rotary fixture").transform;
        link.rotaryTable.SetParent(simulation.machine.transform, false);
        link.rotaryTable.position = new Vector3(0,1.32f,1);
        var plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plate.name = "Rotary table plate";
        plate.transform.SetParent(link.rotaryTable, false);
        plate.transform.localScale = new Vector3(1.25f,.045f,1.25f);
        plate.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Steel.mat");
        Object.DestroyImmediate(plate.GetComponent<Collider>());
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Workpiece zero marker";
        marker.transform.SetParent(link.rotaryTable, false);
        marker.transform.localPosition = new Vector3(.42f,.055f,0);
        marker.transform.localScale = new Vector3(.15f,.02f,.045f);
        marker.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ProcessTeal.mat");
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        link.zeroMarker = marker.transform;
        link.toolMarker = simulation.machine.tool.GetComponentInChildren<Renderer>();
        foreach (var label in Object.FindObjectsByType<TextMesh>())
            if (label.text.Contains("CNC-01")) label.text = label.text.Replace("CNC-01", "CNC-001");
        BuildDashboard(client, link, simulation, false);
        EnsureEventSystem();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, "Assets/Scenes/FactoryConnected.unity")) throw new InvalidOperationException("Failed to save Lab3 scene");
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var t = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        t.SetParent(parent,false);
        t.anchorMin = t.anchorMax = t.pivot = new Vector2(0,1);
        t.anchoredPosition = new Vector2(x,-y);
        t.sizeDelta = new Vector2(w,h);
        return t;
    }
    private static Image Panel(string name, Transform parent, float x,float y,float w,float h,Color color)
    {
        var image = Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();
        image.color=color; image.raycastTarget=false; return image;
    }
    private static Text Label(string value,Transform parent,float x,float y,float w,float h,int size,Color? color=null)
    {
        var text = Rect(value,parent,x,y,w,h).gameObject.AddComponent<Text>();
        text.font=font; text.text=value; text.fontSize=size; text.color=color ?? new Color(.87f,.93f,.97f);
        text.raycastTarget=false; text.supportRichText=false;
        return text;
    }
    private static Button Button(string title,Transform parent,float x,float y,float w,out Text label)
    {
        var panel=Panel(title,parent,x,y,w,43,new Color(.12f,.29f,.34f));
        panel.raycastTarget=true;
        var button=panel.gameObject.AddComponent<Button>(); button.targetGraphic=panel;
        label=Label(title,panel.transform,5,0,w-10,43,17); label.alignment=TextAnchor.MiddleCenter;
        return button;
    }

    private static void BuildDashboard(CncTelemetryClient client,CncMachineLink link,FactorySimulation simulation,bool lab2)
    {
        var canvas = new GameObject("Telemetry Dashboard",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=.5f;
        var panel=Panel("Telemetry panel",canvas.transform,0,0,0,0,Ink);
        panel.rectTransform.anchorMin=new Vector2(.7f,0); panel.rectTransform.anchorMax=Vector2.one;
        panel.rectTransform.offsetMin=panel.rectTransform.offsetMax=Vector2.zero;
        var content=Rect("Content",panel.transform,0,0,480,900);
        var layout=content.gameObject.AddComponent<FactoryDashboardLayout>(); layout.container=panel.rectTransform;
        var ui=canvas.AddComponent<CncTelemetryUI>(); ui.enabled=false;
        ui.client=client; ui.link=link; ui.simulation=simulation;
        Label(lab2 ? "ЛАБОРАТОРНАЯ РАБОТА 2" : "ЛАБОРАТОРНАЯ РАБОТА 3",content,24,22,432,25,14,Accent);
        Label("ДАННЫЕ СТАНКА",content,24,59,432,37,27);
        ui.sourceText=Label("ИСТОЧНИК: HTTPS-СЕРВЕР",content,24,106,432,30,14,Accent);
        ui.machineText=Label("Ожидание ответа",content,24,144,432,30,18);
        ui.connectionText=Label("Запрос...",content,24,187,432,47,14);
        Label("КООРДИНАТЫ ОСЕЙ",content,24,249,432,22,13,Accent);
        ui.axesText=Label("X —\nY —\nZ —\nA —\nC —",content,24,280,432,140,21);
        ui.spindleText=Label("Шпиндель: —",content,24,425,432,70,16);
        ui.zeroText=Label("Ноль детали: —",content,24,503,432,35,14);
        ui.timeText=Label("Источник: —\nПолучено: —",content,24,545,432,46,14);
        ui.warningsText=Label("Ожидание данных",content,24,603,432,111,14);
        ui.cycleText=Label("Ожидание",content,24,718,432,49,16,Accent);
        ui.refreshButton=Button("Обновить сейчас",content,24,779,432,out _);
        ui.modeButton=Button("Включить тестовые данные",content,24,836,432,out var modeLabel);
        ui.modeButtonText=modeLabel;
        if (lab2)
        {
            var main=Rect("Lab2 details",canvas.transform,0,0,1120,900);
            Label("ЛР 02 / ПОЛУЧЕНИЕ ДАННЫХ",main,42,35,1040,55,32);
            Label("HTTPS-запрос  →  разбор JSON  →  проверка  →  вывод",main,42,102,1040,37,23,Accent);
            Label("https://karasevv.com/test/mt_data.json",main,42,154,1040,36,22);
            Label("ОСЬ       ДИАПАЗОН И ПОЛУЧЕННОЕ ЗНАЧЕНИЕ",main,42,224,1040,34,21);
            ui.axisRows=new Text[5];
            for (int i=0;i<5;i++)
            {
                Panel("Axis row",main,38,274+i*92,1035,80,new Color(.065f,.095f,.13f));
                ui.axisRows[i]=Label("Ожидание ответа",main,56,282+i*92,1000,72,23);
            }
            Label("Период опроса — 5 секунд. Запросы выполняются асинхронно.\nВремя получения и время измерения показаны отдельно.",main,42,772,1030,74,23);
        }
        else
        {
            var badge=Panel("Connected workshop title",canvas.transform,24,22,690,80,new Color(.035f,.055f,.08f,.94f));
            Label("AEM / УЧАСТОК С ВНЕШНИМИ ДАННЫМИ",badge.transform,16,10,660,31,23);
            Label("CNC-001 · координаты, инструмент и статус из JSON",badge.transform,16,48,660,25,17,Accent);
        }
        ui.enabled=true;
    }
}
