using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyBasicCharger : MonoBehaviour
{
    [Header("Referencia al jugador")]
    public Transform player;            // si está vacío, buscamos por tag

    [Header("Detección")]
    public float detectionRange = 5f;   // distancia para empezar a embestir
    public float visionHeightTolerance = 1.5f; // diferencia de altura máxima

    [Header("Embestida")]
    public float chargeSpeed = 10f;     // velocidad horizontal de embestida
    public float chargeDuration = 0.25f; // cuánto dura la embestida
    public float cooldownTime = 1.0f;   // tiempo de descanso tras embestir

    [Header("Daño")]
    public int damage = 1;
    public bool damageOnlyWhenCharging = true; // si false, hace daño siempre al tocar

    [Header("Línea de visión")]
    public LayerMask obstacleMask;   // capas que bloquean la vista (suelo, paredes)


    private Rigidbody2D rb;
    private bool isCharging = false;
    private bool onCooldown = false;



    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Aseguramos configuración base
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1f;

        // Por defecto: enemigo anclado (no se mueve ni gira)
        rb.constraints = RigidbodyConstraints2D.FreezePositionX |
                        RigidbodyConstraints2D.FreezePositionY |
                        RigidbodyConstraints2D.FreezeRotation;
    }



    void Start()
    {
        // Si no hemos asignado el player a mano, lo buscamos por tag
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                player = p.transform;
        }
    }

    void Update()
    {
        if (player == null || isCharging || onCooldown)
            return;

        // Distancia al jugador
        Vector2 toPlayer = player.position - transform.position;
        float dist = Mathf.Abs(toPlayer.x);
        float heightDiff = Mathf.Abs(toPlayer.y);

        // Solo detecta si está cerca en X y más o menos a la misma altura
        if (dist <= detectionRange && heightDiff <= visionHeightTolerance)
        {
            // --- COMPROBAR LÍNEA DE VISIÓN ---
            Vector2 origin = transform.position;
            Vector2 dir = toPlayer.normalized;
            float rayDistance = dist;

            // Dibujo para depurar (se ve solo en modo Play)
            Debug.DrawRay(origin, dir * rayDistance, Color.red);

            // Raycast que solo choca con las capas de obstacleMask
            RaycastHit2D hit = Physics2D.Raycast(origin, dir, rayDistance, obstacleMask);

            // Si NO hemos chocado con nada de obstacleMask, significa que no hay pared en medio
            if (hit.collider == null)
            {
                StartCoroutine(ChargeRoutine(Mathf.Sign(toPlayer.x)));
            }
        }

    }

    private System.Collections.IEnumerator ChargeRoutine(float direction)
    {
        isCharging = true;

        // LIBERAMOS X durante la embestida
        rb.constraints = RigidbodyConstraints2D.FreezePositionY |
                        RigidbodyConstraints2D.FreezeRotation;

        float timer = 0f;

        while (timer < chargeDuration)
        {
            Vector2 v = rb.linearVelocity;
            v.x = direction * chargeSpeed;
            rb.linearVelocity = v;

            timer += Time.deltaTime;
            yield return null;
        }

        // Parar embestida
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // 👈 VOLVEMOS A BLOQUEAR X E Y SIEMPRE
        rb.constraints = RigidbodyConstraints2D.FreezePositionX |
                        RigidbodyConstraints2D.FreezePositionY |
                        RigidbodyConstraints2D.FreezeRotation;

        isCharging = false;
        onCooldown = true;
        yield return new WaitForSeconds(cooldownTime);
        onCooldown = false;
    }



    // --- DAÑO AL JUGADOR ---
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Buscamos el PlayerController2D en el objeto con el que chocamos
        var playerController = collision.collider.GetComponentInParent<PlayerController2D>();
        if (playerController == null)
            return;

        // Si solo queremos hacer daño mientras embiste:
        if (damageOnlyWhenCharging && !isCharging)
            return;

        // 👉 Daño SIN respawn, igual que los hazards
        playerController.TakeDamageNoRespawn(damage);
    }


}
