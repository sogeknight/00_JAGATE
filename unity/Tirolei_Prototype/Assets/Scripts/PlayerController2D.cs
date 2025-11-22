using UnityEngine;
using UnityEngine.SceneManagement;   // *** NUEVO
using System.Collections; 

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(TrailRenderer))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 6f;
    public float jumpForce = 14f;

    [Range(0f, 1f)]
    public float jumpCutMultiplier = 0.5f;
    [Header("Wall Jump Tuning")]
    [Range(0f, 1f)]
    public float wallJumpCutMultiplier = 0.5f;
    [Header("Wall Jump Press Tuning")]
    public float wallJumpMaxHoldTime = 0.35f;   // más margen para cargar

    private float wallJumpElapsed = 0f;        // tiempo que llevas desde el wall jump
    public float wallJumpMinHorizontal = 5f;   // velocidad mínima en X al saltar de pared
    public float wallJumpMaxHorizontal = 13f;  // velocidad máxima en X al saltar de pared

    private float currentWallJumpDir = 0f;     // dirección del wall jump actual




    [Header("Coyote Time / Jump Buffer")]
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.1f;

    [Header("Gravedad avanzada")]
    // Multiplicador de gravedad cuando CAES (y < 0)
    public float fallGravityMultiplier = 2.5f;

    // Multiplicador extra cuando subes pero YA has soltado el botón de salto.
    // Si no quieres low jump todavía, déjalo en 1.
    public float lowJumpGravityMultiplier = 2f;

    // Velocidad máxima en caída (valor NEGATIVO)
    public float maxFallSpeed = -20f;


        [Header("Habilidades")]
    public bool canDoubleJump = false;
    public bool canWallGrab  = false;

    [Header("Wall Grab / Slide / Jump")]
    public float wallSlideSpeed = -2.5f;          // velocidad máxima bajando por pared
    public Vector2 wallJumpForce = new Vector2(10f, 14f);

    // 🔹 Tiempo de bloqueo del control horizontal tras wall jump
    public float wallJumpControlLockTime = 0.15f;

    // Estado interno de habilidades
    private bool hasUsedDoubleJump = false;
    private bool isOnWall = false;
    private float lastWallNormalX = 0f;
    private bool isWallJumping = false;


    // 🔹 Contador interno del lock
    private float wallJumpLockTimer = 0f;




    [Header("Vida / Vidas / Respawn")]
    public int maxHealth = 3;              // número de vidas totales
    public string hazardTag = "Hazard";    // tag de pinchos, lava, etc.

    [Header("Daño / Invencibilidad")]
    public float invincibilityTime = 1.5f; // tiempo invencible tras daño/muerte
    public float flashInterval = 0.1f;     // velocidad del parpadeo

    [Header("Daño por Hazard")]
    public float hazardTickInterval = 0.25f;  // cada cuánto hace daño si sigues encima
    private float nextHazardDamageTime = 0f;

    [Header("Caída al vacío")]
    public float fallDeathY = -20f;           // *** NUEVO: altura límite para considerar caída
    private bool fallDamagePending = false;   // *** NUEVO

    private int currentHealth;        // vidas actuales
    private Vector3 spawnPosition;

    private bool isInHazard = false;
    private bool isInvincible = false;
    private bool isDead = false;
    private SpriteRenderer sr;
    private Coroutine invCoroutine;

    private Rigidbody2D rb;
    private TrailRenderer tr;

    // Contactos con el suelo (solo si el golpe viene desde abajo)
    private int groundContacts = 0;
    private bool isGrounded => groundContacts > 0;

    // Timers internos
    private float coyoteTimer = 0f;
    private float jumpBufferTimer = 0f;

    // 🔹 Último input horizontal del jugador
    private float lastMoveInputX = 0f;


    private float lastWallJumpDir = 0f;
    private bool justTouchedWall = false;

    [Header("Wall Grab Timing")]
    public float minAirTimeBeforeWallGrab = 0.06f;  // tiempo mínimo en el aire antes de permitir pared
    private float airTimeSinceLeftGround = 0f;      // cuánto llevas sin suelo


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        tr = GetComponent<TrailRenderer>();
        sr = GetComponent<SpriteRenderer>(); 

        // Guardamos la posición inicial del player como punto de respawn
        spawnPosition = transform.position;
    }

    void Start()
    {
        // Forzar mínimo 3 vidas
        maxHealth = Mathf.Max(3, maxHealth);

        // Configurar rastro
        tr.time = 0.12f;
        tr.startWidth = 0.25f;
        tr.endWidth = 0.05f;
        tr.startColor = Color.white;
        tr.endColor = new Color(1f, 1f, 1f, 0f);

        // Vidas iniciales
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (isDead) return;

        // === MOVIMIENTO HORIZONTAL ===
        float xInput = Input.GetAxisRaw("Horizontal");

        // Guardamos el último input horizontal "real"
        if (Mathf.Abs(xInput) > 0.01f)
        {
            lastMoveInputX = xInput;
        }

        // Partimos de la velocidad actual
        Vector2 v = rb.linearVelocity;

        // Actualizamos el lock del wall jump
        if (wallJumpLockTimer > 0f)
        {
            wallJumpLockTimer -= Time.deltaTime;
        }

        // Solo tocamos v.x si YA no estamos bloqueados por wall jump
        if (wallJumpLockTimer <= 0f)
        {
            if (isGrounded || isOnWall)
            {
                // En suelo o pared → control total, como siempre
                v.x = xInput * moveSpeed;
            }
            else
            {
                // En el aire → que responda mucho más rápido al input
                float target = xInput * moveSpeed;
                float airAccel = 50f;              // prueba entre 20 y 30 para sentirlo
                float maxDelta = airAccel * Time.deltaTime;

                // MoveTowards da cambios más directos que un Lerp suave
                v.x = Mathf.MoveTowards(v.x, target, maxDelta);
            }

        }

        rb.linearVelocity = v;






        // === COYOTE TIME (con fallback por si groundContacts se ha roto) ===
        bool groundedLike = isGrounded;

        // Si por la razón que sea groundContacts está en 0 pero estás "apoyado"
        // (velocidad vertical casi 0), lo tratamos como suelo SOLO si NO estás en pared.
        if (!groundedLike && !isOnWall && Mathf.Abs(rb.linearVelocity.y) < 0.01f)
        {
            groundedLike = true;
        }


        if (groundedLike)
        {
            coyoteTimer = coyoteTime;

            // Al "sentir" suelo, reseteamos doble salto
            hasUsedDoubleJump = false;

            // Salimos del estado de wall jump, pero NO tocamos isOnWall.
            isWallJumping = false;

            // Recién en suelo → tiempo en el aire = 0
            airTimeSinceLeftGround = 0f;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
            airTimeSinceLeftGround += Time.deltaTime;
        }





        // === JUMP BUFFER ===
        if (Input.GetKeyDown(KeyCode.Z))
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        // 🔒 Clamp para evitar valores negativos raros
        if (coyoteTimer < 0f) coyoteTimer = 0f;
        if (jumpBufferTimer < 0f) jumpBufferTimer = 0f;


        // === SALTO PRINCIPAL USANDO COYOTE + BUFFER ===
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
        // === DOBLE SALTO (solo si está desbloqueado) ===
        // NO permitir doble salto mientras estás pegado a una pared
        else if (canDoubleJump && jumpBufferTimer > 0f && !isGrounded && !isOnWall && !hasUsedDoubleJump)
        {
            jumpBufferTimer = 0f;
            hasUsedDoubleJump = true;

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
        // Si acabamos de tocar pared, limpiamos el flag.
        // Si además tenías Z pulsado, solo rellenamos el buffer una vez.
        if (justTouchedWall)
        {
            justTouchedWall = false;

            if (Input.GetKey(KeyCode.Z))
            {
                jumpBufferTimer = jumpBufferTime;
            }
}




        if (canWallGrab && isOnWall && !groundedLike &&
            Input.GetKeyDown(KeyCode.Z))


        {



            // Dirección base: pared + último input
            float baseDir = (lastWallJumpDir != 0f)
                ? lastWallJumpDir
                : (lastMoveInputX != 0f ? Mathf.Sign(lastMoveInputX) : 1f);
            float jumpDir = baseDir;

            // Separación un poco mayor para salir claramente del collider de la pared
            float separation = 0.15f;
            transform.position += new Vector3(jumpDir * separation, 0f, 0f);

            isOnWall = false;
            groundContacts = 0;
            coyoteTimer = 0f;

            // Impulso inicial: vertical + HORIZONTAL MÍNIMO
            rb.linearVelocity = new Vector2(wallJumpMinHorizontal * jumpDir, wallJumpForce.y);

            // Iniciamos modo "estoy en wall jump"
            isWallJumping      = true;
            hasUsedDoubleJump  = false;  // <--- habilita doble salto tras impulso
            wallJumpElapsed    = 0f;     // <--- NO 0.3
            currentWallJumpDir = jumpDir;



            wallJumpLockTimer = wallJumpControlLockTime;

            // consumimos el buffer
            jumpBufferTimer   = 0f;
        }











        if (isWallJumping)
        {
            wallJumpElapsed += Time.deltaTime;

            // si el jugador apunta hacia la pared → cancelar impulso rápido y reenganchar
            float xDir = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(xDir) > 0.1f &&
                Mathf.Sign(xDir) == -Mathf.Sign(currentWallJumpDir) &&
                wallJumpElapsed > 0.03f) // antes era 0.08 → demasiado lento
            {
                isWallJumping = false;
                wallJumpLockTimer = 0f;
                return;
            }

            // impulso progresivo
            float t = Mathf.Clamp01(wallJumpElapsed / wallJumpMaxHoldTime);
            float curvedT = 1f - (1f - t) * (1f - t);
            float targetHorizontal = Mathf.Lerp(wallJumpMinHorizontal, wallJumpMaxHorizontal, curvedT);

            Vector2 vel = rb.linearVelocity;
            vel.x = currentWallJumpDir * targetHorizontal;
            rb.linearVelocity = vel;

            // fin del estado incluso si el jugador no suelta nada
            if (wallJumpElapsed >= wallJumpMaxHoldTime)
            {
                isWallJumping = false;
                wallJumpLockTimer = 0f;
            }
        }









        // === CORTE DE SALTO NORMAL (suelo / doble) ===
        if (!isWallJumping && Input.GetKeyUp(KeyCode.Z) && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                rb.linearVelocity.y * (1f - jumpCutMultiplier)
            );
        }






        // === DAÑO CONTINUO POR HAZARD MIENTRAS ESTÁS ENCIMA ===
        if (isInHazard)
        {
            TryApplyHazardDamage();
        }

        // === MUERTE POR CAÍDA AL VACÍO === *** NUEVO
        if (!isDead)
        {
            if (!fallDamagePending && transform.position.y < fallDeathY)
            {
                fallDamagePending = true;
                TakeDamage(1);   // cuenta como una vida perdida
            }
            else if (transform.position.y >= fallDeathY)
            {
                // Volvemos a zona segura: permitir que vuelva a contar otra caída
                fallDamagePending = false;
            }
        }
    }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!collision.collider.CompareTag("Ground"))
                return;

            bool hitGround = false;
            bool hitWall   = false;
            float wallDir  = 0f;

            foreach (var contact in collision.contacts)
            {
                // SUELO (normal apunta hacia arriba)
                if (contact.normal.y > 0.3f)
                {
                    hitGround = true;
                }

                // PARED (normal básicamente horizontal)
                if (Mathf.Abs(contact.normal.x) > 0.3f && contact.normal.y < 0.8f)
                {
                    hitWall = true;
                    wallDir = (contact.point.x < transform.position.x) ? 1f : -1f;
                }
            }

            // Contar suelo si lo hay
            if (hitGround)
            {
                groundContacts++;
                // OJO: ya no tocamos isOnWall aquí.
            }

            // Cualquier contacto lateral lo tratamos como pared,
            // PERO solo si llevamos un mínimo tiempo en el aire.
            if (hitWall && airTimeSinceLeftGround >= minAirTimeBeforeWallGrab)
            {
                isOnWall = true;
                lastWallJumpDir = wallDir;

                // acabas de tocar pared en este frame
                justTouchedWall = true;

                // al volver a tocar pared, quitamos el lock del último wall jump
                wallJumpLockTimer = 0f;
            }

        }




    void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Ground"))
            return;

        bool touchingWall = false;
        float wallDir = 0f;

        foreach (var contact in collision.contacts)
        {
            // Contacto horizontal (pared)
            if (Mathf.Abs(contact.normal.x) > 0.3f && contact.normal.y < 0.5f)
            {
                touchingWall = true;
                wallDir = (contact.point.x < transform.position.x) ? 1f : -1f;
                break;
            }
        }

        if (touchingWall && !isGrounded && !isWallJumping &&
            airTimeSinceLeftGround >= minAirTimeBeforeWallGrab)
        {
            isOnWall = true;
            lastWallJumpDir = wallDir;

            // Evitamos que el coyote-time piense que sigues en suelo
            coyoteTimer = 0f;
        }



    }




    void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Ground"))
            return;

        groundContacts--;
        if (groundContacts < 0) groundContacts = 0;

        // Al salir del collider de suelo/pared, asumimos que ya no estamos en pared
        isOnWall = false;
    }


    // === DAÑO POR PELIGROS (TRIGGERS) ===
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(hazardTag))
            return;

        isInHazard = true;
        TryApplyHazardDamage();  // primer toque
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(hazardTag))
            return;

        isInHazard = false;
    }

    private void TryApplyHazardDamage()
    {
        // cooldown general de daño por hazard
        if (Time.time < nextHazardDamageTime)
            return;

        // AHORA: daño SIN respawn, solo baja vidas y parpadea
        TakeDamageNoRespawn(1);

        // siguiente vez que puede hacer daño
        nextHazardDamageTime = Time.time + hazardTickInterval;
    }

        public void TakeDamageNoRespawn(int amount)
        {
            // Igual que TakeDamage, pero sin llamar a DieAndRespawn cuando aún quedan vidas
            if (isDead || isInvincible) return;

            // Activamos invencibilidad inmediatamente para evitar golpes múltiples
            isInvincible = true;

            currentHealth -= amount;
            currentHealth = Mathf.Max(0, currentHealth);

            if (currentHealth > 0)
            {
                // Todavía quedan vidas: solo parpadeo + invencibilidad
                StartInvincibility();
            }
            else
            {
                // Sin vidas: GAME OVER (reinicia la escena)
                StartCoroutine(GameOverRoutine());
            }
        }




    public void TakeDamage(int amount)
    {
        // Si está muerto o invencible, ignorar daño
        if (isDead || isInvincible) return;

        // Activamos invencibilidad inmediatamente para evitar golpes múltiples
        isInvincible = true;

        // === Las vidas son el contador de "vidas que te quedan" ===
        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);

        if (currentHealth > 0)
        {
            // Todavía te quedan vidas: muerte + respawn
            DieAndRespawn();
        }
        else
        {
            // Sin vidas: GAME OVER (reinicia la escena)
            StartCoroutine(GameOverRoutine());   // *** NUEVO
        }
    }

    private void DieAndRespawn()
    {
        if (isDead) return;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        isDead = true;

        // Reset velocidad y congelar física
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        // Limpiar rastro y apagarlo para que no "arrastre" al respawn
        if (tr != null)
        {
            tr.Clear();
            tr.emitting = false;
        }

        // Pequeño "tiempo de muerte"
        yield return new WaitForSeconds(0.6f);

        // Teleport a punto de respawn
        transform.position = spawnPosition;

        // *** OJO: AQUÍ YA NO RESETEAMOS currentHealth ***
        // ANTES: currentHealth = maxHealth;  <-- EL PROBLEMA
        // AHORA: las vidas se gestionan en TakeDamage y solo se reinician al recargar escena.

        // Reactivar física
        rb.simulated = true;

        // Volver a encender el trail
        if (tr != null)
        {
            tr.emitting = true;
        }

        // Invencibilidad + parpadeo al reaparecer
        StartInvincibility();

        isDead = false;
    }

    // === GAME OVER: SIN VIDAS, REINICIAR ESCENA ===
    private IEnumerator GameOverRoutine()   // *** NUEVO
    {
        isDead = true;

        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        if (tr != null)
        {
            tr.Clear();
            tr.emitting = false;
        }

        // Pequeño delay para ver que has muerto con 0 vidas
        yield return new WaitForSeconds(0.6f);

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);   // al recargar, Start() vuelve a poner currentHealth = maxHealth
    }

    private void StartInvincibility()
    {
        if (invCoroutine != null)
            StopCoroutine(invCoroutine);

        invCoroutine = StartCoroutine(InvincibilityCoroutine());
    }

    private IEnumerator InvincibilityCoroutine()
    {
        float elapsed = 0f;
        bool visible = true;

        // 🔸 Activamos invencibilidad
        isInvincible = true;

        // 🔸 Ignorar colisiones entre el layer del Player y el layer Enemy
        int playerLayer = gameObject.layer;
        int enemyLayer  = LayerMask.NameToLayer("Enemy");

        if (enemyLayer != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);
        }

        // 🔸 Parpadeo visual mientras dura la invencibilidad
        while (elapsed < invincibilityTime)
        {
            visible = !visible;
            if (sr != null)
                sr.enabled = visible;

            yield return new WaitForSeconds(flashInterval);
            elapsed += flashInterval;
        }

        // Aseguramos que se quede visible
        if (sr != null)
            sr.enabled = true;

        // 🔸 Volver a activar colisiones con enemigos
        if (enemyLayer != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);
        }

        isInvincible = false;
    }


    void FixedUpdate()
    {
        if (isDead) return;

        Vector2 v = rb.linearVelocity;

        // === GRAVEDAD EXTRA AL CAER ===
        if (v.y < 0f)
        {
            float extraGravity = Physics2D.gravity.y * rb.gravityScale * (fallGravityMultiplier - 1f);
            v.y += extraGravity * Time.fixedDeltaTime;
        }
        // === LOW JUMP ===
        else if (v.y > 0f && !Input.GetKey(KeyCode.Z) && lowJumpGravityMultiplier > 1f)
        {
            float extraGravity = Physics2D.gravity.y * rb.gravityScale * (lowJumpGravityMultiplier - 1f);
            v.y += extraGravity * Time.fixedDeltaTime;
        }

        // === WALL SLIDE (solo si habilidad está activa) ===
        if (canWallGrab && isOnWall && !isGrounded)
        {
            // Si llegas a la pared todavía subiendo por un salto/doble salto,
            // cortamos inmediatamente la subida vertical.
            if (v.y > 0f)
            {
                v.y = 0f;
            }

            // Y si estás cayendo demasiado rápido, aplicamos el límite de slide
            if (v.y < wallSlideSpeed)
            {
                v.y = wallSlideSpeed;
            }
        }




        // === LÍMITE DE VELOCIDAD EN CAÍDA GLOBAL ===
        if (v.y < maxFallSpeed)
        {
            v.y = maxFallSpeed;
        }

        rb.linearVelocity = v;
    }



    void OnGUI()
    {
        // Estilo propio para el texto de vidas
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 32;                        // 🔴 Aquí haces el texto más grande (prueba 32, 40, 50…)
        style.normal.textColor = Color.white;       // Color del texto
        style.alignment = TextAnchor.UpperLeft;     // Alineado arriba a la izquierda

        // Rect más grande para que quepa el texto
        GUI.Label(new Rect(10, 10, 300, 60), "Vidas: " + currentHealth + "/" + maxHealth, style);
    }

}
