using UnityEngine;

// Yard sale. On a ragdoll limb (shin or forearm): in a bail, a hit on this limb harder than tolerance
// knocks its ski or pole loose. Each limb checks its own hits, so the gear comes off one piece at a time.
// While skiing the limb has no collision, so nothing comes off. Skier puts the gear back on when the skier gets up.
public class Gear : MonoBehaviour
{
    public Transform item;          // the ski or pole, a child of this limb, with its own collider
    public float tolerance = 40f;   // impact impulse (N·s) on this limb that knocks the item loose

    Rigidbody limb;
    Pose rest;

    void Awake()
    {
        limb = GetComponent<Rigidbody>();
        rest = new Pose(item.localPosition, item.localRotation);
    }

    void OnCollisionEnter(Collision c) => Hit(c);
    void OnCollisionStay(Collision c) => Hit(c);   // a second hard hit while still touching the snow

    void Hit(Collision c)
    {
        if (item.parent != transform || c.impulse.magnitude < tolerance) return;
        item.SetParent(null, true);
        var rb = item.gameObject.AddComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.linearVelocity = limb.GetPointVelocity(item.position);
        rb.angularVelocity = limb.angularVelocity;
    }

    public void Attach()
    {
        if (item.parent == transform) return;
        DestroyImmediate(item.GetComponent<Rigidbody>());   // now, so the item rejoins the limb before the next physics step
        item.SetParent(transform, false);
        item.SetLocalPositionAndRotation(rest.position, rest.rotation);
    }
}
