using UnityEngine;

public class AbilityPickup : MonoBehaviour
{
    public enum AbilityType
    {
        DoubleJump,
        WallGrab
    }

    public AbilityType abilityType;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Buscamos el PlayerController2D en lo que ha entrado
        PlayerController2D player = other.GetComponent<PlayerController2D>();
        if (player == null) return;

        switch (abilityType)
        {
            case AbilityType.DoubleJump:
                player.canDoubleJump = true;
                break;

            case AbilityType.WallGrab:
                player.canWallGrab = true;
                break;
        }

        // Aquí puedes poner animación, sonido, etc.
        // De momento lo destruimos.
        Destroy(gameObject);
    }
}
