using UnityEngine;

// A straight rail along local z. Its length is the z scale and its top is half the y scale up.
// A kinked, curved, or humped rail is a chain of straight segments (next and prev).
public class Rail : MonoBehaviour
{
    public Rail next, prev;   // the segments that carry on from this end and from the start
    public Vector3 Dir => transform.forward;
    public bool Metal => transform.lossyScale.x <= 0.2f;   // a steel rail. A box is wider (the same split as Shapes.Grind).

    // Closest point on the top line, and where it sits along the rail (0 is the start, 1 is the end, not clamped).
    public Vector3 Closest(Vector3 p, out float t)
    {
        Vector3 span = transform.forward * transform.lossyScale.z;
        Vector3 start = transform.position + transform.up * (transform.lossyScale.y / 2f) - span / 2f;
        t = Vector3.Dot(p - start, span) / span.sqrMagnitude;
        return start + span * Mathf.Clamp01(t);
    }

    // True if other is this rail or another segment of its chain.
    public bool Joined(Rail other)
    {
        for (var r = this; r; r = r.next) if (r == other) return true;
        for (var r = prev; r; r = r.prev) if (r == other) return true;
        return false;
    }
}
