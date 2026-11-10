using UnityEngine;
using UnityEngine.InputSystem;

// Arcade skier. A sphere Rigidbody does the collision. These forces make the feel.
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class Skier : MonoBehaviour
{
    [Header("Ground")]
    public float gravity = 18f;           // m/s², stronger than real so airs feel snappy
    public float snapDistance = 0.5f;     // the skier sticks to ground this close below
    public float snapMaxSpeed = 3f;       // but leaves it when moving away faster than this (lips, knuckles)
    public float alignRate = 12f;         // how fast the body tilts to the slope

    [Header("Carve")]
    public float turnRate = 140f;         // deg/s at full stick when slow
    public float fastTurnRate = 45f;      // deg/s at full stick at turnFadeSpeed and above
    public float turnFadeSpeed = 30f;     // m/s
    public float carveGrip = 360f;        // deg/s that the velocity bends toward the skis
    public float snowFriction = 0.6f;     // m/s²
    public float drag = 0.004f;           // air drag, sets the top speed
    public float tuckDrag = 0.0015f;
    public float pushAccel = 4f;          // tuck on a flat pushes the skier
    public float pushMaxSpeed = 8f;

    [Header("Skid: left stick back, 135° to 225°")]
    public float skidTurnRate = 260f;     // quick turn at 135° or 225°, deg/s, none at 180°
    public float skidGrip = 60f;          // low grip lets the skis slide sideways
    public float skidDecel = 10f;         // m/s² when the skis are fully sideways
    public float checkDecel = 5f;         // m/s² at a full feather, scaled by how far the stick is pulled
    public float hockeyTurnRate = 600f;   // full pull: deg/s that the skis swing across the travel line
    public float hockeyDecel = 16f;
    public float sprayRate = 600f;        // powder particles per second at a full hockey stop
    public ParticleSystem spray;

    [Header("Air")]
    public float popSpeed = 6f;
    public float coyoteTime = 0.12f;      // pop still works this long after the lip
    public float flickWindow = 0.25f;     // max time from stick down to stick up
    public float spinRate = 540f;         // deg/s, left stick x
    public float flipRate = 360f;         // deg/s, left stick y
    public float airLevelRate = 4f;       // with no flip input, the body levels out when near upright
    public float landMaxTilt = 60f;       // a clean landing has the body within this of the slope normal
    public float landMaxYaw = 45f;        // and the skis within this of the travel line, forward or switch

    [Header("Rail")]
    public float railReach = 0.6f;        // lock on within this distance to the side
    public float railMaxAngle = 45f;      // and with travel within this of the rail
    public float railFriction = 1f;       // m/s²
    public float balanceDrift = 1.5f;     // how fast the balance falls away from center
    public float balanceControl = 2.5f;   // how hard the left stick pushes the balance
    public float railSwing = 70f;         // deg the skis swing off the rail line at full balance
    public float slipSpeed = 2f;          // sideways speed when the skier slips off

    public Transform body;                // squashes to show crouch and tuck

    Rigidbody rb;
    Rail rail;
    readonly Collider[] near = new Collider[8];
    float radius, airTime, balance, railSpeed, railSign, railCooldown, lastGroundedTime = -1f, lastDownTime = -1f;
    float lastSteer = 1f, hockeySide;
    bool grounded, wasHockey, popQueued, respawnQueued;
    Vector3 heading, groundUp = Vector3.up, spawnPos;
    Quaternion spawnRot;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var col = GetComponent<SphereCollider>();
        radius = col.radius;
        col.material = new PhysicsMaterial
        {
            dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum,
        };
        spawnPos = transform.position;
        spawnRot = transform.rotation;
        heading = transform.forward;
        Application.targetFrameRate = 60;
    }

    // Buttons and the flick are read every frame, then used in FixedUpdate.
    void Update()
    {
        var pad = Gamepad.current;
        if (pad == null) return;
        float y = pad.rightStick.ReadValue().y;
        if (y < -0.6f) lastDownTime = Time.time;
        else if (y > 0.6f && Time.time - lastDownTime < flickWindow) { popQueued = true; lastDownTime = -1f; }
        if (pad.selectButton.wasPressedThisFrame) respawnQueued = true;
        if (body) body.localScale = new Vector3(1f, y < -0.6f ? 0.7f : pad.buttonSouth.isPressed ? 0.85f : 1f, 1f);
    }

    void FixedUpdate()
    {
        if (respawnQueued || rb.position.y < spawnPos.y - 200f) { Respawn(); return; }
        var pad = Gamepad.current;
        Vector2 stick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
        bool tuck = pad != null && pad.buttonSouth.isPressed;
        float dt = Time.fixedDeltaTime;
        Spray(0f, Vector3.up);
        if (rail) { RideRail(stick, dt); return; }
        Vector3 v = rb.linearVelocity;

        // On the ground, cast along the body's down axis so walls and pipes work later.
        bool wasGrounded = grounded;
        Vector3 down = grounded ? -groundUp : Vector3.down;
        grounded = Physics.SphereCast(rb.position, radius * 0.9f, down, out RaycastHit hit,
                       radius * 0.1f + snapDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                   && Vector3.Dot(v, hit.normal) < snapMaxSpeed;
        if (grounded && !wasGrounded && airTime > 0.2f && !Land(hit.normal, v)) { Bail(); return; }
        airTime = grounded ? 0f : airTime + dt;

        if (grounded)
        {
            lastGroundedTime = Time.time;
            groundUp = hit.normal;
            float away = Vector3.Dot(v, groundUp);
            if (away > 0f) v -= groundUp * away;      // hold the skier down over small crests

            Vector3 h = Vector3.ProjectOnPlane(heading, groundUp);
            if (h.sqrMagnitude > 1e-4f) heading = h.normalized;
            float speed = v.magnitude;
            Vector3 travel = speed > 0.1f ? v / speed : heading;

            // Stick angle: 0 is up, 180 is back. A pull between 135 and 225 skids, a full pull is a hockey stop.
            float pull = Mathf.Min(stick.magnitude, 1f), angle = Mathf.Abs(Mathf.Atan2(stick.x, stick.y) * Mathf.Rad2Deg);
            bool skid = speed > 2f && pull > 0.25f && angle >= 135f;
            bool hockey = skid && pull > 0.9f;
            if (Mathf.Abs(stick.x) > 0.2f) lastSteer = Mathf.Sign(stick.x);
            float grip = carveGrip, decel = snowFriction;
            if (hockey)
            {
                // Swing the skis across the travel line. A stick lean picks the side.
                // A straight pull keeps the side of the last turn, chosen once when the stop starts.
                if (Mathf.Abs(stick.x) > 0.1f) hockeySide = Mathf.Sign(stick.x);
                else if (!wasHockey) hockeySide = lastSteer;
                heading = Vector3.RotateTowards(heading, Quaternion.AngleAxis(90f * hockeySide, groundUp) * travel, hockeyTurnRate * Mathf.Deg2Rad * dt, 0f);
                grip = 0f;
                decel += hockeyDecel;
            }
            else
            {
                // A skid turns by the stick angle (full at 135° or 225°), so the pull length only sets the scrub.
                float turn = skid ? Mathf.Sign(stick.x) * (180f - angle) / 45f * skidTurnRate
                                  : stick.x * Mathf.Lerp(turnRate, fastTurnRate, speed / turnFadeSpeed);
                heading = Quaternion.AngleAxis(turn * dt, groundUp) * heading;
                if (skid)
                {
                    grip = skidGrip;
                    decel += checkDecel * pull + skidDecel * (1f - Mathf.Abs(Vector3.Dot(travel, heading)));
                }
            }

            // Carve: the velocity bends toward the ski line, forward or switch.
            Vector3 line = Vector3.Dot(v, heading) >= 0f ? heading : -heading;
            v = Vector3.RotateTowards(v, line * speed, grip * Mathf.Deg2Rad * dt, 0f);
            v = Vector3.MoveTowards(v, Vector3.zero, decel * dt);
            if (tuck) v += heading * (pushAccel * Mathf.Clamp01(1f - v.magnitude / pushMaxSpeed) * dt);
            if (skid) Spray((hockey ? 1f : 0.3f) * Mathf.Clamp01(speed / 10f), travel + groundUp * 0.7f);
            wasHockey = hockey;
        }
        else
        {
            wasHockey = false;
            // Left stick spins (x) and flips (y). With no flip input, the body levels out when it is near upright.
            Quaternion r = rb.rotation * Quaternion.Euler(stick.y * flipRate * dt, stick.x * spinRate * dt, 0f);
            Vector3 up = r * Vector3.up;
            if (Mathf.Abs(stick.y) < 0.2f && Vector3.Angle(up, Vector3.up) < 45f)
                r = Quaternion.Slerp(r, Quaternion.FromToRotation(up, Vector3.up) * r, 1f - Mathf.Exp(-airLevelRate * dt));
            rb.MoveRotation(r);
        }

        if (popQueued && Time.time - lastGroundedTime < coyoteTime)
        {
            v += (groundUp + Vector3.up).normalized * popSpeed;
            grounded = false;
            lastGroundedTime = -1f;
        }
        popQueued = false;

        v += Vector3.down * (gravity * dt);
        v -= v * (v.magnitude * (tuck ? tuckDrag : drag) * dt);
        rb.linearVelocity = v;

        if (grounded)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(heading, groundUp);
            if (fwd.sqrMagnitude > 1e-4f)
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(fwd, groundUp), 1f - Mathf.Exp(-alignRate * dt)));
        }
        TryLockRail(v);
    }

    // Touchdown after a real air. A clean landing snaps the skis to the travel line.
    bool Land(Vector3 n, Vector3 v)
    {
        Quaternion r = rb.rotation;
        if (Vector3.Angle(r * Vector3.up, n) > landMaxTilt) return false;
        Vector3 fwd = Vector3.ProjectOnPlane(r * Vector3.forward, n).normalized;
        Vector3 travel = Vector3.ProjectOnPlane(v, n);
        if (travel.sqrMagnitude < 1f) { heading = fwd; return true; }
        travel.Normalize();
        float yaw = Vector3.Angle(fwd, travel);
        if (yaw > landMaxYaw && yaw < 180f - landMaxYaw) return false;
        heading = yaw <= 90f ? travel : -travel;
        return true;
    }

    void TryLockRail(Vector3 v)
    {
        if (Time.time < railCooldown) return;
        Vector3 feet = rb.position - Vector3.up * radius;
        int count = Physics.OverlapSphereNonAlloc(feet, railReach + 1f, near, Physics.AllLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            if (!near[i].TryGetComponent(out Rail r)) continue;
            Vector3 up = r.transform.up, along = r.Dir;
            Vector3 d = feet - r.Closest(feet, out float t);
            float above = Vector3.Dot(d, up);
            float angle = Vector3.Angle(Vector3.ProjectOnPlane(v, up), along);
            // Lock only when level with or above the rail, not rising, close to the side, and moving along it.
            if (t <= 0f || t >= 1f || above < -0.15f || above > 0.8f || Vector3.Dot(v, up) > 1f) continue;
            if ((d - up * above).magnitude > railReach || (angle > railMaxAngle && angle < 180f - railMaxAngle)) continue;

            rail = r;
            railSign = angle <= 90f ? 1f : -1f;
            railSpeed = Mathf.Abs(Vector3.Dot(v, along));
            Vector3 dir = along * railSign;
            balance = Mathf.Clamp(Vector3.Dot(v, Vector3.Cross(up, dir)) / Mathf.Max(railSpeed, 1f), -0.5f, 0.5f); // side speed starts a lean
            heading = Vector3.Dot(rb.rotation * Vector3.forward, dir) >= 0f ? dir : -dir;
            grounded = false;
            return;
        }
    }

    void RideRail(Vector2 stick, float dt)
    {
        Vector3 up = rail.transform.up, dir = rail.Dir * railSign;
        Vector3 top = rail.Closest(rb.position - up * radius, out float t);
        railSpeed += (Vector3.Dot(Vector3.down, dir) * gravity - railFriction) * dt;

        // The balance falls away from center, faster at speed. A small wobble keeps it moving. The left stick pushes it.
        float wobble = Mathf.PerlinNoise(Time.time * 0.7f, 0f) - 0.5f;
        balance += ((balance * balanceDrift + wobble) * (1f + railSpeed / 15f) + stick.x * balanceControl) * dt;

        if (popQueued) { popQueued = false; LeaveRail(up * popSpeed, true); return; }
        if (Mathf.Abs(balance) >= 1f)
        {
            LeaveRail(Vector3.Cross(up, dir) * (Mathf.Sign(balance) * slipSpeed), false);
            airTime = -1f;   // a slip is not a bail, so the short drop skips the landing check
            return;
        }
        if (t <= 0f || t >= 1f || railSpeed < 1f) { LeaveRail(Vector3.zero, false); return; }

        // The balance swings the skis left and right off the rail line.
        Vector3 skis = Quaternion.AngleAxis(balance * railSwing, up) * heading;
        Vector3 target = top + up * radius;
        rb.linearVelocity = dir * railSpeed + (target - rb.position) * (Mathf.Min(1f, 15f * dt) / dt);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(skis, up), 1f - Mathf.Exp(-alignRate * dt)));
    }

    void LeaveRail(Vector3 push, bool popped)
    {
        rb.linearVelocity = rail.Dir * (railSign * railSpeed) + push;
        rail = null;
        railCooldown = Time.time + 0.4f;
        lastGroundedTime = popped ? -1f : Time.time;   // a late pop still works just after the rail end
        balance = 0f;
    }

    void Spray(float amount, Vector3 dir)
    {
        if (!spray) return;
        var emission = spray.emission;
        emission.rateOverTime = amount * sprayRate;
        if (amount > 0f) spray.transform.rotation = Quaternion.LookRotation(dir);
    }

    // ponytail: a bail returns to the start. The ragdoll replaces this in step 3.
    void Bail() => Respawn();

    // ponytail: returns to the start point only, the step 2 marker replaces spawnPos.
    void Respawn()
    {
        respawnQueued = false;
        rail = null;
        grounded = false;
        balance = airTime = 0f;
        rb.linearVelocity = Vector3.zero;
        rb.position = spawnPos;
        rb.rotation = spawnRot;
        transform.SetPositionAndRotation(spawnPos, spawnRot);
        heading = spawnRot * Vector3.forward;
        groundUp = Vector3.up;
    }

    void OnGUI()
    {
        if (Gamepad.current == null) GUI.Box(new Rect(20, 20, 200, 26), "Connect a controller");
    }
}
