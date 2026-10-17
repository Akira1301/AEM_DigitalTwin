using System.IO;
using AEM.DigitalTwin;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Camera rendering also works in batchmode, where ScreenCapture is not serviced.
public static class FactoryPreview
{
    public static void Capture(string path)
    {
        const int width = 1600, height = 900, worldWidth = 1120;
        var main = Camera.main;
        var canvas = Object.FindAnyObjectByType<Canvas>();
        var oldTarget = main.targetTexture;
        var oldRect = main.rect;
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        var scaler = canvas.GetComponent<CanvasScaler>();
        var oldScaleMode = scaler.uiScaleMode;
        float oldScale = scaler.scaleFactor;
        var previous = RenderTexture.active;
        var world = new RenderTexture(worldWidth, height, 24);
        var overlay = new RenderTexture(width, height, 24);
        var uiCamera = new GameObject("Preview UI camera").AddComponent<Camera>();
        var worldPixels = new Texture2D(worldWidth, height, TextureFormat.RGBA32, false);
        var uiPixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            main.cullingMask &= ~(1 << 5);
            main.targetTexture = world;
            main.rect = new Rect(0, 0, 1, 1);
            main.Render();
            RenderTexture.active = world;
            worldPixels.ReadPixels(new Rect(0, 0, worldWidth, height), 0, 0);
            worldPixels.Apply();
            uiCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            uiCamera.transform.position = new Vector3(0, 0, -1000);
            uiCamera.cullingMask = 1 << 5;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = Color.clear;
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = 450;
            uiCamera.targetTexture = overlay;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 10;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1;
            scaler.SendMessage("Update");
            Canvas.ForceUpdateCanvases();
            canvas.GetComponentInChildren<FactoryDashboardLayout>().SendMessage("LateUpdate");
            foreach (var label in canvas.GetComponentsInChildren<Text>()) label.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            uiCamera.Render();
            RenderTexture.active = overlay;
            uiPixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            uiPixels.Apply();
            var sceneColors = worldPixels.GetPixels32();
            var uiColors = uiPixels.GetPixels32();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                Color foreground = uiColors[i];
                Color background = x < worldWidth ? sceneColors[y * worldWidth + x] : Color.black;
                Color result = Color.Lerp(background, foreground, foreground.a);
                result.a = 1;
                uiColors[i] = result;
            }
            uiPixels.SetPixels32(uiColors);
            uiPixels.Apply();
            File.WriteAllBytes(path, uiPixels.EncodeToPNG());
        }
        finally
        {
            main.targetTexture = oldTarget;
            main.rect = oldRect;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            scaler.uiScaleMode = oldScaleMode;
            scaler.scaleFactor = oldScale;
            scaler.SendMessage("Update");
            RenderTexture.active = previous;
            Object.DestroyImmediate(uiCamera.gameObject);
            Object.DestroyImmediate(worldPixels);
            Object.DestroyImmediate(uiPixels);
            world.Release();
            overlay.Release();
            Object.DestroyImmediate(world);
            Object.DestroyImmediate(overlay);
        }
    }
}
