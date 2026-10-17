using System;
using System.Collections.Generic;
using System.IO;
using AEM.DigitalTwin;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class FactorySceneBuilder
{
    private static Material floor, dark, steel, white, yellow, teal, black, glass, green, amber, red, glow;
    private static Font font;

    [MenuItem("AEM/Создать сцену Factory")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Materials");
        Directory.CreateDirectory("Assets/Prefabs");
        AssetDatabase.Refresh();
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        if (pipeline == null) throw new InvalidOperationException("URP asset Assets/Settings/PC_RPAsset.asset is missing.");
        // A small animated teaching scene does not need GPU-driven instance culling.
        // Disable it so explicit camera renders in batch validation are deterministic.
        pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
        EditorUtility.SetDirty(pipeline);
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        floor = Mat("Floor", new Color(.27f, .32f, .36f), .1f);
        dark = Mat("Conveyor", new Color(.065f, .09f, .12f), .3f);
        steel = Mat("Steel", new Color(.64f, .73f, .79f), .8f);
        white = Mat("Machine", new Color(.77f, .82f, .85f), .25f);
        yellow = Mat("SafetyYellow", new Color(1f, .64f, .055f), .2f);
        teal = Mat("ProcessTeal", new Color(.06f, .58f, .59f), .4f);
        black = Mat("Rubber", new Color(.025f, .033f, .043f), 0);
        glass = Mat("ControlScreen", new Color(.02f, .17f, .22f), .3f);
        green = Mat("SignalGreen", new Color(.12f, 1f, .4f), 0, true);
        amber = Mat("SignalAmber", new Color(1f, .58f, .03f), 0, true);
        red = Mat("SignalRed", new Color(1f, .07f, .04f), 0, true);
        glow = Mat("Lighting", new Color(.8f, .92f, 1f), 0, true);
        BuildHall();
        var conveyor = BuildConveyor();
        var machine = BuildMachine();
        var manipulator = BuildManipulator();
        var agv = BuildAGV();
        var slots = BuildStorage();
        var simulation = new GameObject("Factory Simulation").AddComponent<FactorySimulation>();
        var twin = simulation.gameObject.AddComponent<DigitalTwinController>();
        twin.machine = machine;
        simulation.conveyor = conveyor;
        simulation.machine = machine;
        simulation.manipulator = manipulator;
        simulation.agv = agv;
        simulation.twin = twin;
        simulation.workpiecePrefab = BuildWorkpiece();
        simulation.storageSlots = slots;
        BuildCamera();
        BuildUI(twin);
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.runInBackground = true;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, "Assets/Scenes/Factory.unity"))
            throw new IOException("Failed to save Factory.unity");
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene("Assets/Scenes/Factory.unity", true) };
        foreach (var existing in EditorBuildSettings.scenes)
            if (existing.path != "Assets/Scenes/Factory.unity") scenes.Add(existing);
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("FACTORY_BUILD_SUCCESS: Assets/Scenes/Factory.unity created with all references.");
    }

    private static Material Mat(string name, Color color, float metallic, bool emission = false)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", metallic > .5f ? .65f : .3f);
        if (emission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.7f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform Root(string name) => new GameObject(name).transform;
    private static Transform Point(string name, Vector3 position, Transform parent = null)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = position;
        return t;
    }
    private static GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 size, Material material, Transform parent)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    private static GameObject Box(string name, Vector3 position, Vector3 size, Material material, Transform parent = null)
        => Shape(name, PrimitiveType.Cube, position, size, material, parent);
    private static void Label(string text, Vector3 position, float size, Transform parent, Color? color = null)
    {
        var t = Point(text, position, parent);
        var mesh = t.gameObject.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.font = font;
        mesh.fontSize = 64;
        mesh.characterSize = size;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color ?? Color.white;
        mesh.GetComponent<Renderer>().sharedMaterial = font.material;
    }
    private static void Line(string name, Vector3 a, Vector3 b, float width, Material material, Transform parent)
    {
        var go = Box(name, (a + b) * .5f, new Vector3(width, .018f, Vector3.Distance(a, b)), material, parent);
        go.transform.rotation = Quaternion.LookRotation(b - a);
    }

    private static void BuildHall()
    {
        var hall = Root("01 · Workshop");
        Box("Industrial floor", new Vector3(0, -.16f, 1), new Vector3(22, .3f, 16), floor, hall);
        Box("Back wall", new Vector3(0, 2.4f, 8.8f), new Vector3(22, 4.8f, .2f), white, hall);
        Box("Left wall", new Vector3(-11, 2.4f, 1), new Vector3(.2f, 4.8f, 16), white, hall);
        Box("Back wall band", new Vector3(0, 1.1f, 8.65f), new Vector3(22, .48f, .1f), teal, hall);
        Box("Left wall band", new Vector3(-10.85f, 1.1f, 1), new Vector3(.1f, .48f, 16), teal, hall);
        for (int x = -10; x <= 10; x += 2)
            Box("Floor joint", new Vector3(x, .003f, 1), new Vector3(.012f, .008f, 16), dark, hall);
        for (int z = -6; z <= 8; z += 2)
            Box("Floor joint", new Vector3(0, .004f, z), new Vector3(22, .008f, .012f), dark, hall);
        for (int x = -10; x <= 10; x += 5)
        {
            Box("Wall column", new Vector3(x, 2.5f, 8.4f), new Vector3(.25f, 5, .35f), dark, hall);
            Box("Luminaire", new Vector3(x, 4.6f, 7.9f), new Vector3(2.8f, .13f, .45f), dark, hall);
            Box("LED diffuser", new Vector3(x, 4.52f, 7.85f), new Vector3(2.55f, .04f, .36f), glow, hall);
            var light = Point("Industrial light", new Vector3(x, 4.3f, 6), hall).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10;
            light.intensity = 2.5f;
            light.color = new Color(.78f, .89f, 1f);
        }
        Label("AEM  /  DIGITAL MANUFACTURING", new Vector3(-.5f, 3.35f, 8.62f), .16f, hall, new Color(.12f, .22f, .28f));
        Label("УЧАСТОК 01   /   ЦИФРОВЫЕ МОДЕЛИ И ДВОЙНИКИ", new Vector3(-.5f, 2.65f, 8.62f), .075f, hall, new Color(.18f, .3f, .36f));
        Line("AGV route", new Vector3(3.2f, .025f, -1.5f), new Vector3(7.2f, .025f, 4), .12f, teal, hall);
        for (int i = 0; i < 5; i++)
        {
            Vector3 p = Vector3.Lerp(new Vector3(3.2f, .04f, -1.5f), new Vector3(7.2f, .04f, 4), i / 5f);
            Shape("Route marker", PrimitiveType.Cylinder, p, new Vector3(.3f, .015f, .3f), yellow, hall);
        }
        Line("Safety boundary", new Vector3(-4.7f, .025f, -3.3f), new Vector3(3.8f, .025f, -3.3f), .10f, yellow, hall);
        Line("Safety boundary", new Vector3(-4.7f, .025f, 3.6f), new Vector3(3.8f, .025f, 3.6f), .10f, yellow, hall);
        for (int i = 0; i < 14; i++)
            Box("Hazard stripe", new Vector3(-2.6f + i * .4f, .03f, -3.5f), new Vector3(.2f, .018f, .3f), i % 2 == 0 ? yellow : black, hall);
        var sun = Point("Key light", Vector3.zero, hall).gameObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(48, -32, 0);
        sun.intensity = 2.1f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.62f, .7f, .78f);
        RenderSettings.ambientEquatorColor = new Color(.4f, .46f, .52f);
        RenderSettings.ambientGroundColor = new Color(.24f, .27f, .31f);
        RenderSettings.skybox = null;
    }

    private static ConveyorController BuildConveyor()
    {
        var root = Root("02 · Conveyor");
        var conveyor = root.gameObject.AddComponent<ConveyorController>();
        Box("Belt bed", new Vector3(-6, .95f, -1.5f), new Vector3(5.9f, .3f, 1.3f), dark, root);
        foreach (float z in new[] { -2.2f, -.8f })
        {
            Box("Conveyor side", new Vector3(-6, 1.05f, z), new Vector3(6, .32f, .12f), steel, root);
            foreach (float x in new[] { -8.3f, -3.7f })
                Box("Conveyor leg", new Vector3(x, .45f, z), new Vector3(.16f, .9f, .16f), dark, root);
        }
        var rollers = new List<Transform>();
        for (int i = 0; i < 23; i++)
        {
            var roller = Shape("Roller", PrimitiveType.Cylinder, new Vector3(-8.75f + i * .25f, 1.15f, -1.5f), new Vector3(.18f, .6f, .18f), i % 3 == 0 ? teal : steel, root).transform;
            roller.rotation = Quaternion.Euler(90, 0, 0);
            rollers.Add(roller);
        }
        conveyor.rollers = rollers.ToArray();
        conveyor.spawnPoint = Point("Blank spawn", new Vector3(-8.3f, 1.43f, -1.5f), root);
        conveyor.pickupPoint = Point("Pickup", new Vector3(-3.6f, 1.43f, -1.5f), root);
        Box("Conveyor nameplate", new Vector3(-6.1f, .73f, -2.29f), new Vector3(2.6f, .5f, .05f), dark, root);
        Label("01  /  ПОДАЧА", new Vector3(-6.1f, .75f, -2.33f), .085f, root);
        return conveyor;
    }

    private static MachineController BuildMachine()
    {
        var root = Root("03 · CNC-01");
        var machine = root.gameObject.AddComponent<MachineController>();
        Box("Base", new Vector3(0, .53f, 1.1f), new Vector3(3.7f, 1.05f, 3.3f), dark, root);
        Box("Machine plinth", new Vector3(0, .18f, 1.1f), new Vector3(3.9f, .36f, 3.5f), white, root);
        Box("Rear housing", new Vector3(0, 1.95f, 2.5f), new Vector3(3.7f, 2.25f, .35f), white, root);
        foreach (float x in new[] { -1.7f, 1.7f })
        {
            Box("Side housing", new Vector3(x, 1.65f, 1.1f), new Vector3(.3f, 1.8f, 2.7f), white, root);
            Box("Safety edge", new Vector3(x, 1.3f, -.28f), new Vector3(.31f, .18f, .05f), yellow, root);
        }
        Box("Table", new Vector3(0, 1.17f, 1), new Vector3(2.8f, .22f, 2.25f), steel, root);
        for (int i = -3; i <= 3; i++)
            Box("Table T-slot", new Vector3(i * .35f, 1.286f, 1), new Vector3(.03f, .01f, 2.1f), dark, root);
        machine.bedPoint = Point("Machining workpiece position", new Vector3(0, 1.49f, 1), root);
        Box("Spindle support", new Vector3(0, 2.8f, 2), new Vector3(.75f, .6f, 1.25f), dark, root);
        machine.spindleSlide = Point("Spindle slide", new Vector3(0, 2.5f, 1), root);
        Box("Spindle motor", new Vector3(0, 2.65f, 1), new Vector3(.65f, .65f, .65f), teal, machine.spindleSlide);
        machine.tool = Point("Rotating cutter", new Vector3(0, 2.12f, 1), machine.spindleSlide);
        Shape("Tool shank", PrimitiveType.Cylinder, new Vector3(0, 2.12f, 1), new Vector3(.15f, .26f, .15f), steel, machine.tool);
        for (int i = 0; i < 3; i++)
        {
            var tooth = Box("Cutting flute", new Vector3(0, 1.96f, 1), new Vector3(.26f, .18f, .045f), i == 0 ? yellow : dark, machine.tool);
            tooth.transform.localRotation = Quaternion.Euler(0, i * 60, 15);
        }
        Box("Operator console", new Vector3(-1.4f, 2.05f, -.65f), new Vector3(.85f, 1.1f, .23f), white, root);
        Box("Display", new Vector3(-1.4f, 2.23f, -.78f), new Vector3(.65f, .42f, .035f), glass, root);
        Label("CNC-01", new Vector3(-1.4f, 2.24f, -.805f), .075f, root);
        Shape("Emergency stop", PrimitiveType.Sphere, new Vector3(-1.5f, 1.8f, -.82f), new Vector3(.15f, .15f, .09f), red, root);
        Shape("Cycle start", PrimitiveType.Sphere, new Vector3(-1.2f, 1.8f, -.82f), new Vector3(.1f, .1f, .08f), green, root);
        Box("Front nameplate", new Vector3(0, .7f, -.57f), new Vector3(2.6f, .42f, .05f), teal, root);
        Label("02  /  CNC-01", new Vector3(0, .71f, -.61f), .1f, root);
        Shape("Beacon stem", PrimitiveType.Cylinder, new Vector3(1.65f, 2.85f, 2.35f), new Vector3(.08f, .3f, .08f), dark, root);
        machine.signalLamp = Shape("Status beacon", PrimitiveType.Cylinder, new Vector3(1.65f, 3.2f, 2.35f), new Vector3(.28f, .16f, .28f), green, root).GetComponent<Renderer>();
        machine.signalLight = Point("Beacon light", new Vector3(1.65f, 3.38f, 2.35f), root).gameObject.AddComponent<Light>();
        machine.signalLight.type = LightType.Point;
        machine.signalLight.intensity = 1.5f;
        machine.signalLight.range = 2;
        machine.readyMaterial = green;
        machine.workingMaterial = amber;
        machine.alarmMaterial = red;
        return machine;
    }

    private static ManipulatorController BuildManipulator()
    {
        var root = Root("04 · Gantry manipulator");
        var controller = root.gameObject.AddComponent<ManipulatorController>();
        foreach (float x in new[] { -4.6f, 3.8f })
        foreach (float z in new[] { -2.8f, 3.4f })
        {
            Box("Gantry post", new Vector3(x, 2.15f, z), new Vector3(.21f, 4.3f, .21f), yellow, root);
            Box("Foot plate", new Vector3(x, .08f, z), new Vector3(.55f, .16f, .55f), dark, root);
            for (int i = 0; i < 4; i++)
                Box("Post hazard band", new Vector3(x, .35f + i * .23f, z), new Vector3(.22f, .1f, .22f), black, root);
        }
        foreach (float z in new[] { -2.8f, 3.4f })
            Box("X rail", new Vector3(-.4f, 4.2f, z), new Vector3(8.8f, .25f, .22f), dark, root);
        controller.bridge = Point("Travelling bridge", new Vector3(-3.6f, 4.35f, .3f), root);
        Box("Z rail", new Vector3(-3.6f, 4.35f, .3f), new Vector3(.3f, .3f, 6.7f), yellow, controller.bridge);
        controller.trolley = Point("Trolley", new Vector3(-3.6f, 4.35f, -1.5f), root);
        Box("Trolley motor", new Vector3(-3.6f, 4.49f, -1.5f), new Vector3(.7f, .36f, .65f), teal, controller.trolley);
        controller.gripper = Point("Gripper", new Vector3(-3.6f, 3.45f, -1.5f), root);
        Box("Gripper body", new Vector3(-3.6f, 3.45f, -1.5f), new Vector3(.8f, .2f, .45f), dark, controller.gripper);
        controller.leftJaw = Box("Left jaw", new Vector3(-4.08f, 3.29f, -1.5f), new Vector3(.1f, .35f, .32f), steel, controller.gripper).transform;
        controller.rightJaw = Box("Right jaw", new Vector3(-3.12f, 3.29f, -1.5f), new Vector3(.1f, .35f, .32f), steel, controller.gripper).transform;
        controller.telescopicColumn = Box("Telescopic column", new Vector3(-3.6f, 3.9f, -1.5f), new Vector3(.12f, .9f, .12f), steel, root).transform;
        return controller;
    }

    private static AGVController BuildAGV()
    {
        var root = Root("05 · AGV-01");
        root.position = new Vector3(3.2f, 0, -1.5f);
        var controller = root.gameObject.AddComponent<AGVController>();
        Box("AGV body", new Vector3(3.2f, .48f, -1.5f), new Vector3(1.65f, .55f, 2f), yellow, root);
        Box("Cargo deck", new Vector3(3.2f, .8f, -1.5f), new Vector3(1.45f, .12f, 1.6f), dark, root);
        Box("Bumper", new Vector3(3.2f, .4f, -2.52f), new Vector3(1.7f, .22f, .12f), black, root);
        var wheels = new List<Transform>();
        foreach (float x in new[] { 2.39f, 4.01f })
        foreach (float z in new[] { -2.1f, -.9f })
        {
            var wheel = Shape("Wheel", PrimitiveType.Cylinder, new Vector3(x, .28f, z), new Vector3(.48f, .11f, .48f), black, root).transform;
            wheel.rotation = Quaternion.Euler(0, 0, 90);
            wheels.Add(wheel);
        }
        controller.wheels = wheels.ToArray();
        Shape("Navigation lidar", PrimitiveType.Cylinder, new Vector3(3.2f, .92f, -2.22f), new Vector3(.22f, .1f, .22f), teal, root);
        Label("AGV-01", new Vector3(3.2f, .54f, -2.525f), .09f, root, new Color(.08f, .1f, .13f));
        controller.cargoPoint = Point("Cargo mount", new Vector3(3.2f, 1.06f, -1.5f), root);
        controller.loadingStation = Point("AGV loading station", root.position);
        controller.deliveryStation = Point("AGV delivery station", new Vector3(7.2f, 0, 4));
        return controller;
    }

    private static Transform[] BuildStorage()
    {
        var root = Root("06 · Finished goods");
        Box("Storage zone", new Vector3(7.35f, .02f, 6.4f), new Vector3(4.7f, .035f, 3.4f), teal, root);
        Box("Pallet", new Vector3(7.35f, .22f, 6.4f), new Vector3(3.9f, .4f, 2.65f), dark, root);
        for (int i = 0; i < 7; i++)
            Box("Pallet slat", new Vector3(5.65f + .56f * i, .44f, 6.4f), new Vector3(.43f, .06f, 2.6f), steel, root);
        Box("Storage sign", new Vector3(7.35f, 1.5f, 7.95f), new Vector3(4.5f, .75f, .12f), dark, root);
        Label("03 / ГОТОВАЯ ПРОДУКЦИЯ", new Vector3(7.35f, 1.5f, 7.87f), .09f, root);
        var slots = new List<Transform>();
        for (int z = 0; z < 3; z++)
        for (int x = 0; x < 4; x++)
            slots.Add(Point("Storage slot " + slots.Count, new Vector3(6.05f + .86f * x, .67f, 5.55f + .8f * z), root));
        return slots.ToArray();
    }

    private static Workpiece BuildWorkpiece()
    {
        var root = Root("Workpiece");
        var part = root.gameObject.AddComponent<Workpiece>();
        part.rawBlank = Box("Raw metal blank", Vector3.zero, new Vector3(.55f, .38f, .55f), steel, root);
        part.finishedPart = new GameObject("Machined stepped part");
        part.finishedPart.transform.SetParent(root, false);
        Shape("Flange", PrimitiveType.Cylinder, new Vector3(0, -.1f, 0), new Vector3(.6f, .075f, .6f), steel, part.finishedPart.transform);
        Shape("Shaft", PrimitiveType.Cylinder, new Vector3(0, .075f, 0), new Vector3(.29f, .14f, .29f), steel, part.finishedPart.transform);
        Shape("Bore", PrimitiveType.Cylinder, new Vector3(0, .217f, 0), new Vector3(.12f, .003f, .12f), dark, part.finishedPart.transform);
        part.finishedPart.SetActive(false);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Prefabs/Workpiece.prefab");
        Object.DestroyImmediate(root.gameObject);
        return prefab.GetComponent<Workpiece>();
    }

    private static void BuildCamera()
    {
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(15, 17, -22);
        camera.transform.LookAt(new Vector3(-.4f, 1.2f, 1.7f));
        camera.orthographic = true;
        camera.orthographicSize = 11.2f;
        camera.rect = new Rect(0, 0, .7f, 1);
        camera.backgroundColor = new Color(.07f, .1f, .14f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 100;
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private static Image Panel(string name, Transform parent, float x, float y, float width, float height, Color color)
    {
        var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
    private static Text Text(string text, Transform parent, float x, float y, float width, float height, int size, Color? color = null)
    {
        var label = Rect(text, parent, x, y, width, height).gameObject.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color ?? new Color(.9f, .94f, .98f);
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }
    private static Button Button(string title, Transform parent, float y, Color color, out Text label)
    {
        var image = Panel(title, parent, 28, y, 424, 48, color);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.disabledColor = new Color(.34f, .37f, .42f, .65f);
        button.colors = colors;
        label = Text(title, image.transform, 10, 0, 404, 48, 19);
        label.alignment = TextAnchor.MiddleCenter;
        return button;
    }
    private static Image Bar(Transform parent, float x, float y, float width, Color color)
    {
        var bg = Panel("Bar track", parent, x, y, width, 6, new Color(.16f, .21f, .28f));
        var fill = Panel("Bar fill", bg.transform, 0, 0, width, 6, color);
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0;
        return fill;
    }

    private static void BuildUI(DigitalTwinController twin)
    {
        var canvas = new GameObject("Digital Twin Dashboard", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;
        var background = Panel("Dashboard", canvas.transform, 0, 0, 0, 0, new Color(.045f, .065f, .095f));
        var r = background.rectTransform;
        r.anchorMin = new Vector2(.7f, 0);
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        var content = Rect("Dashboard content", background.transform, 0, 0, 480, 900);
        // Scale the designed dashboard to any Game view aspect ratio without clipping.
        var fitter = content.gameObject.AddComponent<FactoryDashboardLayout>();
        fitter.container = r;
        var ui = canvas.AddComponent<DigitalTwinUI>();
        ui.enabled = false;
        ui.twin = twin;
        Color muted = new Color(.48f, .59f, .69f);
        Color accent = new Color(.22f, .88f, .75f);
        Panel("Accent", content, 28, 30, 35, 4, accent);
        Text("AEM  /  УЧАСТОК 01", content, 77, 20, 360, 28, 15, muted);
        Text("ЦИФРОВОЙ ДВОЙНИК\nИЗДЕЛИЯ", content, 28, 63, 424, 77, 28);
        ui.productText = Text("Изделие: AEM-00124", content, 28, 152, 424, 34, 22, accent);
        Text("ЭТАП ПРОИЗВОДСТВА", content, 28, 208, 424, 25, 13, muted);
        ui.stageText = Text("Ожидание", content, 28, 238, 424, 40, 23);
        ui.stageMarkers = new Image[7];
        for (int i = 0; i < 7; i++)
            ui.stageMarkers[i] = Panel("Stage " + i, content, 28 + i * 62, 291, 52, 5, muted);
        ui.machineText = Text("СТАНОК   /   CNC-01", content, 28, 320, 424, 30, 17);
        Text("ТЕМПЕРАТУРА", content, 28, 372, 205, 24, 13, muted);
        Text("ВИБРАЦИЯ", content, 254, 372, 198, 24, 13, muted);
        ui.temperatureText = Text("25.0 °C", content, 28, 404, 214, 50, 32);
        ui.vibrationText = Text("0.00 мм/с", content, 254, 409, 198, 40, 25);
        ui.temperatureFill = Bar(content, 28, 465, 424, new Color(1f, .7f, .24f));
        Text("0 °C                      НОРМА 25–70 °C                   100 °C", content, 28, 480, 424, 24, 11, muted);
        ui.stateDot = Panel("State indicator", content, 28, 530, 10, 10, accent);
        Text("СОСТОЯНИЕ", content, 49, 521, 170, 25, 13, muted);
        ui.stateText = Text("НОРМА", content, 220, 517, 232, 30, 22, accent);
        ui.progressText = Text("ОБРАБОТКА  0%", content, 28, 565, 424, 22, 13, muted);
        ui.processingFill = Bar(content, 28, 597, 424, accent);
        Text("ИЗГОТОВЛЕНО ДЕТАЛЕЙ", content, 28, 630, 300, 25, 14, muted);
        ui.countText = Text("000", content, 341, 615, 111, 48, 35, accent);
        ui.recommendationText = Text("Цикл работает автоматически.", content, 28, 682, 424, 63, 15, muted);
        ui.overheatButton = Button("Симулировать перегрев", content, 756, new Color(.61f, .26f, .15f), out var faultLabel);
        ui.faultButtonText = faultLabel;
        ui.resetButton = Button("Сбросить аварию", content, 817, new Color(.13f, .29f, .36f), out _);
        ui.enabled = true;
        var badge = Panel("Workshop overlay", canvas.transform, 25, 22, 500, 65, new Color(.045f, .065f, .095f, .9f));
        Text("AEM / ПРОИЗВОДСТВЕННЫЙ УЧАСТОК", badge.transform, 17, 10, 475, 26, 19);
        Text("УЧЕБНАЯ СИМУЛЯЦИЯ  ·  АВТОМАТИЧЕСКИЙ ЦИКЛ", badge.transform, 17, 39, 475, 20, 12, muted);
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
