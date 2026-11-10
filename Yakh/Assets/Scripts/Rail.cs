using UnityEngine;

// A straight rail along local z. Its length is the z scale and its top is half the y scale up.
// ponytail: straight rails only, curved rails can move to the Splines package.
public class Rail : MonoBehaviour
{
    public Vector3 Dir => transform.forward;

    // Closest point on the top line, and where it sits along the rail (0 is the start, 1 is the end, not clamped).
    public Vector3 Closest(Vector3 p, out float t)
    {
        Vector3 span = transform.forward * transform.lossyScale.z;
        Vector3 start = transform.position + transform.up * (transform.lossyScale.y / 2f) - span / 2f;
        t = Vector3.Dot(p - start, span) / span.sqrMagnitude;
        return start + span * Mathf.Clamp01(t);
    }
}
