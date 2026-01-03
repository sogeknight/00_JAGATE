using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Tirolei/VFX/Tile Break VFX Profile", fileName = "TileBreakVFXProfile")]
public class TileBreakVFXProfile : ScriptableObject
{
    [Serializable]
    public class TileVFX
    {
        public TileBase tile;

        [Header("Debris settings")]
        public int count = 24;
        public Color color = Color.white;
        public float size = 0.25f;
        public float lifetime = 9.2f;

        [Header("Motion")]
        public float speedMin = 0.5f;
        public float speedMax = 2.5f;
        public float spreadX = 1.0f;
        public float spreadY = 1.0f;
    }

    [Header("Default (si no hay match de tile)")]
    public bool useDefaultIfNoMatch = true;
    public TileVFX defaultVFX = new TileVFX
    {
        tile = null,
        count = 18,
        color = new Color(1f, 0f, 1f, 1f),
        size = 0.22f,
        lifetime = 9.0f,
        speedMin = 0.4f,
        speedMax = 2.0f,
        spreadX = 1.0f,
        spreadY = 1.0f
    };

    [Header("Mappings")]
    public List<TileVFX> mappings = new List<TileVFX>();

    // Lookup rápido
    Dictionary<TileBase, TileVFX> _map;

    public bool TryGet(TileBase tile, out TileVFX vfx)
    {
        if (_map == null) BuildMap();

        if (tile != null && _map.TryGetValue(tile, out vfx))
            return true;

        if (useDefaultIfNoMatch)
        {
            vfx = defaultVFX;
            return true;
        }

        vfx = null;
        return false;
    }

    [System.Serializable]
    public struct TierMapping
    {
        public MaterialTier tier;
        public TileVFX vfx;
    }

    [Header("Tier Mappings (para CHUNKS / WorldMaterial)")]
    public List<TierMapping> tierMappings = new List<TierMapping>();

    public bool TryGetForTier(MaterialTier tier, out TileVFX vfx)
    {
        for (int i = 0; i < tierMappings.Count; i++)
        {
            if (tierMappings[i].tier == tier)
            {
                vfx = tierMappings[i].vfx;
                return true;
            }
        }

        vfx = default;
        return false;
    }


    void OnEnable() => BuildMap();

    void BuildMap()
    {
        _map = new Dictionary<TileBase, TileVFX>();
        for (int i = 0; i < mappings.Count; i++)
        {
            var m = mappings[i];
            if (m == null || m.tile == null) continue;
            _map[m.tile] = m; // si repites tile, gana el último
        }
    }
}
