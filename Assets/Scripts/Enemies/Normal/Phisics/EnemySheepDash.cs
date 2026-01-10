using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemySheepDash : MonoBehaviour, IPiercingBounceReceiver
{

    [Header("Divagación (oveja)")]
    public float wanderSpeed = 2f;          // velocidad al divagar
    public float dirSwitchInterval = 2f;    // cada cuánto cambia de dirección (segundos)
    public float stuckPosTolerance = 0.01f; // umbral para considerar que no avanza en X
    public float stuckTimeToFlip = 0.3f;    // tiempo atascado antes de cambiar de lado

    [Header("Detección Player (solo distancia X)")]
    public float visionRange = 8f;          // rango horizontal para activar dash

    [Header("Dash")]
    public float dashSpeed = 12f;
    public float dashDuration = 0.25f;
    public float dashCooldown = 1.2f;

    [Header("Daño al jugador")]
    public int contactDamage = 1;

    [Header("Vida")]
    public int maxHP = 1;

    private int currentHP;


    [Header("Debug")]
    public bool debugInfo = false;

    private Rigidbody2D rb;
    private Transform player;

    private float currentDir = 1f;
    private float dirTimer = 0f;

    private float dashTimer = 0f;
    private float cooldownTimer = 0f;

    private float lastX;
    private float stuckTimer = 0f;

    private enum State { Wander, Dashing, Cooldown }
    private State state = State.Wander;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Que NO rote nunca ni haga piruetas
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
            player = p.transform;

        // dirección inicial random
        currentDir = (Random.value < 0.5f) ? -1f : 1f;
        lastX = transform.position.x;
        currentHP = maxHP;

    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        dirTimer += dt;

        switch (state)
        {
            case State.Wander:
                UpdateWander(dt);
                TryStartDash();
                break;

            case State.Dashing:
                UpdateDash(dt);
                break;

            case State.Cooldown:
                UpdateCooldown(dt);
                break;
        }

        if (debugInfo)
        {
            Debug.DrawLine(transform.position,
                transform.position + Vector3.right * currentDir,
                Color.yellow, 0f, false);
        }
    }

    // ====================
    // ESTADO: WANDER
    // ====================
    private void UpdateWander(float dt)
    {
        // mover en X
        ApplyHorizontalSpeed(currentDir * wanderSpeed);

        // cambiar dirección cada cierto tiempo
        if (dirTimer >= dirSwitchInterval)
        {
            FlipDir();
            dirTimer = 0f;
        }

        // comprobar si está atascado (no cambia su X)
        float deltaX = Mathf.Abs(transform.position.x - lastX);
        if (deltaX < stuckPosTolerance)
        {
            stuckTimer += dt;
            if (stuckTimer >= stuckTimeToFlip)
            {
                // lleva un rato sin avanzar: cambia de lado
                FlipDir();
                stuckTimer = 0f;
                dirTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastX = transform.position.x;
    }

    // ====================
    // DISPARO DEL DASH
    // ====================
    private void TryStartDash()
    {
        if (player == null) return;
        if (cooldownTimer > 0f) return;

        float dx = player.position.x - transform.position.x;
        float adx = Mathf.Abs(dx);

        if (adx <= visionRange)
        {
            // empezar dash hacia el lado del player
            currentDir = Mathf.Sign(dx);
            state = State.Dashing;
            dashTimer = dashDuration;

            ApplyHorizontalSpeed(currentDir * dashSpeed);
        }
    }

    // ====================
    // ESTADO: DASH
    // ====================
    private void UpdateDash(float dt)
    {
        dashTimer -= dt;

        // mantener solo la X a velocidad de dash
        ApplyHorizontalSpeed(currentDir * dashSpeed);

        if (dashTimer <= 0f)
        {
            state = State.Cooldown;
            cooldownTimer = dashCooldown;
        }
    }

    // ====================
    // ESTADO: COOLDOWN
    // ====================
    private void UpdateCooldown(float dt)
    {
        cooldownTimer -= dt;

        // se queda quieto en X, deja que la gravedad haga lo suyo
        ApplyHorizontalSpeed(0f);

        if (cooldownTimer <= 0f)
        {
            state = State.Wander;
            dirTimer = 0f;
            stuckTimer = 0f;
            lastX = transform.position.x;
        }
    }

    // ====================
    // UTILIDADES MOVIMIENTO
    // ====================
    private void ApplyHorizontalSpeed(float speedX)
    {
        rb.linearVelocity = new Vector2(speedX, rb.linearVelocity.y);
    }

    private void FlipDir()
    {
        currentDir = -currentDir;
    }

    // ====================
    // DAÑO AL PLAYER (TRIGGER)
    // ====================
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // respetar la invencibilidad del bounce
        PlayerBounceAttack bounce = other.GetComponent<PlayerBounceAttack>();
        if (bounce != null && bounce.invincibleDuringBounce && bounce.isInvincible)
            return;

        PlayerHealth hp = other.GetComponentInParent<PlayerHealth>();
        if (hp != null)
            hp.TakeDamage(contactDamage);
    }

    // ====================
    // CONTROLAR REBOTE FÍSICO DEL PLAYER
    // ====================
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player")) return;

        Rigidbody2D playerRb = collision.rigidbody;
        if (playerRb == null) return;

        // Cortar la componente de velocidad del player en la normal de impacto
        // para evitar "pelotazo" hacia atrás al hacer Spark.
        ContactPoint2D contact = collision.GetContact(0);
        Vector2 n = contact.normal.normalized;

        Vector2 v = playerRb.linearVelocity;
        float vn = Vector2.Dot(v, n);

        // Solo si va "entrando" en la normal (vn > 0), se cancela esa parte.
        if (vn > 0f)
        {
            v -= vn * n;
            playerRb.linearVelocity = v;
        }
    }

        // ======================================================
        //  BOUNCE ATTACK: esta oveja se DEJA atravesar
        // ======================================================
    public bool ApplyPiercingBounce(BounceImpactData impact, float incomingDamage, out float remainingDamage)
    {
        // Daño que viene del bounce (ya viene entera la “bala” aquí)
        int dmg = Mathf.Max(0, impact.damage);

        // Si por lo que sea viene 0, no hacemos nada pero seguimos atravesando
        if (dmg <= 0 || currentHP <= 0)
        {
            remainingDamage = incomingDamage;
            return true; // sigue atravesando igual
        }

        int hpBefore = currentHP;

        // Aplicamos daño al enemigo
        int applied = Mathf.Min(hpBefore, dmg);
        currentHP = hpBefore - applied;

        if (currentHP <= 0)
        {
            Die();
        }

        // El “daño restante” es lo que le sobra al proyectil-bounce
        // para seguir atravesando otras cosas
        remainingDamage = Mathf.Max(0f, incomingDamage - applied);

        // CLAVE: esta oveja SIEMPRE se deja atravesar físicamente.
        // Nunca bloquea el bounce.
        return true;
    }


    private void Die()
    {
        // Aquí luego metes animación, sonido, drop, etc.
        Destroy(gameObject);
    }

}


