using UnityEngine;

/// <summary>Arcade car controller built on WheelColliders. Tuned for mobile:
/// forgiving steering, strong brakes, downforce at speed, no flip-overs
/// (low centre of mass). Keyboard works for editor testing; touch input
/// comes from MobileControls.</summary>
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheels")]
    public WheelCollider wheelFL;
    public WheelCollider wheelFR;
    public WheelCollider wheelRL;
    public WheelCollider wheelRR;
    public Transform wheelMeshFL;
    public Transform wheelMeshFR;
    public Transform wheelMeshRL;
    public Transform wheelMeshRR;

    [Header("Engine")]
    public float maxSpeed = 55f;          // m/s  (~198 km/h)
    public float maxMotorTorque = 2200f;
    public float maxBrakeTorque = 4500f;
    public float reverseTorque = 1200f;
    public float maxSteerAngle = 28f;
    public float downforce = 120f;
    /// <summary>Quadratic aero drag, 1/2*rho*CdA in N*s^2/m^2
    /// (~0.35 = CdA ~0.6, a typical sports car). Gives believable
    /// coast-down and stops downhill overspeed past maxSpeed, which
    /// the motor cut-off alone cannot do (no drag force existed).</summary>
    public float dragCoefficient = 0.35f;

    [Header("Arcade feel")]
    public float steerSpeed = 6f;
    public float gripFactor = 1f;
    /// <summary>Raised by the Brakes garage upgrade (1 = stock).</summary>
    public float brakeMultiplier = 1f;
    /// <summary>Raised by the Aero garage upgrade (1 = stock).</summary>
    public float downforceMultiplier = 1f;
    /// <summary>Set by WeatherSystem every frame (rain = 0.75).</summary>
    public float weatherGripMultiplier = 1f;
    public float handlingMultiplier = 1f;
    public float accelerationMultiplier = 1f;

    Rigidbody rb;
    float steerInput;
    float throttleInput;
    float brakeInput;
    // True when the brake input comes from S/Down or the touch BRAKE
    // button (reverse intent), as opposed to Space (pure brake).
    bool reverseInput;
    float currentSteer;
    float lateralSpeed;

    // ---- WheelCollider base setup (physics audit 2026-11-10) ----
    // Cached base friction stiffnesses (prefab: 1.0 / 1.0), scaled by
    // weather/season grip in ApplyGripToWheels(). -1 = not yet applied.
    float baseForwardStiffness = 1f;
    float baseSidewaysStiffness = 1f;
    float appliedGripStiffness = -1f;

    // ---- Physics safety (fixed 2026-11-07, round-3 bug #1) ----
    // A WheelCollider / solver blow-up once sent the car to ~895,000 km/h
    // and then NaN, after which the car vanished and the camera showed an
    // empty void. Three layers of defence, cheapest first:
    //  - hard velocity / angular-velocity caps every FixedUpdate, so a bad
    //    solver impulse can never integrate into the stratosphere;
    //  - a NaN / Infinity guard that restores the last known-good pose;
    //  - ContinuousDynamic collision detection so the car cannot tunnel
    //    into a wall at 100+ km/h (tunnelling + deep penetration is the
    //    classic trigger for the solver explosion).
    // 110 m/s (~396 km/h) is above every car's top speed (fastest is
    // 325 km/h = 90 m/s), so the cap never interferes with legal driving.
    public float maxVelocityMs = 110f;
    public float maxAngularVelocity = 10f;
    Vector3 lastGoodPosition;
    Quaternion lastGoodRotation;
    bool hasLastGood;

    // ---- Stuck-at-spawn watchdog (fixed 2026-11-07, round-3 bug #2) ----
    // If the touch BRAKE gets stuck at 1 (pointer-up lost) while the
    // driver holds GAS at a standstill, the 4500 Nm brake torque beats
    // the motor and the car never moves. A brake held > 3 s in exactly
    // that situation, with no keyboard brake held, is treated as stale
    // and ignored. A real driver holding both pedals is unaffected for
    // the first 3 s, and the brake still works the moment GAS is lifted.
    float staleBrakeTimer;
    bool staleBrakeWarned;

    // ---- Wedge / stuck auto-nudge (fixed 2026-11-07, Phase A
    // free-roam playtest) ----
    // The car repeatedly wedged nose-first against building faces
    // (e.g. (92,281), (104,284), (-58,358), (53,403)): the driver
    // holds W, the car pushes into the wall at ~0 m/s, and reversing
    // by hand is fiddly (the nose scrapes along the wall, or the
    // driver steers the wrong way while reversing and stays wedged).
    // If the car is trying to move (throttle held) but has been
    // effectively stationary for 2.5 s of game time, it is wedged:
    // automatically reverse for 1.5 s with the wheels turned, backing
    // off in an arc so the nose clears the wall at a new angle. The
    // steer direction alternates each nudge so a second nudge tries
    // the other side. A car that is merely stopped (no throttle) or
    // driving normally never triggers this: an unblocked car passes
    // 0.5 m/s in well under 1 s of game time.
    float stuckTimer;
    float nudgeTimer;
    float nudgeSteer = 0.6f;

    // ---- Out-of-world / upside-down auto-reset (round-3 bug #1) ----
    // The launch itself is fixed at the source (TrafficCar no longer
    // cuts diagonally across the city; see TrafficCar.cs), but a racing
    // game must never lose the car: if the car leaves the world or ends
    // up upside down and slow, it is put back on the road at the last
    // known-good pose instead of falling through the void forever.
    // lastGood is only recorded while the car is on the ground, inside
    // the world and upright, so a reset never lands in the void.
    float upsideDownTimer;
    Vector3 prevVelocity;
    bool hasPrevVelocity;

    /// <summary>True while sliding sideways fast enough to skid.</summary>
    public bool IsSkidding
    {
        get { return Mathf.Abs(lateralSpeed) > 4f && GetSpeedMs() > 8f; }
    }

    /// <summary>Current throttle input 0-1 (for engine/exhaust sound).</summary>
    public float ThrottleInput { get { return throttleInput; } }

    /// <summary>Sideways speed magnitude (for skid sound volume).</summary>
    public float SlipAmount { get { return Mathf.Abs(lateralSpeed); } }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.55f, 0.1f);
        // Yaw damping (fixed 2026-11-07, race playtest): the default
        // angular damping (0.05) lets the car keep rotating once it
        // starts turning, so a 90-degree corner easily becomes a spin
        // (the car over-rotates past the target heading, the driver
        // corrects, it spins the other way, and it ends up in the
        // buildings). 1.5 damps the yaw oscillation so the car settles
        // on its new heading instead of spinning, without killing the
        // WheelCollider steering (which is far stronger).
        rb.angularDamping = 1.5f;
        // Never sleep: a sleeping Rigidbody ignores WheelCollider motor
        // torque, which left the car stuck at spawn until something else
        // woke it (round-3 bug #2: stuck on 2 of 3 fresh launches).
        rb.sleepThreshold = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ConfigureWheels();
        lastGoodPosition = transform.position;
        lastGoodRotation = transform.rotation;
        hasLastGood = true;
        CarSelection.ApplyToCar(this);
        UpgradeSystem.ApplyToCar(this);
        CarVisuals.Rebuild(transform, isPlayer: true);
    }

    void Start()
    {
        // Clear any touch input state carried into this scene (e.g. a
        // GAS/BRAKE press from the menu) so the car never starts braked.
        if (MobileControls.Instance != null) MobileControls.Instance.ResetInputs();
    }

    void Update()
    {
        // Manual unstick (R): the auto-reset can still leave the car on a
        // curb or grass verge if the last good pose was marginal. R puts
        // the car back on the nearest road centre, facing along the road,
        // so the driver is never soft-locked. Keyboard only for now; a
        // touch reset button on the HUD is still open for mobile.
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.LogWarning("[CarController] manual reset (R) to nearest road");
            ResetCar(lastGoodPosition, lastGoodRotation);
        }
    }

    /// <summary>Apply a selected car's stats (top speed, acceleration,
    /// handling). Called by CarSelection at spawn.</summary>
    public void ApplyStats(float topSpeedMs, float acceleration, float handling)
    {
        maxSpeed = topSpeedMs;
        accelerationMultiplier = acceleration;
        handlingMultiplier = handling;
    }

    /// <summary>Recolour the body mesh to the selected car's colour.</summary>
    public void SetBodyColor(Color color)
    {
        Transform body = transform.Find("Body");
        if (body == null) return;
        Renderer rend = body.GetComponent<Renderer>();
        if (rend != null) rend.material.color = color;
    }

    void FixedUpdate()
    {
        GuardPhysics();
        WatchForReset();
        ReadInput();
        WatchStuck();
        if (throttleInput > 0.01f || brakeInput > 0.01f || Mathf.Abs(steerInput) > 0.01f)
            rb.WakeUp();
        ApplySteering();
        ApplyMotor();
        ApplyGripToWheels();
        ApplyDownforce();
        ApplyDrag();
        ClampVelocity();
        WatchVelocityJump();
        UpdateWheelVisual(wheelFL, wheelMeshFL);
        UpdateWheelVisual(wheelFR, wheelMeshFR);
        UpdateWheelVisual(wheelRL, wheelMeshRL);
        UpdateWheelVisual(wheelRR, wheelMeshRR);
    }

    /// <summary>Auto-reset (round-3 bug #1): out of the world (|x|/|z|
    /// beyond the 7700 m world, below the lowest terrain, or flung high)
    /// or upside down and slow for 2 s -> back on the road at the last
    /// known-good pose. The race timer keeps running; the driver loses
    /// a few seconds, not the car. Bounds are the wired world, not the
    /// old 1x1 km Shibuya: PLATEAU terrain spans y -11..45 m raw and
    /// sits at ~+-55 m worst case after the per-district anchor shift,
    /// and the mountain ring / touge climb to 220 m — so the Y band is
    /// generous (-60..300) — a false reset is far worse than a slow one.</summary>
    void WatchForReset()
    {
        Vector3 p = transform.position;
        float halfWorld = DistrictData.WorldSize * 0.5f;
        bool outOfWorld = Mathf.Abs(p.x) > halfWorld || Mathf.Abs(p.z) > halfWorld
            || p.y < -60f || p.y > 300f; // 300: mountains / touge reach 220 m
        if (outOfWorld)
        {
            Debug.LogWarning("[CarController] out of world at " + p + ", resetting to last good pose");
            ResetCar(lastGoodPosition, lastGoodRotation);
            upsideDownTimer = 0f;
            return;
        }
        if (transform.up.y < 0.2f && GetSpeedMs() < 3f)
        {
            upsideDownTimer += Time.fixedDeltaTime;
            if (upsideDownTimer > 2f)
            {
                Debug.LogWarning("[CarController] upside down and stopped, resetting to last good pose");
                ResetCar(lastGoodPosition, lastGoodRotation);
                upsideDownTimer = 0f;
            }
        }
        else upsideDownTimer = 0f;
    }

    /// <summary>Wedged-car auto-nudge (Phase A free-roam playtest).
    /// While a nudge is running, the player's inputs are overridden:
    /// full reverse with the wheels turned, so the car backs off the
    /// wall in an arc. Otherwise the stuck timer only accumulates
    /// while the driver is holding throttle but the car is barely
    /// moving and upright; it resets the moment the car moves.</summary>
    void WatchStuck()
    {
        if (nudgeTimer > 0f)
        {
            nudgeTimer -= Time.fixedDeltaTime;
            // Override: reverse (brake input = reverse motor) with the
            // wheels turned. ApplyMotor releases the brake torque while
            // reversing and ApplySteering flips the sign, so the nose
            // swings in the direction of nudgeSteer while backing up.
            throttleInput = 0f;
            brakeInput = 1f;
            reverseInput = true;
            steerInput = nudgeSteer;
            if (nudgeTimer <= 0f)
            {
                stuckTimer = 0f;
                Debug.LogWarning("[CarController] stuck nudge finished");
            }
            return;
        }
        bool tryingToMove = throttleInput > 0.1f;
        bool barelyMoving = GetSpeedMs() < 0.5f;
        bool upright = transform.up.y > 0.5f;
        if (tryingToMove && barelyMoving && upright)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > 2.5f)
            {
                nudgeTimer = 1.5f;
                nudgeSteer = -nudgeSteer; // alternate sides each nudge
                stuckTimer = 0f;
                // Break the static jam: a pure motor reverse often
                // cannot overcome the body-on-wall friction once the
                // nose is hooked on a building corner (verified
                // 2026-11-07: three nudges fired at (14.6,45) and the
                // car never moved). Give the car a backward velocity
                // kick so it separates from the wall; the reverse
                // motor then sustains the back-off. 4 m/s is well
                // under the 12 m/s reverse cap.
                if (rb != null)
                    rb.AddForce(-transform.forward * 4f, ForceMode.VelocityChange);
                Debug.LogWarning("[CarController] stuck against obstacle at "
                    + transform.position + " (throttle held, speed ~0 for 2.5s)"
                    + " - nudging off in reverse, steer " + nudgeSteer.ToString("0.0"));
            }
        }
        else stuckTimer = 0f;
    }

    /// <summary>Diagnostic: logs a one-line warning when the velocity
    /// jumps by more than 12 m/s in a single physics step (a solver
    /// explosion signature), with the position, so the cause can be
    /// found in Player.log after a bad run.</summary>
    void WatchVelocityJump()
    {
        Vector3 v = rb.linearVelocity;
        if (hasPrevVelocity)
        {
            float jump = (v - prevVelocity).magnitude;
            if (jump > 12f)
                Debug.LogWarning("[CarController] velocity jump +" + jump.ToString("0.0")
                    + " m/s at " + transform.position + " speed=" + v.magnitude.ToString("0.0")
                    + " throttle=" + throttleInput.ToString("0.00") + " steer=" + steerInput.ToString("0.00"));
        }
        prevVelocity = v;
        hasPrevVelocity = true;
    }

    /// <summary>Hard-impact response (round-3 bug #1). A collision with
    /// a kinematic traffic car used to inject a huge, mostly vertical
    /// impulse that launched the player into the void. Kill any upward
    /// velocity above 5 m/s on impact and cap the speed, so a T-bone
    /// costs the driver speed, not the car. Also logs the impact so the
    /// cause is visible in Player.log.</summary>
    void OnCollisionEnter(Collision collision)
    {
        float impact = collision.relativeVelocity.magnitude;
        if (impact < 5f) return;
        Debug.LogWarning("[CarController] hard impact " + impact.ToString("0.0")
            + " m/s with " + collision.gameObject.name
            + " at " + transform.position + " speed=" + GetSpeedMs().ToString("0.0"));
        if (!collision.gameObject.CompareTag("Traffic")) return;
        Vector3 v = rb.linearVelocity;
        if (v.y > 5f) v.y = 5f;
        if (v.magnitude > 60f) v = v.normalized * 60f;
        rb.linearVelocity = v;
    }

    /// <summary>NaN / Infinity guard (round-3 bug #1). If the solver ever
    /// produces a non-finite velocity, spin or position, restore the last
    /// known-good pose instead of letting the car fly into the void.
    /// Also records the last good pose while the car is sane.</summary>
    void GuardPhysics()
    {
        if (rb == null) return;
        Vector3 v = rb.linearVelocity;
        Vector3 w = rb.angularVelocity;
        Vector3 p = transform.position;
        bool bad =
            float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
            float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z) ||
            float.IsNaN(w.x) || float.IsNaN(w.y) || float.IsNaN(w.z) ||
            float.IsInfinity(w.x) || float.IsInfinity(w.y) || float.IsInfinity(w.z) ||
            float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
            float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z);
        if (bad)
        {
            Debug.LogWarning("[CarController] non-finite physics state detected, restoring last good pose");
            if (hasLastGood) ResetCar(lastGoodPosition, lastGoodRotation);
            else ResetCar(new Vector3(0f, 0.6f, -40f), Quaternion.identity);
            return;
        }
        // Record the last good pose ONLY while the car is on the ground,
        // inside the world, upright, with all four wheels in contact
        // AND on a road (FIX 2026-11-07, race blocker #4): the old code
        // recorded anywhere on the ground plane, so after driving onto
        // grass next to a building the "last good" pose WAS the grass —
        // R then restored HP but left the car beached, and the snap in
        // ResetCar could not help because it started from a grass pose
        // far from any road centre. On-road-only recording means R
        // always returns the car to a pose it can drive away from.
        // Bounds are the wired world (7700 m, real terrain heights), not
        // the old flat 1x1 km Shibuya (y 0..3, |x|/|z| < 490). IsOnRoad
        // still uses the Shibuya procedural grid — outside Shibuya the
        // car is rarely "on road" by that definition, so lastGood updates
        // less often there and a reset may return the car to Shibuya; a
        // terrain-aware road check is a gameplay-phase follow-up.
        if (v.magnitude < maxVelocityMs && p.y >= -60f && p.y < 300f // 300: touge/mountains reach 220 m
            && Mathf.Abs(p.x) < DistrictData.WorldSize * 0.5f
            && Mathf.Abs(p.z) < DistrictData.WorldSize * 0.5f
            && transform.up.y > 0.7f && AllWheelsGrounded() && IsOnRoad(p))
        {
            lastGoodPosition = p;
            lastGoodRotation = transform.rotation;
            hasLastGood = true;
        }
    }

    /// <summary>True when the car is on asphalt: within half a road
    /// width (7 m) of a road centreline. Roads are 14 m wide on the
    /// grid x/z in {-450,-300,-150,0,150,300,450}, running the full
    /// 1000 m (NS roads span z +-500, EW roads span x +-500).</summary>
    bool IsOnRoad(Vector3 p)
    {
        foreach (float g in RoadGrid)
        {
            if (Mathf.Abs(p.x - g) <= 7f) return true;
            if (Mathf.Abs(p.z - g) <= 7f) return true;
        }
        return false;
    }

    /// <summary>True when every (non-null) wheel is touching the ground.
    /// Used to record lastGood only in a pose the car can drive away from.</summary>
    bool AllWheelsGrounded()
    {
        if (wheelFL != null && !wheelFL.isGrounded) return false;
        if (wheelFR != null && !wheelFR.isGrounded) return false;
        if (wheelRL != null && !wheelRL.isGrounded) return false;
        if (wheelRR != null && !wheelRR.isGrounded) return false;
        return true;
    }

    /// <summary>Hard velocity caps (round-3 bug #1). Applied after the
    /// forces so a bad solver impulse is cut down before the next
    /// physics step can integrate it into orbit.</summary>
    void ClampVelocity()
    {
        if (rb == null) return;
        Vector3 v = rb.linearVelocity;
        if (v.sqrMagnitude > maxVelocityMs * maxVelocityMs)
            rb.linearVelocity = v.normalized * maxVelocityMs;
        Vector3 w = rb.angularVelocity;
        if (w.sqrMagnitude > maxAngularVelocity * maxAngularVelocity)
            rb.angularVelocity = w.normalized * maxAngularVelocity;
    }

    void ReadInput()
    {
        float steer = Input.GetAxis("Horizontal");
        float throttle = 0f;
        float brake = 0f;

        if (MobileControls.Instance != null)
        {
            steer = Mathf.Clamp(steer + MobileControls.Instance.Steer, -1f, 1f);
            throttle = Mathf.Max(throttle, MobileControls.Instance.Throttle);
            brake = Mathf.Max(brake, MobileControls.Instance.Brake);
        }

        float keyThrottle = Input.GetAxis("Vertical");
        bool keyBrake = Input.GetKey(KeyCode.Space);
        if (keyThrottle > 0f) throttle = Mathf.Max(throttle, keyThrottle);
        else if (keyThrottle < 0f) { brake = Mathf.Max(brake, -keyThrottle); keyBrake = true; }
        if (Input.GetKey(KeyCode.Space)) brake = 1f;

        // Reverse intent: S/Down or the touch BRAKE button. Space is a
        // pure brake and never reverses. The touch BRAKE doubles as
        // reverse at a standstill, like S.
        reverseInput = keyThrottle < 0f ||
            (MobileControls.Instance != null && MobileControls.Instance.Brake > 0.01f);

        steerInput = steer;
        throttleInput = throttle;
        brakeInput = brake;

        // Stale touch-brake watchdog (round-3 bug #2): GAS held at a
        // standstill while the (touch) brake is on and no keyboard brake
        // is held. After 3 s the brake is treated as a lost pointer-up
        // and ignored, so the car can always drive away from spawn.
        if (brakeInput > 0.5f && throttleInput > 0.5f && GetSpeedMs() < 1f && !keyBrake)
        {
            staleBrakeTimer += Time.fixedDeltaTime;
            if (staleBrakeTimer > 3f)
            {
                brakeInput = 0f;
                if (!staleBrakeWarned)
                {
                    staleBrakeWarned = true;
                    Debug.LogWarning("[CarController] stale touch-brake ignored (held >3s while throttling at standstill)");
                }
            }
        }
        else staleBrakeTimer = 0f;
    }

    void ApplySteering()
    {
        float targetSteer = steerInput * maxSteerAngle * handlingMultiplier;
        // FIX (2026-11-07, race blocker #3): reverse steering was
        // inverted — S+D swung the nose left, S+A right, opposite to
        // forward. While reversing (moving backwards, or holding the
        // reverse input at a standstill about to reverse), flip the
        // steer sign so D always swings the nose right and A left,
        // exactly as in forward driving.
        float forwardSpeed = rb != null
            ? Vector3.Dot(rb.linearVelocity, transform.forward) : 0f;
        bool reversing = forwardSpeed < -0.5f
            || (reverseInput && brakeInput > 0.01f && forwardSpeed < 0.5f);
        if (reversing) targetSteer = -targetSteer;
        // Less steering at high speed so the car stays stable.
        // 0.65 (was 0.55): at V-max the lock is 28*0.35 = 9.8 deg
        // (was 12.6 deg, x1.36 handling = 17.1 deg on the fastest
        // cars — enough to spin an arcade car at 200+ km/h).
        // Low-speed steering is untouched (factor is 1.0 at rest).
        float speedFactor = 1f - Mathf.Clamp01(GetSpeedMs() / maxSpeed) * 0.65f;
        targetSteer *= speedFactor;
        currentSteer = Mathf.MoveTowards(currentSteer, targetSteer,
            steerSpeed * maxSteerAngle * Time.fixedDeltaTime);
        wheelFL.steerAngle = currentSteer;
        wheelFR.steerAngle = currentSteer;
    }

    /// <summary>WheelCollider setup, owned in code (physics audit
    /// 2026-11-10). The prefab AND the Main scene each carry their own
    /// full WheelCollider copies (the scene car is not a prefab
    /// instance), so tuning YAML in one place silently diverges from
    /// the other — Awake re-asserts the tuned values every run, and
    /// the numbers below are the single source of truth.
    /// Suspension (per corner, sprung mass ~330 kg): front spring
    /// 35000 / damper 4500 unchanged (natural freq ~1.6 Hz, damping
    /// ratio ~0.66). REAR spring 35000 -> 38000, damper 4500 -> 4800
    /// (ratio ~0.68, matched): the old identical front/rear setup let
    /// the rear squat under acceleration and dive the nose under
    /// braking; the stiffer rear resists squat without hardening the
    /// ride. Travel 0.25 m, target 0.5, wheel mass 20, radius 0.35 m
    /// unchanged. Friction curves unchanged (forward 0.4/1.0/0.8/0.5,
    /// sideways 0.2/1.0/0.5/0.75); only their stiffness is ever scaled,
    /// by weather/season grip in ApplyGripToWheels().</summary>
    void ConfigureWheels()
    {
        SetupWheel(wheelFL, 35000f, 4500f);
        SetupWheel(wheelFR, 35000f, 4500f);
        SetupWheel(wheelRL, 38000f, 4800f);
        SetupWheel(wheelRR, 38000f, 4800f);
    }

    void SetupWheel(WheelCollider w, float spring, float damper)
    {
        if (w == null) return;
        w.radius = 0.35f;
        w.mass = 20f;
        w.suspensionDistance = 0.25f;
        w.wheelDampingRate = 0.25f;
        JointSpring s = w.suspensionSpring;
        s.spring = spring;
        s.damper = damper;
        s.targetPosition = 0.5f;
        w.suspensionSpring = s;
        WheelFrictionCurve fwd = w.forwardFriction;
        fwd.extremumSlip = 0.4f;
        fwd.extremumValue = 1f;
        fwd.asymptoteSlip = 0.8f;
        fwd.asymptoteValue = 0.5f;
        fwd.stiffness = 1f;
        w.forwardFriction = fwd;
        WheelFrictionCurve side = w.sidewaysFriction;
        side.extremumSlip = 0.2f;
        side.extremumValue = 1f;
        side.asymptoteSlip = 0.5f;
        side.asymptoteValue = 0.75f;
        side.stiffness = 1f;
        w.sidewaysFriction = side;
        baseForwardStiffness = 1f;
        baseSidewaysStiffness = 1f;
    }

    /// <summary>Weather/season grip reaches the TYRES (physics audit
    /// 2026-11-10 — this was missing). Before this, weatherGripMultiplier
    /// (rain 0.75, winter 0.85, floor 0.60) only weakened the artificial
    /// lateral helper force in ApplyMotor; the WheelCollider friction
    /// curves were never touched at runtime, so braking distances and
    /// wheelspin were identical wet and dry. Now the friction stiffness
    /// of all four tyres scales with the effective grip, so rain cuts
    /// peak grip ~25% (forward extremum 1.0 -> 0.75, sideways asymptote
    /// 0.75 -> 0.56) and braking distance grows ~33% in rain, while
    /// the curve SHAPE is preserved — grip loss is progressive, not a
    /// cliff. Only weather/season is applied here: gripFactor (Tires /
    /// Suspension upgrades) already scales the helper force, and
    /// applying it here too would double-count it. Reads last frame's
    /// LateUpdate value (SeasonManager writes the weather x season
    /// product after WeatherSystem's Update); the 1-frame lag is
    /// irrelevant for a 3 s crossfade. Skipped when unchanged.</summary>
    void ApplyGripToWheels()
    {
        float g = Mathf.Clamp(weatherGripMultiplier, 0.6f, 1f);
        if (Mathf.Abs(g - appliedGripStiffness) < 0.001f) return;
        appliedGripStiffness = g;
        SetWheelGrip(wheelFL, g);
        SetWheelGrip(wheelFR, g);
        SetWheelGrip(wheelRL, g);
        SetWheelGrip(wheelRR, g);
    }

    void SetWheelGrip(WheelCollider w, float g)
    {
        if (w == null) return;
        WheelFrictionCurve fwd = w.forwardFriction;
        fwd.stiffness = baseForwardStiffness * g;
        w.forwardFriction = fwd;
        WheelFrictionCurve side = w.sidewaysFriction;
        side.stiffness = baseSidewaysStiffness * g;
        w.sidewaysFriction = side;
    }

    void ApplyMotor()
    {
        float speed = GetSpeedMs();
        // Signed speed along the car's nose: needed before the motor
        // branches so braking and reversing are told apart.
        float forwardSpeedNow = rb != null
            ? Vector3.Dot(rb.linearVelocity, transform.forward) : 0f;
        float motor = 0f;
        if (throttleInput > 0.01f)
        {
            // Progressive top-speed taper (was a hard cut: full torque
            // below maxSpeed, zero at it, so the car surged on/off at
            // V-max). Full torque to 90% of top speed, then linear to
            // zero at top speed. Low-speed torque is untouched, so
            // curb-climbing / unstick behaviour does not change.
            float taper = Mathf.Clamp01((maxSpeed - speed) / (maxSpeed * 0.1f));
            motor = maxMotorTorque * accelerationMultiplier * throttleInput * taper;
        }
        // FIX (2026-11-07, race blocker #3b): the old braking branch
        // fired on ANY motion with S held (speed > 0.5 m/s), so the
        // moment the car crept backwards the reverse motor was cut to
        // zero — reverse top speed was ~0.5 m/s (2 km/h) and the
        // steering fix could never be observed. Braking now only cuts
        // the motor while moving FORWARD; reversing keeps the reverse
        // motor (capped at 12 m/s backwards, ~43 km/h).
        else if (brakeInput > 0.01f && forwardSpeedNow > 0.5f)
        {
            motor = 0f; // braking, handled below
        }
        else if (brakeInput > 0.01f)
        {
            motor = forwardSpeedNow > -12f ? -reverseTorque * brakeInput : 0f;
        }
        wheelRL.motorTorque = motor;
        wheelRR.motorTorque = motor;

        // Brake torque. BUG (found 2026-11-07, run 6): the brake was
        // applied on all four wheels even while reversing, so the 4500 Nm
        // brake locked the wheels against the 1200 Nm reverse motor and
        // the car could NEVER reverse — S did nothing from a standstill,
        // which wedged it against any wall. Now the brake is released
        // while the reverse input is held and the car is not moving
        // forward (stopped or already reversing), so the reverse motor
        // can turn the wheels. Space (reverseInput false) still brakes
        // and holds the car in every state.
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float brakeTorque = brakeInput * maxBrakeTorque * brakeMultiplier;
        if (reverseInput && throttleInput <= 0.01f && forwardSpeed < 0.5f)
            brakeTorque = 0f;
        wheelFL.brakeTorque = brakeTorque;
        wheelFR.brakeTorque = brakeTorque;
        wheelRL.brakeTorque = brakeTorque;
        wheelRR.brakeTorque = brakeTorque;

        // Extra lateral grip so the arcade car does not slide forever.
        // Capped: the old uncapped force reached hundreds of m/s^2 at high
        // lateral speed and could feed a solver blow-up (round-3 bug #1).
        // Raised 6.0 (was 4.0) 2026-11-07: the car slid sideways through
        // 90-degree corners and spun, which wrecked it on buildings.
        var fwd = rb.linearVelocity;
        lateralSpeed = Vector3.Dot(fwd, transform.right);
        if (fwd.sqrMagnitude > 0.01f)
        {
            Vector3 lateral = transform.right * Vector3.Dot(fwd, transform.right);
            Vector3 gripForce = -lateral * gripFactor * weatherGripMultiplier * 6f;
            if (gripForce.sqrMagnitude > 40f * 40f)
                gripForce = gripForce.normalized * 40f;
            rb.AddForce(gripForce, ForceMode.Acceleration);
        }
    }

    void ApplyDownforce()
    {
        float speed = GetSpeedMs();
        // Progressive aero (was flat: the old linear formula
        // downforce*speed*0.02 hit the 30 m/s^2 cap at 12.5 m/s, so the
        // car had full ~3 g of aero load from 45 km/h to V-max and grip
        // felt identical at 60 and 300 km/h). Now the load grows with
        // speed/maxSpeed: 0 at rest, 15 m/s^2 at half top speed, 30 at
        // V-max (120*1*1*0.25 = 30). Same cap, same value at top speed
        // — only the mid-range is now progressive. Cap still 30 m/s^2
        // (~3 g) so the suspension solver is never stressed.
        float accel = Mathf.Min(downforce * downforceMultiplier * (speed / maxSpeed) * 0.25f, 30f);
        rb.AddForce(-transform.up * accel, ForceMode.Acceleration);
    }

    /// <summary>Quadratic aero drag (new — none existed: linearDamping
    /// is 0 and no drag force was applied, so coasting never slowed the
    /// car and downhill it could overspeed past maxSpeed unchecked).
    /// a = 0.35 * v^2 / mass: ~0.19 m/s^2 at 100 km/h, ~0.76 at
    /// 198 km/h, ~2.6 at 365 km/h. Negligible at the 4 m/s stuck-nudge
    /// reverse kick. Uses rb.mass, not a hardcoded 1400.</summary>
    void ApplyDrag()
    {
        float speed = GetSpeedMs();
        if (speed < 0.5f || rb == null) return;
        float dragAccel = dragCoefficient * speed * speed / rb.mass;
        rb.AddForce(-rb.linearVelocity.normalized * dragAccel, ForceMode.Acceleration);
    }

    void UpdateWheelVisual(WheelCollider col, Transform mesh)
    {
        if (col == null || mesh == null) return;
        Vector3 pos;
        Quaternion rot;
        col.GetWorldPose(out pos, out rot);
        mesh.position = pos;
        // The wheel visual is built with its axle along local Y (torus +
        // cylinder children), but a WheelCollider pose has the axle along
        // local X. Without this 90-degree correction the tyre torus lay
        // flat like a horizontal disc and the wheels were invisible
        // from the side. The prefab's own 90-degree Z rotation exists
        // for exactly this reason; GetWorldPose overwrites it, so it
        // must be re-applied here.
        mesh.rotation = rot * Quaternion.Euler(0f, 0f, 90f);
    }

    public float GetSpeedMs()
    {
        if (rb == null) return 0f;
        float s = rb.linearVelocity.magnitude;
        // A non-finite speed must never reach the HUD / sound / camera:
        // Mathf.RoundToInt(NaN) is int.MinValue (-2147483648), which is
        // what the round-3 speedometer showed after the blow-up.
        return float.IsNaN(s) || float.IsInfinity(s) ? 0f : s;
    }

    public float GetSpeedKmh()
    {
        return GetSpeedMs() * 3.6f;
    }

    public void ResetCar(Vector3 position, Quaternion rotation)
    {
        // Snap the reset pose onto the nearest road centre, facing along
        // it. The old code respawned at the raw last-good pose, which
        // could be half on a sidewalk curb or a grass verge at the world
        // edge: the wheels then had no traction and W/S did nothing
        // (soft-lock, seen twice in the 2026-11-07 race playtests after
        // out-of-world resets at the north and west edges). A reset must
        // always land somewhere drivable.
        Quaternion snappedRot;
        Vector3 snappedPos = SnapToRoadCenter(position, rotation, out snappedRot);
        // FIX (2026-11-07, race blocker #4b): teleport through the
        // Rigidbody, not the Transform. Setting transform.position on a
        // dynamic Rigidbody from Update() was silently eaten — the
        // physics body kept its old pose, so R repaired HP (CarDamage)
        // and logged "manual reset" yet the car never moved (verified
        // 11x in Player.log: reset logged, car still wedged against
        // the same building). rb.position moves the body itself;
        // Physics.SyncTransforms() pushes it to the Transform at once.
        Vector3 before = transform.position;
        if (rb != null)
        {
            rb.position = snappedPos;
            rb.rotation = snappedRot;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
            rb.WakeUp();
        }
        else
        {
            transform.position = snappedPos;
            transform.rotation = snappedRot;
        }
        Physics.SyncTransforms();
        Debug.LogWarning("[CarController] reset " + before + " -> " + snappedPos
            + " yaw " + snappedRot.eulerAngles.y.ToString("0"));
        transform.position = snappedPos;
        transform.rotation = snappedRot;
        currentSteer = 0f;
        staleBrakeTimer = 0f;
        upsideDownTimer = 0f;
        hasPrevVelocity = false;
        lastGoodPosition = snappedPos;
        lastGoodRotation = snappedRot;
        hasLastGood = true;
    }

    // ---- Road-centre snap ----
    // City roads run on the grid x/z in {-450,-300,-150,0,150,300,450}
    // (±450 is the ring road). Snapping to the nearest grid line puts
    // the car on asphalt, centred, facing along the road.
    static readonly float[] RoadGrid = { -450f, -300f, -150f, 0f, 150f, 300f, 450f };

    static float NearestGrid(float v)
    {
        float best = RoadGrid[0];
        float bd = Mathf.Abs(v - best);
        foreach (float g in RoadGrid)
        {
            float d = Mathf.Abs(v - g);
            if (d < bd) { bd = d; best = g; }
        }
        return best;
    }

    static float NearestYaw(float yaw, float a, float b)
    {
        float da = Mathf.Abs(Mathf.DeltaAngle(yaw, a));
        float db = Mathf.Abs(Mathf.DeltaAngle(yaw, b));
        return da <= db ? a : b;
    }

    Vector3 SnapToRoadCenter(Vector3 p, Quaternion rot, out Quaternion snappedRot)
    {
        // Nearest point on the road NETWORK, not on infinite grid lines.
        // The span is the wired world half-size (the old +-500 was the
        // 1x1 km Shibuya; the grid lines are now the fallback road
        // network across the whole city, and the ground check at the end
        // keeps the pose on real terrain). (The old +-300 clamp for inner
        // roads was wrong and could snap a car on a road at e.g. z=400 to
        // a point 100 m away instead of its own road.)
        float yaw = rot.eulerAngles.y;

        // North-south option: nearest grid x; the road at that x spans
        // the wired world (see above).
        float gx = NearestGrid(p.x);
        float zSpan = DistrictData.WorldSize * 0.5f;
        float cz = Mathf.Clamp(p.z, -zSpan, zSpan);
        float dNS = Mathf.Sqrt((p.x - gx) * (p.x - gx) + (p.z - cz) * (p.z - cz));
        Vector3 pNS = new Vector3(gx, 0.6f, cz);
        float yawNS = NearestYaw(yaw, 0f, 180f);

        // East-west option: nearest grid z; same span rule.
        float gz = NearestGrid(p.z);
        float xSpan = DistrictData.WorldSize * 0.5f;
        float cx = Mathf.Clamp(p.x, -xSpan, xSpan);
        float dEW = Mathf.Sqrt((p.x - cx) * (p.x - cx) + (p.z - gz) * (p.z - gz));
        Vector3 pEW = new Vector3(cx, 0.6f, gz);
        float yawEW = NearestYaw(yaw, 90f, 270f);

        // Pick the nearer road — BUT if the nearer point is within 10 m
        // of the car, the car is already essentially on that road and
        // still stuck (it is wedged against something ON that road, e.g.
        // the Tower sitting on the ring at (388,446): the snap put the
        // car 4 m away inside the Tower and the solver pushed it back).
        // Resetting to the same road cannot help, so take the other road
        // (the crossroad) instead, which is a real escape. If both are
        // within 10 m (an intersection), shift the point 15 m along the
        // road away from the direction the car faces (away from the
        // obstacle it is looking at).
        Vector3 snapped;
        float snappedYaw;
        if (dNS <= dEW)
        {
            if (dNS < 10f && dEW >= 10f) { snapped = pEW; snappedYaw = yawEW; }
            else if (dNS < 10f) { snapped = pNS - RoadDir(yawNS) * 15f; snappedYaw = yawNS; }
            else { snapped = pNS; snappedYaw = yawNS; }
        }
        else
        {
            if (dEW < 10f && dNS >= 10f) { snapped = pNS; snappedYaw = yawNS; }
            else if (dEW < 10f) { snapped = pEW - RoadDir(yawEW) * 15f; snappedYaw = yawEW; }
            else { snapped = pEW; snappedYaw = yawEW; }
        }
        // Ground the reset pose on the real terrain (PLATEAU): the old
        // code always returned y = 0.6, but the wired world is real Tokyo
        // terrain (up to ~+-55 m after the per-district anchor shift), so
        // a reset to y = 0.6 could land inside the terrain and loop
        // forever (fall -> reset -> same buried pose). If no ground exists
        // under the snapped point (outside the wired districts) keep the
        // last-good pose instead of teleporting into the void. With no
        // PLATEAU world at all, the flat procedural ground plane applies.
        if (PlateauMapLoader.Instance == null)
        {
            snapped.y = 0.6f;
            snappedRot = Quaternion.Euler(0f, snappedYaw, 0f);
        }
        else if (PlateauMapLoader.TryGetGroundHeight(snapped.x, snapped.z, out float gy))
        {
            snapped.y = gy + 0.6f;
            snappedRot = Quaternion.Euler(0f, snappedYaw, 0f);
        }
        else
        {
            snapped = p;
            snappedRot = rot;
        }
        return snapped;
    }

    // Unit direction for a cardinal yaw (0 north, 90 east, 180 south, 270 west).
    static Vector3 RoadDir(float yawDeg)
    {
        float r = yawDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
    }
}
