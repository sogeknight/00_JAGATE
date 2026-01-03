using UnityEngine;

public enum WorldMaterialId { Dirt, Stone, Wood, Glass }

[CreateAssetMenu(menuName = "Tirolei/World Material Profile")]
public class WorldMaterialProfile : ScriptableObject
{
    public WorldMaterialId id;

    [Header("VFX")]
    public GameObject debrisPrefab;     // Prefab con ParticleSystem(s)
    public float sizeMultiplier = 1f;   // Escala global del efecto
    public float lifetimeMultiplier = 1f;

    [Header("SFX")]
    public AudioClip breakClip;

    [Header("Tuning")]
    public int extraBurstCount = 0;     // + partículas si quieres “más rotura”
}
