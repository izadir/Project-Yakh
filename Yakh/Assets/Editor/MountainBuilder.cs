using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static TestSlopeBuilder;

// Menu Yakh > Build Mountain makes Assets/Scenes/Mountain.unity: the Mountain component (it builds the snow, runs,
// features, and trees when the scene loads, see Mountain.cs), the skier at the summit, the chase camera, and haze.
static class MountainBuilder
{
    const string ScenePath = "Assets/Scenes/Mountain.unity";

    [MenuItem("Yakh/Build Mountain")]
    static void Build()
    {
        if (LayerMask.NameToLayer("Obstacle") < 0) { Debug.LogError("Add an Obstacle layer in Project Settings > Tags and Layers first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var m = new GameObject("Mountain").AddComponent<Mountain>();
        m.snow = Snow;
        m.feature = Feature;
        m.steel = Steel;
        m.bark = Mat("Bark", new Color(0.35f, 0.22f, 0.12f));
        m.pine = Mat("Pine", new Color(0.13f, 0.32f, 0.2f));
        AddSkier(Mountain.Spawn);

        // Haze: the fog, and a sky of the same color, so past the walls there is only haze.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.003f;
        RenderSettings.fogColor = new Color(0.78f, 0.86f, 0.95f);
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = RenderSettings.fogColor;
        Camera.main.farClipPlane = 1000f;   // the haze hides everything past it
        EditorSceneManager.SaveScene(scene, ScenePath);
    }
}
