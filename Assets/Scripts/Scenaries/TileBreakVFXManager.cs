using UnityEngine;
using UnityEngine.Tilemaps;

public class TileBreakVFXManager : MonoBehaviour
{
    [Header("Profile")]
    public TileBreakVFXProfile profile;

    [Header("Debug")]
    public bool warnIfNoSpawner = true;

    // Cache simple para no hacer FindObjectOfType cada vez
    private static TileBreakVFXManager _cached;

    /// <summary>
    /// Devuelve el manager asociado al contexto de un transform.
    /// Primero busca en padres, si no, usa el primero que encuentre en escena.
    /// </summary>
    public static TileBreakVFXManager GetFor(Transform ctx)
    {
        if (ctx != null)
        {
            var local = ctx.GetComponentInParent<TileBreakVFXManager>();
            if (local != null) return local;
        }

        if (_cached == null)
            _cached = FindFirstObjectByType<TileBreakVFXManager>();

        return _cached;
    }

    /// <summary>
    /// Para rotura real de Tilemap (cuando tienes TileBase).
    /// </summary>
    public void PlayForTile(Tilemap tilemap, Vector3Int cell, TileBase tileBroken)
    {
        if (profile == null) return;

        if (!profile.TryGet(tileBroken, out var vfx) || vfx == null)
            return;

        var spawner = DebrisSpawner.Instance;
        if (spawner == null)
        {
            if (warnIfNoSpawner) Debug.LogWarning("[TileBreakVFXManager] No hay DebrisSpawner.Instance en escena.");
            return;
        }

        Vector3 world = tilemap.GetCellCenterWorld(cell);
        world.z = 0f;

        spawner.SpawnCustom(
            worldPos: world,
            count: vfx.count,
            startColor: vfx.color,
            startSize: vfx.size,
            startLifetime: vfx.lifetime,
            speedMin: vfx.speedMin,
            speedMax: vfx.speedMax,
            spreadX: vfx.spreadX,
            spreadY: vfx.spreadY
        );
    }

    /// <summary>
    /// Para tu realidad actual (chunks / WorldMaterial).
    /// Usa el DEFAULT del profile (robusto).
    /// </summary>
    public void PlayAtWorld(Vector3 worldPos)
    {
        Debug.Log("[VFX MANAGER] PlayAtWorld");

        Debug.Log("[VFX MANAGER] profile = " + (profile != null));

        var vfx = profile != null ? profile.defaultVFX : null;
        Debug.Log("[VFX MANAGER] defaultVFX = " + (vfx != null));

        Debug.Log("[VFX MANAGER] Spawner = " + (DebrisSpawner.Instance != null));

        if (profile == null || vfx == null || DebrisSpawner.Instance == null)
            return;

        DebrisSpawner.Instance.SpawnCustom(
            worldPos,
            vfx.count,
            vfx.color,
            vfx.size,
            vfx.lifetime,
            vfx.speedMin,
            vfx.speedMax,
            vfx.spreadX,
            vfx.spreadY
        );
    }

}
