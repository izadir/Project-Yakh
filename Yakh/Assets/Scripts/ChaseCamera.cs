using UnityEngine;

// Low chase camera close behind the skier, like Skate 3.
[RequireComponent(typeof(Camera))]
public class ChaseCamera : MonoBehaviour
{
    public Skier target;
    public float distance = 5f, height = 1.6f, lookHeight = 0.6f;
    public float turnSmooth = 4f;         // how fast the camera swings behind the travel direction
    public float baseFov = 60f, fastFov = 80f, fastSpeed = 30f;

    Camera cam;
    Vector3 dir = Vector3.forward;

    void Awake() => cam = GetComponent<Camera>();

    void LateUpdate()
    {
        Vector3 v = target.Travel;
        if (v.sqrMagnitude > 1f) dir = Vector3.Slerp(dir, v.normalized, 1f - Mathf.Exp(-turnSmooth * Time.deltaTime));
        Vector3 p = target.transform.position;
        transform.position = p - dir * distance + Vector3.up * height;
        transform.LookAt(p + Vector3.up * lookHeight);
        cam.fieldOfView = Mathf.Lerp(baseFov, fastFov, v.magnitude / fastSpeed);
    }
}
