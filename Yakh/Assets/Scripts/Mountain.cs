using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static Shapes;

// The mountain, a big area to play in. Built when the scene loads, so the scene file stays small
// (Yakh > Build Mountain makes the scene). The design is fixed, so it is the same every time.
// The snow is a height field: a fall line profile that gets steeper all the way down, natural bumps, and walls at the
// sides. Runs are paths carved into it, in order. Each one flattens a corridor, and its height offset along the path
// raises a spine (a ridge with a flat top) or cuts a canyon (a U gully). A branch splits from another run and never
// undoes it. Cat tracks are flat roads across the mountain. Canyons and cat tracks are gaps: where Main Street or a
// ridge meets one, a kicker sends the skier over it. Where the Tube passes under Highline, a tunnel roof carries the
// ridge over it. Park features sit on the runs, and trees cover the rest as forest and glades. Trees and the steep walls
// of park features and tunnel portals are on the Obstacle layer.
public class Mountain : MonoBehaviour
{
    public Material snow, feature, steel, bark, pine;

    const float Cell = 4f, Edge = 245f, Walls = 205f, Band = 20f, Arch = 6f;
    const int Sub = 4;   // a fine quad is Cell / Sub = 1 m
    static readonly float Sin75 = Mathf.Sin(75f * Mathf.Deg2Rad);

    // Fall line from the summit: length (m), start and end angle (deg, positive is downhill). It gets steeper all the way
    // down. Hands off (simulated): the park holds 21 to 23 m/s (its jumps are sized for that), the upper mountain
    // carries 27 to 31 (the chain), the middle 33 to 40, the steeps 41 to 44, and the face reaches 46. The runout
    // brings that down to about 20, and the uphill end stops the skier.
    static readonly (float len, float a0, float a1)[] Fall =
    {
        (50, 0, 0),         // summit, the spawn
        (40, 0, 10),
        (120, 10, 10),      // roll in
        (10, 10, 9),
        (340, 9, 9),        // the mega park
        (40, 9, 12),
        (250, 12, 14),      // upper mountain
        (250, 14, 17),
        (300, 17, 20),      // middle
        (300, 20, 23),
        (300, 23, 26),      // the steeps
        (300, 26, 29),
        (250, 29, 32),      // the face
        (80, 32, 12),
        (150, 12, 4),       // runout
        (100, 4, 0),
        (20, 0, -30),       // uphill end stops the skier
        (50, -30, -30),
    };
    static readonly List<Vector2> FallLine = Walk();   // (z, y) every 2 m or less

    // Runs, carved in this order, so a canyon cuts through everything. Name, half width (the flat part), falloff to the
    // natural snow, gap (a canyon or cat track that other runs jump), height offsets along the run (distance, m: up is
    // a spine, down is a canyon, a negative distance counts from the end), and the path (x, z) through the mountain.
    static readonly (float, float)[] Flat = new (float, float)[0];
    static readonly Run Main = new("Main Street", 40f, 20f, false, Flat,   // straight through the park, then a slight wiggle
        new(0, 30), new(0, 200), new(0, 600), new(12, 850), new(-10, 1100), new(8, 1400), new(-8, 1700), new(10, 2000), new(0, 2300), new(0, 2750));
    static readonly Run Highline = new("Highline", 8f, 26f, false, new[] { (60f, 0f), (220f, 14f), (-260f, 14f), (-257f, 0f) },   // splits off Main, a 14 m cliff onto the face
        new(-20, 620), new(-70, 720), new(-115, 840), new(-140, 1000), new(-148, 1200), new(-135, 1400), new(-148, 1600),
        new(-140, 1800), new(-115, 2000), new(-90, 2150), new(-60, 2280), new(-25, 2420));   // merges back into Main
    static readonly (float, float)[] Fork = { (0f, 14f), (555f, 14f), (558f, 0f) };   // a fork of a ridge: full height, a 14 m cliff onto the face
    static readonly Run Wishbone = new("Wishbone", 8f, 26f, false, new[] { (0f, 0f), (130f, 14f) },
        new(95, 1180), new(125, 1300), new(150, 1450), new(158, 1620));
    static readonly Run WishL = new("Wishbone left", 8f, 26f, false, Fork,
        new(158, 1620), new(135, 1750), new(112, 1900), new(110, 2060), new(95, 2160), new(70, 2300), new(45, 2440)) { branch = true };   // merges into Main
    static readonly Run WishR = new("Wishbone right", 8f, 26f, false, Fork,
        new(158, 1620), new(180, 1750), new(182, 1900), new(175, 2060), new(160, 2160), new(135, 2300), new(95, 2420), new(60, 2500)) { branch = true };
    static readonly Run Tube = new("Tube", 6f, 6f, false, Flat,   // splits off Main, twice through Highline in a tunnel, back into Main
        new(-25, 1470), new(-75, 1515), new(-150, 1570), new(-192, 1630), new(-198, 1760), new(-190, 1880), new(-135, 1960),
        new(-80, 2010), new(-30, 2060));
    static readonly Run TrackA = new("Cat track A", 6f, 8f, true, Flat, new(-205, 700), new(205, 740));
    static readonly Run TrackB = new("Cat track B", 6f, 8f, true, Flat, new(205, 1240), new(-205, 1280));
    static readonly Run TrackC = new("Cat track C", 6f, 8f, true, Flat, new(-205, 1790), new(205, 1830));
    static readonly Run Canyon = new("Canyon", 13f, 8f, true, new[] { (0f, 0f), (50f, -12f), (-150f, -12f), (-1f, 0f) },   // a long fade, so the floor runs downhill to the exit
        new(70, 610), new(120, 720), new(132, 850), new(95, 1000), new(10, 1120), new(-70, 1180), new(-95, 1270), new(-60, 1350),
        new(20, 1410), new(65, 1490), new(60, 1570), new(35, 1650));   // the exit rises onto Main Street
    static readonly Run CanyonArm = new("Canyon arm", 13f, 8f, true, new[] { (0f, -12f) },   // splits around an island and rejoins
        new(120, 720), new(170, 780), new(180, 860), new(150, 920), new(108, 950)) { branch = true };
    static readonly Run[] Runs = { Main, Highline, Wishbone, WishL, WishR, Tube, TrackA, TrackB, TrackC, Canyon, CanyonArm };

    // Mega park rail garden at the top of Main Street: distance past the park start, side offset, length, width, height, kink (see Grind).
    static readonly (float at, float x, float len, float width, float height, float kink)[] Rails =
    {
        (15, 21, 24, 0.6f, 0.5f, 0), (15, 31, 24, 0.08f, 0.6f, 0),
        (70, 26, 40, 0.08f, 0.6f, 0),
        (130, 21, 16, 0.08f, 0.5f, 30), (130, 31, 30, 0.6f, 0.4f, 0),
        (190, 26, 50, 0.08f, 0.7f, 0),
        (260, 21, 20, 0.6f, 0.5f, 0), (260, 31, 18, 0.08f, 0.5f, 35),
    };

    public static Vector3 Spawn => new(0f, Height(0f, 20f) + 0.45f, 20f);

    void Awake()
    {
        int obstacle = LayerMask.NameToLayer("Obstacle");
        Snow();
        Features(obstacle);
        Forest(obstacle);
        StaticBatchingUtility.Combine(gameObject);
    }

    // The height field as flat shaded quads, in chunks of 40 rows. A 4 m quad that misses the snow by more than 15 cm
    // (canyon walls, spine flanks, run edges), at its center (the mean of its corners, so a gully along a diagonal counts)
    // or at the middle of an edge, becomes 16 quads of 1 m.
    // Each quad splits along the diagonal that fits the snow better, so a gully floor has no ridges across it.
    void Snow()
    {
        int cols = Mathf.RoundToInt(2f * Edge / Cell), rows = Mathf.FloorToInt(FallLine[^1].x / Cell);
        // The height samples are pure math, so they run on all cores. The meshes are made on the main thread.
        var h = new float[2 * rows + 1, 2 * cols + 1];   // every 2 m: the corners, edge middles, and centers of the 4 m quads
        Parallel.For(0, 2 * rows + 1, i =>
        {
            for (int j = 0; j <= 2 * cols; j++) h[i, j] = Height(-Edge + j * Cell / 2f, i * Cell / 2f);
        });
        var fine = new bool[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int i = 2 * r, j = 2 * c;
                float a = h[i, j], b = h[i + 2, j], cc = h[i + 2, j + 2], d = h[i, j + 2], m = h[i + 1, j + 1];
                fine[r, c] = Mathf.Max(Mathf.Abs(m - (a + b + cc + d) / 4f), Miss(h[i + 1, j], a, b), Miss(h[i + 2, j + 1], b, cc),
                                       Miss(h[i + 1, j + 2], cc, d), Miss(h[i, j + 1], d, a)) > 0.15f;
            }
        bool Fine(int r, int c) => r >= 0 && c >= 0 && r < rows && c < cols && fine[r, c];

        // A 1 m vertex, in 1 m steps (every second one is on the 2 m grid). On the edge of a 4 m quad it stays on the
        // straight edge, so the 1 m and 4 m quads meet with no gap.
        Vector3 F(int i, int j)
        {
            int r = i / Sub, c = j / Sub, ri = i % Sub, cj = j % Sub;
            float y = ri == 0 && !(Fine(r - 1, c) && Fine(r, c)) ? Mathf.Lerp(h[2 * r, 2 * c], h[2 * r, Mathf.Min(2 * c + 2, 2 * cols)], cj / (float)Sub)
                    : cj == 0 && !(Fine(r, c - 1) && Fine(r, c)) ? Mathf.Lerp(h[2 * r, 2 * c], h[Mathf.Min(2 * r + 2, 2 * rows), 2 * c], ri / (float)Sub)
                    : i % 2 == 0 && j % 2 == 0 ? h[i / 2, j / 2] : Height(-Edge + j * Cell / Sub, i * Cell / Sub);
            return new Vector3(-Edge + j * Cell / Sub, y, i * Cell / Sub);
        }

        Vector3 V(int r, int c) => new(-Edge + c * Cell, h[2 * r, 2 * c], r * Cell);
        var chunks = new List<Vector3>[(rows + 39) / 40];
        Parallel.For(0, chunks.Length, k =>
        {
            var v = chunks[k] = new List<Vector3>();
            var f = new Vector3[Sub + 1, Sub + 1];
            for (int r = 40 * k; r < Mathf.Min(40 * k + 40, rows); r++)
                for (int c = 0; c < cols; c++)
                {
                    if (!fine[r, c]) { Split(v, V(r, c), V(r + 1, c), V(r + 1, c + 1), V(r, c + 1), h[2 * r + 1, 2 * c + 1]); continue; }
                    for (int i = 0; i <= Sub; i++)
                        for (int j = 0; j <= Sub; j++) f[i, j] = F(Sub * r + i, Sub * c + j);
                    for (int i = 0; i < Sub; i++)
                        for (int j = 0; j < Sub; j++)
                        {
                            Vector3 m = (f[i, j] + f[i + 1, j + 1]) / 2f;
                            Split(v, f[i, j], f[i + 1, j], f[i + 1, j + 1], f[i, j + 1], Height(m.x, m.z));
                        }
                }
        });
        foreach (var v in chunks) Solid("Snow", v, snow, 0);

        // Invisible walls round the edge. The banks alone do not hold a skier who crosses them on a slant.
        var bounds = new GameObject("Bounds").transform;
        bounds.SetParent(transform, false);
        float end = rows * Cell, mid = (FallY(0f) + FallY(end)) / 2f;
        foreach (var (c, size) in new[]
        {
            (new Vector3(-Edge, mid, end / 2f), new Vector3(2f, 1200f, end)), (new Vector3(Edge, mid, end / 2f), new Vector3(2f, 1200f, end)),
            (new Vector3(0f, mid, 0f), new Vector3(2f * Edge, 1200f, 2f)), (new Vector3(0f, mid, end), new Vector3(2f * Edge, 1200f, 2f)),
        })
        {
            var box = bounds.gameObject.AddComponent<BoxCollider>();
            box.center = c;
            box.size = size;
        }
    }

    void Features(int obstacle)
    {
        // Gap kickers where Main Street, a ridge, or the Tube meets a canyon or a cat track. A road is a level bench, so on
        // a steep slope its downhill edge would be a lip. A kicker is three quarters as wide as the run (60 m on Main
        // Street, so the outer edges still drop into a canyon), and at least 12 m. It stops at a canyon's rim, or at the
        // start of a cat track's cut. On a ridge it stops where the crossing starts to cut the ridge.
        foreach (var r in new[] { Main, Highline, Wishbone, WishL, WishR, Tube })
            foreach (var g in Runs)
                if (g.gap)
                    foreach (var (s, sg, sin) in Crossings(r, g))
                    {
                        bool rim = g.Offset(sg) < -1f && r.Offset(s) <= 0f;
                        var k = Jump(14f, 28f, 0f, 0f, 0f, 0f);
                        Put(r, s - ((rim ? g.half : g.half + g.falloff) / sin + 2f) - End(k).x, 0f, Mathf.Max(1.5f * r.half, 12f), k, obstacle);
                    }

        // Upper Main Street: a chain of step down gaps. Each island lands the skier and kicks them on to the next.
        // Sized for the 15° slope there (simulated): 23 to 34 m/s at the lip lands on each 34 m deck.
        float at = Put(Main, 800f, 0f, 16f, Jump(14f, 26f, 0f, 0f, 0f, 0f), obstacle);
        at = Put(Main, at + 28f, 0f, 16f, Island(2f, 34f, 10f, 26f), obstacle);
        at = Put(Main, at + 28f, 0f, 16f, Island(2f, 34f, 10f, 26f), obstacle);
        Put(Main, at + 28f, 0f, 16f, GapLanding(2.5f, 34f, 20f, 5f, 8f), obstacle);

        // A gap along the top of the Highline spine, sized like the chain.
        at = Put(Highline, 400f, 0f, 10f, Jump(14f, 26f, 0f, 0f, 0f, 0f), obstacle);
        Put(Highline, at + 28f, 0f, 10f, GapLanding(2.5f, 34f, 20f, 5f, 8f), obstacle);

        // Mega park, at the top of Main Street, sized for the 21 to 26 m/s a skier carries there (simulated with the speed the
        // climb up a kicker costs). Left: a jump line, each table top bigger. Center: two gaps, then the money booter.
        // Gap 1 and gap 2 need about 20 m/s, the booter about 23: tuck for it. Come up short on a gap and you
        // hit the landing wall. Right: a rail garden.
        Main.Near(0f, 230f, out float p, out _, out _);
        Put(Main, p + 10f, -26f, 14f, Jump(8f, 24f, 12f, 14f, 3f, 5f), obstacle);
        Put(Main, p + 60f, -26f, 14f, Jump(10f, 26f, 18f, 15f, 4f, 6f), obstacle);
        Put(Main, p + 125f, -26f, 14f, Jump(12f, 28f, 24f, 16f, 5f, 7f), obstacle);
        Put(Main, p + 200f, -26f, 14f, Jump(15f, 30f, 30f, 17f, 6f, 8f), obstacle);
        at = Put(Main, p + 10f, 0f, 16f, Jump(14f, 28f, 0f, 0f, 0f, 0f), obstacle);
        Put(Main, at + 10f, 0f, 16f, GapLanding(3.5f, 14f, 20f, 5f, 8f), obstacle);
        at = Put(Main, p + 100f, 0f, 16f, Jump(16f, 30f, 0f, 0f, 0f, 0f), obstacle);
        Put(Main, at + 8f, 0f, 16f, GapLanding(4.5f, 16f, 21f, 6f, 9f), obstacle);
        at = Put(Main, p + 200f, 0f, 18f, Jump(18f, 32f, 0f, 0f, 0f, 0f), obstacle);
        Put(Main, at + 12f, 0f, 18f, GapLanding(5.5f, 18f, 22f, 7f, 11f), obstacle);
        foreach (var g in Rails)
        {
            Frame(Main, p + g.at, out Vector3 o, out Quaternion rot);
            Grind(transform, o, rot, 0f, g.x, g.len, g.width, g.height, g.kink, feature, steel);
        }

        foreach (var (s, _, _) in Crossings(Tube, Highline)) Tunnel(Tube, s, obstacle);
    }

    // A tunnel where a path runs under a ridge: a roof over the path for as long as the ridge stands 1.5 m above the
    // arch. Its top is the ridge as it would be without the path, so a rider on the ridge rides straight over it. Inside,
    // an ice arch 6 m high spans the path's flat. Each portal is a wall with the arch cut out.
    void Tunnel(Run path, float at, int obstacle)
    {
        float Clear(float s) { Vector2 c = path.At(s); return Height(c.x, c.y, out _, path) - FallY(c.y) - Arch - 1.5f; }
        float s0 = at, s1 = at;
        while (Clear(s0 - 1f) > 0f) s0 -= 1f;
        while (Clear(s1 + 1f) > 0f) s1 += 1f;
        int n = Mathf.CeilToInt((s1 - s0) / 2f), m = Mathf.RoundToInt(2f * (path.half + path.falloff));   // 2 m along, 1 m across
        var top = new Vector3[n + 1, m + 1];
        var arch = new Vector3[n + 1, m + 1];
        for (int i = 0; i <= n; i++)
        {
            float s = Mathf.Lerp(s0, s1, i / (float)n);
            Vector2 c = path.At(s), dir = (path.At(s + 1f) - path.At(s - 1f)).normalized, side = new(dir.y, -dir.x);
            for (int j = 0; j <= m; j++)
            {
                float d = j - m / 2f, u = d / path.half;
                Vector2 q = c + side * d;
                top[i, j] = new Vector3(q.x, Height(q.x, q.y, out _, path), q.y);
                arch[i, j] = new Vector3(q.x, FallY(c.y) + (Mathf.Abs(u) < 1f ? Arch * Mathf.Sqrt(1f - u * u) : 0f), q.y);
            }
        }
        var roof = new List<Vector3>();
        var ice = new List<Vector3>();
        var portals = new List<Vector3>();
        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
            {
                Quad(roof, top[i, j], top[i + 1, j], top[i + 1, j + 1], top[i, j + 1]);
                if (Mathf.Abs(j + 0.5f - m / 2f) < path.half) Quad(ice, arch[i, j], arch[i, j + 1], arch[i + 1, j + 1], arch[i + 1, j]);   // faces down
            }
        for (int j = 0; j < m; j++)
        {
            Quad(portals, top[0, j], top[0, j + 1], arch[0, j + 1], arch[0, j]);
            Quad(portals, top[n, j + 1], top[n, j], arch[n, j], arch[n, j + 1]);
        }
        Solid("Tunnel", roof, snow, 0);
        Solid("Ice", ice, feature, 0);
        Solid("Portal", portals, feature, obstacle);
    }

    // Trees on a jittered 9 m grid: dense forest in patches, thin glades between them. None on a run, a cat track,
    // or ground steeper than about 39°. Each tree has its own trunk collider, but they draw in batches of 250
    // (one mesh for the bark, one for the needles), so 2000 trees cost about 20 draw calls.
    void Forest(int obstacle)
    {
        Mesh trunk = Lathe(4, 1f, 0.2f, 0f, 0.15f, 1.2f, 0f, 1.2f);
        Mesh needles = Lathe(6, 1f, 0f, 0.9f, 1.7f, 0.9f, 0.6f, 2.5f, 1.3f, 2.3f, 0.4f, 3.8f, 0.9f, 3.6f, 0f, 5.4f);
        var barkParts = new List<CombineInstance>();
        var pineParts = new List<CombineInstance>();
        var rng = new System.Random(7);
        float R() => (float)rng.NextDouble();
        var root = new GameObject("Forest").transform;
        root.SetParent(transform, false);
        for (float z = 70f; z < FallLine[^1].x - 30f; z += 9f)
            for (float x = -Edge + 5f; x < Edge - 5f; x += 9f)
            {
                float tx = x + (R() - 0.5f) * 8f, tz = z + (R() - 0.5f) * 8f, size = 0.8f + 0.7f * R(), yaw = 360f * R();
                if (R() > (Mathf.PerlinNoise(tx * 0.012f + 3f, tz * 0.012f + 9f) > 0.45f ? 0.45f : 0.1f)) continue;
                float y = Height(tx, tz, out float groomed);
                if (groomed > 0.1f) continue;
                if (new Vector2(Height(tx + 2f, tz) - Height(tx - 2f, tz), Height(tx, tz + 2f) - Height(tx, tz - 2f)).magnitude / 4f > 0.8f) continue;
                var go = new GameObject("Tree", typeof(CapsuleCollider)) { layer = obstacle };
                go.transform.SetParent(root, false);
                go.transform.SetLocalPositionAndRotation(new Vector3(tx, y - 0.3f, tz), Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = Vector3.one * size;
                var cap = go.GetComponent<CapsuleCollider>();   // the trunk. The skier passes through the outer branches.
                cap.radius = 0.35f;
                cap.height = 3f;
                cap.center = new Vector3(0f, 1.5f, 0f);
                barkParts.Add(new CombineInstance { mesh = trunk, transform = go.transform.localToWorldMatrix });
                pineParts.Add(new CombineInstance { mesh = needles, transform = go.transform.localToWorldMatrix });
                if (pineParts.Count == 250) Draw();
            }
        Draw();

        void Draw()
        {
            foreach (var (parts, m) in new[] { (barkParts, bark), (pineParts, pine) })
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(parts.ToArray(), true, true);
                var go = new GameObject("Trees", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = m;
                parts.Clear();
            }
        }
    }

    // The snow height at (x, z). groomed is how much a run flattens it there (1 on a run, 0 off all runs).
    // skip leaves one run out (a tunnel roof is the snow as it would be without the path under it).
    public static float Height(float x, float z) => Height(x, z, out _);

    static float Height(float x, float z, out float groomed, Run skip = null)
    {
        float wall = Mathf.Max(0f, Mathf.Abs(x) - Walls);
        float h = FallY(z) + wall * 0.7f + Mathf.Min(1f, wall / 10f) * 3f * Noise(x + 500f, z)
                + Mathf.Clamp01((z - 60f) / 60f) * (5f * Noise(x * 0.25f, z * 0.25f) + 1.2f * Noise(x, z));
        groomed = 0f;
        foreach (var r in Runs)
        {
            if (r == skip || !r.Near(x, z, out float s, out float d, out Vector2 c)) continue;
            // A run is level across at the height of its center line. Within 60 m of its ends it eases to the fall line,
            // so a run that starts or ends in another run does not cut or fill it, and past its ends it fades out.
            float level = Mathf.Clamp01(Mathf.Min(s, r.Length - s) / 60f), fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(-s, s - r.Length) / r.falloff);
            float w = (1f - Mathf.SmoothStep(0f, 1f, (d - r.half) / r.falloff)) * fade;
            float off = r.Offset(s) * fade, hr = Mathf.Lerp(h, FallY(Mathf.Lerp(z, c.y, level)), w);
            if (off > 0f) hr += off * (d < r.half ? 1f : 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (d - r.half) / r.falloff));
            else if (off < 0f) hr += off * Lip(1f - d * d / (r.half * r.half));
            h = !r.branch ? hr : off > 0f ? Mathf.Max(h, hr) : Mathf.Min(h, hr);   // a branch keeps the ridge or gully it splits from
            groomed = Mathf.Max(groomed, w);
        }
        return h;
    }

    // The quad a b c d (as Quad) in two triangles, split along the diagonal nearer mid, the snow height at its center.
    static void Split(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float mid)
    {
        if (Miss(mid, a.y, c.y) <= Miss(mid, b.y, d.y)) Quad(v, a, b, c, d);
        else Quad(v, b, c, d, a);
    }

    // A canyon's U, max(t, 0), with the corner rounded, so the wall eases over into the snow about 3 m each side of the rim.
    static float Lip(float t) => t >= 0.5f ? t : t <= -0.5f ? 0f : (t + 0.5f) * (t + 0.5f) / 2f;

    // How far a straight line from height a to height b misses the snow height mid at its middle.
    static float Miss(float mid, float a, float b) => Mathf.Abs(mid - (a + b) / 2f);

    static float Noise(float x, float z) => (Mathf.PerlinNoise(x * 0.03f + 31f, z * 0.03f + 17f) - 0.5f) * 2f;

    static List<Vector2> Walk()
    {
        var line = new List<Vector2> { Vector2.zero };
        foreach (var (len, a0, a1) in Fall)
        {
            int n = Mathf.Max(Mathf.CeilToInt(len / 2f), Steps(a0, a1));
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(a0, a1, (i + 0.5f) / n) * Mathf.Deg2Rad;
                line.Add(line[^1] + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * (len / n));
            }
        }
        return line;
    }

    static readonly Comparer<Vector2> ByZ = Comparer<Vector2>.Create((a, b) => a.x.CompareTo(b.x));

    static float FallY(float z)
    {
        int i = FallLine.BinarySearch(new Vector2(z, 0f), ByZ);
        if (i < 0) i = ~i;
        i = Mathf.Clamp(i, 1, FallLine.Count - 1);
        Vector2 a = FallLine[i - 1], b = FallLine[i];
        return Mathf.Lerp(a.y, b.y, Mathf.InverseLerp(a.x, b.x, z));
    }

    static int Steps(float a0, float a1) => Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 4f));

    // Where run a crosses run b: distance along each, and the sine of the angle between them.
    static IEnumerable<(float s, float sb, float sin)> Crossings(Run a, Run b)
    {
        for (int i = 0; i + 1 < a.p.Length; i++)
            for (int j = 0; j + 1 < b.p.Length; j++)
            {
                Vector2 p = a.p[i], r = a.p[i + 1] - p, q = b.p[j], u = b.p[j + 1] - q, pq = q - p;
                float den = r.x * u.y - r.y * u.x, t = (pq.x * u.y - pq.y * u.x) / den, k = (pq.x * r.y - pq.y * r.x) / den;
                if (Mathf.Abs(den) > 1e-6f && t >= 0f && t <= 1f && k >= 0f && k <= 1f)
                    yield return (a.s[i] + t * r.magnitude, b.s[j] + k * u.magnitude, Mathf.Abs(den) / (r.magnitude * u.magnitude));
            }
    }

    // The point on run r's center line at distance s, on the snow, and the slope of the run there.
    static void Frame(Run r, float s, out Vector3 o, out Quaternion rot)
    {
        Vector2 c = r.At(s), dir = (r.At(s + 1f) - r.At(s - 1f)).normalized;
        float y = Height(c.x, c.y), drop = y - Height(c.x + dir.x * 3f, c.y + dir.y * 3f);
        o = new Vector3(c.x, y, c.y);
        rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)) * Quaternion.Euler(Mathf.Atan2(drop, 3f) * Mathf.Rad2Deg, 0f, 0f);
    }

    // A feature on run r from distance s along it, x to the side, on the plane of the run there. Returns where it ends.
    float Put(Run r, float s, float x, float w, List<(float, float, float)> profile, int obstacle)
    {
        Frame(r, s, out Vector3 o, out Quaternion rot);
        return s + Feature(o, rot, x, w, profile, obstacle);
    }

    // A park feature built up from the plane through o (rot is its slope): profile pieces (length, start and end angle
    // relative to the plane, positive is down), in a lane at x, w wide. Curves become 4° steps. The top is ground.
    // Pieces steeper than 60° and the sides are walls on the Obstacle layer. The sides reach 4 m under the plane, so
    // they stay in the snow where it curves. Returns the length along the plane.
    float Feature(Vector3 o, Quaternion rot, float x, float w, List<(float len, float a0, float a1)> profile, int obstacle)
    {
        var top = new List<Vector3>();
        var walls = new List<Vector3>();
        Vector3 P(float s, float h, float side) => o + rot * new Vector3(x + side * w / 2f, h, s);
        float s0 = 0f, h0 = 0f;
        foreach (var (len, a0, a1) in profile)
        {
            int n = Steps(a0, a1);
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(a0, a1, (i + 0.5f) / n);
                float s1 = s0 + len / n * Mathf.Cos(a * Mathf.Deg2Rad), h1 = h0 - len / n * Mathf.Sin(a * Mathf.Deg2Rad);
                Quad(Mathf.Abs(a) > 60f ? walls : top, P(s0, h0, -1f), P(s1, h1, -1f), P(s1, h1, 1f), P(s0, h0, 1f));
                Quad(walls, P(s0, -4f, -1f), P(s1, -4f, -1f), P(s1, h1, -1f), P(s0, h0, -1f));   // left side
                Quad(walls, P(s0, -4f, 1f), P(s0, h0, 1f), P(s1, h1, 1f), P(s1, -4f, 1f));       // right side
                s0 = s1;
                h0 = h1;
            }
        }
        if (Mathf.Abs(h0) > 0.01f) Debug.LogWarning($"A park feature at {o} ends {h0:F2} m off its plane.");
        Solid("Feature", top, feature, 0);
        Solid("Wall", walls, feature, obstacle);
        return s0;
    }

    // A kicker up to a lip (deg up from the plane), then a table and a landing back down to the plane.
    // With table 0 it is the kicker of a gap jump: a steep back face drops from the lip to the plane.
    static List<(float, float, float)> Jump(float kicker, float lip, float table, float land, float knuckle, float trans)
    {
        var p = new List<(float, float, float)> { (kicker, 0f, -lip), (1.5f, -lip, -lip) };
        float h = End(p).y;
        if (table <= 0f) p.Add((h / Sin75, 75f, 75f));
        else
        {
            p.Add((table, 0f, 0f));
            Land(p, h, land, knuckle, trans);
        }
        return p;
    }

    // The far side of a gap: a steep face up to a deck at height h, then a landing back down to the plane.
    static List<(float, float, float)> GapLanding(float h, float deck, float land, float knuckle, float trans)
    {
        var p = new List<(float, float, float)> { (h / Sin75, -75f, -75f), (deck, 0f, 0f) };
        Land(p, h, land, knuckle, trans);
        return p;
    }

    // The far side of a gap that sends the skier on: a face up to a deck at height h, a kicker up to a lip, and a back face.
    static List<(float, float, float)> Island(float h, float deck, float kicker, float lip)
    {
        var p = new List<(float, float, float)> { (h / Sin75, -75f, -75f), (deck, 0f, 0f), (kicker, 0f, -lip), (1.5f, -lip, -lip) };
        p.Add((End(p).y / Sin75, 75f, 75f));
        return p;
    }

    // From height h back down to the plane: a knuckle, a straight sized to fit, and a transition.
    static void Land(List<(float, float, float)> p, float h, float angle, float knuckle, float trans)
    {
        float straight = (h + End(new() { (knuckle, 0f, angle), (trans, angle, 0f) }).y) / Mathf.Sin(angle * Mathf.Deg2Rad);
        if (straight < 0f) Debug.LogWarning($"A landing from {h:F1} m is too short for its knuckle and transition.");
        p.Add((knuckle, 0f, angle));
        p.Add((straight, angle, angle));
        p.Add((trans, angle, 0f));
    }

    // Where a profile ends: distance along its plane (x) and height above it (y), with the same 4° steps as Feature.
    static Vector2 End(List<(float len, float a0, float a1)> p)
    {
        var e = Vector2.zero;
        foreach (var (len, a0, a1) in p)
        {
            int n = Steps(a0, a1);
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(a0, a1, (i + 0.5f) / n) * Mathf.Deg2Rad;
                e += new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * (len / n);
            }
        }
        return e;
    }

    void Solid(string name, List<Vector3> v, Material m, int layer)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)) { layer = layer };
        go.transform.SetParent(transform, false);
        var mesh = Flat(v);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        go.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    class Run
    {
        public readonly float half, falloff;
        public readonly bool gap;
        public bool branch;            // splits from another run: a ridge branch keeps the higher snow, a gully branch the lower
        public readonly Vector2[] p;   // the smoothed path (x, z)
        public readonly float[] s;     // distance along it at each point
        readonly (float s, float off)[] offsets;
        readonly List<int>[] bands = new List<int>[160];   // the segments near each 20 m band of z, from z = -100

        public Run(string name, float half, float falloff, bool gap, (float, float)[] offsets, params Vector2[] points)
        {
            this.half = half;
            this.falloff = falloff;
            this.gap = gap;
            // Chaikin: cut every corner four times for a smooth path. The ends stay.
            var pts = new List<Vector2>(points);
            for (int k = 0; k < 4; k++)
            {
                var next = new List<Vector2> { pts[0] };
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    next.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.25f));
                    next.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.75f));
                }
                next.Add(pts[^1]);
                pts = next;
            }
            p = pts.ToArray();
            s = new float[p.Length];
            for (int i = 1; i < p.Length; i++) s[i] = s[i - 1] + Vector2.Distance(p[i - 1], p[i]);
            this.offsets = System.Array.ConvertAll(offsets, o => (o.Item1 < 0f ? s[^1] + o.Item1 : o.Item1, o.Item2));
            float reach = half + falloff;
            for (int i = 0; i + 1 < p.Length; i++)
                for (int b = Of(Mathf.Min(p[i].y, p[i + 1].y) - reach); b <= Of(Mathf.Max(p[i].y, p[i + 1].y) + reach); b++)
                    (bands[b] ??= new List<int>()).Add(i);
        }

        static readonly List<int> NoSegments = new();

        static int Of(float z) => Mathf.Clamp(Mathf.FloorToInt((z + 100f) / Band), 0, 159);

        public float Length => s[^1];

        // The nearest point on the path to (x, z): distance along it (s, below 0 or past Length beyond the ends),
        // distance from it (d), and the point (c).
        // False if (x, z) is out of the run's reach.
        public bool Near(float x, float z, out float along, out float d, out Vector2 c)
        {
            along = 0f;
            c = default;
            float best = float.MaxValue;
            var q = new Vector2(x, z);
            var band = bands[Of(z)] ?? NoSegments;
            for (int k = 0; k < band.Count; k++)   // a for loop: foreach on the interface would allocate on every call
            {
                int i = band[k];
                Vector2 a = p[i], ab = p[i + 1] - a;
                float t = Vector2.Dot(q - a, ab) / ab.sqrMagnitude, tc = Mathf.Clamp01(t);
                Vector2 at = a + ab * tc;
                float dd = (q - at).sqrMagnitude;
                if (dd < best) { best = dd; along = s[i] + ((i == 0 && t < 0f) || (i == p.Length - 2 && t > 1f) ? t : tc) * ab.magnitude; c = at; }
            }
            d = Mathf.Sqrt(best);
            return d < half + falloff;
        }

        public float Offset(float along)
        {
            for (int i = 0; i + 1 < offsets.Length; i++)
                if (along < offsets[i + 1].s) return Mathf.Lerp(offsets[i].off, offsets[i + 1].off, Mathf.InverseLerp(offsets[i].s, offsets[i + 1].s, along));
            return offsets.Length > 0 ? offsets[^1].off : 0f;
        }

        public Vector2 At(float along)
        {
            int i = System.Array.BinarySearch(s, along);
            if (i < 0) i = ~i;
            i = Mathf.Clamp(i, 1, s.Length - 1);
            return Vector2.LerpUnclamped(p[i - 1], p[i], (along - s[i - 1]) / (s[i] - s[i - 1]));
        }
    }
}
