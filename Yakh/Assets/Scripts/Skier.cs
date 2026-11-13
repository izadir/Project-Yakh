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
    public float pivotGrip = 100f;        // skis far off the travel line (out of a hockey stop) bend it only this fast, deg/s
    public float pivotScrub = 1.2f;       // and scrub this part of the speed per second while they are sideways to it
    public float sprayRate = 600f;        // powder particles per second at a full hockey stop
    public ParticleSystem spray;

    [Header("Air")]
    public float popSpeed = 6f;
    public float coyoteTime = 0.12f;      // pop still works this long after the lip
    public float flickWindow = 0.25f;     // max time from stick down to stick up
    public float spinRate = 540f;         // deg/s, left stick x
    public float flipRate = 360f;         // deg/s, left stick y
    public float tuckFlip = 1.3f;         // a tuck (A) flips this much faster. Spins stay the same.
    public float maxAssistRate = 900f;    // stick neutral: the rotation carries on and lines up with the landing, up to this deg/s
    public float landMaxTilt = 60f;       // a clean landing has the body within this of the slope normal
    public float landMaxYaw = 60f;        // and the skis within this of the travel line, forward or switch
    public float landMaxImpact = 28f;     // and hits the snow slower than this, m/s into the slope. Speed times the
                                          // landing angle: a big air on a steep landing is fine, the same air to flat bails.

    [Header("Rail")]
    public float railReach = 0.8f;        // grind assist: lock on within this distance to the side
    public float railAbove = 1.2f;        // and up to this high above the rail top
    public float railMaxAngle = 45f;      // with travel within this of the rail
    public float railMaxTilt = 45f;       // and the body within this of the rail's up, so an upside down skier does not lock
    public float railMaxFlip = 150f;      // and not mid flip faster than this (deg/s), so the air assist cannot snap a flip onto a rail
    public float railSnapRate = 25f;      // 1/s, how fast the skier eases onto the rail line
    public float railFriction = 0.5f;     // m/s²
    public float lockZone = 0.4f;         // inside this balance the rail pulls the skis back to center
    public float recenterRate = 3f;
    public float balanceDrift = 2f;       // outside the lock zone the balance falls away this fast
    public float balanceControl = 2f;     // how hard the left stick rocks the skis
    public float wobble = 0.4f;           // small random rock, grows with speed
    public float railSwing = 60f;         // deg the skis swing off the rail line at full balance
    public float slipSpeed = 2f;          // sideways speed when the skier slips off
    public float sparkRate = 400f;        // sparks per second on a metal rail at 10 m/s and faster
    public ParticleSystem sparks;

    [Header("Bail")]
    public float getUpTime = 2.5f;        // s in the ragdoll before the skier gets up where it lies
    public float ragdollFriction = 0.35f; // low, so a crash slides on the snow
    public float tripSpeed = 4f;          // hitting the side of a rail or box, or a tree or wall, faster than this (m/s into it) bails

    [Header("Pose")]
    public float crouchRate = 12f;        // 1/s, how fast the knees bend and straighten between stand, tuck, and crouch
    public float fullBend = 1.5f;         // knee bend of the full crouch: a pop, a tuck in the air, or a shifty. 1 is a half squat.
    public float tuckBend = 0.9f;         // knee bend of a tuck on the snow
    public float absorb = 2f;             // landing: the knees sink this fast (bend/s) per m/s of impact, then spring back
    public float skiWobble = 20f;         // landing: the skis wobble this fast (deg/s) per m/s of impact
    public float armSwing = 0.6f;         // deg the arms swing per m/s² of turn or brake. A loose spring, so they lag and settle.
    public float airBend = 0.6f;          // knee bend in the air, ready for the landing
    public float shiftyAngle = 60f;       // right stick left or right in the air turns the hips and skis this far, not the chest
    public float leanScale = 0.5f;        // the skier rolls at the ankles this part of the lean that balances a turn
    public float maxAnkle = 25f;          // deg the skier rolls at the ankles into a turn, at most
    public float edgeRatio = 1.2f;        // the skis roll on edge this many times the ankle roll

    [Header("Tricks: LB and RB are the left and right foot, LT and RT the left and right hand")]
    public float trickHold = 0.8f;        // s a trick is held before it counts
    public float comboWindow = 0.25f;     // s, a limb added this soon after a trick starts joins it and keeps its start time
    public float trickRate = 15f;         // 1/s, how fast a trick pose blends in and out (about 0.15 s)
    public float armsInSpin = 1.25f;      // arms in (LT + RT) spins this much faster

    [Header("Dev")]
    public float boostSpeed = 5f;         // D pad right adds this much speed. ponytail: dev only, remove before release.

    public Transform body;                // its Rigidbody children are the ragdoll, posed by Animate while skiing
    public Rigidbody pelvis;              // the ragdoll root, the camera follows it in a bail

    // What the camera follows: the rail line on a rail, so the snap correction never shakes it.
    public Vector3 Travel => bailed ? pelvis.linearVelocity : rail ? rail.Dir * (railSign * railSpeed) : rb.linearVelocity;
    public Vector3 Focus => bailed ? pelvis.transform.position : transform.position;
    // How far the skier leans into a turn, -1 to 1 (positive to the left, 1 is maxAnkle). The camera tilts with it.
    public float Lean => bailed ? 0f : ankle / maxAnkle;

    Rigidbody rb;
    Rigidbody[] parts;
    Pose[] rest;
    Gear[] gear;
    Rail rail, lastRail;
    readonly Collider[] near = new Collider[8];
    int railMask, obstacleMask, groundMask;
    float radius, airTime, balance, railSpeed, railSign, railCooldown, lastGroundedTime = -1f, lastDownTime = -1f;
    float lastSteer = 1f, hockeySide, railSide, spinVel, flipVel, spinInit, flipInit, getUpAt;
    bool grounded, wasHockey, recovering, popQueued, respawnQueued, boostQueued, bailed;
    Vector3 heading, groundUp = Vector3.up, spawnPos, lastV;   // lastV: the velocity before the physics step, for a trip
    Quaternion spawnRot;

    // Pose (see Animate)
    string[] role;                        // part names, cached, as each role turns its own way
    int[] parent;                         // index of each part's joint parent, -1 for the pelvis
    Quaternion[] turn;
    readonly Transform[] skis = new Transform[2], boots = new Transform[2];
    readonly Pose[] skiRest = new Pose[2];
    readonly Vector2[] swing = new Vector2[4], swingV = new Vector2[4];   // per side: arm (pitch, roll), then ski wobble (pitch, yaw), deg
    Vector3 thighLeg, shinLeg, force, prevTravel;   // force: acceleration minus gravity in body axes, it swings the arms
    Vector3 carve;                        // what the skis do to the velocity on the snow (turn and braking), m/s² in body axes
    float bend, sink, sinkV, twist, lookSide = 1f;   // twist: deg the upper body turns toward the travel line (see Animate)
    float ankle;                          // deg everything above the ankles rolls into a turn, positive to the left
    float grindYaw, shifty;               // deg the chest and head turn toward the grind, and the hips and skis turn in a shifty
    Vector3 landNormal = Vector3.up;      // the slope normal where the jump arc meets the snow
    readonly Vector4[] legPose = new Vector4[2];   // per leg: extra bend, kick forward, swing out, ski tip up (deg)
    readonly Quaternion[] thighTurn = new Quaternion[2], shinTurn = new Quaternion[2], skiTurn = new Quaternion[2];
    readonly Vector3[] skiAt = new Vector3[2];
    Vector2 chestPose;                    // crunch forward, twist (deg)
    readonly float[] reachW = new float[2];        // per arm: how far it blends from its swing to its reach target
    readonly Vector3[] reachAt = new Vector3[2], upperDir = new Vector3[2], foreDir = new Vector3[2];
    readonly Quaternion[] foreReach = new Quaternion[2];
    readonly int[] thighs = new int[2];
    int chest;
    Vector2 armLen;                       // shoulder to elbow, elbow to the glove

    // Tricks: the held limbs, plus Tail when the right stick swaps a grab to the tail
    const int LFoot = 1, RFoot = 2, LHand = 4, RHand = 8, Tail = 16;
    const int Spread = LFoot | RFoot, ArmsIn = LHand | RHand, LTip = LFoot | LHand, RTip = RFoot | RHand, LMute = LFoot | RHand, RMute = RFoot | LHand;
    int trick;
    float trickStart;
    GUIStyle hud;

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

        Physics.gravity = Vector3.down * gravity;   // the ragdoll falls like the skier
        railMask = LayerMask.GetMask("Rail");
        obstacleMask = LayerMask.GetMask("Obstacle");   // trees and steep walls
        groundMask = Physics.DefaultRaycastLayers & ~railMask & ~obstacleMask;   // both are solid, but they are never ground
        gear = body.GetComponentsInChildren<Gear>();
        parts = body.GetComponentsInChildren<Rigidbody>();
        rest = System.Array.ConvertAll(parts, p => new Pose(p.transform.localPosition, p.transform.localRotation));
        var snow = new PhysicsMaterial { dynamicFriction = ragdollFriction, staticFriction = ragdollFriction };
        foreach (var c in body.GetComponentsInChildren<Collider>()) c.sharedMaterial = snow;
        SetRagdoll(false);
        // The baggy pant legs touch, so the left leg and the right leg pass through each other.
        foreach (var l in parts)
            foreach (var r in parts)
                if (l.name is "Thigh" or "Shin" && r.name is "Thigh" or "Shin" && l.transform.localPosition.x < 0f && r.transform.localPosition.x > 0f)
                    Physics.IgnoreCollision(l.GetComponent<Collider>(), r.GetComponent<Collider>());

        role = System.Array.ConvertAll(parts, p => p.name);
        parent = System.Array.ConvertAll(parts, p => p.TryGetComponent(out CharacterJoint j) ? System.Array.IndexOf(parts, j.connectedBody) : -1);
        turn = new Quaternion[parts.Length];
        int shin = System.Array.IndexOf(role, "Shin");
        thighLeg = rest[shin].position - rest[parent[shin]].position;   // hip to knee
        shinLeg = rest[shin].rotation * new Vector3(0f, parts[shin].GetComponent<MeshFilter>().sharedMesh.bounds.min.y, 0f);   // knee to ankle
        for (int i = 0; i < parts.Length; i++)
        {
            int s = rest[i].position.x < 0f ? 0 : 1, u = parent[i];
            if (role[i] == "Chest") chest = i;
            if (role[i] == "Thigh") thighs[s] = i;
            if (role[i] != "Forearm") continue;
            upperDir[s] = (rest[i].position - rest[u].position).normalized;
            foreDir[s] = rest[i].rotation * Vector3.down;
            armLen = new Vector2(Vector3.Distance(rest[i].position, rest[u].position), -parts[i].transform.GetChild(0).localPosition.y);   // child 0: the glove
        }
        foreach (var g in gear)
        {
            if (g.name != "Shin") continue;
            int s = g.transform.localPosition.x < 0f ? 0 : 1;
            skis[s] = g.item;
            boots[s] = g.transform.Find("Boot");
            skiRest[s] = new Pose(body.InverseTransformPoint(g.item.position), Quaternion.Inverse(body.rotation) * g.item.rotation);
        }
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
        if (pad.dpad.right.wasPressedThisFrame) boostQueued = true;
        Tricks(pad);
        if (!bailed) Animate(pad.rightStick.ReadValue(), pad.buttonSouth.isPressed, Time.deltaTime);
    }

    // The held limbs pick the trick, in the air only. Three or four limbs keep the last trick. A limb added soon
    // after a trick starts joins it and keeps its start time, so a combo can build up one button at a time.
    void Tricks(Gamepad pad)
    {
        int held = (pad.leftShoulder.isPressed ? LFoot : 0) | (pad.rightShoulder.isPressed ? RFoot : 0)
                 | (pad.leftTrigger.isPressed ? LHand : 0) | (pad.rightTrigger.isPressed ? RHand : 0);
        if (grounded || rail || bailed) held = 0;
        if (IsGrab(held) && pad.rightStick.ReadValue().y < -0.5f) held |= Tail;
        if (held != 0 && TrickName(held) == null) held = trick;
        if (held == trick) return;
        if (trick == 0 || (held & trick) != trick || Time.time - trickStart > comboWindow) trickStart = Time.time;
        trick = held;
    }

    static bool IsGrab(int t) => (t & Spread) != 0 && (t & ArmsIn) != 0;

    static string TrickName(int t) => (t & ~Tail) switch
    {
        LFoot or RFoot => "Kick",
        LHand or RHand => "Arm out",
        Spread => "Spread eagle",
        ArmsIn => "Arms in",
        LTip or RTip => (t & Tail) != 0 ? "Tail grab" : "Tip grab",
        LMute or RMute => (t & Tail) != 0 ? "Cross tail grab" : "Mute",
        _ => null,
    };

    void FixedUpdate()
    {
        if (respawnQueued || airTime > 10f) { Respawn(); return; }   // 10 s in the air: the skier fell off the world
        Emit(spray, 0f, Vector3.up);
        Emit(sparks, 0f, Vector3.up);
        if (bailed) { if (Time.time > getUpAt) GetUp(); return; }
        var pad = Gamepad.current;
        Vector2 stick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
        bool tuck = pad != null && pad.buttonSouth.isPressed;
        float dt = Time.fixedDeltaTime;
        force = Quaternion.Inverse(rb.rotation) * ((Travel - prevTravel) / dt + Vector3.up * gravity);   // zero in the air, so the arms hang still
        prevTravel = Travel;
        if (boostQueued)
        {
            boostQueued = false;
            if (rail) railSpeed += boostSpeed;
            else rb.linearVelocity += (rb.linearVelocity.sqrMagnitude > 1f ? rb.linearVelocity.normalized : heading) * boostSpeed;
        }
        if (rail) { RideRail(stick, dt); return; }
        Vector3 v = rb.linearVelocity;

        // On the ground, cast along the body's down axis so walls and pipes work later.
        bool wasGrounded = grounded;
        Vector3 down = grounded ? -groundUp : Vector3.down;
        grounded = Physics.SphereCast(rb.position, radius * 0.9f, down, out RaycastHit hit,
                       radius * 0.1f + snapDistance, groundMask, QueryTriggerInteraction.Ignore)
                   && Vector3.Dot(v, hit.normal) < snapMaxSpeed;
        if (grounded && !wasGrounded && airTime > 0.2f && (IsGrab(trick) || !Land(hit.normal, v))) { Bail(v); return; }   // a grab held into the landing bails
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
                carve = Vector3.zero;
                // The knees sink and the skis wobble, more for a harder hit (see Animate).
                float impact = Mathf.Max(0f, -Vector3.Dot(v, groundUp));
                sinkV += absorb * impact;
                for (int s = 2; s < 4; s++) swingV[s] += Random.insideUnitCircle.normalized * (skiWobble * impact);
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
            float grip = carveGrip, decel = snowFriction, sprayAmount = skid ? (hockey ? 1f : 0.3f) : 0f;
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
                else if (!recovering)
                {
                    // Skis well off the travel line (as just out of a hockey stop) cannot carve. They skid round
                    // with less grip and scrub speed, so a sharp pivot loses speed and drifts wide.
                    float off = 90f - Mathf.Abs(90f - Vector3.Angle(travel, heading));   // 0 to 90, forward or switch
                    float t = Mathf.InverseLerp(10f, 45f, off);
                    grip = Mathf.Lerp(carveGrip, pivotGrip, t);
                    decel += t * pivotScrub * speed * Mathf.Sin(off * Mathf.Deg2Rad);
                    sprayAmount = 0.3f * t;
                }
            }

            // Recovery after a landing or a rail: the skis are too far off the travel line to carve, so they slide
            // and swing back to it. Forward, unless the skis point clearly backward (then switch).
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

            // Carve: the velocity bends toward the ski line, forward or switch. What the skis do to it (the turn and the
            // braking, not gravity or the snow faces) leans the skier (see Animate). Smoothed over about 0.1 s, because
            // each snow face tilts the ski line a little.
            Vector3 line = Vector3.Dot(v, heading) >= 0f ? heading : -heading, before = v;
            v = Vector3.RotateTowards(v, line * speed, grip * Mathf.Deg2Rad * dt, 0f);
            v = Vector3.MoveTowards(v, Vector3.zero, decel * dt);
            if (tuck) v += heading * (pushAccel * Mathf.Clamp01(1f - v.magnitude / pushMaxSpeed) * dt);
            carve = Vector3.Lerp(carve, Quaternion.Inverse(rb.rotation) * (v - before) / dt, 1f - Mathf.Exp(-10f * dt));
            if (sprayAmount > 0f) Emit(spray, sprayAmount * Mathf.Clamp01(speed / 10f) * sprayRate, travel + groundUp * 0.7f);
            wasHockey = hockey;
        }
        else
        {
            wasHockey = false;
            // Left stick spins (x) and flips (y). With the stick neutral on an axis, the rotation carries on, at least
            // at half the speed it started with, and lines up with the landing: body up to the slope normal,
            // skis to the travel line (forward, unless clearly switch or already spinning toward switch).
            float land = PredictLanding(v, out Vector3 n);
            landNormal = n;
            Quaternion r = rb.rotation;
            Vector3 up = r * Vector3.up, right = r * Vector3.right;
            if (Mathf.Abs(stick.y) > 0.2f)
            {
                flipVel = stick.y * flipRate * (tuck ? tuckFlip : 1f);
                flipInit = Mathf.Abs(flipVel);
            }
            else
            {
                float a = Vector3.SignedAngle(Vector3.ProjectOnPlane(up, right), Vector3.ProjectOnPlane(n, right), right);
                flipVel = Assist(Remaining(a, 360f, flipVel), flipInit, land, dt);
            }
            if (Mathf.Abs(stick.x) > 0.2f)
            {
                spinVel = stick.x * spinRate * (trick == ArmsIn ? armsInSpin : 1f);
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
        rb.linearVelocity = lastV = v;

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
        if (Vector3.Angle(r * Vector3.up, n) > landMaxTilt || Vector3.Dot(v, -n) > landMaxImpact) return false;
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
                    groundMask, QueryTriggerInteraction.Ignore))
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
        int count = Physics.OverlapSphereNonAlloc(feet, railReach + railAbove, near, railMask);
        for (int i = 0; i < count; i++)
        {
            if (!near[i].TryGetComponent(out Rail r) || (lastRail && r.Joined(lastRail) && Time.time < railCooldown)) continue;
            Vector3 up = r.transform.up, along = r.Dir;
            Vector3 d = feet - r.Closest(feet, out float t);
            float above = Vector3.Dot(d, up);
            float angle = Vector3.Angle(Vector3.ProjectOnPlane(v, up), along);
            // Grind assist: lock when level with or above the rail, not rising fast, close to the side, and moving along it.
            if (t <= 0f || t >= 1f || above < -0.15f || above > railAbove || Vector3.Dot(v, up) > 1.5f) continue;
            if ((d - up * above).magnitude > railReach || (angle > railMaxAngle && angle < 180f - railMaxAngle)) continue;
            if (Vector3.Angle(rb.rotation * Vector3.up, up) > railMaxTilt || Mathf.Abs(flipVel) > railMaxFlip) continue;

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
            rb.detectCollisions = false;   // the rail is solid. While locked, RideRail holds the skier on its top line.
            return;
        }
    }

    void RideRail(Vector2 stick, float dt)
    {
        // A kink or a curve: past the end of a segment the skier carries on along the next one with its speed (the one
        // before, riding backward). At a bend the skier can be just short of the new segment, so it starts inside it.
        Vector3 top = rail.Closest(rb.position, out float t);
        Rail on = railSign > 0f ? rail.next : rail.prev;
        if (on && (railSign > 0f ? t >= 1f : t <= 0f))
        {
            top = (rail = on).Closest(rb.position, out t);
            t = Mathf.Clamp(t, 0.01f, 0.99f);
        }
        Vector3 up = rail.transform.up, dir = rail.Dir * railSign, side = Vector3.Cross(up, dir);
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
        if (rail.Metal) Emit(sparks, sparkRate * Mathf.Clamp01(railSpeed / 10f), up * 0.6f - dir);   // thrown back and up
        // The skis sit sideways on the rail and the balance rocks them around that.
        Vector3 skis = Quaternion.AngleAxis(railSide * 90f + balance * railSwing, up) * dir;
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(skis, up), 1f - Mathf.Exp(-alignRate * dt)));
    }

    void LeaveRail(Vector3 push, bool popped)
    {
        rb.linearVelocity = rail.Dir * (railSign * railSpeed) + push;
        rb.detectCollisions = true;
        lastRail = rail;
        rail = null;
        railCooldown = Time.time + 0.4f;               // only this rail (its chain), so the next rail can lock at once
        lastGroundedTime = popped ? -1f : Time.time;   // a late pop still works just after the rail end
        // A pop or a rising exit (a kicker) gets the landing check. A slip or a flat end never bails.
        airTime = popped || rb.linearVelocity.y > 1f ? 0f : -1f;
        balance = 0f;
    }

    static void Emit(ParticleSystem ps, float rate, Vector3 dir)
    {
        if (!ps) return;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        if (rate > 0f) ps.transform.rotation = Quaternion.LookRotation(dir);
    }

    // The ragdoll takes over. Each part starts with the skier's velocity and air spin.
    // The skier sphere stays at the crash point with no collision until GetUp.
    void Bail(Vector3 v)
    {
        bailed = true;
        getUpAt = Time.time + getUpTime;
        Vector3 w = rb.rotation * new Vector3(flipVel, spinVel, 0f) * Mathf.Deg2Rad;
        rb.linearVelocity = Vector3.zero;
        rb.detectCollisions = false;
        SetRagdoll(true);
        foreach (var p in parts)
        {
            p.linearVelocity = v + Vector3.Cross(w, p.transform.position - rb.position);
            p.angularVelocity = w;
        }
    }

    // Trip: a fast hit into the side of a rail or box, or into a tree or wall, bails, with the speed from before the hit.
    // A hit on the top of a rail or box only bumps the skier up. An obstacle has no top to ride on.
    void OnCollisionEnter(Collision c)
    {
        int bit = 1 << c.gameObject.layer;
        if (bailed || ((railMask | obstacleMask) & bit) == 0) return;
        Vector3 n = c.GetContact(0).normal;
        if (((obstacleMask & bit) != 0 || Vector3.Dot(n, c.transform.up) < 0.5f) && Vector3.Dot(lastV, -n) > tripSpeed) Bail(lastV);
    }

    // The skier gets up where the ragdoll lies, keeps its speed along the slope, and faces the way it slides
    // (down the fall line when it is still). Rails count here, so a ragdoll on a box puts the skier on top of it.
    void GetUp()
    {
        if (!Physics.Raycast(pelvis.position + Vector3.up, Vector3.down, out RaycastHit hit, 4f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) { Respawn(); return; }
        Vector3 v = Vector3.ProjectOnPlane(pelvis.linearVelocity, hit.normal);
        Vector3 f = v.sqrMagnitude > 1f ? v : Vector3.ProjectOnPlane(Vector3.down, hit.normal);
        if (f.sqrMagnitude < 0.01f) f = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, hit.normal);
        Place(hit.point + hit.normal * radius, Quaternion.LookRotation(f, hit.normal), v);
    }

    // ponytail: returns to the start point only, the step 2 marker replaces spawnPos.
    void Respawn() => Place(spawnPos, spawnRot, Vector3.zero);

    void Place(Vector3 pos, Quaternion rot, Vector3 v)
    {
        if (bailed)
        {
            bailed = false;
            SetRagdoll(false);
            for (int i = 0; i < parts.Length; i++) parts[i].transform.SetLocalPositionAndRotation(rest[i].position, rest[i].rotation);
            foreach (var g in gear) g.Attach();
        }
        rb.detectCollisions = true;
        respawnQueued = popQueued = boostQueued = false;
        rail = null;
        grounded = recovering = false;
        balance = airTime = spinVel = flipVel = spinInit = flipInit = 0f;
        rb.linearVelocity = prevTravel = v;
        rb.position = pos;
        rb.rotation = rot;
        transform.SetPositionAndRotation(pos, rot);
        heading = rot * Vector3.forward;
        groundUp = rot * Vector3.up;
    }

    // While skiing the parts are kinematic and ride along with no collision. Interpolation is off then,
    // or it would fight the parent transform.
    void SetRagdoll(bool on)
    {
        foreach (var p in parts)
        {
            p.isKinematic = !on;
            p.detectCollisions = on;
            p.interpolation = on ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
        }
    }

    // Kinematic pose while skiing. The knees bend toward the crouch for a pop (right stick down) or a tuck (A),
    // and the hips drop so the ankles stay on the skis. A landing sinks the knees further and they spring back,
    // the skis wobble, and the arms swing loose with the turns. In a turn the skier rolls at the ankles and the skis
    // go on edge. In the air the knees bend a little for the landing, the arms float out for balance, and a trick
    // moves the limbs it holds (see Tricks). Each part turns at its joint (its pivot) by a rotation in body axes, on
    // top of its parent's turn, from its rest pose. A ski turns with its leg's swing but not with the knee bend.
    void Animate(Vector2 stick, bool tuck, float dt)
    {
        dt = Mathf.Min(dt, 1f / 30f);   // a long frame would blow up the stiff ski springs
        bool air = !grounded && !rail;
        float target = Mathf.Max(Mathf.Max(fullBend * Mathf.Clamp01(-stick.y), tuck ? (grounded ? tuckBend : fullBend) : 0f),
                                 air ? Mathf.Max(airBend, fullBend * Mathf.Abs(stick.x)) : 0f);   // a shifty tucks the knees
        bend = Mathf.Lerp(bend, target, 1f - Mathf.Exp(-crouchRate * dt));
        sinkV -= (150f * sink + 14f * sinkV) * dt;
        sink += sinkV * dt;
        float b = Mathf.Clamp(bend + sink, -0.2f, fullBend + 0.2f), lean = 20f * b;
        float ease = 1f - Mathf.Exp(-(air ? trickRate : 2f * trickRate) * dt);   // faster on the snow, so the skis land flat
        int limbs = trick & ~Tail;
        bool tail = (trick & Tail) != 0, grab = IsGrab(trick);
        // Switch, like a stance swap in Skate 3: the upper body leads the travel line. While the skis swing round under it
        // (a skid, a hockey stop, a pivot, a spin), the chest and head turn toward the way the skier moves, 3/4 of the
        // angle, up to 60° and 75° more. Riding switch the skier looks back over the shoulder on the side the skis came
        // round. The side holds past 150°, so it does not flip while the skis sit straight back. In the air the pose
        // holds and the side follows the spin, so a 180 lands looking the way it turned. On a rail the grind turn leads.
        // ponytail: no foot stagger. Add one if the switch stance looks too square.
        Vector3 travel = Vector3.ProjectOnPlane(rb.linearVelocity, transform.up);
        float twistT = rail ? 0f : twist;
        if (!rail && travel.sqrMagnitude > 1f)
        {
            float d = Vector3.SignedAngle(transform.forward, travel, transform.up);
            if (Mathf.Abs(d) < 150f) lookSide = d < 0f ? -1f : 1f;
            if (grounded) twistT = 0.75f * lookSide * Mathf.Abs(d);
        }
        twist = Mathf.Lerp(twist, twistT, 1f - Mathf.Exp(-8f * dt));
        float chestYaw = Mathf.Clamp(0.45f * twist, -60f, 60f), headYaw = Mathf.Clamp(twist - chestYaw, -75f, 75f);
        // On a rail the chest turns 60% of the way to the grind direction and the head the rest, so the skier faces down
        // the rail. In the air the right stick turns the hips and skis (a shifty) under a still chest.
        float toGrind = rail ? Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(Travel, transform.up), transform.up) : 0f;
        grindYaw = Mathf.Lerp(grindYaw, Mathf.Clamp(toGrind, -100f, 100f), ease);
        shifty = Mathf.Lerp(shifty, air ? stick.x * shiftyAngle : 0f, ease);
        Quaternion hips = Quaternion.Euler(0f, shifty, 0f);

        // Legs, each (extra bend, kick forward, swing out, ski tip up). In the air the leg on the low side of the
        // landing reaches straight down for the snow and the other folds up, fully at 45° of roll, not upside down.
        Vector3 up = transform.up, fwd = transform.forward;
        float roll = air && Vector3.Dot(up, landNormal) > 0.3f
            ? Mathf.Clamp(Vector3.SignedAngle(up, Vector3.ProjectOnPlane(landNormal, fwd), fwd) / 45f, -1f, 1f) : 0f;
        // The hips drop with the shared bend, so on the snow the ankles stay on the skis. A leg's own bend in the air
        // lifts its foot, not the body.
        Vector3 drop = thighLeg + shinLeg - Quaternion.Euler(-45f * b, 0f, 0f) * thighLeg - Quaternion.Euler(25f * b, 0f, 0f) * shinLeg;
        bool cross = limbs is LMute or RMute;

        // Turning: the ankles roll into the turn, leanScale of the lean that balances what the skis do to the velocity
        // (the turn and the braking, so a traverse leans into the hill and a hockey stop leans into the stop). Not the
        // whole acceleration: the snow faces and the ground snap kick it every step, and that shook the skier. Everything
        // above the ankles leans with them, on the point between the skis. The inside (uphill) knee bends a little and
        // the outside leg straightens a little, so both skis stay on the snow (rise: how far each ankle moves). Both skis
        // roll on edge edgeRatio times the ankle roll.
        float ankleT = grounded ? Mathf.Clamp(leanScale * Mathf.Atan2(-carve.x, gravity) * Mathf.Rad2Deg, -maxAnkle, maxAnkle) : 0f;
        ankle = Mathf.Lerp(ankle, ankleT, 1f - Mathf.Exp(-8f * dt));
        int inside = ankle > 0f ? 0 : 1;
        float rise = Mathf.Abs(skiRest[1].position.x - skiRest[0].position.x) / 2f * Mathf.Tan(Mathf.Abs(ankle) * Mathf.Deg2Rad);
        float Lift(float x) => (Quaternion.Euler(-45f * x, 0f, 0f) * thighLeg + Quaternion.Euler(25f * x, 0f, 0f) * shinLeg).y;   // ankle height under the hip
        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1f : 1f, low = side * roll;
            Vector4 t = new(low > 0f ? -b * low : (fullBend - b) * -low, 0f, 45f * Mathf.Max(low, 0f), 0f);
            if ((limbs & (s == 0 ? LFoot : RFoot)) != 0)
                t = limbs == Spread ? new(0.1f - b, 0f, 45f, 0f)       // spread eagle: straight, 45° out
                  : !grab ? new(0.1f - b, 45f, 0f, 0f)                 // kick: straight, 45° forward
                  : tail ? new(2f - b, -15f, 0f, -15f)                 // knee up, the tail up 30° behind the hip
                  : new(1.6f - b, 45f, cross ? -8f : 15f, 0f);         // knee up, the tip up in front, beside the chest or across it
            Vector4 l = legPose[s] = Vector4.Lerp(legPose[s], t, ease);
            float bs = Mathf.Clamp(b + l.x, -0.2f, 2f);
            if (rise > 0.001f)
            {
                // The bend that moves the ankle up by rise (inside) or down by rise (outside). The lift grows with the bend.
                float lo = -0.2f, hi = 2f, at = Lift(bs) + (s == inside ? rise : -rise);
                for (int k = 0; k < 12; k++) { float m = (lo + hi) / 2f; if (Lift(m) < at) lo = m; else hi = m; }
                bs = (lo + hi) / 2f;
            }
            Quaternion swingQ = hips * Quaternion.Euler(0f, 0f, side * l.z) * Quaternion.Euler(-l.y, 0f, 0f);
            thighTurn[s] = swingQ * Quaternion.Euler(-45f * bs, 0f, 0f);
            shinTurn[s] = swingQ * Quaternion.Euler(25f * bs, 0f, 0f);
            skiTurn[s] = swingQ * Quaternion.Euler(-l.w, 0f, 0f);
            int pv = parent[thighs[s]];   // the pelvis
            Vector3 hipAt = rest[pv].position + drop + hips * (rest[thighs[s]].position - rest[pv].position);
            skiAt[s] = hipAt + thighTurn[s] * thighLeg + shinTurn[s] * shinLeg + skiTurn[s] * (skiRest[s].position - rest[thighs[s]].position - thighLeg - shinLeg);
        }
        Vector3 pivot = (skiAt[0] + skiAt[1]) / 2f;
        Quaternion tilt = Quaternion.AngleAxis(ankle, Vector3.forward), edge = Quaternion.AngleAxis(edgeRatio * ankle, Vector3.forward);
        for (int s = 0; s < 2; s++) skiAt[s] = pivot + tilt * (skiAt[s] - pivot);

        // A tip grab crunches the chest forward and turns the grabbing shoulder forward. A tail grab leans back a little
        // and turns the grabbing shoulder back.
        float handSide = (limbs & RHand) != 0 ? 1f : -1f;
        Vector2 chestT = !grab ? Vector2.zero : tail ? new(-10f, 30f * handSide) : new(15f, -handSide * (cross ? 20f : 8f));
        chestPose = Vector2.Lerp(chestPose, chestT, ease);
        Quaternion chestTurn = tilt * Quaternion.Euler(lean + chestPose.x, chestYaw + chestPose.y + 0.6f * grindYaw, 0f);

        // Arms: on the snow they swing with the turns. In the air they float out a little for balance, and drift so they
        // never sit still. A lone trigger raises its arm. A grab reaches for the ski, and arms in cross the chest.
        // In a deep crouch on the snow the arms swing back, so the pole tips stay out of the snow.
        Vector2 arm = Vector2.ClampMagnitude(new Vector2(force.z, -force.x) * armSwing, 40f);
        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1f : 1f, drift = Time.time * 1.3f;
            int hand = s == 0 ? LHand : RHand;
            Vector2 aim = !air ? arm + new Vector2(28f * Mathf.Max(0f, b - 0.6f), 0f) : new(-10f + 8f * Noise(drift, s), side * (limbs == hand ? 100f : grab ? 45f : 30f) + 8f * Noise(drift, s + 2));
            Spring(ref swing[s], ref swingV[s], aim, s == 0 ? 50f : 65f, 6f, dt);   // not the same, so the arms never move as one
            Spring(ref swing[2 + s], ref swingV[2 + s], Vector2.zero, 250f, 6f, dt);
            bool reach = limbs == ArmsIn || (grab && (limbs & hand) != 0);
            if (reach)
            {
                int k = (limbs & LFoot) != 0 ? 0 : 1;   // the grabbed ski
                Vector3 at = limbs == ArmsIn ? rest[chest].position + drop + chestTurn * new Vector3(-side * 0.1f, 0.24f, 0.24f)
                                             : skiAt[k] + skiTurn[k] * skiRest[k].rotation * new Vector3(0f, 0.03f, tail ? -0.5f : 0.5f);
                reachAt[s] = reachW[s] < 0.01f ? at : Vector3.Lerp(reachAt[s], at, ease);
            }
            reachW[s] = Mathf.Lerp(reachW[s], reach ? 1f : 0f, ease);
        }

        for (int i = 0; i < parts.Length; i++)
        {
            int p = parent[i], s = rest[i].position.x < 0f ? 0 : 1;
            Vector2 a = swing[s];
            turn[i] = role[i] switch
            {
                "Chest" => chestTurn,
                "Head" => turn[p] * Quaternion.Euler(-0.6f * (lean + chestPose.x), headYaw - chestPose.y + 0.4f * grindYaw, 0f),   // the eyes stay on the slope ahead, behind in switch, or down the rail
                "Pelvis" => tilt * hips,
                "Thigh" => tilt * thighTurn[s],
                "Shin" => tilt * shinTurn[s],
                "UpperArm" => turn[p] * Quaternion.Euler(a.x, 0f, a.y),
                "Forearm" => turn[p] * Quaternion.Euler(0.5f * a.x, 0f, 0f),    // a little whip. The elbow has no sideways swing.
                _ => Quaternion.identity,
            };
            Vector3 pos = p < 0 ? pivot + tilt * (rest[i].position + drop - pivot) : parts[p].transform.localPosition + turn[p] * (rest[i].position - rest[p].position);
            if (reachW[s] > 0.001f && role[i] == "UpperArm")
            {
                Reach(s, pos, reachAt[s], out Quaternion upper, out foreReach[s]);
                turn[i] = Quaternion.Slerp(turn[i], upper, reachW[s]);
            }
            if (reachW[s] > 0.001f && role[i] == "Forearm") turn[i] = Quaternion.Slerp(turn[i], foreReach[s], reachW[s]);
            parts[i].transform.SetLocalPositionAndRotation(pos, turn[i] * rest[i].rotation);
        }
        for (int s = 0; s < 2; s++)
        {
            Vector3 p = body.TransformPoint(skiAt[s]);
            Quaternion r = body.rotation * edge * skiTurn[s] * Quaternion.Euler(swing[2 + s].x, swing[2 + s].y, 0f) * skiRest[s].rotation;
            skis[s].SetPositionAndRotation(p, r);
            boots[s].SetPositionAndRotation(p, r);   // the boot has the ski's pivot, so it stays in the binding
        }
    }

    // Two bone reach in body space: the upper arm from the shoulder and the forearm to the glove, each a turn from its
    // rest direction. The elbow bends out, down, and back. Past arm's length the straight arm points at the target.
    void Reach(int s, Vector3 shoulder, Vector3 target, out Quaternion upper, out Quaternion fore)
    {
        float a = armLen.x, c = armLen.y;
        Vector3 dir = (target - shoulder).normalized;
        float d = Mathf.Clamp(Vector3.Distance(target, shoulder), Mathf.Abs(a - c) + 0.01f, a + c - 0.001f);
        float cos = (a * a + d * d - c * c) / (2f * a * d);
        Vector3 bendTo = Vector3.ProjectOnPlane(new Vector3(s == 0 ? -1f : 1f, -0.5f, -0.3f), dir).normalized;
        Vector3 elbow = shoulder + a * (cos * dir + Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos)) * bendTo);
        upper = Quaternion.FromToRotation(upperDir[s], (elbow - shoulder) / a);
        fore = Quaternion.FromToRotation(foreDir[s], (shoulder + dir * d - elbow) / c);
    }

    static float Noise(float t, int seed) => (Mathf.PerlinNoise(t, seed * 3.7f) - 0.5f) * 2f;

    // Damped spring toward target. k sets how fast it returns, a damp below 2√k lets it swing past.
    static void Spring(ref Vector2 x, ref Vector2 v, Vector2 target, float k, float damp, float dt)
    {
        v += (k * (target - x) - damp * v) * dt;
        x += v * dt;
    }

    void OnGUI()
    {
        if (Gamepad.current == null) GUI.Box(new Rect(20, 20, 200, 26), "Connect a controller");
        // ponytail: a dev readout until the trick HUD. The held trick and its time, green once it counts.
        if (trick == 0) return;
        float held = Time.time - trickStart;
        hud ??= new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
        hud.normal.textColor = held >= trickHold ? Color.green : Color.white;
        GUI.Label(new Rect(0f, Screen.height - 90f, Screen.width, 60f), $"{TrickName(trick)}  {held:F1} s", hud);
    }
}
