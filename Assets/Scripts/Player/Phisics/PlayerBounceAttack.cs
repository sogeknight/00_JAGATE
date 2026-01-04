using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMovementController))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerPhysicsStateController))]
public class PlayerBounceAttack : MonoBehaviour
{
    [Header("Ataque")]
    public KeyCode attackKey = KeyCode.X;
    public KeyCode attackPadKey = KeyCode.JoystickButton2;
    public float maxDistance = 10f;

    [Header("Llama / Costes (prototipo)")]
    public bool useFlame = true;
    public float maxFlame = 100f;
    public float flame = 100f;

    [Tooltip("Coste al INICIAR el bounce.")]
    public float flameCostStart = 8f;

    [Tooltip("Coste por cada REBOTE real.")]
    public float flameCostPerBounce = 3f;

    [Tooltip("Coste por unidad de distancia recorrida durante el bounce (opcional).")]
    public float flameCostPerUnit = 0f;

    [Tooltip("Si no hay llama suficiente para iniciar, no permite bounce.")]
    public bool blockBounceIfNoFlame = true;

    [Tooltip("Si se queda sin llama durante el bounce, termina automáticamente.")]
    public bool endBounceWhenOutOfFlame = true;

    [Header("Debug")]
    public bool debugFlame = true;
    public bool debugOnScreen = true;

    [HideInInspector] public float flameSpentTotal = 0f;
    [HideInInspector] public float flameSpentThisBounce = 0f;

    [Header("Invencibilidad")]
    public bool invincibleDuringBounce = true;
    [HideInInspector] public bool isInvincible = false;

    [Header("Colisión de rebote (capas que quieres 'golpear' con el CircleCast)")]
    [Tooltip("Aquí normalmente van suelo/pared + enemigos/rompibles si quieres impactarlos con el cast.")]
    public LayerMask bounceLayers;

    [Header("Mundo sólido (solo suelo/pared). NO enemigos.")]
    [Tooltip("Esto se usa SOLO para evitar atravesar suelo/pared en el 'post-pierce'. Debe ser SOLO ground/walls.")]
    public LayerMask worldSolidLayers;

    public float skin = 0.02f;

    [Header("Evitar bloqueo por colisión con Enemy/Hazard durante Aim/Bounce")]
    [Tooltip("Marca aquí la layer Enemy (collider sólido del enemigo).")]
    public LayerMask enemyLayers;
    [Tooltip("Marca aquí la layer Hazard si tu hitbox/hazard te molesta durante Aim/Bounce.")]
    public LayerMask hazardLayers;

    public bool ignoreEnemyCollisionWhileAiming = true;
    public bool ignoreEnemyCollisionWhileBouncing = true;

    [Tooltip("Si true, también ignoramos Hazard durante Aim/Bounce (si tu Hitbox está en Hazard y molesta).")]
    public bool ignoreHazardDuringAimBounce = true;

    private Collider2D playerCol;
    private int playerLayer;
    private readonly List<int> enemyLayerIds = new List<int>();
    private readonly List<int> hazardLayerIds = new List<int>();
    private bool ignoringAimBounceCollisions = false;

    [Header("Daño")]
    public int bounceDamage = 20;

    [Header("Preview trayectoria")]
    public LineRenderer previewLine;
    public int previewSegments = 30;
    public int previewMaxBounces = 5;
    public Color previewColor = Color.cyan;

    [Header("Suavizado fin de trayecto")]
    [Range(0f, 1f)] public float slowDownFraction = 0.3f;
    [Range(0.05f, 1f)] public float minStepFactor = 0.2f;

    [Header("Pierce: avance seguro post-impacto")]
    [Tooltip("Distancia mínima para separarte del collider tras romperlo.")]
    public float pierceSeparationMin = 0.06f;

    [Tooltip("Multiplica el radio para calcular avance post-pierce (antes se hacía a ciegas).")]
    public float pierceSeparationRadiusFactor = 0.7f;

    [Tooltip("Máximo avance post-pierce para evitar empujones absurdos si el collider es grande.")]
    public float pierceSeparationMax = 0.25f;

    private Rigidbody2D rb;
    private PlayerMovementController movement;
    private CircleCollider2D circle;
    private PlayerPhysicsStateController phys;
    private PlayerSparkBoost spark;

    private bool isAiming = false;
    private bool isBouncing = false;

    private Vector2 aimDirection = Vector2.right;
    private Vector2 bounceDir = Vector2.right;
    private Vector2 lastPreviewDir = Vector2.right;

    private float remainingDistance = 0f;
    private float fixedStepSize = 0f;
    private float ballRadius = 0f;

    private Vector2 aimStartPosition;

    // Sólidos NO-pierce: evita spamear impactos infinitos
    private readonly HashSet<Collider2D> impactedThisBounce = new HashSet<Collider2D>();
    // Pierce: evita multi-hit dentro del mismo bounce, pero sigue atravesando
    private readonly HashSet<Collider2D> piercedThisBounce = new HashSet<Collider2D>();

    public bool IsAiming => isAiming;
    public bool IsBouncing => isBouncing;

    public event Action OnBounceStart;
    public event Action OnBounceEnd;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovementController>();
        circle = GetComponent<CircleCollider2D>();
        phys = GetComponent<PlayerPhysicsStateController>();
        spark = GetComponent<PlayerSparkBoost>();

        if (phys == null) Debug.LogError("[PlayerBounceAttack] Falta PlayerPhysicsStateController en el Player.");
        if (circle == null) Debug.LogError("[PlayerBounceAttack] Falta CircleCollider2D.");

        playerCol = GetComponent<Collider2D>();
        playerLayer = gameObject.layer;

        ballRadius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

        fixedStepSize = maxDistance / Mathf.Max(1, previewSegments);
        ConfigurePreview();

        CacheLayerIds(enemyLayers, enemyLayerIds);
        CacheLayerIds(hazardLayers, hazardLayerIds);
    }

    /// <summary>
    /// CORTE DURO: se llama desde SparkBoost cuando el player recoge un Spark.
    /// Esto elimina cualquier "trayecto pendiente" del bounce y evita que continúe.
    /// </summary>
    public void ForceCancelFromSpark()
    {
        // Corta de raíz el movimiento pendiente
        remainingDistance = 0f;

        // Mata estados
        if (isAiming) EndAiming();
        if (isBouncing) EndBounce();

        // Por seguridad: limpia todo
        impactedThisBounce.Clear();
        piercedThisBounce.Clear();

        SetAimBounceCollisionIgnore(false);
        ClearPreview();

        // Asegura desbloqueo y flags
        movement.movementLocked = false;
        isInvincible = false;
    }

    private void CacheLayerIds(LayerMask mask, List<int> outList)
    {
        outList.Clear();
        for (int i = 0; i < 32; i++)
        {
            if (((mask.value >> i) & 1) == 1)
                outList.Add(i);
        }
    }

    private void Update()
    {
        // Si Spark está activo/dashing => bounce fuera INMEDIATO.
        // Además usamos ForceCancelFromSpark para cortar remainingDistance YA.
        if (spark != null && (spark.IsSparkActive() || spark.IsDashing()))
        {
            ForceCancelFromSpark();
            return;
        }

        bool attackDown = Input.GetKeyDown(attackKey) || Input.GetKeyDown(attackPadKey);
        bool attackUp = Input.GetKeyUp(attackKey) || Input.GetKeyUp(attackPadKey);
        bool jumpDown = Input.GetKeyDown(movement.jumpKey) || Input.GetKeyDown(KeyCode.JoystickButton0);

        if (isAiming) phys.RequestBounceAiming();
        if (isBouncing) phys.RequestBounceBouncing();

        if (!isAiming && !isBouncing && attackDown)
        {
            if (!CanStartAiming())
                return;

            StartAiming();

            if (ignoreEnemyCollisionWhileAiming)
                SetAimBounceCollisionIgnore(true);
        }

        if (isAiming)
            HandleAiming();

        if (isAiming && attackUp)
        {
            if (aimDirection.sqrMagnitude < 0.1f)
                aimDirection = Vector2.right;

            StartBounce();
        }

        if (isBouncing && (jumpDown || attackDown))
            EndBounce();
    }

    private void FixedUpdate()
    {
        // Corte duro también en FixedUpdate por si Spark se activa entre ticks
        if (spark != null && (spark.IsSparkActive() || spark.IsDashing()))
            return;

        if (isAiming)
        {
            if (useFlame && blockBounceIfNoFlame && flameCostStart > 0f && flame < flameCostStart)
            {
                EndAiming();
                return;
            }

            rb.MovePosition(aimStartPosition);
            return;
        }

        if (!isBouncing) return;

        float baseStep = fixedStepSize;
        float moveDist = baseStep;

        if (slowDownFraction > 0f)
        {
            float slowZone = maxDistance * slowDownFraction;
            if (remainingDistance < slowZone)
            {
                float t = remainingDistance / Mathf.Max(0.0001f, slowZone);
                float factor = Mathf.Lerp(minStepFactor, 1f, t);
                moveDist = baseStep * factor;
            }
        }

        if (moveDist > remainingDistance) moveDist = remainingDistance;

        if (moveDist <= 0f)
        {
            EndBounce();
            return;
        }

        Vector2 dir = (bounceDir.sqrMagnitude > 0.001f ? bounceDir : aimDirection).normalized;

        int safety = 0;
        const int MAX_INTERNAL_HITS = 24;

        while (moveDist > 0f && remainingDistance > 0f && safety++ < MAX_INTERNAL_HITS)
        {
            Vector2 origin = rb.position;

            if (!TryCircleCastFiltered(origin, ballRadius, dir, moveDist + skin, bounceLayers, out RaycastHit2D hit))
            {
                rb.MovePosition(origin + dir * moveDist);
                remainingDistance -= moveDist;

                if (useFlame && flameCostPerUnit > 0f)
                    SpendFlame(moveDist * flameCostPerUnit);

                CheckOutOfFlameAndEndIfNeeded();
                if (!isBouncing) return;

                if (remainingDistance <= 0f)
                    EndBounce();

                return;
            }

            float travel = Mathf.Max(0f, hit.distance - skin);

            if (travel > 0f)
            {
                rb.MovePosition(origin + dir * travel);
                remainingDistance -= travel;

                if (useFlame && flameCostPerUnit > 0f)
                    SpendFlame(travel * flameCostPerUnit);
            }

            moveDist = Mathf.Max(0f, moveDist - travel);

            bool broke = ApplyPiercingImpact(hit.collider, dir, bounceDamage, out _);

            // Pierce: si rompe, no rebota, sigue recto
            if (broke)
            {
                float desiredPush = Mathf.Max(pierceSeparationMin, ballRadius * pierceSeparationRadiusFactor);
                desiredPush = Mathf.Min(desiredPush, pierceSeparationMax);

                float pushed = SafeAdvanceWithoutEnteringWorldSolids(dir, desiredPush, hit.collider);

                if (pushed > 0f)
                {
                    remainingDistance -= pushed;
                    moveDist = Mathf.Max(0f, moveDist - pushed);

                    if (useFlame && flameCostPerUnit > 0f)
                        SpendFlame(pushed * flameCostPerUnit);

                    CheckOutOfFlameAndEndIfNeeded();
                    if (!isBouncing) return;
                }

                if (remainingDistance <= 0f)
                {
                    EndBounce();
                    return;
                }

                if (moveDist <= 0f)
                    return;

                continue;
            }

            // Rebote real
            bounceDir = Vector2.Reflect(dir, hit.normal).normalized;

            if (useFlame && flameCostPerBounce > 0f)
                SpendFlame(flameCostPerBounce);

            CheckOutOfFlameAndEndIfNeeded();
            if (!isBouncing) return;

            if (remainingDistance <= 0f)
                EndBounce();

            return;
        }

        CheckOutOfFlameAndEndIfNeeded();
        if (!isBouncing) return;

        if (remainingDistance <= 0f)
            EndBounce();
    }

    private bool TryCircleCastFiltered(Vector2 origin, float radius, Vector2 dir, float distance, LayerMask mask, out RaycastHit2D bestHit)
    {
        bestHit = default;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, radius, dir, distance, mask);

        float bestDist = float.PositiveInfinity;
        bool bestIsPierceable = false;
        bool found = false;

        const float TIE_EPS = 0.05f;
        float zeroReject = Mathf.Max(0.001f, skin * 1.5f);

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h.collider) continue;

            if (h.collider.isTrigger) continue;
            if (playerCol != null && h.collider == playerCol) continue;
            if (piercedThisBounce.Contains(h.collider)) continue;

            bool isPierceable = (h.collider.GetComponentInParent<IPiercingBounceReceiver>() != null);



            float d = h.distance;

            if (!found)
            {
                bestDist = d;
                bestHit = h;
                bestIsPierceable = isPierceable;
                found = true;
                continue;
            }

            if (d + TIE_EPS < bestDist)
            {
                bestDist = d;
                bestHit = h;
                bestIsPierceable = isPierceable;
                continue;
            }

            if (Mathf.Abs(d - bestDist) <= TIE_EPS)
            {
                if (!bestIsPierceable && isPierceable)
                {
                    bestDist = d;
                    bestHit = h;
                    bestIsPierceable = true;
                }
            }
        }

        return found;
    }

    private float SafeAdvanceWithoutEnteringWorldSolids(Vector2 dir, float distance, Collider2D ignoreCol)
    {
        if (distance <= 0f) return 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        Vector2 origin = rb.position;

        int mask = worldSolidLayers.value;

        if (mask == 0)
            mask = (bounceLayers.value & ~enemyLayers.value & ~hazardLayers.value);

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, ballRadius, dir, distance + skin, mask);

        float allowed = distance;

        if (hits != null && hits.Length > 0)
        {
            float best = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (!h.collider) continue;
                if (ignoreCol != null && h.collider == ignoreCol) continue;
                if (h.collider.isTrigger) continue;

                if (h.distance < best)
                {
                    best = h.distance;
                    found = true;
                }
            }

            if (found)
                allowed = Mathf.Max(0f, best - skin);
        }

        if (allowed > 0f)
        {
            rb.MovePosition(origin + dir * allowed);
            return allowed;
        }

        float tiny = Mathf.Min(pierceSeparationMin, distance);
        if (tiny > 0f)
        {
            RaycastHit2D block = Physics2D.CircleCast(origin, ballRadius, dir, tiny + skin, mask);
            if (!block.collider)
            {
                rb.MovePosition(origin + dir * tiny);
                return tiny;
            }
        }

        return 0f;
    }

    private bool ApplyPiercingImpact(Collider2D col, Vector2 direction, float incomingDamage, out float remainingDamage)
    {
        remainingDamage = incomingDamage;
        if (col == null) return false;

        // Si ya lo “pierceaste” en este bounce, compórtate como si NO existiera:
        // Esto evita el bug de “pegado al collider => rebota / comportamientos raros”.
        if (piercedThisBounce.Contains(col))
            return true;

        // Evita aplicar daño 200 veces por micro-hits en el mismo bounce
        if (impactedThisBounce.Contains(col))
            return false;

        var receiverPierce = col.GetComponentInParent<IPiercingBounceReceiver>();
        if (receiverPierce != null)
        {
            impactedThisBounce.Add(col);

            var impact = new BounceImpactData((int)Mathf.Ceil(incomingDamage), direction, gameObject);

            // CLAVE: usa el bool de la interfaz. Eso define si atraviesas o rebotas.
            bool pierced = receiverPierce.ApplyPiercingBounce(impact, incomingDamage, out float rem);
            remainingDamage = rem;

            if (pierced)
                piercedThisBounce.Add(col);

            return pierced; // true => seguir recto; false => rebote normal
        }


        // Si NO es pierceable, es impacto normal (rebote)
        var receiver = col.GetComponentInParent<IBounceImpactReceiver>();
        if (receiver != null)
        {
            receiver.ReceiveBounceImpact(new BounceImpactData(bounceDamage, direction, gameObject));
            impactedThisBounce.Add(col);
        }

        remainingDamage = 0f;
        return false;
    }

    private bool IsActuallyBrokenNow(Collider2D col)
    {
        if (col == null) return true;
        if (!col.enabled) return true;
        if (!col.gameObject.activeInHierarchy) return true;

        var wm = col.GetComponentInParent<WorldMaterial>();
        if (wm != null)
            return wm.IsBroken;   // <- ESTO ES LO REAL (hp/isBroken)

        return false;
    }




    // ================== LLAMA ==================

    private bool HasFlame(float amount) => (!useFlame) || flame >= amount;

    private bool SpendFlame(float amount)
    {
        if (!useFlame) return true;
        if (amount <= 0f) return true;
        if (flame < amount) return false;

        flame -= amount;
        if (flame < 0f) flame = 0f;

        flameSpentTotal += amount;
        if (isBouncing) flameSpentThisBounce += amount;

        if (debugFlame)
            Debug.Log($"[Flame] -{amount:0.00} | flame={flame:0.00}/{maxFlame:0.00}");

        return true;
    }

    private bool CanStartAiming()
    {
        if (!useFlame) return true;

        if (flameCostStart > 0f)
            return flame >= flameCostStart;

        return flame > 0f;
    }

    private void CheckOutOfFlameAndEndIfNeeded()
    {
        if (!useFlame || !endBounceWhenOutOfFlame) return;
        if (flame > 0f) return;

        EndBounce();
    }

    // ================== ESTADOS ==================

    private void StartAiming()
    {
        isAiming = true;

        aimDirection = Vector2.right;
        lastPreviewDir = aimDirection;

        movement.movementLocked = true;
        aimStartPosition = rb.position;

        fixedStepSize = maxDistance / Mathf.Max(1, previewSegments);
        UpdatePreview();
    }

    private void HandleAiming()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector2 input = new Vector2(x, y);
        if (input.sqrMagnitude < 0.01f)
            input = lastPreviewDir;

        input.Normalize();

        aimDirection = input;
        lastPreviewDir = input;

        UpdatePreview();
    }

    private void StartBounce()
    {
        if (useFlame && flameCostStart > 0f)
        {
            if (blockBounceIfNoFlame && !HasFlame(flameCostStart))
            {
                EndAiming();
                return;
            }

            if (!SpendFlame(flameCostStart))
            {
                EndAiming();
                return;
            }
        }

        isAiming = false;
        isBouncing = true;

        impactedThisBounce.Clear();
        piercedThisBounce.Clear();

        flameSpentThisBounce = 0f;
        remainingDistance = maxDistance;

        ClearPreview();

        bounceDir = lastPreviewDir.sqrMagnitude > 0.001f ? lastPreviewDir.normalized : Vector2.right;

        if (invincibleDuringBounce)
            isInvincible = true;

        if (ignoreEnemyCollisionWhileBouncing)
            SetAimBounceCollisionIgnore(true);

        OnBounceStart?.Invoke();
    }

    private void EndAiming()
    {
        isAiming = false;
        ClearPreview();
        movement.movementLocked = false;

        if (!isBouncing)
            SetAimBounceCollisionIgnore(false);
    }

    private void EndBounce()
    {
        isBouncing = false;
        ClearPreview();

        movement.movementLocked = false;
        isInvincible = false;

        SetAimBounceCollisionIgnore(false);

        OnBounceEnd?.Invoke();
    }

    // ================== PREVIEW ==================

    private void UpdatePreview()
    {
        if (!isAiming || previewLine == null)
        {
            ClearPreview();
            return;
        }

        Vector2 origin = rb.position;
        Vector2 dir = lastPreviewDir.sqrMagnitude > 0.001f ? lastPreviewDir.normalized : Vector2.right;

        float remaining = maxDistance;
        int bounceCount = 0;

        List<Vector3> points = new List<Vector3> { origin };
        int maxPoints = Mathf.Max(2, previewSegments + 1);

        while (remaining > 0f && points.Count < maxPoints && bounceCount <= previewMaxBounces)
        {
            float stepDist = fixedStepSize;
            if (stepDist <= 0f) break;

            RaycastHit2D hit = Physics2D.CircleCast(origin, ballRadius, dir, stepDist + skin, bounceLayers);

            if (hit.collider != null)
            {
                float travel = Mathf.Max(0f, hit.distance - skin);
                Vector2 hitPos = origin + dir * travel;

                points.Add(hitPos);
                remaining -= travel;
                origin = hitPos;

                dir = Vector2.Reflect(dir, hit.normal).normalized;
                bounceCount++;

                if (travel <= 0.001f)
                    origin += dir * 0.01f;
            }
            else
            {
                Vector2 nextPos = origin + dir * stepDist;
                points.Add(nextPos);
                remaining -= stepDist;
                origin = nextPos;
            }
        }

        previewLine.positionCount = points.Count;
        previewLine.SetPositions(points.ToArray());
        previewLine.enabled = true;
    }

    private void ClearPreview()
    {
        if (previewLine == null) return;
        previewLine.enabled = false;
        previewLine.positionCount = 0;
    }

    private void ConfigurePreview()
    {
        if (previewLine == null)
        {
            previewLine = GetComponent<LineRenderer>();
            if (previewLine == null)
                previewLine = gameObject.AddComponent<LineRenderer>();
        }

        previewLine.useWorldSpace = true;
        previewLine.startWidth = 0.08f;
        previewLine.endWidth = 0.08f;

        var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.color = previewColor;
        previewLine.material = mat;

        previewLine.startColor = Color.white;
        previewLine.endColor = Color.white;
        previewLine.sortingLayerName = "Default";
        previewLine.sortingOrder = 100;

        previewLine.positionCount = 0;
        previewLine.enabled = false;
    }

    // ================== IGNORE COLLISIONS DURING AIM/BOUNCE ==================

    private void SetAimBounceCollisionIgnore(bool ignore)
    {
        if (ignore == ignoringAimBounceCollisions) return;
        ignoringAimBounceCollisions = ignore;

        for (int i = 0; i < enemyLayerIds.Count; i++)
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIds[i], ignore);

        if (ignoreHazardDuringAimBounce)
        {
            for (int i = 0; i < hazardLayerIds.Count; i++)
                Physics2D.IgnoreLayerCollision(playerLayer, hazardLayerIds[i], ignore);
        }
    }
}
