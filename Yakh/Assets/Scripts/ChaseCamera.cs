using UnityEngine;

// Low chase camera close behind the skier, like Skate 3.
[RequireComponent(typeof(Camera))]
public class ChaseCamera : MonoBehaviour
{
    public Skier target;
    public float distance = 5f, height = 1.6f, lookHeight = 0.6f;
    public float turnSmooth = 4f;         // how fast the camera swings behind the travel direction
    public float baseFov = 60f, fastFov = 80f, fastSpeed = 30f;
    public float maxRoll = 8f;            // deg the camera tilts into the hardest turn (the skier's full lean)
    public float rollCurve = 3f;          // the tilt grows exponentially with the lean: half the lean tilts about 18% of maxRoll
    public float rollSmooth = 6f;         // 1/s, how fast the tilt follows

    Camera cam;
    Vector3 dir = Vector3.forward;
    float roll;

    void Awake() => cam = GetComponent<Camera>();

    void LateUpdate()
    {
        Vector3 v = target.Travel;
        if (v.sqrMagnitude > 1f) dir = Vector3.Slerp(dir, v.normalized, 1f - Mathf.Exp(-turnSmooth * Time.deltaTime));
        Vector3 p = target.Focus;
        transform.position = p - dir * distance + Vector3.up * height;
        transform.LookAt(p + Vector3.up * lookHeight);
        // Tilt into the turn, the way the skier leans.
        float lean = target.Lean, want = Mathf.Sign(lean) * maxRoll * (Mathf.Exp(rollCurve * Mathf.Abs(lean)) - 1f) / (Mathf.Exp(rollCurve) - 1f);
        roll = Mathf.Lerp(roll, want, 1f - Mathf.Exp(-rollSmooth * Time.deltaTime));
        transform.rotation *= Quaternion.Euler(0f, 0f, roll);
        cam.fieldOfView = Mathf.Lerp(baseFov, fastFov, v.magnitude / fastSpeed);
    }
}
