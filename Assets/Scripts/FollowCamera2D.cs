using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FollowCamera2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody2D targetRb;

    [Header("Dead Zone Horizontal (world units)")]
    public float deadZoneLeft = 2f;
    public float deadZoneRight = 2f;

    [Header("Dead Zone Vertical (world units)")]
    public float deadZoneUp = 1.0f;
    public float deadZoneDown = 0.7f;

    [Header("Smooth Horizontal")]
    public float smoothTimeX = 0.15f;

    [Header("Smooth Vertical (solo cuando va lento)")]
    public float smoothTimeY = 0.15f;

    [Header("Umbral de velocidad vertical para seguir instantáneo")]
    public float verticalSpeedThreshold = 5f;

    [Header("Z Offset")]
    public float offsetZ = -10f;

    [Header("Teleport / Respawn Snap")]
    [Tooltip("Si se hace un teleport (checkpoint, puerta, etc.), se puede solicitar un snap duro al target.")]
    public bool allowSnapRequests = true;

    private float velX;
    private float velY;

    // Flag interno para pedir un snap en el siguiente LateUpdate
    private bool snapRequested;

    private void Reset()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p)
            {
                target = p.transform;
                targetRb = p.GetComponent<Rigidbody2D>();
            }
        }

        Camera cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    /// <summary>
    /// Llamar cuando hagas un respawn/teleport duro del player.
    /// La cámara se colocará EXACTAMENTE encima del target en el siguiente LateUpdate,
    /// reseteando las velocidades internas para que no arrastre smooth viejo.
    /// </summary>
    public void RequestImmediateSnap()
    {
        if (!allowSnapRequests) return;
        snapRequested = true;
    }

    private void LateUpdate()
    {
        if (!target) return;

        // SNAP DURO (respawn / checkpoint)
        if (snapRequested)
        {
            snapRequested = false;

            float px = target.position.x;
            float py = target.position.y;

            velX = 0f;
            velY = 0f;

            transform.position = new Vector3(px, py, offsetZ);
            return;
        }

        // Posición actual de la cámara
        float camX = transform.position.x;
        float camY = transform.position.y;

        // Posición del jugador
        float px2 = target.position.x;
        float py2 = target.position.y;

        float newCamX = camX;
        float newCamY = camY;

        // ---------------------------
        // DEAD ZONE HORIZONTAL
        // ---------------------------
        float leftLimit = camX - deadZoneLeft;
        float rightLimit = camX + deadZoneRight;

        if (px2 < leftLimit)
            newCamX = px2 + deadZoneLeft;
        else if (px2 > rightLimit)
            newCamX = px2 - deadZoneRight;

        // ---------------------------
        // DEAD ZONE VERTICAL
        // ---------------------------
        float downLimit = camY - deadZoneDown;
        float upLimit = camY + deadZoneUp;

        bool outsideVerticalDeadZone = false;

        if (py2 < downLimit)
        {
            newCamY = py2 + deadZoneDown;
            outsideVerticalDeadZone = true;
        }
        else if (py2 > upLimit)
        {
            newCamY = py2 - deadZoneUp;
            outsideVerticalDeadZone = true;
        }

        // ---------------------------
        // HORIZONTAL: siempre suave
        // ---------------------------
        float smoothedX = Mathf.SmoothDamp(camX, newCamX, ref velX, smoothTimeX);

        // ---------------------------
        // VERTICAL: modo híbrido
        // ---------------------------
        float smoothedY;

        float absVy = 0f;
        if (targetRb != null)
        {
            absVy = Mathf.Abs(targetRb.linearVelocity.y);
        }

        bool goingFast = absVy > verticalSpeedThreshold;

        if (outsideVerticalDeadZone || goingFast)
        {
            // SIN suavizado: la cámara engancha directamente
            smoothedY = newCamY;
        }
        else
        {
            // Movimiento suave cuando está cerca y va lento
            smoothedY = Mathf.SmoothDamp(camY, newCamY, ref velY, smoothTimeY);
        }

        transform.position = new Vector3(smoothedX, smoothedY, offsetZ);
    }
}
