using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMovementController))]
[RequireComponent(typeof(PlayerPhysicsStateController))]
[RequireComponent(typeof(Collider2D))]
public class PlayerSparkBoost : MonoBehaviour
{
    [Header("Input (Spark Boost)")]
    public KeyCode boostKey = KeyCode.X;
    public KeyCode boostPadKey = KeyCode.JoystickButton2;

    [Header("Aim (8-dir from Horizontal+Vertical)")]
    [Range(0f, 0.5f)] public float aimDeadzone = 0.25f;
    public bool keepLastAimWhenNoInput = true;

    [Header("Spark - Ventana")]
    public float defaultWindowDuration = 1.2f;
    [Range(0f, 1f)] public float goodStart = 0.60f;
    [Range(0f, 1f)] public float perfectStart = 0.85f;

    [Header("Anchor (CINEMÁTICO)")]
    public bool anchorPlayerDuringSpark = true;

    [Header("Anti auto-dash")]
    public bool ignoreInputOnPickupFrame = true;
    [Range(0f, 0.2f)] public float pickupInputBlockTime = 0.06f;

    [Header("Dash (Spark) - CINEMÁTICO")]
    public float dashSpeed = 22f;
    [Range(0.02f, 0.60f)] public float dashDuration = 0.16f;
    public LayerMask dashCollisionMask = ~0;
    [Range(0f, 0.06f)] public float dashSkin = 0.015f;

    [Header("Dash Tuning")]
    [Range(0.5f, 1f)] public float dashDistanceScale = 1f;

    [Header("Dash Rebotes (CINEMÁTICO)")]
    public bool dashBouncesOnWalls = true;
    [Range(0, 12)] public int dashMaxBounces = 6;

    [Header("Dash - Pickups (priority)")]
    [Range(0f, 0.25f)] public float pickupSweepExtra = 0.08f;
    public bool snapToPickupAnchorOnDash = true;

    [Header("Timing")]
    public float goodMultiplier = 1.15f;
    public float perfectMultiplier = 1.35f;
    public bool continuousTiming = false;

    [Header("Consume")]
    public bool consumeSparkOnUse = true;

    // -------------------------
    // Preview
    // -------------------------
    [Header("Preview Trajectory (AUTO)")]
    public LineRenderer sparkPreviewLine;
    public float previewWidth = 0.08f;
    public int previewSegments = 32;
    public string previewSortingLayer = "Default";
    public int previewSortingOrder = 9999;

    [Header("Preview Safety Margin")]
    [Range(0.10f, 1.00f)] public float previewSafetyMultiplier = 0.99f;

    [Header("Preview Colors (strength)")]
    public Color previewColorMiss    = new Color(1f, 0.8f, 0.2f, 0.95f);
    public Color previewColorGood    = new Color(0.4f, 0.9f, 1f, 0.95f);
    public Color previewColorPerfect = new Color(1f, 0.2f, 0.2f, 0.95f);

    [Header("Preview Mode")]
    public bool previewOnlyDirection = true;
    [Range(0.15f, 3f)] public float directionLineLength = 1.25f;
    public bool directionLineUseAimDeadzone = true;


    // -------------------------
    // Internals
    // -------------------------
    private Rigidbody2D rb;
    private PlayerMovementController move;
    private PlayerPhysicsStateController phys;
    private Collider2D playerCol;

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[64];

    // State
    private bool sparkActive;
    private float sparkTimer;
    private float sparkTimerTotal;

    private bool dashActive;
    private float dashTimer;
    private Vector2 dashDir;
    private float dashSpeedFinal;
    private float dashRemainingDist;
    private int dashBouncesUsed;

    // Anchor
    private Vector2 sparkAnchorPos;
    private bool anchorValid;

    // Locks / restore
    private bool cachedMoveLockValid;
    private bool cachedMoveLockValue;

    private float baseGravity;
    private RigidbodyConstraints2D baseConstraints;
    private RigidbodyInterpolation2D baseInterp;
    private RigidbodyType2D baseBodyType;

    // Input / aim
    private Vector2 lastAim = Vector2.up;
    private int pickupFrame = -999;
    private float pickupInputBlockedUntil = -1f;
    private bool dashUsedThisSpark;

    // Optional: disable BounceAttack cleanly (no absorción)
    private PlayerBounceAttack bounceAtk;
    private bool bounceAtkHadComponent;
    private bool bounceAtkWasEnabled;
    private bool bounceAtkStateCached;

    public bool IsSparkActive() => sparkActive;
    public bool IsDashing() => dashActive;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        move = GetComponent<PlayerMovementController>();
        phys = GetComponent<PlayerPhysicsStateController>();
        playerCol = GetComponent<Collider2D>();

        baseGravity = rb.gravityScale;
        baseConstraints = rb.constraints;
        baseInterp = rb.interpolation;
        baseBodyType = rb.bodyType;

        bounceAtk = GetComponent<PlayerBounceAttack>();
        bounceAtkHadComponent = (bounceAtk != null);

        ConfigurePreviewAuto();
        SetPreviewVisible(false);
    }

    private void OnDisable() => ForceEndAll();
    private void OnDestroy() => ForceEndAll();

    private void Update()
    {
        if (!sparkActive) return;

        bool boostDown = Input.GetKeyDown(boostKey) || Input.GetKeyDown(boostPadKey);

        // Bloqueo anti “auto dash” en el frame del pickup
        if (!dashActive && ignoreInputOnPickupFrame && Time.frameCount == pickupFrame)
            return;

        // Timer de ventana
        sparkTimer -= Time.deltaTime;
        if (sparkTimer <= 0f)
        {
            EndSparkWindow(restoreMovement: true);
            return;
        }

        UpdatePreview();

        if (dashActive) return;

        if (boostDown)
        {
            if (Time.time < pickupInputBlockedUntil) return;
            TriggerDash();
        }
    }

    // IMPORTANTÍSIMO: aquí se mata la vibración.
    // Clavamos la posición DESPUÉS de que otros scripts intenten mover.
    private void LateUpdate()
    {
        if (sparkActive && !dashActive && anchorPlayerDuringSpark && anchorValid)
        {
            // Bloqueo duro para que el controller no meta ruido
            move.movementLocked = true;

            rb.position = sparkAnchorPos;
            transform.position = sparkAnchorPos;
        }
    }

    private void FixedUpdate()
    {
        if (dashActive)
        {
            phys.RequestDash();
            DashStep(Time.fixedDeltaTime);
            return;
        }

        // NO anclamos aquí (FixedUpdate) -> evita guerra de posiciones y jitter
    }

    // =========================
    // Public spark API
    // =========================
    public void ActivateSpark(float duration) => ActivateSpark(duration, rb.position);

    public void ActivateSpark(float duration, Vector2 anchorWorldPos)
    {
        // Si venías de dash, ciérralo
        if (dashActive) EndDashInternal();

        // 1) Primero cancela el bounce (esto pone movementLocked = false dentro del bounce)
        if (bounceAtkHadComponent && bounceAtk != null)
        {
            bounceAtk.ForceCancelFromSpark();
        }

        // 2) AHORA cacheas el lock ya limpio (false)
        CacheMovementLock();

        // 3) Y ya lo deshabilitas
        DisableBounceAttackClean();



        sparkActive = true;
        dashUsedThisSpark = false;

        sparkTimerTotal = Mathf.Max(0.05f, (duration > 0f) ? duration : defaultWindowDuration);
        sparkTimer = sparkTimerTotal;

        sparkAnchorPos = anchorWorldPos;
        anchorValid = true;

        pickupFrame = Time.frameCount;
        pickupInputBlockedUntil = Time.time + Mathf.Max(0f, pickupInputBlockTime);

        // Estado RB CINEMÁTICO (determinista) durante ventana
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll; // <- corta micro-oscilación
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        rb.position = sparkAnchorPos;
        transform.position = sparkAnchorPos;

        move.movementLocked = true;

        SetPreviewVisible(true);
    }

    // =========================
    // Dash
    // =========================
    private void TriggerDash()
    {
        if (dashUsedThisSpark) return;
        dashUsedThisSpark = true;

        float progress = 1f - (sparkTimer / sparkTimerTotal);
        float mult = ComputeMultiplier(progress);

        // Dirección se lee UNA VEZ y queda fijada para todo el dash
        dashDir = GetAimDirection8();
        if (dashDir.sqrMagnitude < 0.0001f) dashDir = Vector2.up;
        dashDir.Normalize();

        dashSpeedFinal = dashSpeed * mult;
        dashRemainingDist = dashSpeedFinal * dashDuration * Mathf.Clamp(dashDistanceScale, 0.5f, 1f);

        dashBouncesUsed = 0;

        // Cerrar ventana spark (sin restores de movimiento aún)
        EndSparkWindow(restoreMovement: false);

        dashActive = true;
        dashTimer = Mathf.Max(0.01f, dashDuration);

        // RB kinematic sin freeze para poder moverlo por posición
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.None;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        move.movementLocked = true;
    }

    private void DashStep(float dt)
    {
        Vector2 startPos = rb.position;
        Vector2 pos = startPos;
        Vector2 dir = dashDir;

        float step = Mathf.Min(dashRemainingDist, dashSpeedFinal * dt);
        float remaining = step;

        int safety = 0;
        const int SAFETY_MAX = 32;

        while (remaining > 0f && safety++ < SAFETY_MAX)
        {
            // 1) prioridad pickups
            if (TryFindPickupAlong(pos, dir, remaining + dashSkin + pickupSweepExtra, out FlameSparkPickup pickup, out float pickupDist))
            {
                if (pickup != null && pickupDist <= remaining + dashSkin)
                {
                    Vector2 anchor = pickup.GetAnchorWorld();

                    EndDashInternal();

                    Vector2 targetPos = snapToPickupAnchorOnDash
                        ? anchor
                        : (pos + dir * Mathf.Max(0f, pickupDist - dashSkin));

                    rb.position = targetPos;
                    transform.position = targetPos;

                    pickup.Consume();
                    ActivateSpark(pickup.windowDuration, anchor);
                    return;
                }
            }

            // 2) paredes
            if (!CastWallsFrom(pos, dir, remaining + dashSkin, out RaycastHit2D hit))
            {
                pos += dir * remaining;
                remaining = 0f;
                break;
            }

            float travel = Mathf.Max(0f, hit.distance - dashSkin);

            if (travel <= 0.0001f)
            {
                if (!dashBouncesOnWalls || dashBouncesUsed >= dashMaxBounces)
                {
                    remaining = 0f;
                    break;
                }

                dir = Vector2.Reflect(dir, hit.normal).normalized;
                dashBouncesUsed++;

                float microPush = Mathf.Max(0.0025f, dashSkin);
                float pushed = Mathf.Min(microPush, remaining);
                pos += dir * pushed;
                remaining -= pushed;
                continue;
            }

            float movedToHit = Mathf.Min(travel, remaining);
            pos += dir * movedToHit;
            remaining -= movedToHit;

            if (remaining <= 0f) break;

            if (!dashBouncesOnWalls || dashBouncesUsed >= dashMaxBounces)
            {
                remaining = 0f;
                break;
            }

            dir = Vector2.Reflect(dir, hit.normal).normalized;
            dashBouncesUsed++;

            float nudge = Mathf.Max(0.0025f, dashSkin);
            float nudged = Mathf.Min(nudge, remaining);
            pos += dir * nudged;
            remaining -= nudged;
        }

        rb.position = pos;
        transform.position = pos;
        dashDir = dir;

        float movedReal = Vector2.Distance(startPos, pos);
        dashRemainingDist -= movedReal;

        dashTimer -= dt;

        if (movedReal <= 0.00001f || dashRemainingDist <= 0.0001f || dashTimer <= 0f)
            EndDash();
    }

    private void EndDash()
    {
        EndDashInternal();

        RestoreMovementLock();
        RestoreBounceAttackIfNeeded();
        RestoreRigidbodyBaseline();
    }

    private void EndDashInternal()
    {
        dashActive = false;
        dashTimer = 0f;
        dashRemainingDist = 0f;
    }

    // =========================
    // Spark window end
    // =========================
    private void EndSparkWindow(bool restoreMovement)
    {
        sparkActive = false;
        sparkTimer = 0f;
        sparkTimerTotal = 0f;
        anchorValid = false;

        SetPreviewVisible(false);

        if (restoreMovement)
        {
            RestoreMovementLock();
            RestoreBounceAttackIfNeeded();
            RestoreRigidbodyBaseline();
        }
        // si no restauramos, dejamos el estado para el dash
    }

    private void ForceEndAll()
    {
        sparkActive = false;
        dashActive = false;

        sparkTimer = 0f;
        sparkTimerTotal = 0f;
        dashTimer = 0f;
        dashRemainingDist = 0f;

        anchorValid = false;

        SetPreviewVisible(false);

        cachedMoveLockValid = false;
        move.movementLocked = false;

        RestoreBounceAttackIfNeeded();
        RestoreRigidbodyBaseline();
    }

    // =========================
    // Aim
    // =========================
    private Vector2 GetAimDirection8()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(x) < aimDeadzone) x = 0f;
        if (Mathf.Abs(y) < aimDeadzone) y = 0f;

        Vector2 v = new Vector2(x, y);
        if (v.sqrMagnitude < 0.0001f)
            return keepLastAimWhenNoInput ? lastAim : Vector2.up;

        if (x != 0f && y != 0f)
            v = new Vector2(Mathf.Sign(x), Mathf.Sign(y)).normalized;
        else
            v = (x != 0f) ? new Vector2(Mathf.Sign(x), 0f) : new Vector2(0f, Mathf.Sign(y));

        lastAim = v;
        return v;
    }

    // =========================
    // Timing helpers
    // =========================
    private float ComputeMultiplier(float progress)
    {
        if (!continuousTiming)
        {
            if (progress >= perfectStart) return perfectMultiplier;
            if (progress >= goodStart) return goodMultiplier;
            return 1f;
        }

        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        return Mathf.Lerp(1f, perfectMultiplier, t);
    }

    private Color ComputePhaseColor(float progress)
    {
        if (progress >= perfectStart) return previewColorPerfect;
        if (progress >= goodStart) return previewColorGood;
        return previewColorMiss;
    }

    // =========================
    // Pickups cast (trigger)
    // =========================
    private bool PickupUsable(FlameSparkPickup p)
    {
        if (p == null) return false;

        var pCol = p.GetComponent<Collider2D>();
        if (pCol != null && !pCol.enabled) return false;

        var pSr = p.GetComponent<SpriteRenderer>();
        if (pSr != null && !pSr.enabled) return false;

        return true;
    }

    private bool TryFindPickupAlong(Vector2 originPos, Vector2 dir, float dist, out FlameSparkPickup bestPickup, out float bestDist)
    {
        bestPickup = null;
        bestDist = float.MaxValue;

        var cfPick = new ContactFilter2D();
        cfPick.useLayerMask = false;
        cfPick.useTriggers = true;

        int count = 0;

        // Cast estable por bounds
        Bounds b = playerCol.bounds;
        Vector2 center = originPos + (Vector2)(b.center - (Vector3)rb.position);
        Vector2 size = b.size;

        count = Physics2D.BoxCast(center, size, 0f, dir, cfPick, castHits, dist);

        if (count <= 0) return false;

        for (int i = 0; i < count; i++)
        {
            var h = castHits[i];
            if (h.collider == null) continue;

            var p = h.collider.GetComponent<FlameSparkPickup>() ?? h.collider.GetComponentInParent<FlameSparkPickup>();
            if (p == null) continue;
            if (!PickupUsable(p)) continue;

            float d = Mathf.Max(0f, h.distance);
            if (d < 0.0015f) continue;

            if (d < bestDist)
            {
                bestDist = d;
                bestPickup = p;
            }
        }

        return bestPickup != null;
    }

    // =========================
    // Wall cast (no triggers)
    // =========================
    private bool CastWallsFrom(Vector2 originPos, Vector2 dir, float dist, out RaycastHit2D bestHit)
    {
        bestHit = default;

        var cfWall = new ContactFilter2D();
        cfWall.useLayerMask = true;
        cfWall.layerMask = dashCollisionMask;
        cfWall.useTriggers = false;

        int count = 0;

        Bounds b = playerCol.bounds;
        Vector2 center = originPos + (Vector2)(b.center - (Vector3)rb.position);
        Vector2 size = b.size;

        count = Physics2D.BoxCast(center, size, 0f, dir, cfWall, castHits, dist);

        if (count <= 0) return false;

        float best = float.MaxValue;
        bool has = false;

        for (int i = 0; i < count; i++)
        {
            var h = castHits[i];
            if (h.collider == null) continue;
            if (h.collider.isTrigger) continue;
            if (h.collider == playerCol) continue;

            if (h.distance < best)
            {
                best = h.distance;
                bestHit = h;
                has = true;
            }
        }

        return has;
    }

    // =========================
    // Movement lock
    // =========================
    private void CacheMovementLock()
    {
        if (!cachedMoveLockValid)
        {
            cachedMoveLockValue = move.movementLocked;
            cachedMoveLockValid = true;
        }
    }

    private void RestoreMovementLock()
    {
        if (cachedMoveLockValid)
        {
            move.movementLocked = cachedMoveLockValue;
            cachedMoveLockValid = false;
        }
        else
        {
            move.movementLocked = false;
        }
    }

    // =========================
    // Rigidbody baseline restore
    // =========================
    private void RestoreRigidbodyBaseline()
    {
        rb.bodyType = baseBodyType;
        rb.gravityScale = baseGravity;
        rb.interpolation = baseInterp;
        rb.constraints = baseConstraints;
    }

    // =========================
    // BounceAttack disable/restore (simple)
    // =========================
    private void DisableBounceAttackClean()
    {
        if (!bounceAtkHadComponent || bounceAtk == null) return;

        if (!bounceAtkStateCached)
        {
            bounceAtkWasEnabled = bounceAtk.enabled;
            bounceAtkStateCached = true;
        }

        // CORTE REAL
        bounceAtk.ForceCancelFromSpark();

        // y apagado
        bounceAtk.enabled = false;
    }


    private void RestoreBounceAttackIfNeeded()
    {
        if (!bounceAtkHadComponent || bounceAtk == null) return;
        if (!bounceAtkStateCached) return;

        bounceAtk.enabled = bounceAtkWasEnabled;
        bounceAtkStateCached = false;
    }

    // =========================
    // Preview (con COLORES)
    // =========================
    private void ConfigurePreviewAuto()
    {
        if (sparkPreviewLine == null)
        {
            var go = new GameObject("SparkPreviewLine");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            sparkPreviewLine = go.AddComponent<LineRenderer>();
        }

        sparkPreviewLine.useWorldSpace = true;
        sparkPreviewLine.startWidth = previewWidth;
        sparkPreviewLine.endWidth = previewWidth;

        Shader sh =
            Shader.Find("Universal RenderPipeline/2D/Unlit") ??
            Shader.Find("Universal Render Pipeline/2D/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default");

        sparkPreviewLine.material = new Material(sh);
        sparkPreviewLine.sortingLayerName = previewSortingLayer;
        sparkPreviewLine.sortingOrder = previewSortingOrder;

        sparkPreviewLine.numCapVertices = 4;
        sparkPreviewLine.numCornerVertices = 2;
        sparkPreviewLine.positionCount = 0;
        sparkPreviewLine.enabled = false;
    }

    private void SetPreviewVisible(bool on)
    {
        if (sparkPreviewLine == null) return;
        sparkPreviewLine.enabled = on;
        if (!on) sparkPreviewLine.positionCount = 0;
    }

    private void UpdatePreview()
    {
        if (sparkPreviewLine == null || !sparkActive || sparkTimerTotal <= 0f) return;

        float progress = 1f - (sparkTimer / sparkTimerTotal);
        float mult = ComputeMultiplier(progress);
        Color c = ComputePhaseColor(progress);

        Vector2 dir0 = GetAimDirection8();
        if (dir0.sqrMagnitude < 0.0001f) dir0 = Vector2.up;
        dir0.Normalize();

        // Color (como antes)
        sparkPreviewLine.startColor = c;
        sparkPreviewLine.endColor = c;
        if (sparkPreviewLine.material != null) sparkPreviewLine.material.color = c;

        // ====== MODO SOLO DIRECCIÓN ======
        if (previewOnlyDirection)
        {
            float len = Mathf.Max(0.05f, directionLineLength);

            Vector3 p0 = rb.position;
            Vector3 p1 = (Vector2)p0 + dir0 * len;

            sparkPreviewLine.positionCount = 2;
            sparkPreviewLine.SetPosition(0, p0);
            sparkPreviewLine.SetPosition(1, p1);
            return;
        }

        // ====== MODO ANTIGUO (trayectoria) ======
        float scale = Mathf.Clamp(dashDistanceScale, 0.5f, 1f);
        float totalDist = dashSpeed * mult * Mathf.Max(0.01f, dashDuration) * scale;
        totalDist *= Mathf.Clamp(previewSafetyMultiplier, 0.10f, 1.00f);

        Vector2 simPos = rb.position;
        Vector2 simDir = dir0;
        float distLeft = totalDist;

        var pts = new List<Vector3>(previewSegments + 4) { simPos };

        int safety = 0;
        const int SAFETY_MAX = 256;

        float segmentLen = totalDist / Mathf.Max(2, previewSegments);

        while (distLeft > 0.00001f && safety++ < SAFETY_MAX && pts.Count < previewSegments + 1)
        {
            float step = Mathf.Min(distLeft, segmentLen);

            if (!CastWallsFrom(simPos, simDir, step + dashSkin, out RaycastHit2D hit))
            {
                simPos += simDir * step;
                pts.Add(simPos);
                distLeft -= step;
                continue;
            }

            float travel = Mathf.Max(0f, hit.distance - dashSkin);
            simPos += simDir * Mathf.Min(travel, step);
            pts.Add(simPos);
            distLeft -= step;

            if (!dashBouncesOnWalls) break;
            simDir = Vector2.Reflect(simDir, hit.normal).normalized;
        }

        if (pts.Count < 2) pts.Add(pts[0]);

        sparkPreviewLine.positionCount = pts.Count;
        sparkPreviewLine.SetPositions(pts.ToArray());
    }

}
