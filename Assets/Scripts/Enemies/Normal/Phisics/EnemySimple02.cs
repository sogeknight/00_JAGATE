using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemySimple : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 80;
    private int currentHealth;

    [Header("Objetivo")]
    public Transform target;
    public float maxChaseDistance = 20f;
    public float stopDistance = 0.6f;

    [Header("Locomoción")]
    public float moveSpeed = 4f;
    public float obstacleJumpVelocity = 10f;
    public float jumpCooldown = 0.2f;

    [Header("Detección obstáculo (A: universal)")]
    public LayerMask obstacleLayers;
    public float frontBoxWidthFactor = 0.6f;
    public float frontBoxHeightFactor = 0.7f;
    public float frontCheckDistance = 0.3f;

    [Header("Dash (ataque)")]
    public bool useDashAttack = true;
    public float dashTriggerMin = 1.5f;
    public float dashTriggerMax = 8f;
    public float chargeTime = 0.25f;
    public float dashSpeed = 12f;
    public float dashDuration = 0.25f;
    public float attackCooldown = 1.2f;

    [Header("Debug")]
    public bool debugCast = false;

    private Rigidbody2D rb;
    private Collider2D col;

    private float jumpCooldownTimer;
    private float attackCooldownTimer;
    private float stateTimer;
    private float dashDir;

    private enum State { Chase, Charging, Dashing, Recover }
    private State state = State.Chase;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        currentHealth = maxHealth;
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        float dt = Time.fixedDeltaTime;
        if (jumpCooldownTimer > 0f) jumpCooldownTimer -= dt;
        if (attackCooldownTimer > 0f) attackCooldownTimer -= dt;

        float dx = target.position.x - transform.position.x;
        float adx = Mathf.Abs(dx);
        float dir = (dx >= 0f) ? 1f : -1f;

        bool grounded = IsGroundedSimple();

        switch (state)
        {
            case State.Chase:
            {
                if (adx > maxChaseDistance)
                {
                    SetVelX(0f);
                    return;
                }

                Flip(dir);

                bool closeHoriz = adx <= stopDistance;
                float targetVelX = closeHoriz ? 0f : dir * moveSpeed;
                SetVelX(targetVelX);

                // A) DETECCIÓN UNIVERSAL DE OBSTÁCULO
                if (grounded && targetVelX != 0f && HasObstacleAhead(dir))
                {
                    if (jumpCooldownTimer <= 0f)
                    {
                        JumpUp();
                        jumpCooldownTimer = jumpCooldown;
                    }
                }

                // Ataque dash (independiente)
                if (useDashAttack && grounded && attackCooldownTimer <= 0f &&
                    adx >= dashTriggerMin && adx <= dashTriggerMax)
                {
                    dashDir = dir;
                    state = State.Charging;
                    stateTimer = chargeTime;
                    SetVelX(0f);
                }
            }
            break;

            case State.Charging:
            {
                stateTimer -= dt;
                SetVelX(0f);

                if (stateTimer <= 0f)
                {
                    state = State.Dashing;
                    stateTimer = dashDuration;

                    Vector2 v = rb.linearVelocity;
                    v.x = dashDir * dashSpeed;
                    rb.linearVelocity = v;
                }
            }
            break;

            case State.Dashing:
            {
                stateTimer -= dt;
                Vector2 v = rb.linearVelocity;
                v.x = dashDir * dashSpeed;
                rb.linearVelocity = v;

                if (stateTimer <= 0f)
                {
                    state = State.Recover;
                    stateTimer = 0.12f;
                    attackCooldownTimer = attackCooldown;
                }
            }
            break;

            case State.Recover:
            {
                stateTimer -= dt;
                SetVelX(0f);
                if (stateTimer <= 0f)
                    state = State.Chase;
            }
            break;
        }
    }

    private void SetVelX(float x)
    {
        Vector2 v = rb.linearVelocity;
        v.x = x;
        rb.linearVelocity = v;
    }

    private void JumpUp()
    {
        Vector2 v = rb.linearVelocity;
        v.y = Mathf.Max(v.y, obstacleJumpVelocity);
        rb.linearVelocity = v;
    }

    private bool HasObstacleAhead(float dir)
    {
        Bounds b = col.bounds;
        Vector2 boxSize = new(
            b.size.x * frontBoxWidthFactor,
            b.size.y * frontBoxHeightFactor
        );

        Vector2 origin = new(
            b.center.x + dir * (b.extents.x + 0.05f),
            b.center.y
        );

        RaycastHit2D hit = Physics2D.BoxCast(
            origin,
            boxSize,
            0f,
            Vector2.right * dir,
            frontCheckDistance,
            obstacleLayers
        );

        if (debugCast)
            DebugDrawBoxCast(origin, boxSize, dir, frontCheckDistance, hit.collider ? Color.red : Color.green);

        return hit.collider != null;
    }

    private bool IsGroundedSimple()
    {
        Bounds b = col.bounds;
        Vector2 origin = new(b.center.x, b.min.y + 0.02f);

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 0.15f, obstacleLayers);

        return hit.collider != null;
    }

    private void Flip(float dir)
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * Mathf.Sign(dir);
        transform.localScale = s;
    }

    private void DebugDrawBoxCast(Vector2 origin, Vector2 size, float dir, float dist, Color c)
    {
        Vector2 p1 = origin + new Vector2(-size.x/2, -size.y/2);
        Vector2 p2 = origin + new Vector2(-size.x/2, size.y/2);
        Vector2 p3 = origin + new Vector2(size.x/2, size.y/2);
        Vector2 p4 = origin + new Vector2(size.x/2, -size.y/2);
        Debug.DrawLine(p1, p2, c, 0.02f);
        Debug.DrawLine(p2, p3, c, 0.02f);
        Debug.DrawLine(p3, p4, c, 0.02f);
        Debug.DrawLine(p4, p1, c, 0.02f);
        Debug.DrawLine(origin, origin + Vector2.right * dir * dist, c, 0.02f);
    }

    public void TakeHit(int dmg)
    {
        currentHealth -= dmg;
        if (currentHealth <= 0)
            Destroy(gameObject);
    }
}
