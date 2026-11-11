using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Shapes;

// Menu Yakh > Build Test Slope makes Assets/Scenes/TestSlope.unity:
// a slope with a table top jump, the skier, and the chase camera.
// The helpers marked internal also build the mountain scene (MountainBuilder.cs). Mesh helpers are in Shapes.cs.
static class TestSlopeBuilder
{
    internal static Material Snow => Mat("Snow", new Color(0.93f, 0.95f, 1f));
    internal static Material Feature => Mat("Feature", new Color(0.55f, 0.75f, 1f));
    internal static Material Steel => Mat("Rail", new Color(0.25f, 0.25f, 0.3f));
    internal static Material Orange => Mat("Pole", new Color(1f, 0.45f, 0.1f));

    const string ScenePath = "Assets/Scenes/TestSlope.unity";
    const float Width = 30f, Thickness = 2f, PoleSpacing = 10f, SpawnAt = 16f;
    // Grind features: profile segment (straight ones only), start distance into it, side offset,
    // length, width, top height above the snow (m), and a kicker kink at the end (deg up, 0 is none).
    // A width over 0.2 m makes a box, not a rail.
    static readonly (int seg, float at, float x, float len, float width, float height, float kink)[] Grinds =
    {
        (2, 12f, 5f, 30f, 0.08f, 0.45f, 0f),   // down rail, main slope, right
        (2, 10f, -6f, 30f, 0.5f, 0.3f, 0f),    // down box, main slope, left
        (2, 5f, -3f, 14f, 0.08f, 0.5f, 30f),   // kicker rail, main slope, 3 m left of the spawn line
        (9, 6f, 5f, 28f, 0.08f, 0.5f, 0f),     // rail on the runout, right
        (9, 6f, -5f, 28f, 0.5f, 0.3f, 0f),     // box on the runout, left
        (11, 8f, 3f, 25f, 0.08f, 0.5f, 0f),    // flat rail on the flat end, 3 m right, so the jump line stays clear
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
        Material snow = Snow, feature = Feature, pole = Orange;

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
                        if (g.seg == si) Grind(slope, p, rot, g.at, g.x, g.len, g.width, g.height, g.kink, feature, Steel);
                p = q;
                dist += len;
            }
        }

        AddSkier(spawn.Value);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // The skier at spawn, facing +z, and the chase camera behind it.
    internal static void AddSkier(Vector3 spawn)
    {
        var skierGo = new GameObject("Skier", typeof(SphereCollider), typeof(Rigidbody), typeof(Skier));
        skierGo.layer = 2; // Ignore Raycast, so the ground check skips the skier
        skierGo.transform.position = spawn;
        skierGo.GetComponent<SphereCollider>().radius = 0.4f;
        var skier = skierGo.GetComponent<Skier>();
        skier.body = new GameObject("Body").transform;
        skier.body.SetParent(skierGo.transform, false);
        skier.body.localPosition = new Vector3(0f, -0.4f, 0f);
        BuildSkier(skier, Mat("Skier", new Color(0.9f, 0.15f, 0.15f)), Steel, Orange);
        foreach (var t in skierGo.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
        skier.spray = Spray(skierGo.transform, Snow);
        skier.sparks = Sparks(skierGo.transform);

        var cam = Camera.main;
        cam.transform.position = spawn + new Vector3(0f, 1.6f, -5f);
        cam.gameObject.AddComponent<ChaseCamera>().target = skier;
    }

    // Polygon skier: flat shaded lathe meshes, feet at the Body pivot, facing +z. Slim build in an athletic stance:
    // knees bent 40°, chest leaning 10°. Wide shoulders on a skinny waist, pants fitted at the hips that get
    // baggier down the leg and bell out at the hem, a neck, relaxed arms with the poles at the hips and the tips trailing behind, a black beanie, black ski boots, and wide twin tip skis. Each limb is a ragdoll part with its pivot at its joint.
    // Joint limits follow Unity's Ragdoll Wizard, shifted for the bent rest pose and widened to the poses Skier.Animate
    // makes (deep knee bends, grabs, the grind turn), so a bail from any pose does not snap. Twist turns about the
    // skier's right axis, and a positive twist swings the far end forward, except at the knee, where negative bends it.
    // Gear on the shins and forearms knocks the skis and poles loose in a hard crash (yard sale).
    static void BuildSkier(Skier skier, Material jacket, Material steel, Material skiColor)
    {
        Material pants = Mat("Pants", new Color(0.15f, 0.18f, 0.3f)), skin = Mat("Skin", new Color(0.95f, 0.8f, 0.65f)), black = Mat("Eye", Color.black);
        Transform b = skier.body;
        Quaternion lean = Quaternion.Euler(10f, 0f, 0f), id = Quaternion.identity;
        Vector3 waist = new Vector3(0f, 1.03f, 0f);
        var pelvis = Limb(b, "Pelvis", Lathe(8, 0.75f, 0f, -0.12f, 0.14f, -0.12f, 0.165f, -0.02f, 0.135f, 0.1f, 0f, 0.12f), new Vector3(0f, 0.95f, 0f), id, pants, 12f, null, 0f, 0f, 0f);
        // A V taper: a skinny waist out to wide shoulders, the widest ring, which slope in to a collar around the neck.
        var chest = Limb(b, "Chest", Lathe(8, 0.55f, 0f, -0.08f, 0.125f, -0.08f, 0.12f, 0.05f, 0.15f, 0.2f, 0.225f, 0.34f, 0.2f, 0.4f, 0.09f, 0.44f, 0.055f, 0.455f, 0f, 0.46f), waist, lean, jacket, 16f, pelvis, -20f, 50f, 10f, 60f);
        var head = Limb(b, "Head", Lathe(8, 1f, 0f, 0f, 0.05f, 0f, 0.05f, 0.07f, 0.095f, 0.1f, 0.11f, 0.17f, 0.105f, 0.25f, 0.06f, 0.3f, 0f, 0.31f), waist + lean * new Vector3(0f, 0.44f, 0f), id, skin, 5f, chest, -40f, 25f, 25f, 75f);
        Piece(head.transform, "Beanie", Lathe(8, 1f, 0f, 0.15f, 0.122f, 0.15f, 0.122f, 0.2f, 0.112f, 0.26f, 0.085f, 0.31f, 0.04f, 0.35f, 0f, 0.355f), Vector3.zero, id, black);
        skier.pelvis = pelvis;
        foreach (float s in new[] { -1f, 1f })
        {
            Part(head.transform, PrimitiveType.Cube, new Vector3(0.035f * s, 0.125f, 0.09f), id, Vector3.one * 0.025f, black, false);

            // Legs: the shin tilts 20° forward from the ankle, the thigh 20° back from the knee, so the hip sits over the ankle.
            // The pant leg is fitted at the hip and gets wider to a bell at the hem. The legs are close, so the hems touch:
            // each leg turns 30° on its own axis so a flat side faces in, and Skier lets the shins pass through each other.
            Quaternion fwd = Quaternion.Euler(20f, 0f, 0f), back = Quaternion.Euler(-20f, 0f, 0f), flat = Quaternion.Euler(0f, 30f, 0f);
            Vector3 ankle = new Vector3(0.09f * s, 0.08f, 0f), knee = ankle + fwd * Vector3.up * 0.44f, hip = knee + back * Vector3.up * 0.45f;
            var thigh = Limb(b, "Thigh", Lathe(6, 1.1f, 0f, -0.45f, 0.1f, -0.45f, 0.088f, -0.2f, 0.075f, -0.02f, 0f, 0f), hip, back * flat, pants, 8f, pelvis, -40f, 120f, 50f);
            var shin = Limb(b, "Shin", Lathe(6, 1.25f, 0f, -0.44f, 0.12f, -0.44f, 0.112f, -0.36f, 0.104f, -0.15f, 0.1f, 0f, 0f, 0.02f), knee, fwd * flat, pants, 5f, thigh, -150f, 40f, 0f);

            // Ski: the collider is on the root, so the twin tips need no scale of their own.
            var ski = new GameObject("Ski", typeof(BoxCollider)).transform;
            ski.SetParent(b, false);
            ski.localPosition = new Vector3(0.09f * s, 0.02f, 0.05f);
            ski.GetComponent<BoxCollider>().size = new Vector3(0.14f, 0.04f, 1.45f);
            Part(ski, PrimitiveType.Cube, Vector3.zero, id, new Vector3(0.14f, 0.04f, 1.45f), skiColor, false);
            foreach (float e in new[] { -1f, 1f })
            {
                var tip = Quaternion.Euler(-20f * e, 0f, 0f);
                Part(ski, PrimitiveType.Cube, new Vector3(0f, 0f, 0.725f * e) + tip * new Vector3(0f, 0f, 0.11f * e), tip, new Vector3(0.14f, 0.04f, 0.22f), skiColor, false);
            }
            // Boot: in the binding, with the pant hem stacked over it, so the toe and the heel show. It is on the shin,
            // so it stays on the leg in a yard sale. While skiing, Skier holds it on the ski.
            var boot = new GameObject("Boot").transform;
            boot.SetParent(b, false);
            boot.localPosition = ski.localPosition;
            Piece(boot, "Shell", Lathe(4, 2.6f, 0f, 0f, 0.085f, 0f, 0.085f, 0.07f, 0.075f, 0.12f, 0f, 0.12f), new Vector3(0f, 0.02f, -0.01f), id, black);
            boot.SetParent(shin.transform, true);
            ski.SetParent(shin.transform, true);
            Yard(shin, ski, 40f);

            // Arms, relaxed: the upper arm hangs from inside the shoulder ring, the elbow bends a little, and the hand
            // sits beside the hip. The pole points down and back at 45°, its tip about 14 cm above the snow.
            Vector3 shoulder = waist + lean * new Vector3(0.19f * s, 0.33f, 0f), upper = new Vector3(0.08f * s, -1f, -0.05f).normalized;
            Vector3 elbow = shoulder + upper * 0.28f, fore = (new Vector3(0.27f * s, 0.82f, 0.14f) - elbow).normalized;
            Vector3 hand = elbow + fore * 0.29f, poleDir = new Vector3(0.12f * s, -1f, -1f).normalized;
            var arm = Limb(b, "UpperArm", Lathe(6, 1f, 0f, -0.29f, 0.042f, -0.29f, 0.05f, -0.04f, 0.035f, 0.02f, 0f, 0.03f), shoulder, Quaternion.FromToRotation(Vector3.down, upper), jacket, 2.5f, chest, -70f, 120f, 50f);
            var forearm = Limb(b, "Forearm", Lathe(6, 1f, 0f, -0.27f, 0.035f, -0.27f, 0.042f, -0.02f, 0f, 0f), elbow, Quaternion.FromToRotation(Vector3.down, fore), jacket, 1.5f, arm, -50f, 95f, 0f);
            Part(forearm.transform, PrimitiveType.Cube, new Vector3(0f, -0.29f, 0f), id, Vector3.one * 0.07f, black, false);
            var pole = Piece(b, "Pole", Lathe(6, 1f, 0f, -1.01f, 0.012f, -1.01f, 0.012f, -0.95f, 0.05f, -0.95f, 0.05f, -0.93f, 0.012f, -0.93f, 0.012f, -0.12f, 0.02f, -0.12f, 0.02f, 0f, 0f, 0f),
                             hand - poleDir * 0.06f, Quaternion.FromToRotation(Vector3.down, poleDir), steel).transform;
            var cap = pole.gameObject.AddComponent<CapsuleCollider>();
            cap.radius = 0.02f;
            cap.height = 1.01f;
            cap.center = new Vector3(0f, -0.505f, 0f);
            pole.SetParent(forearm.transform, true);
            Yard(forearm, pole, 9f);
        }
    }

    static void Yard(Rigidbody limb, Transform item, float tolerance)
    {
        var g = limb.gameObject.AddComponent<Gear>();
        g.item = item;
        g.tolerance = tolerance;
    }

    // A ragdoll part: a piece with a convex collider and a kinematic Rigidbody, joined to its parent part at its pivot.
    // Twist turns about the skier's right axis, swing about its forward axis, yaw about its up axis.
    static Rigidbody Limb(Transform body, string name, Mesh mesh, Vector3 pos, Quaternion rot, Material m, float mass,
                          Rigidbody parent, float lowTwist, float highTwist, float swing, float yaw = 0f)
    {
        var go = Piece(body, name, mesh, pos, rot, m);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.GetComponent<MeshCollider>().convex = true;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        if (!parent) return rb;
        var j = go.AddComponent<CharacterJoint>();
        j.connectedBody = parent;
        j.axis = Quaternion.Inverse(rot) * Vector3.right;
        j.swingAxis = Quaternion.Inverse(rot) * Vector3.forward;
        j.lowTwistLimit = new SoftJointLimit { limit = lowTwist };
        j.highTwistLimit = new SoftJointLimit { limit = highTwist };
        j.swing1Limit = new SoftJointLimit { limit = swing };
        j.swing2Limit = new SoftJointLimit { limit = yaw };   // the chest and head turn, so a switch look stays inside the limit
        j.enableProjection = true;
        j.enablePreprocessing = false;   // as the wizard does, so a part that starts inside geometry does not explode
        return rb;
    }

    static GameObject Piece(Transform parent, string name, Mesh mesh, Vector3 pos, Quaternion rot, Material m)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(pos, rot);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    internal static Material Mat(string name, Color color, string shader = "Universal Render Pipeline/Lit")
    {
        string path = $"Assets/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        m = new Material(Shader.Find(shader)) { color = color };
        AssetDatabase.CreateAsset(m, path);
        return m;
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

    // Grind sparks: short bright streaks thrown back and up from the skis on a metal rail. Skier sets the rate and the direction.
    static ParticleSystem Sparks(Transform skier)
    {
        var go = new GameObject("Sparks", typeof(ParticleSystem));
        go.transform.SetParent(skier, false);
        go.transform.localPosition = new Vector3(0f, -0.4f, 0f);   // the bottom of the skier's sphere, on the rail top
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.6f, 0.15f));
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 40f;
        shape.radius = 0.05f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.03f;
        r.lengthScale = 1f;
        r.sharedMaterial = Mat("Spark", new Color(1f, 0.8f, 0.3f), "Universal Render Pipeline/Particles/Unlit");
        return ps;
    }
}
