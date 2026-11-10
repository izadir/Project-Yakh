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
    public float recoverRate = 220f;      // after a landing or a rail, deg/s the skis slide back to the travel line

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
    public float maxAssistRate = 900f;    // stick neutral: the rotation carries on and lines up with the landing, up to this deg/s
    public float landMaxTilt = 60f;       // a clean landing has the body within this of the slope normal
    public float landMaxYaw = 60f;        // and the skis within this of the travel line, forward or switch

    [Header("Rail")]
    public float railReach = 0.8f;        // grind assist: lock on within this distance to the side
    public float railAbove = 1.2f;        // and up to this high above the rail top
    public float railMaxAngle = 45f;      // with travel within this of the rail
    public float railSnapRate = 25f;      // 1/s, how fast the skier eases onto the rail line
    public float railFriction = 0.5f;     // m/s²
    public float lockZone = 0.4f;         // inside this balance the rail pulls the skis back to center
    public float recenterRate = 3f;
    public float balanceDrift = 2f;       // outside the lock zone the balance falls away this fast
    public float balanceControl = 2f;     // how hard the left stick rocks the skis
    public float wobble = 0.4f;           // small random rock, grows with speed
    public float railSwing = 60f;         // deg the skis swing off the rail line at full balance
    public float slipSpeed = 2f;          // sideways speed when the skier slips off

    public Transform body;                // squashes to show crouch and tuck

    // What the camera follows: the rail line on a rail, so the snap correction never shakes it.
    public Vector3 Travel => rail ? rail.Dir * (railSign * railSpeed) : rb.linearVelocity;

    Rigidbody rb;
    Rail rail, lastRail;
    readonly Collider[] near = new Collider[8];
    float radius, airTime, balance, railSpeed, railSign, railCooldown, lastGroundedTime = -1f, lastDownTime = -1f;
    float lastSteer = 1f, hockeySide, railSide, spinVel, flipVel, spinInit, flipInit;
    bool grounded, wasHockey, recovering, popQueued, respawnQueued;
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
            if (!wasGrounded)
            {
                // Touchdown: the skis start where the body points. If that is off the travel line, they slide back to it.
                Vector3 f = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, groundUp);
                if (f.sqrMagnitude > 1e-4f) heading = f.normalized;
                recovering = true;
                spinVel = flipVel = spinInit = flipInit = 0f;
            }

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

            // Recovery after a landing or a rail: the skis are too far off the travel line to carve, so they slide
            // and swing back to it. Forward, unless the skis point clearly backward (then switch).
            float sprayAmount = skid ? (hockey ? 1f : 0.3f) : 0f;
            if (recovering)
            {
                Vector3 target = Vector3.Angle(heading, travel) > 110f ? -travel : travel;
                float off = Vector3.Angle(heading, target);
                if (skid || off < 8f || speed < 2f) recovering = false;
                else
                {
                    heading = Vector3.RotateTowards(heading, target, recoverRate * Mathf.Deg2Rad * dt, 0f);
                    grip = skidGrip;
                    decel += 0.5f * skidDecel * Mathf.Sin(off * Mathf.Deg2Rad);
                    sprayAmount = 0.3f * off / 90f;
                }
            }

            // Carve: the velocity bends toward the ski line, forward or switch.
            Vector3 line = Vector3.Dot(v, heading) >= 0f ? heading : -heading;
            v = Vector3.RotateTowards(v, line * speed, grip * Mathf.Deg2Rad * dt, 0f);
            v = Vector3.MoveTowards(v, Vector3.zero, decel * dt);
            if (tuck) v += heading * (pushAccel * Mathf.Clamp01(1f - v.magnitude / pushMaxSpeed) * dt);
            if (sprayAmount > 0f) Spray(sprayAmount * Mathf.Clamp01(speed / 10f), travel + groundUp * 0.7f);
            wasHockey = hockey;
        }
        else
        {
            wasHockey = false;
            // Left stick spins (x) and flips (y). With the stick neutral on an axis, the rotation carries on, at least
            // at half the speed it started with, and lines up with the landing: body up to the slope normal,
            // skis to the travel line (forward, unless clearly switch or already spinning toward switch).
            float land = PredictLanding(v, out Vector3 n);
            Quaternion r = rb.rotation;
            Vector3 up = r * Vector3.up, right = r * Vector3.right;
            if (Mathf.Abs(stick.y) > 0.2f)
            {
                flipVel = stick.y * flipRate;
                flipInit = Mathf.Abs(flipVel);
            }
            else
            {
                float a = Vector3.SignedAngle(Vector3.ProjectOnPlane(up, right), Vector3.ProjectOnPlane(n, right), right);
                flipVel = Assist(Remaining(a, 360f, flipVel), flipInit, land, dt);
            }
            if (Mathf.Abs(stick.x) > 0.2f)
            {
                spinVel = stick.x * spinRate;
                spinInit = Mathf.Abs(spinVel);
            }
            else if (Vector3.Dot(up, n) > 0f)   // line the skis up only while the body is the right way up
            {
                Vector3 travel = Vector3.ProjectOnPlane(v, n);
                float a = travel.sqrMagnitude > 1f ? Vector3.SignedAngle(Vector3.ProjectOnPlane(r * Vector3.forward, n), travel, n) : 0f;
                spinVel = Assist(Mathf.Abs(spinVel) < 30f && Mathf.Abs(a) <= 110f ? a : Remaining(a, 180f, spinVel), spinInit, land, dt);
            }
            rb.MoveRotation(r * Quaternion.Euler(flipVel * dt, spinVel * dt, 0f));
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

    // Touchdown after a real air: false is a bail. A clean landing that is a bit off slides back in (see recovering).
    bool Land(Vector3 n, Vector3 v)
    {
        Quaternion r = rb.rotation;
        if (Vector3.Angle(r * Vector3.up, n) > landMaxTilt) return false;
        Vector3 travel = Vector3.ProjectOnPlane(v, n);
        if (travel.sqrMagnitude < 1f) return true;
        float yaw = Vector3.Angle(Vector3.ProjectOnPlane(r * Vector3.forward, n), travel);
        return yaw <= landMaxYaw || yaw >= 180f - landMaxYaw;
    }

    // Time to touchdown along the jump arc, and the slope normal there. Drag is ignored. The cast sphere adds
    // snapDistance, because the ground check counts touchdown that far above the snow.
    // ponytail: up to 30 sphere casts per physics step in the air, cache it if it shows in the profiler.
    float PredictLanding(Vector3 v, out Vector3 normal)
    {
        const float step = 0.1f;
        Vector3 p = rb.position;
        for (float t = step; t <= 3f; t += step)
        {
            Vector3 q = rb.position + v * t + Vector3.down * (0.5f * gravity * t * t), seg = q - p;
            if (Physics.SphereCast(p, radius + snapDistance, seg.normalized, out RaycastHit hit, seg.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return t - step + step * hit.distance / seg.magnitude;
            }
            p = q;
        }
        normal = Vector3.up;
        return 3f;
    }

    // Degrees left to the next target (every period degrees from a). Slow: the short way. Rotating: keep going
    // the same way, unless a target was just passed, then back up the short way.
    static float Remaining(float a, float period, float vel)
    {
        float ahead = Mathf.Repeat(a, period), shortest = ahead > period / 2f ? ahead - period : ahead;
        if (Mathf.Abs(vel) < 30f) return shortest;
        float same = vel > 0f ? ahead : ahead - period;
        return Mathf.Abs(same) > period * 0.8f ? shortest : same;
    }

    // Rate that reaches the target at touchdown, no slower than half the starting rate and never past the target.
    float Assist(float remaining, float startRate, float timeLeft, float dt)
    {
        float rate = Mathf.Clamp(Mathf.Abs(remaining) / Mathf.Max(timeLeft, dt), Mathf.Min(0.5f * startRate, maxAssistRate), maxAssistRate);
        return Mathf.Sign(remaining) * Mathf.Min(rate, Mathf.Abs(remaining) / dt);
    }

    void TryLockRail(Vector3 v)
    {
        Vector3 feet = rb.position - Vector3.up * radius;
        int count = Physics.OverlapSphereNonAlloc(feet, railReach + railAbove, near, Physics.AllLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            if (!near[i].TryGetComponent(out Rail r) || (r == lastRail && Time.time < railCooldown)) continue;
            Vector3 up = r.transform.up, along = r.Dir;
            Vector3 d = feet - r.Closest(feet, out float t);
            float above = Vector3.Dot(d, up);
            float angle = Vector3.Angle(Vector3.ProjectOnPlane(v, up), along);
            // Grind assist: lock when level with or above the rail, not rising fast, close to the side, and moving along it.
            if (t <= 0f || t >= 1f || above < -0.15f || above > railAbove || Vector3.Dot(v, up) > 1.5f) continue;
            if ((d - up * above).magnitude > railReach || (angle > railMaxAngle && angle < 180f - railMaxAngle)) continue;

            rail = r;
            railSign = angle <= 90f ? 1f : -1f;
            // Keep most of the speed, not only the part along the rail, so an angled entry does not stall.
            railSpeed = Mathf.Lerp(Mathf.Abs(Vector3.Dot(v, along)), Vector3.ProjectOnPlane(v, up).magnitude, 0.7f);
            Vector3 dir = along * railSign;
            balance = Mathf.Clamp(Vector3.Dot(v, Vector3.Cross(up, dir)) / Mathf.Max(railSpeed, 1f), -0.3f, 0.3f); // side speed starts a rock
            heading = Vector3.Dot(rb.rotation * Vector3.forward, dir) >= 0f ? dir : -dir;
            // The skis turn sideways onto the rail, to the side the body already points, else the side of the last turn.
            float yaw = Vector3.SignedAngle(dir, Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, up), up);
            railSide = Mathf.Abs(yaw) > 15f && Mathf.Abs(yaw) < 165f ? Mathf.Sign(yaw) : lastSteer;
            grounded = false;
            return;
        }
    }

    void RideRail(Vector2 stick, float dt)
    {
        Vector3 up = rail.transform.up, dir = rail.Dir * railSign, side = Vector3.Cross(up, dir);
        Vector3 top = rail.Closest(rb.position - up * radius, out float t);
        railSpeed += (Vector3.Dot(Vector3.down, dir) * gravity - railFriction) * dt;

        // Inside the lock zone the rail pulls the skis back to center. Outside it the balance falls away.
        // The stick rocks the skis, so too much rock tips the skier out of the zone and off the rail.
        float speedFactor = 1f + railSpeed / 15f;
        float pull = Mathf.Abs(balance) < lockZone ? -balance * recenterRate
                                                   : Mathf.Sign(balance) * (Mathf.Abs(balance) - lockZone) * balanceDrift * speedFactor;
        float noise = (Mathf.PerlinNoise(Time.time * 0.7f, 0f) - 0.5f) * 2f * wobble * speedFactor;
        balance += (pull + noise + stick.x * balanceControl) * dt;

        // A pop out goes to the side the stick leans.
        if (popQueued) { popQueued = false; LeaveRail(up * popSpeed + side * (stick.x * 2.5f), true); return; }
        if (Mathf.Abs(balance) >= 1f) { LeaveRail(side * (Mathf.Sign(balance) * slipSpeed), false); return; }
        if (t <= 0f || t >= 1f || railSpeed < 1f) { LeaveRail(Vector3.zero, false); return; }

        Vector3 target = top + up * radius;
        rb.linearVelocity = dir * railSpeed + (target - rb.position) * (Mathf.Min(1f, railSnapRate * dt) / dt);
        // The skis sit sideways on the rail and the balance rocks them around that.
        Vector3 skis = Quaternion.AngleAxis(railSide * 90f + balance * railSwing, up) * dir;
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(skis, up), 1f - Mathf.Exp(-alignRate * dt)));
    }

    void LeaveRail(Vector3 push, bool popped)
    {
        rb.linearVelocity = rail.Dir * (railSign * railSpeed) + push;
        lastRail = rail;
        rail = null;
        railCooldown = Time.time + 0.4f;               // only this rail, so the next rail can lock at once
        lastGroundedTime = popped ? -1f : Time.time;   // a late pop still works just after the rail end
        airTime = popped ? 0f : -1f;                   // only a pop out gets the landing check, a slip or the end never bails
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
        grounded = recovering = false;
        balance = airTime = spinVel = flipVel = spinInit = flipInit = 0f;
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
