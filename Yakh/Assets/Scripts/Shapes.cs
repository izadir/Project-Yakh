using System.Collections.Generic;
using UnityEngine;

// Mesh and object helpers for the levels: the test slope (built in the editor) and the mountain (built at load).
public static class Shapes
{
    const float KinkLen = 3f;

    // A rail, or a box if wider than 0.2 m, on a straight surface: p is a point on it and rot its slope. at is the
    // distance along it, x the side offset, height the top above it, and kink a kicker kink at the end (deg up, 0 is none).
    // Solid, on the Rail layer (see Bar). The posts have no collider.
    public static void Grind(Transform parent, Vector3 p, Quaternion rot, float at, float x, float len, float width, float height, float kink,
                             Material boxMat, Material steel)
    {
        Vector3 up = rot * Vector3.up, fwd = rot * Vector3.forward;
        bool box = width > 0.2f;
        float h = box ? height : 0.08f;
        Vector3 c = p + fwd * (at + len / 2f) + up * (height - h / 2f) + rot * Vector3.right * x;
        var bar = Bar(parent, box ? "Box" : "Rail", c, rot, new Vector3(width, h, len), box ? boxMat : steel);
        if (!box)
            for (float z = -len / 2f + 0.5f; z < len / 2f; z += 7f)
                Part(parent, PrimitiveType.Cube, c + fwd * z - up * (height / 2f), rot, new Vector3(0.05f, height, 0.05f), steel, false);
        if (kink > 0f)
        {
            // Kicker: a short segment kinks up off the end, so the skier leaves the rail rising.
            var kr = rot * Quaternion.Euler(-kink, 0f, 0f);
            Vector3 end = c + fwd * (len / 2f), tip = end + kr * Vector3.forward * KinkLen;
            bar.next = Bar(parent, bar.name, (end + tip) / 2f, kr, new Vector3(width, h, KinkLen), steel);
            float rise = Vector3.Dot(tip - p, up);
            Part(parent, PrimitiveType.Cube, tip - up * (rise / 2f), rot, new Vector3(0.05f, rise, 0.05f), steel, false);
        }
    }

    // A grind feature: solid, on the Rail layer. The skier's ground check skips that layer,
    // and the skier has no collision while it rides a rail.
    public static Rail Bar(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Material m)
    {
        var bar = Part(parent, PrimitiveType.Cube, pos, rot, size, m);
        bar.name = name;
        bar.layer = LayerMask.NameToLayer("Rail");
        return bar.AddComponent<Rail>();
    }

    public static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 size, Material m, bool solid = true)
    {
        var go = GameObject.CreatePrimitive(type);
        if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(pos, rot);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // Flat shaded solid of revolution. The profile is (radius, height) pairs from the bottom center to the top center.
    // depth squashes it front to back. A face, not an edge, points forward (+z), so details sit flat on it.
    public static Mesh Lathe(int sides, float depth, params float[] profile)
    {
        var v = new List<Vector3>();
        Vector3 P(int i, int s)
        {
            float a = (s + 0.5f) * 2f * Mathf.PI / sides, r = profile[2 * i];
            return new Vector3(Mathf.Sin(a) * r, profile[2 * i + 1], Mathf.Cos(a) * r * depth);
        }
        for (int i = 0; i + 1 < profile.Length / 2; i++)
            for (int s = 0; s < sides; s++)
                Quad(v, P(i + 1, s + 1), P(i + 1, s), P(i, s), P(i, s + 1));
        return Flat(v);
    }

    // Two triangles for the quad a b c d, given clockwise as seen from its front.
    public static void Quad(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        v.Add(a); v.Add(b); v.Add(c);   // no array per quad: the mountain makes about a million of them
        v.Add(a); v.Add(c); v.Add(d);
    }

    // A mesh from triangle corners with no shared vertices, so every face gets its own flat normal.
    public static Mesh Flat(List<Vector3> v)
    {
        var m = new Mesh { indexFormat = v.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        m.SetVertices(v);
        var t = new int[v.Count];
        for (int i = 0; i < t.Length; i++) t[i] = i;
        m.triangles = t;
        m.RecalculateNormals();
        return m;
    }
}
