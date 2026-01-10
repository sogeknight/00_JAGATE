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
    public bool debugFlame = false;
    public bool debugOnScreen = true;

    [HideInInspector] public float flameSpentTotal = 0f;
    [HideInInspector] public float flameSpentThisBounce = 0f;

    [Header("Invencibilidad")]
    public bool invincibleDuringBounce = true;
    [HideInInspector] public bool isInvincible = false;

    [Header("Colisión (cast)")]
    [Tooltip("Capas que quieres 'golpear' con el CircleCast durante bounce + preview (suelo/pared, enemigos, rompibles...).")]
    public LayerMask bounceLayers;

    [Header("Mundo sólido (solo suelo/pared). NO enemigos.")]
    [Tooltip("Esto se usa SOLO para evitar meterte en sólidos al hacer post-pierce. Debe ser SOLO ground/walls.")]
    public LayerMask worldSolidLayers;

    [Tooltip("Skin para evitar stuck / casts a 0.")]
    public float skin = 0.02f;

    [Header("Evitar bloqueo por colisión con Enemy/Hazard durante Aim/Bounce")]
    public LayerMask enemyLayers;
    public LayerMask hazardLayers;

    public bool ignoreEnemyCollisionWhileAiming = true;
    public bool ignoreEnemyCollisionWhileBouncing = true;
    public bool ignoreHazardDuringAimBounce = true;

    private Collider2D playerCol;
    private int playerLayer;
    private readonly List<int> enemyLayerIds = new List<int>();
    private readonly List<int> hazardLayerIds = new List<int>();
    private bool ignoringAimBounceCollisions = false;

    [Header("Daño")]
    public int bounceDamage = 20;

    [Header("Preview: pierce prediction")]
    [Tooltip("Si es true, la preview intenta predecir si un bloque se rompe con 1 golpe (HP <= bounceDamage).")]
    public bool previewPredictPierceUsingWorldMaterialHP = true;

    [Tooltip("Si no se puede predecir (no hay WorldMaterial), qué hacemos en preview: true=asumir pierce, false=asumir rebote.")]
    public bool previewAssumePierceWhenUnknown = true;


    [Header("Preview trayectoria")]
    public LineRenderer previewLine;
    [Min(2)] public int previewSegments = 30;
    [Min(0)] public int previewMaxBounces = 5;



    [Header("Preview: Pickup magnet (solo visual)")]
    public bool previewMagnetizePickups = true;

    // IMPORTANTE: pon aquí la layer donde están tus FlameSparkPickup (o un LayerMask específico)
    public LayerMask pickupLayers;


    [Header("Aim Assist: Auto-target pickup cercano (mantiene 8 dirs)")]
    public bool aimAssistPickups = true;

    [Tooltip("Radio en el que el aim se auto-redondea hacia un pickup cercano (solo durante AIM).")]
    public float aimAssistPickupRadius = 1.25f;


    [Tooltip("Radio en el que la preview 'engancha' un pickup cercano.")]
    [Range(0.02f, 1.0f)] public float pickupMagnetRadius = 0.35f;

    [Tooltip("Cuánta curvatura/atracción aplica (0=off, 1=engancha total).")]
    [Range(0f, 1f)] public float pickupMagnetStrength = 1.0f;

    [Tooltip("Cuántos puntos finales se curvan hacia el pickup (más = más redondo).")]
    [Range(2, 8)] public int pickupMagnetTailPoints = 4;

    // Marcador visual (círculo) como el Spark
    public bool previewShowPickupCircle = true;

    // Estado persistente de "la preview va a coger un pickup"
    private bool previewHitPickup = false;
    private Vector2 previewPickupAnchor = default;

    public LineRenderer pickupPreviewCircle;
    public float pickupCircleRadius = 0.35f;
    public int pickupCircleSegments = 32;
    public float pickupCircleWidth = 0.06f;
    public string pickupCircleSortingLayer = "Default";
    public int pickupCircleSortingOrder = 10050;


    [Header("Aim Snapping (solo + y X)")]
    public bool snapAimTo8Dirs = true;

    [Tooltip("Si el input está cerca de 0, mantenemos la última dirección válida.")]
    [Range(0.01f, 0.6f)] public float aimInputDeadzone = 0.20f;

    [Tooltip("Anti-drift: si un eje es muy pequeño, se anula (evita diagonales raras por drift del stick).")]
    [Range(0f, 0.25f)] public float driftCancel = 0.08f;

    [Header("Preview: visual")]
    public Color previewColor = Color.cyan;

    [Header("Pickup: Debug Circle")]
    public bool pickupCircleUseRealCatchRadius = true;
    [Range(0.1f, 2.0f)] public float pickupCircleRadiusMultiplier = 1.0f;
    public float pickupCircleRadiusOverride = 0.35f; // si no usas real


    [Header("Rebote: corrección de normal (anti diagonales raras)")]
    [Range(0.5f, 0.99f)]
    public float normalSnapThreshold = 0.85f; // si |ny| > esto, tratamos como suelo/techo


    [Tooltip("Longitud visual de la preview (no afecta al ataque real). Si 0, usa maxDistance.")]
    public float previewDistance = 0f;

    [Header("Suavizado fin de trayecto (REAL bounce)")]
    [Range(0f, 1f)] public float slowDownFraction = 0.30f;
    [Range(0.05f, 1f)] public float minStepFactor = 0.20f;

    [Header("Pierce: avance seguro post-impacto")]
    public float pierceSeparationMin = 0.06f;
    public float pierceSeparationRadiusFactor = 0.7f;
    public float pierceSeparationMax = 0.25f;


    [Header("Debug: Pickup Magnet")]
    public bool debugPickupMagnet = true;
    [Range(1, 60)] public int debugPickupEveryNFrames = 6;
    public bool debugPickupDraw = true;

    private int _dbgPickupFrame;
    private int _dbgBestProbeIndex = -1;
    private float _dbgBestDistFromStart = 0f;
    private Vector2 _dbgBestProbePoint;
    private Vector2 _dbgBestProbePrevPoint;
    private string _dbgBestPickupName = "";
    private int _dbgBestOverlapCount = 0;
    private bool _dbgMagnetReturnedTrue = false;


    private float _pickupCastRadius = 0f;



    private Rigidbody2D rb;
    private PlayerMovementController movement;
    private CircleCollider2D circle;
    private PlayerPhysicsStateController phys;
    private PlayerSparkBoost spark;

    private bool isAiming = false;
    private bool isBouncing = false;

    // Auto-aim por defecto (solo si NO hay input del jugador)
    private bool usingDefaultPickupAim = false;


    private Vector2 lastAimDir = Vector2.right; // dirección “bloqueada” (8 dirs)
    private float remainingDistance = 0f;
    private float fixedStepSize = 0f;
    private float ballRadius = 0f;

    private Vector2 aimStartPosition;

    // Para evitar multi-hits / loops
    private readonly HashSet<Collider2D> impactedThisBounce = new HashSet<Collider2D>();
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

        playerCol = GetComponent<Collider2D>();
        playerLayer = gameObject.layer;

        if (circle == null) Debug.LogError("[PlayerBounceAttack] Falta CircleCollider2D.");
        if (phys == null) Debug.LogError("[PlayerBounceAttack] Falta PlayerPhysicsStateController en el Player.");

        ballRadius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        _pickupCastRadius = ComputePickupCastRadiusFromAllPlayerColliders();

        fixedStepSize = maxDistance / Mathf.Max(1, previewSegments);

        ConfigurePreview();

        CacheLayerIds(enemyLayers, enemyLayerIds);
        CacheLayerIds(hazardLayers, hazardLayerIds);

        _pickupFilter = new ContactFilter2D();
        _pickupFilter.useLayerMask = true;
        _pickupFilter.layerMask = pickupLayers;
        _pickupFilter.useTriggers = true;   // CLAVE: SIEMPRE incluir triggers
        _pickupFilter.useDepth = false;
    }

    private void CacheLayerIds(LayerMask mask, List<int> outList)
    {
        outList.Clear();
        for (int i = 0; i < 32; i++)
            if (((mask.value >> i) & 1) == 1)
                outList.Add(i);
    }

    /// <summary>
    /// CORTE DURO: se llama desde SparkBoost cuando el player recoge un Spark.
    /// Elimina cualquier trayecto pendiente del bounce y evita que continúe.
    /// </summary>
    public void ForceCancelFromSpark()
    {
        remainingDistance = 0f;

        if (isAiming) EndAiming();
        if (isBouncing) EndBounce();

        impactedThisBounce.Clear();
        piercedThisBounce.Clear();

        SetAimBounceCollisionIgnore(false);
        ClearPreview();

        movement.movementLocked = false;
        isInvincible = false;
    }

    private void Update()
    {
        // Si Spark está activo/dashing => bounce fuera inmediato.
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
            if (!CanStartAiming()) return;
            StartAiming();

            if (ignoreEnemyCollisionWhileAiming)
                SetAimBounceCollisionIgnore(true);
        }

        if (isAiming)
            HandleAiming();

        if (isAiming && attackUp)
            StartBounce();

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

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

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

        // MOVIMIENTO REAL: usa exactamente la misma simulación que el preview (mismo cast, mismas reglas)
        SimStepResult step = SimulateOneStep(
            rb.position,
            lastAimDir,
            moveDist,
            bounceLayers,
            piercedThisBounce,
            isPreview: false,
            out Vector2 newPos,
            out Vector2 newDir,
            out Collider2D hitCol,
            out bool didBounce,
            out bool didPierce,
            out float traveled);

        // Mueve
        rb.MovePosition(newPos);

        // Consume distancia (lo realmente recorrido)
        remainingDistance -= traveled;
        if (useFlame && flameCostPerUnit > 0f && traveled > 0f)
            SpendFlame(traveled * flameCostPerUnit);

        if (didPierce)
        {
            // ya se añadió piercedThisBounce dentro de ApplyPiercingImpact
            // Mantiene dirección
        }
        else if (didBounce)
        {
            lastAimDir = newDir; // actualiza dir tras rebote (ya cuantizada)
            if (useFlame && flameCostPerBounce > 0f)
                SpendFlame(flameCostPerBounce);
        }

        CheckOutOfFlameAndEndIfNeeded();
        if (!isBouncing) return;

        if (remainingDistance <= 0f)
            EndBounce();
    }


    private float ComputePickupCastRadiusFromAllPlayerColliders()
    {
        // Queremos un radio que represente “lo primero que puede tocar el trigger” en runtime.
        // Si el Player tiene BoxCollider2D + CircleCollider2D, el Box puede tocar antes.
        // Aproximamos usando el círculo que envuelve los bounds de cada collider.

        float best = 0f;
        var cols = GetComponents<Collider2D>();
        if (cols == null || cols.Length == 0) return ballRadius;

        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (!col || !col.enabled) continue;

            // Ignora triggers si quieres que el cuerpo “real” sea el que recoge.
            // Si tu Player tiene triggers propios, no deben influir.
            if (col.isTrigger) continue;

            if (col is CircleCollider2D cc)
            {
                float s = Mathf.Max(cc.transform.lossyScale.x, cc.transform.lossyScale.y);
                best = Mathf.Max(best, Mathf.Abs(cc.radius * s));
            }
            else
            {
                // Radio del círculo que envuelve el rectángulo de bounds (aprox.)
                var e = col.bounds.extents; // en mundo
                float r = Mathf.Sqrt(e.x * e.x + e.y * e.y);
                best = Mathf.Max(best, r);
            }
        }

        // Fallback seguro
        if (best <= 0.0001f) best = ballRadius;
        return best;
    }


    // ======================================================================
    // SIMULACIÓN UNIFICADA (REAL + PREVIEW)
    // ======================================================================

    private enum SimStepResult
    {
        NoHitMoved,
        HitPierced,
        HitBounced,
        HitBlockedNoMove
    }

    private SimStepResult SimulateOneStep(
        Vector2 startPos,
        Vector2 dir,
        float requestedMove,
        LayerMask mask,
        HashSet<Collider2D> previewOrRuntimePierced,
        bool isPreview,
        out Vector2 outPos,
        out Vector2 outDir,
        out Collider2D outHitCol,
        out bool didBounce,
        out bool didPierce,
        out float traveled)
    {
        outPos = startPos;
        outDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        outHitCol = null;
        didBounce = false;
        didPierce = false;
        traveled = 0f;

        float castDist = Mathf.Max(0f, requestedMove);
        if (castDist <= 0f)
            return SimStepResult.HitBlockedNoMove;

        // Cast
        if (!TryCircleCastFiltered(startPos, ballRadius, outDir, castDist + skin, mask, previewOrRuntimePierced, out RaycastHit2D hit) || hit.collider == null)
        {
            // no hit: avanzamos full
            outPos = startPos + outDir * castDist;
            traveled = castDist;
            return SimStepResult.NoHitMoved;
        }

        outHitCol = hit.collider;

        // Distancia hasta el contacto “útil”
        float travelToHit = Mathf.Max(0f, hit.distance - skin);

        // 1) Avanza hasta el hit (si hay espacio)
        if (travelToHit > 0f)
        {
            outPos = startPos + outDir * travelToHit;
            traveled = travelToHit;
        }
        else
        {
            outPos = startPos;
            traveled = 0f;
        }

        // 2) Contacto inmediato (ground / pegado): resolver de forma determinista
        bool inContact = (hit.distance <= skin * 1.25f);

        // En PREVIEW no aplicamos daño real; en REAL sí.
        bool pierceable = IsPierceable(hit.collider);

        if (pierceable)
        {
            bool pierced = false;

            if (isPreview)
            {
                pierced = PredictPiercePreview(hit.collider, bounceDamage);
            }
            else
            {
                pierced = ApplyPiercingImpact(hit.collider, outDir, bounceDamage, out _);
            }


            if (pierced)
            {
                didPierce = true;

                // Evita loops: marca como “pierced” en ambos modos para que el cast lo ignore después
                if (previewOrRuntimePierced != null)
                    previewOrRuntimePierced.Add(hit.collider);

                // Empuje post-pierce (salir del collider)
                float desiredPush = Mathf.Max(pierceSeparationMin, ballRadius * pierceSeparationRadiusFactor);
                desiredPush = Mathf.Min(desiredPush, pierceSeparationMax);

                float pushed = isPreview
                    ? PreviewSafeAdvanceWithoutEnteringWorldSolids(outPos, outDir, desiredPush, hit.collider)
                    : SafeAdvanceWithoutEnteringWorldSolids(outDir, desiredPush, hit.collider);

                if (pushed < 0.0001f)
                    pushed = Mathf.Min(desiredPush, pierceSeparationMin);

                outPos = outPos + outDir * pushed;
                traveled += pushed;

                // Dirección no cambia
                return SimStepResult.HitPierced;
            }
        }

        // 3) Si no pierce: rebote
        didBounce = true;

        // Normal tal cual viene del hit; la limpieza de dientes y diagonales
        // la hace ComputeBouncedDir -> QuantizeSurfaceNormal.
        outDir = ComputeBouncedDir(outDir, hit.normal);




        // Si estamos pegados (hit.distance ~ 0), NO lo trates como "bloqueado":
        // empuja fuera y consume un pelín de longitud para que la preview pueda continuar
        if (inContact && traveled <= 0.0001f)
        {
            float pushOut = Mathf.Max(skin * 3f, 0.02f);

            // empuja fuera del suelo
            outPos = startPos + hit.normal * pushOut;

            // consume algo para evitar loops infinitos en la simulación de preview
            traveled = pushOut;
        }
        else if (inContact)
        {
            // caso normal: ya avanzaste algo, pero igual conviene sacar un pelo fuera
            outPos = outPos + hit.normal * Mathf.Max(skin * 3f, 0.02f);
        }

        return SimStepResult.HitBounced;

    }

    private bool TryCircleCastFiltered(
        Vector2 origin,
        float radius,
        Vector2 dir,
        float distance,
        LayerMask mask,
        HashSet<Collider2D> ignoreSet,
        out RaycastHit2D bestHit)
    {
        bestHit = default;

        // Dir normalizada para todos los tests
        Vector2 dirNorm = (dir.sqrMagnitude > 0.0001f) ? dir.normalized : Vector2.right;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, radius, dirNorm, distance, mask);
        float bestDist = float.PositiveInfinity;
        bool found = false;

        const float TIE_EPS = 0.01f;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h.collider) continue;
            if (h.collider.isTrigger) continue;
            if (playerCol != null && h.collider == playerCol) continue;
            if (ignoreSet != null && ignoreSet.Contains(h.collider)) continue;

            float d = h.distance;

            // SOLO filtro especial para contactos “pegados” al origen:
            // si estamos prácticamente dentro de un collider
            // y la normal mira CASI en la misma dirección que nos movemos,
            // lo ignoramos (es la pared de detrás / esquina rara).
            if (d <= skin * 1.5f)
            {
                float dot = Vector2.Dot(dirNorm, h.normal);
                // dot > 0.25 => normal bastante alineada con dir (backface)
                if (dot > 0.25f)
                    continue;
            }

            // A partir de aquí: selección normal por distancia mínima
            if (!found)
            {
                bestDist = d;
                bestHit = h;
                found = true;
                continue;
            }

            if (d + TIE_EPS < bestDist)
            {
                bestDist = d;
                bestHit = h;
            }
        }

        return found;
    }



    // ======================================================================
    // RUNTIME PIERCE + POST-PUSH
    // ======================================================================

    private bool IsPierceable(Collider2D col)
    {
        return col != null && col.GetComponentInParent<IPiercingBounceReceiver>() != null;
    }

    private bool ApplyPiercingImpact(Collider2D col, Vector2 direction, float incomingDamage, out float remainingDamage)
    {
        remainingDamage = incomingDamage;
        if (col == null) return false;

        // Si ya lo atravesaste en este bounce, ignóralo como sólido
        if (piercedThisBounce.Contains(col))
            return true;

        // Evita micro-hits repetidos sobre el mismo collider
        if (impactedThisBounce.Contains(col))
            return false;

        var receiverPierce = col.GetComponentInParent<IPiercingBounceReceiver>();
        if (receiverPierce != null)
        {
            impactedThisBounce.Add(col);

            var impact = new BounceImpactData((int)Mathf.Ceil(incomingDamage), direction, gameObject);
            bool pierced = receiverPierce.ApplyPiercingBounce(impact, incomingDamage, out float rem);
            remainingDamage = rem;

            if (pierced)
                piercedThisBounce.Add(col);

            return pierced;
        }

        // Impacto normal (no pierce)
        var receiver = col.GetComponentInParent<IBounceImpactReceiver>();
        if (receiver != null)
        {
            receiver.ReceiveBounceImpact(new BounceImpactData(bounceDamage, direction, gameObject));
            impactedThisBounce.Add(col);
        }

        remainingDamage = 0f;
        return false;
    }

    private bool PredictPiercePreview(Collider2D col, float incomingDamage)
    {
        if (!previewPredictPierceUsingWorldMaterialHP)
            return previewAssumePierceWhenUnknown;

        if (col == null) return previewAssumePierceWhenUnknown;

        // Caso "ideal": WorldMaterial define HP real
        WorldMaterial wm = col.GetComponentInParent<WorldMaterial>();
        if (wm != null)
        {
            if (wm.indestructible) return false;
            return incomingDamage >= wm.structuralHP;
        }

        // Si no hay WorldMaterial, no podemos saber.
        return previewAssumePierceWhenUnknown;
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
            // Importante: este método se usa dentro del tick de bounce (ya movimos), así que NO hacemos MovePosition aquí.
            // Solo devolvemos “cuánto se puede empujar” y el caller ajusta la posición virtual.
            return allowed;
        }

        float tiny = Mathf.Min(pierceSeparationMin, distance);
        if (tiny > 0f)
        {
            RaycastHit2D block = Physics2D.CircleCast(origin, ballRadius, dir, tiny + skin, mask);
            if (!block.collider)
                return tiny;
        }

        return 0f;
    }

    private float PreviewSafeAdvanceWithoutEnteringWorldSolids(Vector2 origin, Vector2 dir, float distance, Collider2D ignoreCol)
    {
        if (distance <= 0f) return 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        int mask = worldSolidLayers.value;
        if (mask == 0) mask = bounceLayers.value;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, ballRadius, dir, distance + skin, mask);

        float allowed = distance;
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h.collider) continue;
            if (h.collider.isTrigger) continue;
            if (ignoreCol != null && h.collider == ignoreCol) continue;

            allowed = Mathf.Min(allowed, Mathf.Max(0f, h.distance - skin));
        }

        return allowed;
    }

    // ======================================================================
    // LLAMA
    // ======================================================================

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
        if (flameCostStart > 0f) return flame >= flameCostStart;
        return flame > 0f;
    }

    private void CheckOutOfFlameAndEndIfNeeded()
    {
        if (!useFlame || !endBounceWhenOutOfFlame) return;
        if (flame > 0f) return;
        EndBounce();
    }

    // ======================================================================
    // ESTADOS AIM / BOUNCE
    // ======================================================================

    private void StartAiming()
    {
        isAiming = true;

        lastAimDir = Vector2.right;

        movement.movementLocked = true;
        aimStartPosition = rb.position;

        fixedStepSize = maxDistance / Mathf.Max(1, previewSegments);
        UpdatePreview();
    }

    private void HandleAiming()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector2 raw = new Vector2(x, y);

        bool hasUserInput =
            Mathf.Abs(x) >= aimInputDeadzone ||
            Mathf.Abs(y) >= aimInputDeadzone;


        Vector2 dir;

        if (hasUserInput)
        {
            // 1) El jugador manda (modo normal 8 dirs)
            usingDefaultPickupAim = false;

            dir = raw.normalized;
            if (snapAimTo8Dirs) dir = Quantize8Dirs(dir);
            lastAimDir = dir;
        }
        else
        {
            // 2) Sin input: por defecto -> pickup más cercano (dirección libre) si es alcanzable
            float reach = (previewDistance > 0.01f) ? previewDistance : maxDistance;

            if (aimAssistPickups && pickupLayers.value != 0 &&
                TryGetNearestPickupAnchor(rb.position, reach, out Vector2 a))
            {
                Vector2 to = a - rb.position;

                if (to.sqrMagnitude > 0.0001f && to.magnitude <= reach + 0.0001f)
                {
                    // CLAVE: dirección LIBRE al pickup (NO 8 dirs)
                    usingDefaultPickupAim = true;
                    lastAimDir = to.normalized;
                }
                else
                {
                    usingDefaultPickupAim = false;
                    lastAimDir = Vector2.right;
                }
            }
            else
            {
                usingDefaultPickupAim = false;
                lastAimDir = Vector2.right;
            }
        }

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

        movement.movementLocked = true;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        impactedThisBounce.Clear();
        piercedThisBounce.Clear();

        flameSpentThisBounce = 0f;
        remainingDistance = maxDistance;

        ClearPreview();

        lastAimDir = (lastAimDir.sqrMagnitude > 0.0001f) ? lastAimDir.normalized : Vector2.right;
        if (snapAimTo8Dirs) lastAimDir = Quantize8Dirs(lastAimDir);

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

    // ======================================================================
    // QUANTIZE (solo 8 dirs: + y X)
    // ======================================================================

    private Vector2 Quantize8Dirs(Vector2 v)
    {
        float x = v.x;
        float y = v.y;

        // anti drift
        float d = Mathf.Max(0.001f, driftCancel);
        if (Mathf.Abs(x) < d) x = 0f;
        if (Mathf.Abs(y) < d) y = 0f;

        if (Mathf.Abs(x) < 0.0001f && Mathf.Abs(y) < 0.0001f)
            return Vector2.right;

        // 4 card + 4 diag
        if (Mathf.Abs(x) < 0.0001f)
            return (y >= 0f) ? Vector2.up : Vector2.down;

        if (Mathf.Abs(y) < 0.0001f)
            return (x >= 0f) ? Vector2.right : Vector2.left;

        float sx = (x >= 0f) ? 1f : -1f;
        float sy = (y >= 0f) ? 1f : -1f;
        return new Vector2(sx, sy).normalized;
    }

    // ======================================================================
    // PREVIEW (usa la MISMA simulación que FixedUpdate)
    // ======================================================================

    private void UpdatePreview()
    {
        if (!isAiming || previewLine == null)
        {
            ClearPreview();
            return;
        }

        Vector2 dir = (lastAimDir.sqrMagnitude > 0.001f) ? lastAimDir.normalized : Vector2.right;
        if (snapAimTo8Dirs) dir = Quantize8Dirs(dir);

        float previewLen = (previewDistance > 0.01f) ? previewDistance : maxDistance;
        float remaining = previewLen;

        float stepDist = fixedStepSize;
        if (stepDist <= 0f) stepDist = previewLen / Mathf.Max(1, previewSegments);

        // Conjunto “pierced” del preview para evitar loops
        HashSet<Collider2D> previewPierced = new HashSet<Collider2D>();

        List<Vector3> points = new List<Vector3>(previewSegments + 2);
        Vector2 pos = rb.position;
        points.Add(pos);

        int bounces = 0;
        int safety = 0;
        const int MAX_ITERS = 512;

        while (remaining > 0.0001f && bounces <= previewMaxBounces && safety++ < MAX_ITERS)
        {
            float move = Mathf.Min(stepDist, remaining);

            SimStepResult res = SimulateOneStep(
                pos,
                dir,
                move,
                bounceLayers,
                previewPierced,
                isPreview: true,
                out Vector2 newPos,
                out Vector2 newDir,
                out _,
                out bool didBounce,
                out bool didPierce,
                out float traveled);

            // Si no se movió nada y está bloqueado, rompe el loop
            if (traveled <= 0.0001f && res == SimStepResult.HitBlockedNoMove)
            {
                // añade un pelín visual para no “desaparecer”
                Vector2 tiny = pos + dir * Mathf.Min(remaining, Mathf.Max(0.05f, skin * 3f));
                points.Add(tiny);
                pos = tiny;
                remaining = 0f;
                break;
            }

            points.Add(newPos);

            remaining -= traveled;
            pos = newPos;

            if (didPierce)
            {
                // dir igual
            }
            else if (didBounce)
            {
                dir = newDir;
                bounces++;
            }

            if (remaining <= 0f) break;
        }

        // Si aún queda, extiende recto para longitud exacta
        if (remaining > 0.0001f)
        {
            Vector2 end = pos + dir * remaining;
            points.Add(end);
        }
        
        // Resample fijo para que la línea NO cambie de “larga/corta”
        // ---- PICKUP MAGNET (solo visual) ----
        EnsurePickupCircle();

        

        // Longitud efectiva (si no hay pickup, es previewLen; si hay, se recorta)
        float effectivePreviewLen = previewLen;

        // PROBE denso para evitar fallos “entre puntos”
        int targetCount = Mathf.Max(2, previewSegments + 1);
        int probeCount = Mathf.Max(targetCount, 24);
        var probe = ResamplePolylineFixedCount_Probe(points, probeCount, previewLen);

        if (TryFindFirstPickupContactCenterAlongPolyline(
                probe,
                out Vector2 pickupAnchor,
                out Vector2 pickupCenter,
                out float catchR,
                out Color pickupColor))
        {
            previewHitPickup = true;
            previewPickupAnchor = pickupAnchor;

            TrimPolylineToAnchor(probe, pickupAnchor);

            SetPickupCircleVisible(true);

            // CÍRCULO REAL: centrado en pickup, radio real de captura
            float drawR = pickupCircleUseRealCatchRadius
                ? (catchR * pickupCircleRadiusMultiplier)
                : pickupCircleRadiusOverride;

            DrawPickupCircle(pickupCenter, drawR, pickupColor);


            // (Opcional) marca el punto real donde el centro del player tocaría
            if (debugPickupDraw)
            {
                Debug.DrawLine(pickupAnchor + Vector2.left * 0.12f, pickupAnchor + Vector2.right * 0.12f, Color.red, 0f, false);
                Debug.DrawLine(pickupAnchor + Vector2.down * 0.12f, pickupAnchor + Vector2.up * 0.12f, Color.red, 0f, false);
            }

            var sampledPickup = ResamplePolylineFixedCount(probe, targetCount, PolylineLength(probe));
            sampledPickup[sampledPickup.Count - 1] = pickupAnchor;

            previewLine.positionCount = sampledPickup.Count;
            previewLine.SetPositions(sampledPickup.ToArray());
            previewLine.enabled = true;
            return;
        }

        else
        {
            previewHitPickup = false;
            previewPickupAnchor = default;
            SetPickupCircleVisible(false);
        }



        _dbgMagnetReturnedTrue = false;
        _dbgBestProbeIndex = -1;
        _dbgBestPickupName = "";
        _dbgBestOverlapCount = 0;

        if (debugPickupDraw && probe != null && probe.Count >= 2)
        {
            // Dibuja la probe (amarillo) para ver si pasa por el pickup o no.
            for (int i = 1; i < probe.Count; i++)
                Debug.DrawLine(probe[i - 1], probe[i], Color.yellow, 0f, false);
        }



        // Resample final usando longitud efectiva
        var sampled = ResamplePolylineFixedCount(points, targetCount, effectivePreviewLen);

        previewLine.positionCount = sampled.Count;
        previewLine.SetPositions(sampled.ToArray());
        previewLine.enabled = true;


    }



    private bool TryMagnetizePreviewToPickup(List<Vector3> points, out Vector2 anchor, out Color circleColor)
    {
        anchor = default;
        circleColor = (previewLine != null) ? previewLine.startColor : previewColor;

        previewHitPickup = false;
        previewPickupAnchor = default;

        bool doLog = debugPickupMagnet && (++_dbgPickupFrame % Mathf.Max(1, debugPickupEveryNFrames) == 0);
        if (doLog)
        {
            Debug.Log($"[PREVIEW MAGNET] start | points={points?.Count ?? 0} | radius={pickupMagnetRadius:F3} | layers={pickupLayers.value}");
        }


        if (!previewMagnetizePickups) return false;
        if (points == null || points.Count < 2) return false;
        if (pickupLayers.value == 0) return false;

        // Muestreo determinista: buscamos el primer punto de la polilínea
        // que entra dentro del radio del imán de algún pickup.
        float bestDistFromStart = float.PositiveInfinity;
        FlameSparkPickup bestPickup = null;

        float accDist = 0f;

        // Nota: points aquí es tu "probe" ya resampleada => spacing bastante uniforme.
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 p = points[i];

            

            // Overlap en radio del imán (incluye triggers si Physics2D.queriesHitTriggers = true, que normalmente lo está)
            _pickupFilter.layerMask = pickupLayers; // por si cambias layers en runtime
            // radio real de captura = radio del player + radio del trigger del pickup (+ un margen)
            float probePickupR = 0.15f;
            float probeCatchRadius = ballRadius + probePickupR + Mathf.Max(0.01f, skin);

            int n = Physics2D.OverlapCircle(p, probeCatchRadius, _pickupFilter, _pickupHits);



            if (doLog && n > 0)
            {
                Debug.Log($"[PREVIEW MAGNET] probe i={i} | overlap n={n} | p=({p.x:F3},{p.y:F3}) | accDist={accDist:F3}");
            }


            if (n > 0)
            {
                // Si hay varios, nos quedamos con el más cercano al punto p (por estabilidad)
                FlameSparkPickup bestHere = null;
                float bestHereSqr = float.PositiveInfinity;

                for (int k = 0; k < n; k++)
                {
                    var col = _pickupHits[k];
                    if (!col || !col.enabled) continue;

                    var hitPickup = col.GetComponentInParent<FlameSparkPickup>();
                    if (!hitPickup) continue;

                    Vector2 a = hitPickup.GetAnchorWorld();

                    float sqr = (a - p).sqrMagnitude;

                    if (sqr < bestHereSqr)
                    {
                        bestHereSqr = sqr;
                        bestHere = hitPickup;

                    }
                }

                if (doLog && n > 0)
                {
                    Debug.Log($"[PREVIEW MAGNET]  bestHere={(bestHere ? bestHere.name : "NULL")} | bestHereSqr={bestHereSqr:F6}");
                }


                if (bestHere != null)
                {
                    // Distancia recorrida aproximada hasta este punto de la polilínea
                    float distFromStart = accDist;

                    if (distFromStart < bestDistFromStart)
                    {
                        bestDistFromStart = distFromStart;
                        bestPickup = bestHere;

                        _dbgBestProbeIndex = i;
                        _dbgBestDistFromStart = distFromStart;
                        _dbgBestProbePoint = p;
                        Vector2 prev = (i > 0) ? (Vector2)points[i - 1] : p;
                        _dbgBestProbePrevPoint = prev;

                        _dbgBestPickupName = bestHere.name;
                        _dbgBestOverlapCount = n;

                        if (doLog)
                            Debug.Log($"[PREVIEW MAGNET]  SELECT bestPickup={_dbgBestPickupName} at i={_dbgBestProbeIndex} dist={_dbgBestDistFromStart:F3}");
                    }

                }
            }

            // acumula longitud hasta el siguiente punto
            if (i < points.Count - 1)
                accDist += Vector2.Distance(points[i], points[i + 1]);
        }

        if (bestPickup == null)
        {
            if (doLog)
                Debug.Log("[PREVIEW MAGNET] RESULT = FALSE (no pickup found along probe)");
            return false;
        }


        if (bestPickup == null) return false;

        // 🔴 CLAVE: NO usar el anchor interno del pickup
        // usamos el punto REAL de contacto contra el collider

        Vector2 center = bestPickup.GetAnchorWorld();

        // Radio de captura REAL (player + trigger)
        float pickupR = GetPickupTriggerRadiusWorld(bestPickup);
        float catchRadius = ballRadius + pickupR + Mathf.Max(0.01f, skin);

        // Anchor = primera intersección del segmento con el círculo de captura
        if (!TrySegmentCircleFirstIntersection(_dbgBestProbePrevPoint, _dbgBestProbePoint, center, catchRadius, out anchor))
        {
            // Si por lo que sea no hay intersección numérica, caemos al punto de probe
            anchor = _dbgBestProbePoint;
        }



        previewHitPickup = true;
        previewPickupAnchor = anchor;

        if (debugPickupDraw)
        {
            // contacto real (ROJO)
            Debug.DrawLine(anchor + Vector2.left * 0.15f, anchor + Vector2.right * 0.15f, Color.red, 0f);
            Debug.DrawLine(anchor + Vector2.down * 0.15f, anchor + Vector2.up * 0.15f, Color.red, 0f);

            // anchor original (AZUL)
            Debug.DrawLine(center, anchor, Color.blue, 0f);

        }



        _dbgMagnetReturnedTrue = true;

        if (doLog)
        {
            float d = Vector2.Distance(_dbgBestProbePoint, anchor);
            Debug.Log($"[PREVIEW MAGNET] RESULT = TRUE | pickup={bestPickup.name} | anchor=({anchor.x:F3},{anchor.y:F3}) | bestProbePointDistToAnchor={d:F3} | bestIndex={_dbgBestProbeIndex}");
        }

        if (debugPickupDraw)
        {
            // Marca el punto de probe donde “enganchó”
            Debug.DrawLine(_dbgBestProbePoint + Vector2.left * 0.08f, _dbgBestProbePoint + Vector2.right * 0.08f, Color.magenta, 0f, false);
            Debug.DrawLine(_dbgBestProbePoint + Vector2.down * 0.08f, _dbgBestProbePoint + Vector2.up * 0.08f, Color.magenta, 0f, false);

            // Marca el anchor
            Debug.DrawLine(anchor + Vector2.left * 0.12f, anchor + Vector2.right * 0.12f, Color.green, 0f, false);
            Debug.DrawLine(anchor + Vector2.down * 0.12f, anchor + Vector2.up * 0.12f, Color.green, 0f, false);

            // Línea entre ambos
            Debug.DrawLine(_dbgBestProbePoint, anchor, Color.green, 0f, false);
        }

        return true;

    }





    private bool TryFindFirstPickupContactCenterAlongPolyline(
        List<Vector3> poly,
        out Vector2 playerCenterAtContact,
        out Vector2 pickupCenter,
        out float catchRadius,
        out Color circleColor)
    {
        playerCenterAtContact = default;
        pickupCenter = default;
        catchRadius = 0f;
        circleColor = (previewLine != null) ? previewLine.startColor : previewColor;

        if (!previewMagnetizePickups) return false;
        if (poly == null || poly.Count < 2) return false;
        if (pickupLayers.value == 0) return false;

        _pickupFilter.useLayerMask = true;
        _pickupFilter.layerMask = pickupLayers;
        _pickupFilter.useTriggers = true;
        _pickupFilter.useDepth = false;

        float bestDistFromStart = float.PositiveInfinity;

        float acc = 0f;

        for (int i = 0; i < poly.Count - 1; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[i + 1];
            Vector2 ab = b - a;

            float segLen = ab.magnitude;
            if (segLen < 1e-5f) continue;

            Vector2 dir = ab / segLen;

            int hitCount = Physics2D.CircleCast(a, _pickupCastRadius, dir, _pickupFilter, _pickupCastHits, segLen);

            if (hitCount > 0)
            {
                for (int h = 0; h < hitCount; h++)
                {
                    var hit = _pickupCastHits[h];
                    if (!hit.collider) continue;

                    var p = hit.collider.GetComponentInParent<FlameSparkPickup>();
                    if (!p) continue;

                    float distFromStart = acc + hit.distance;
                    if (distFromStart < bestDistFromStart)
                    {
                        bestDistFromStart = distFromStart;

                        // Centro del player cuando toca (esto es lo que debes recortar en la polyline)
                        playerCenterAtContact = a + dir * hit.distance;

                        // Centro del pickup (para dibujar círculo real de captura)
                        pickupCenter = p.GetAnchorWorld();

                        // Radio real de captura (player "cast radius" + radio trigger pickup)
                        float pickupR = GetPickupTriggerRadiusWorld(p);
                        catchRadius = _pickupCastRadius + pickupR + Mathf.Max(0.01f, skin);

                        // Debug reutilizando tus campos
                        _dbgBestProbeIndex = i;
                        _dbgBestDistFromStart = distFromStart;
                        _dbgBestProbePoint = playerCenterAtContact;
                        _dbgBestProbePrevPoint = a;
                        _dbgBestPickupName = p.name;
                    }
                }
            }

            acc += segLen;
        }

        if (bestDistFromStart == float.PositiveInfinity) return false;

        previewHitPickup = true;
        previewPickupAnchor = playerCenterAtContact;
        return true;
    }





    private static bool TrySegmentCircleFirstIntersection(
        Vector2 a, Vector2 b,
        Vector2 center, float radius,
        out Vector2 hit)
    {
        hit = default;

        Vector2 d = b - a;
        Vector2 f = a - center;

        float r = Mathf.Max(0.0001f, radius);

        float A = Vector2.Dot(d, d);
        if (A < 1e-8f) return false;

        float B = 2f * Vector2.Dot(f, d);
        float C = Vector2.Dot(f, f) - r * r;

        float disc = B * B - 4f * A * C;
        if (disc < 0f) return false;

        float sqrt = Mathf.Sqrt(disc);

        // dos soluciones paramétricas en la recta
        float t1 = (-B - sqrt) / (2f * A);
        float t2 = (-B + sqrt) / (2f * A);

        // buscamos la primera intersección dentro del segmento [0..1]
        float t = float.PositiveInfinity;

        if (t1 >= 0f && t1 <= 1f) t = t1;
        if (t2 >= 0f && t2 <= 1f) t = Mathf.Min(t, t2);

        if (!float.IsFinite(t) || t == float.PositiveInfinity) return false;

        hit = a + d * t;
        return true;
    }


    private float GetPickupCatchRadius(FlameSparkPickup pickup)
    {
        if (!pickup) return ballRadius;

        Collider2D col = pickup.GetComponentInChildren<Collider2D>();
        if (!col) col = pickup.GetComponentInParent<Collider2D>();

        if (col is CircleCollider2D cc)
        {
            float s = Mathf.Max(cc.transform.lossyScale.x, cc.transform.lossyScale.y);
            return ballRadius + cc.radius * s;
        }

        // fallback: bounds
        return ballRadius + Mathf.Max(col.bounds.extents.x, col.bounds.extents.y);
    }



    private float GetPickupTriggerRadiusWorld(FlameSparkPickup pickup)
    {
        if (pickup == null) return 0.15f;

        // Intentamos estimar el “radio” del trigger del pickup en mundo.
        // Esto funciona para CircleCollider2D y también “aproxima” para otros.
        var c = pickup.GetComponentInChildren<Collider2D>();
        if (!c) c = pickup.GetComponentInParent<Collider2D>();
        if (!c) return 0.15f;

        if (c is CircleCollider2D cc)
        {
            // radio en mundo (escala)
            float s = Mathf.Max(cc.transform.lossyScale.x, cc.transform.lossyScale.y);
            return Mathf.Abs(cc.radius * s);
        }

        // Aproximación: usamos el mayor semieje de bounds (en mundo)
        var b = c.bounds;
        return Mathf.Max(b.extents.x, b.extents.y);
    }

    private bool ValidatePickupAtAnchor(Vector2 anchor, FlameSparkPickup expected)
    {
        // validación final: en el anchor, realmente estamos dentro del "radio de captura".
        _pickupFilter.layerMask = pickupLayers;

        int n = Physics2D.OverlapCircle(anchor, 0.001f, _pickupFilter, _pickupHits);
        if (n <= 0) return false;

        for (int i = 0; i < n; i++)
        {
            var hitCol = _pickupHits[i];
            if (!hitCol || !hitCol.enabled) continue;

            var hitPickup = hitCol.GetComponentInParent<FlameSparkPickup>();
            if (hitPickup == expected) return true;
        }

        return false;
    }







    #pragma warning disable
    public bool TryGetNearestPickupAnchor(Vector2 center, float radius, out Vector2 anchor)
    {
        anchor = default;
        if (pickupLayers.value == 0) return false;

        // Si te llaman con radius <= 0, usa el radio por defecto del aim assist
        float searchRadius = (radius > 0f) ? radius : aimAssistPickupRadius;
        if (searchRadius <= 0f) return false;

        // Rellenamos el array reutilizable con los pickups que caen dentro del radio
        int count = Physics2D.OverlapCircleNonAlloc(center, searchRadius, _pickupHits, pickupLayers);
        if (count <= 0) return false;

        float bestSqr = float.PositiveInfinity;
        FlameSparkPickup best = null;

        for (int i = 0; i < count; i++)
        {
            var col = _pickupHits[i];
            if (!col || !col.enabled) continue;

            var p = col.GetComponentInParent<FlameSparkPickup>();
            if (!p) continue;

            Vector2 a = p.GetAnchorWorld();
            float sqr = (a - center).sqrMagnitude;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = p;
                anchor = a;
            }
        }

        return best != null;
    }
    #pragma warning restore



    private void ApplyMagnetCurve(List<Vector3> points, Vector2 anchor)
    {
        int tail = Mathf.Clamp(pickupMagnetTailPoints, 2, 8);
        int start = Mathf.Max(0, points.Count - tail);

        // Fuerza real (permite “semi-imán” sin enganchar del todo)
        float s = Mathf.Clamp01(pickupMagnetStrength);

        for (int i = start; i < points.Count; i++)
        {
            float t = (i - start) / (float)Mathf.Max(1, (points.Count - 1 - start));
            // ease-in hacia el final: redondea sin “codo”
            float ease = t * t;

            Vector2 p = points[i];
            Vector2 target = Vector2.Lerp(p, anchor, s);
            Vector2 curved = Vector2.Lerp(p, target, ease);

            points[i] = curved;
        }

        // Asegura que el final cae exactamente en el anchor (si s=1)
        if (s >= 0.999f)
            points[points.Count - 1] = anchor;
    }


    private List<Vector3> ResamplePolylineFixedCount(List<Vector3> src, int targetCount, float totalLength)
    {
        List<Vector3> outPts = new List<Vector3>(Mathf.Max(2, targetCount));
        if (src == null || src.Count == 0) return outPts;
        if (targetCount < 2) targetCount = 2;

        // Longitudes acumuladas
        List<float> cum = new List<float>(src.Count);
        cum.Add(0f);

        float acc = 0f;
        for (int i = 1; i < src.Count; i++)
        {
            acc += Vector3.Distance(src[i - 1], src[i]);
            cum.Add(acc);
        }

        float maxLen = Mathf.Max(0.0001f, Mathf.Min(totalLength, acc));
        float step = maxLen / (targetCount - 1);

        int seg = 0;
        for (int k = 0; k < targetCount; k++)
        {
            float d = Mathf.Min(maxLen, step * k);

            while (seg < cum.Count - 2 && cum[seg + 1] < d)
                seg++;

            float d0 = cum[seg];
            float d1 = cum[seg + 1];
            float t = (d1 <= d0) ? 0f : (d - d0) / (d1 - d0);

            Vector3 p = Vector3.Lerp(src[seg], src[seg + 1], t);
            outPts.Add(p);
        }

        return outPts;
    }

    private static float PolylineLength(List<Vector3> pts)
    {
        if (pts == null || pts.Count < 2) return 0f;
        float len = 0f;
        for (int i = 1; i < pts.Count; i++)
            len += Vector3.Distance(pts[i - 1], pts[i]);
        return len;
    }

    private static void TrimPolylineToAnchor(List<Vector3> pts, Vector2 anchor)
    {
        if (pts == null || pts.Count < 2) return;

        // Encuentra el segmento (i -> i+1) cuya proyección al anchor sea la más cercana
        int bestSeg = -1;
        float bestSqr = float.PositiveInfinity;
        float bestT = 0f;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[i + 1];
            Vector2 ab = b - a;

            float abLenSqr = ab.sqrMagnitude;
            if (abLenSqr < 1e-8f) continue;

            float t = Vector2.Dot(anchor - a, ab) / abLenSqr;
            t = Mathf.Clamp01(t);

            Vector2 proj = a + ab * t;
            float sqr = (proj - anchor).sqrMagnitude;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestSeg = i;
                bestT = t;
            }
        }

        if (bestSeg < 0) return;

        // Recorta todo después del segmento ganador
        int cutIndex = bestSeg + 1;
        if (cutIndex < pts.Count - 1)
            pts.RemoveRange(cutIndex + 1, pts.Count - (cutIndex + 1));

        // Fuerza el último punto EXACTO al anchor
        pts[pts.Count - 1] = anchor;
    }


    private List<Vector3> ResamplePolylineFixedCount_Probe(List<Vector3> src, int probeCount, float totalLength)
    {
        // Simple wrapper para no duplicar lógica mental
        return ResamplePolylineFixedCount(src, Mathf.Max(2, probeCount), totalLength);
    }


    private void ClearPreview()
    {
        if (previewLine != null)
        {
            previewLine.enabled = false;
            previewLine.positionCount = 0;
        }

        if (pickupPreviewCircle != null)
        {
            pickupPreviewCircle.enabled = false;
            // NO: pickupPreviewCircle.positionCount = 0;
        }
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

        previewLine.startColor = previewColor;
        previewLine.endColor = previewColor;

        previewLine.sortingLayerName = "Default";
        previewLine.sortingOrder = 100;

        previewLine.positionCount = 0;
        previewLine.enabled = false;
    }

    // ======================================================================
    // IGNORE COLLISIONS DURING AIM/BOUNCE
    // ======================================================================

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


    private Vector2 ComputeBouncedDir(Vector2 incomingDir, Vector2 normal)
    {
        // 1) Normaliza dirección de entrada
        if (incomingDir.sqrMagnitude < 1e-4f)
            incomingDir = Vector2.right;
        else
            incomingDir = incomingDir.normalized;

        // 2) Normal “limpia” de la superficie (filtra dientes de minitiles
        //    pero deja diagonales reales cuando tocan)
        Vector2 cleanNormal = QuantizeSurfaceNormal(normal);

        // 3) Rebote físico sobre esa normal
        Vector2 reflected = Vector2.Reflect(incomingDir, cleanNormal);

        // 4) Ajuste final a tus 8 direcciones jugables
        return Quantize8Dirs(reflected);
    }

    /// <summary>
    /// Cuantiza la normal física a 4 ó 8 direcciones “macro”:
    /// - Si el ángulo está muy cerca de 45° => diagonal pura (±1, ±1).
    /// - En caso contrario => pared (±1,0) o suelo/techo (0,±1).
    /// Así filtramos dientes de minitiles pero conservamos diagonales reales.
    /// </summary>
    private Vector2 QuantizeSurfaceNormal(Vector2 n)
    {
        if (n.sqrMagnitude < 1e-6f)
            return Vector2.up;

        n = n.normalized;

        float ax = Mathf.Abs(n.x);
        float ay = Mathf.Abs(n.y);

        // Tolerancia para considerar que es “de verdad” diagonal (casi 45°).
        // Si ax y ay son parecidos -> diagonal. Si uno domina claramente -> cardinal.
        const float DIAG_TOL = 0.15f; // puedes exponerlo en el inspector si quieres afinar

        if (Mathf.Abs(ax - ay) <= DIAG_TOL)
        {
            // Diagonal real: nos quedamos con el signo de cada componente
            float sx = Mathf.Sign(n.x);
            float sy = Mathf.Sign(n.y);
            return new Vector2(sx, sy).normalized;
        }

        // No es diagonal “pura”: lo tratamos como pared o suelo/techo
        if (ax > ay)
        {
            // pared izquierda/derecha
            return new Vector2(Mathf.Sign(n.x), 0f);
        }
        else
        {
            // suelo/techo
            return new Vector2(0f, Mathf.Sign(n.y));
        }
    }


    /// <summary>
    /// Convierte la normal “fea” del Composite (0.73,0.68, etc.)
    /// en una normal de grid: (±1,0) o (0,±1).
    /// </summary>
    private Vector2 CleanGridNormal(Vector2 raw)
    {
        if (raw.sqrMagnitude < 1e-5f)
            return Vector2.up;

        raw.Normalize();

        float ax = Mathf.Abs(raw.x);
        float ay = Mathf.Abs(raw.y);

        // Si la normal está más cerca de vertical, la colapsamos a (0,±1).
        // Si está más cerca de horizontal, la colapsamos a (±1,0).
        if (ax > ay)
            return new Vector2(Mathf.Sign(raw.x), 0f);
        else
            return new Vector2(0f, Mathf.Sign(raw.y));
    }


    private readonly Collider2D[] _pickupHits = new Collider2D[64];
    private ContactFilter2D _pickupFilter;

    private readonly RaycastHit2D[] _pickupCastHits = new RaycastHit2D[32];



    private void EnsurePickupCircle()
    {
        if (!previewShowPickupCircle) return;

        if (pickupPreviewCircle == null)
        {
            // crea un GO hijo para no ensuciar el mismo LR del preview
            var go = new GameObject("BouncePreview_PickupCircle");
            go.transform.SetParent(null); // en world space, da igual; si prefieres, SetParent(transform)
            pickupPreviewCircle = go.AddComponent<LineRenderer>();
        }

        pickupPreviewCircle.useWorldSpace = true;
        pickupPreviewCircle.loop = true;
        pickupPreviewCircle.positionCount = Mathf.Max(8, pickupCircleSegments);
        pickupPreviewCircle.startWidth = pickupCircleWidth;
        pickupPreviewCircle.endWidth = pickupCircleWidth;

        var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.color = previewColor; // reusa tu color actual de preview
        pickupPreviewCircle.material = mat;

        pickupPreviewCircle.startColor = previewColor;
        pickupPreviewCircle.endColor = previewColor;

        pickupPreviewCircle.sortingLayerName = pickupCircleSortingLayer;
        pickupPreviewCircle.sortingOrder = pickupCircleSortingOrder;

        pickupPreviewCircle.enabled = false;
    }

    private void SetPickupCircleVisible(bool on)
    {
        if (pickupPreviewCircle == null) return;

        pickupPreviewCircle.enabled = on;

        // NO pongas positionCount=0 al apagarlo: eso lo "mata" y dependes de que
        // en el mismo frame se vuelva a rellenar. Así es como te falla "a veces".
        if (on)
        {
            int segs = Mathf.Max(8, pickupCircleSegments);
            if (pickupPreviewCircle.positionCount != segs)
                pickupPreviewCircle.positionCount = segs;
        }
    }



    private void DrawPickupCircle(Vector2 center, float radius, Color c)
    {
        if (pickupPreviewCircle == null) return;

        pickupPreviewCircle.startColor = c;
        pickupPreviewCircle.endColor = c;
        if (pickupPreviewCircle.material != null) pickupPreviewCircle.material.color = c;

        int segs = Mathf.Max(8, pickupCircleSegments);
        if (pickupPreviewCircle.positionCount != segs) pickupPreviewCircle.positionCount = segs;

        float r = Mathf.Max(0.01f, radius);
        for (int i = 0; i < segs; i++)
        {
            float t = i / (float)segs;
            float a = t * Mathf.PI * 2f;
            pickupPreviewCircle.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(a) * r,
                center.y + Mathf.Sin(a) * r,
                0f
            ));
        }
    }



}



// TEST 123
// TEST 123
// TEST 123
// TEST 123
// TEST 123

