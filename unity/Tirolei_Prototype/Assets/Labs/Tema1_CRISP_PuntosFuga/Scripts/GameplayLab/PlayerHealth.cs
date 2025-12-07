using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Vida del jugador")]
    public int maxHealth = 5;

    [Tooltip("Vida actual (se rellena automáticamente al iniciar)")]
    public int currentHealth;

    private void Awake()
    {
        // Garantiza la vida inicial SIEMPRE
        currentHealth = maxHealth;
        Debug.Log("[PlayerHealth] Awake → currentHealth = " + currentHealth);
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log("[PlayerHealth] Daño → currentHealth = " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("[PlayerHealth] Player muerto");
    }
}
