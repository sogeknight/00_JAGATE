using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerSimpleController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("Input")]
    public KeyCode jumpKey = KeyCode.Z;

    [Header("Ground Detection (por colisiones)")]
    public string groundTag = "Ground";

    private Rigidbody2D rb;

    // contamos cuántos contactos con suelo tenemos
    private int groundContacts = 0;
    private bool isGrounded => groundContacts > 0;

    private GameplayTelemetry telemetry;

    private const bool DEBUG_MOVEMENT = false;



    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        telemetry = GameplayTelemetry.Instance;

        if (telemetry != null)
        {
            telemetry.LogEvent("LEVEL_START", transform.position, "Lab_Tema1_CRISP_PuntosFuga");
        }

    }

    private void Update()
    {
        // Movimiento horizontal usando linearVelocity (Unity 6)
        Vector2 v = rb.linearVelocity;
        float inputX = Input.GetAxisRaw("Horizontal");
        v.x = inputX * moveSpeed;
        rb.linearVelocity = v;

        bool zPressed = Input.GetKeyDown(jumpKey);
        if (DEBUG_MOVEMENT) Debug.Log($"DBG -> isGrounded={isGrounded}, contacts={groundContacts}, zPressed={zPressed}, velY={rb.linearVelocity.y}");


        if (zPressed && isGrounded)
        {
            if (DEBUG_MOVEMENT) Debug.Log("DBG -> JUMP con Z, aplicando fuerza");

            // Telemetría de salto
            if (telemetry != null)
            {
                telemetry.LogEvent("JUMP", transform.position, $"velX={v.x:F2}");
            }

            v = rb.linearVelocity;
            v.y = jumpForce;
            rb.linearVelocity = v;
        }

    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag(groundTag))
        {
            bool wasGrounded = isGrounded;   // antes de sumar
            groundContacts++;
            if (DEBUG_MOVEMENT) Debug.Log($"DBG -> OnCollisionEnter con suelo, groundContacts={groundContacts}");


            if (!wasGrounded && isGrounded && telemetry != null)
            {
                telemetry.LogEvent("LAND", transform.position);
            }
        }

    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag(groundTag))
        {
            groundContacts--;
            if (DEBUG_MOVEMENT) Debug.Log($"DBG -> OnCollisionExit con suelo, groundContacts={groundContacts}");

        }
    }
}
