using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu Yakh > Build Test Slope makes Assets/Scenes/TestSlope.unity:
// a slope with a table top jump, the skier, and the chase camera.
static class TestSlopeBuilder
{
    const string ScenePath = "Assets/Scenes/TestSlope.unity";
    const float Width = 30f, Thickness = 2f, PoleSpacing = 10f, SpawnAt = 16f, KinkLen = 3f;
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
                        var bar = Bar(slope, box ? "Box" : "Rail", c, rot, new Vector3(g.width, h, g.len), box ? feature : steel);
                        if (!box)
                            for (float z = -g.len / 2f + 0.5f; z < g.len / 2f; z += 7f)
                                Part(slope, PrimitiveType.Cube, c + fwd * z - up * (g.height / 2f), rot, new Vector3(0.05f, g.height, 0.05f), steel, false);
                        if (g.kink > 0f)
                        {
                            // Kicker: a short segment kinks up off the end, so the skier leaves the rail rising.
                            var kr = rot * Quaternion.Euler(-g.kink, 0f, 0f);
                            Vector3 end = c + fwd * (g.len / 2f), tip = end + kr * Vector3.forward * KinkLen;
                            bar.next = Bar(slope, bar.name, (end + tip) / 2f, kr, new Vector3(g.width, h, KinkLen), steel);
                            float rise = Vector3.Dot(tip - p, up);
                            Part(slope, PrimitiveType.Cube, tip - up * (rise / 2f), rot, new Vector3(0.05f, rise, 0.05f), steel, false);
                        }
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
        BuildSkier(skier, suit, steel, pole);
        foreach (var t in skierGo.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
        skier.spray = Spray(skierGo.transform, snow);

        var cam = Camera.main;
        cam.transform.position = spawn.Value + new Vector3(0f, 1.6f, -5f);
        cam.gameObject.AddComponent<ChaseCamera>().target = skier;
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Polygon skier: flat shaded lathe meshes, feet at the Body pivot, facing +z. Slim build in an athletic stance:
    // knees bent 40°, chest leaning 10°. Baggy pants, a long slim jacket, a neck, bent arms that hold poles,
    // a black beanie, and wide twin tip skis. Each limb is a ragdoll part with its pivot at its joint.
    // Joint limits follow Unity's Ragdoll Wizard, shifted for the bent rest pose. Twist turns about the skier's
    // right axis, and a positive twist swings the far end forward, except at the knee, where negative bends it.
    // Gear on the shins and forearms knocks the skis and poles loose in a hard crash (yard sale).
    static void BuildSkier(Skier skier, Material jacket, Material steel, Material skiColor)
    {
        Material pants = Mat("Pants", new Color(0.15f, 0.18f, 0.3f)), skin = Mat("Skin", new Color(0.95f, 0.8f, 0.65f)), black = Mat("Eye", Color.black);
        Transform b = skier.body;
        Quaternion lean = Quaternion.Euler(10f, 0f, 0f), id = Quaternion.identity;
        Vector3 waist = new Vector3(0f, 1.03f, 0f);
        var pelvis = Limb(b, "Pelvis", Lathe(8, 0.75f, 0f, -0.12f, 0.15f, -0.12f, 0.17f, -0.02f, 0.16f, 0.1f, 0f, 0.12f), new Vector3(0f, 0.95f, 0f), id, pants, 12f, null, 0f, 0f, 0f);
        // The shoulders are the widest ring of the jacket and slope in to a collar around the neck.
        var chest = Limb(b, "Chest", Lathe(8, 0.62f, 0f, -0.08f, 0.165f, -0.08f, 0.16f, 0.05f, 0.16f, 0.2f, 0.175f, 0.36f, 0.11f, 0.43f, 0.06f, 0.455f, 0f, 0.46f), waist, lean, jacket, 16f, pelvis, -20f, 20f, 10f);
        var head = Limb(b, "Head", Lathe(8, 1f, 0f, 0f, 0.05f, 0f, 0.05f, 0.07f, 0.095f, 0.1f, 0.11f, 0.17f, 0.105f, 0.25f, 0.06f, 0.3f, 0f, 0.31f), waist + lean * new Vector3(0f, 0.44f, 0f), id, skin, 5f, chest, -40f, 25f, 25f);
        Piece(head.transform, "Beanie", Lathe(8, 1f, 0f, 0.15f, 0.122f, 0.15f, 0.122f, 0.2f, 0.112f, 0.26f, 0.085f, 0.31f, 0.04f, 0.35f, 0f, 0.355f), Vector3.zero, id, black);
        skier.pelvis = pelvis;
        foreach (float s in new[] { -1f, 1f })
        {
            Part(head.transform, PrimitiveType.Cube, new Vector3(0.035f * s, 0.125f, 0.09f), id, Vector3.one * 0.025f, black, false);

            // Legs: the shin tilts 20° forward from the ankle, the thigh 20° back from the knee, so the hip sits over the ankle.
            Quaternion fwd = Quaternion.Euler(20f, 0f, 0f), back = Quaternion.Euler(-20f, 0f, 0f);
            Vector3 ankle = new Vector3(0.11f * s, 0.08f, 0f), knee = ankle + fwd * Vector3.up * 0.44f, hip = knee + back * Vector3.up * 0.45f;
            var thigh = Limb(b, "Thigh", Lathe(6, 1f, 0f, -0.45f, 0.09f, -0.45f, 0.095f, -0.05f, 0f, 0f), hip, back, pants, 8f, pelvis, -40f, 50f, 30f);
            var shin = Limb(b, "Shin", Lathe(6, 1f, 0f, -0.44f, 0.1f, -0.44f, 0.09f, 0f, 0f, 0.02f), knee, fwd, pants, 5f, thigh, -90f, 40f, 0f);

            // Ski: the collider is on the root, so the twin tips need no scale of their own.
            var ski = new GameObject("Ski", typeof(BoxCollider)).transform;
            ski.SetParent(b, false);
            ski.localPosition = new Vector3(0.11f * s, 0.02f, 0.05f);
            ski.GetComponent<BoxCollider>().size = new Vector3(0.14f, 0.04f, 1.45f);
            Part(ski, PrimitiveType.Cube, Vector3.zero, id, new Vector3(0.14f, 0.04f, 1.45f), skiColor, false);
            foreach (float e in new[] { -1f, 1f })
            {
                var tip = Quaternion.Euler(-20f * e, 0f, 0f);
                Part(ski, PrimitiveType.Cube, new Vector3(0f, 0f, 0.725f * e) + tip * new Vector3(0f, 0f, 0.11f * e), tip, new Vector3(0.14f, 0.04f, 0.22f), skiColor, false);
            }
            ski.SetParent(shin.transform, true);
            Yard(shin, ski, 40f);

            // Arms: the upper arm hangs from inside the shoulder ring, the forearm reaches forward to the hand.
            // The pole goes from the hand down and back to just above the snow.
            Vector3 shoulder = waist + lean * new Vector3(0.14f * s, 0.35f, 0f), upper = new Vector3(0.15f * s, -1f, 0.1f).normalized;
            Vector3 elbow = shoulder + upper * 0.28f, fore = (new Vector3(0.22f * s, 0.92f, 0.32f) - elbow).normalized;
            Vector3 hand = elbow + fore * 0.29f, poleDir = new Vector3(0.08f * s, -1f, -0.4f).normalized;
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
    // Twist turns about the skier's right axis, swing about its forward axis.
    static Rigidbody Limb(Transform body, string name, Mesh mesh, Vector3 pos, Quaternion rot, Material m, float mass,
                          Rigidbody parent, float lowTwist, float highTwist, float swing)
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
        j.swing2Limit = new SoftJointLimit { limit = 0f };
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

    // Flat shaded solid of revolution. The profile is (radius, height) pairs from the bottom center to the top center.
    // depth squashes it front to back. A face, not an edge, points forward (+z), so details sit flat on it.
    static Mesh Lathe(int sides, float depth, params float[] profile)
    {
        var v = new System.Collections.Generic.List<Vector3>();
        Vector3 P(int i, int s)
        {
            float a = (s + 0.5f) * 2f * Mathf.PI / sides, r = profile[2 * i];
            return new Vector3(Mathf.Sin(a) * r, profile[2 * i + 1], Mathf.Cos(a) * r * depth);
        }
        for (int i = 0; i + 1 < profile.Length / 2; i++)
            for (int s = 0; s < sides; s++)
                v.AddRange(new[] { P(i + 1, s + 1), P(i + 1, s), P(i, s), P(i + 1, s + 1), P(i, s), P(i, s + 1) });
        var m = new Mesh { vertices = v.ToArray(), triangles = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Range(0, v.Count)) };
        m.RecalculateNormals();   // no shared vertices, so every face gets its own flat normal
        return m;
    }

    // A grind feature: solid, on the Rail layer. The skier's ground check skips that layer,
    // and the skier has no collision while it rides a rail.
    static Rail Bar(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Material m)
    {
        var bar = Part(parent, PrimitiveType.Cube, pos, rot, size, m);
        bar.name = name;
        bar.layer = LayerMask.NameToLayer("Rail");
        return bar.AddComponent<Rail>();
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
