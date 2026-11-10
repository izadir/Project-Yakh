using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu Yakh > Build Test Slope makes Assets/Scenes/TestSlope.unity:
// a slope with a table top jump, the skier, and the chase camera.
static class TestSlopeBuilder
{
    const string ScenePath = "Assets/Scenes/TestSlope.unity";
    const float Width = 30f, Thickness = 2f, PoleSpacing = 10f, SpawnAt = 16f;
    // Grind features: profile segment (straight ones only), start distance into it, side offset,
    // length, width, and top height above the snow (m). A width over 0.2 m makes a box, not a rail.
    static readonly (int seg, float at, float x, float len, float width, float height)[] Grinds =
    {
        (2, 12f, 5f, 30f, 0.08f, 0.45f),   // down rail, main slope, right
        (2, 10f, -6f, 30f, 0.5f, 0.3f),    // down box, main slope, left
        (9, 6f, 5f, 28f, 0.08f, 0.5f),     // rail on the runout, right
        (9, 6f, -5f, 28f, 0.5f, 0.3f),     // box on the runout, left
        (11, 8f, 0f, 25f, 0.08f, 0.5f),    // flat rail on the flat end
    };

    // The hill from top to bottom: length (m), start and end angle (deg, positive is downhill), feature color.
    // A start angle that differs from the previous end angle makes a sharp edge, as at the lip.
    static readonly (float len, float a0, float a1, bool feature)[] Profile =
    {
        (12, 0, 0, false),      // start deck
        (10, 0, 18, false),     // roll in
        (50, 18, 18, false),    // main slope
        (7, 18, -20, true),     // kicker transition
        (1.5f, -20, -20, true), // lip
        (8, 0, 0, true),        // table
        (3, 0, 30, true),       // knuckle
        (30, 30, 30, true),     // landing
        (8, 30, 10, false),
        (40, 6, 6, false),      // runout
        (15, 6, 0, false),
        (40, 0, 0, false),      // flat end
        (12, 0, -30, false),    // uphill end stops the skier, about 23 m tall so it holds at full speed
        (40, -30, -30, false),
    };

    [MenuItem("Yakh/Build Test Slope")]
    static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Material snow = Mat("Snow", new Color(0.93f, 0.95f, 1f)), feature = Mat("Feature", new Color(0.55f, 0.75f, 1f));
        Material pole = Mat("Pole", new Color(1f, 0.45f, 0.1f)), suit = Mat("Skier", new Color(0.9f, 0.15f, 0.15f));
        Material steel = Mat("Rail", new Color(0.25f, 0.25f, 0.3f));

        var slope = new GameObject("Slope").transform;
        Vector3 p = Vector3.zero;
        Vector3? spawn = null;
        float dist = 0f, nextPole = 0f;
        for (int si = 0; si < Profile.Length; si++)
        {
            var s = Profile[si];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(s.a1 - s.a0) / 4f)); // curves become 4° steps
            float len = s.len / steps;
            for (int i = 0; i < steps; i++)
            {
                var rot = Quaternion.Euler(Mathf.Lerp(s.a0, s.a1, (i + 0.5f) / steps), 0f, 0f);
                Vector3 up = rot * Vector3.up, fwd = rot * Vector3.forward, q = p + fwd * len;
                Part(slope, PrimitiveType.Cube, (p + q) / 2f - up * (Thickness / 2f), rot, new Vector3(Width, Thickness, len), s.feature ? feature : snow);
                for (; nextPole <= dist + len; nextPole += PoleSpacing)
                {
                    Vector3 at = p + fwd * (nextPole - dist) + Vector3.up;
                    foreach (float x in new[] { -1f, 1f })
                        Part(slope, PrimitiveType.Cube, at + Vector3.right * (x * (Width / 2f + 0.5f)), Quaternion.identity, new Vector3(0.15f, 2f, 0.15f), pole);
                }
                if (dist + len >= SpawnAt) spawn ??= p + fwd * (SpawnAt - dist) + up * 0.45f;
                if (i == 0)
                    foreach (var g in Grinds)
                    {
                        if (g.seg != si) continue;
                        // A trigger, so the skier never collides with it. Skier.cs locks on instead. Posts have no collider.
                        bool box = g.width > 0.2f;
                        float h = box ? g.height : 0.08f;
                        Vector3 c = p + fwd * (g.at + g.len / 2f) + up * (g.height - h / 2f) + Vector3.right * g.x;
                        var bar = Part(slope, PrimitiveType.Cube, c, rot, new Vector3(g.width, h, g.len), box ? feature : steel);
                        bar.name = box ? "Box" : "Rail";
                        bar.GetComponent<Collider>().isTrigger = true;
                        bar.AddComponent<Rail>();
                        if (!box)
                            for (float z = -g.len / 2f + 0.5f; z < g.len / 2f; z += 7f)
                                Part(slope, PrimitiveType.Cube, c + fwd * z - up * (g.height / 2f), rot, new Vector3(0.05f, g.height, 0.05f), steel, false);
                    }
                p = q;
                dist += len;
            }
        }

        var skierGo = new GameObject("Skier", typeof(SphereCollider), typeof(Rigidbody), typeof(Skier));
        skierGo.layer = 2; // Ignore Raycast, so the ground check skips the skier
        skierGo.transform.position = spawn.Value;
        skierGo.GetComponent<SphereCollider>().radius = 0.4f;
        var skier = skierGo.GetComponent<Skier>();
        skier.body = new GameObject("Body").transform;
        skier.body.SetParent(skierGo.transform, false);
        skier.body.localPosition = new Vector3(0f, -0.4f, 0f);
        // Base model, feet at the Body pivot: legs, torso, head, and two eyes on the front (+z).
        Material pants = Mat("Pants", new Color(0.15f, 0.18f, 0.3f)), skin = Mat("Skin", new Color(0.95f, 0.8f, 0.65f)), eye = Mat("Eye", Color.black);
        foreach (float x in new[] { -0.12f, 0.12f })
        {
            Part(skier.body, PrimitiveType.Capsule, new Vector3(x, 0.45f, 0f), Quaternion.identity, new Vector3(0.18f, 0.425f, 0.18f), pants, false);
            Part(skier.body, PrimitiveType.Sphere, new Vector3(x * 0.46f, 1.73f, 0.125f), Quaternion.identity, Vector3.one * 0.045f, eye, false);
            Part(skierGo.transform, PrimitiveType.Cube, new Vector3(x, -0.38f, 0f), Quaternion.identity, new Vector3(0.1f, 0.04f, 1.7f), pole, false);
        }
        Part(skier.body, PrimitiveType.Capsule, new Vector3(0f, 1.2f, 0f), Quaternion.identity, new Vector3(0.42f, 0.35f, 0.28f), suit, false);
        Part(skier.body, PrimitiveType.Sphere, new Vector3(0f, 1.7f, 0f), Quaternion.identity, Vector3.one * 0.28f, skin, false);
        skier.spray = Spray(skierGo.transform, snow);

        var cam = Camera.main;
        cam.transform.position = spawn.Value + new Vector3(0f, 1.6f, -5f);
        cam.gameObject.AddComponent<ChaseCamera>().target = skier;
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static Material Mat(string name, Color color)
    {
        string path = $"Assets/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 size, Material m, bool solid = true)
    {
        var go = GameObject.CreatePrimitive(type);
        if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(pos, rot);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // Powder spray: small snow balls thrown from the skis. Skier.cs sets the rate and the direction.
    static ParticleSystem Spray(Transform skier, Material snow)
    {
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh ball = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);

        var go = new GameObject("Spray", typeof(ParticleSystem));
        go.transform.SetParent(skier, false);
        go.transform.localPosition = new Vector3(0f, -0.35f, 0f);
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.7f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.2f);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 3000;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.4f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = ball;
        r.sharedMaterial = snow;
        return ps;
    }
}
