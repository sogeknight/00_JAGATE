using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class ProceduralRoomGenerator : MonoBehaviour
{
    [Header("Grid donde se crearán las salas")]
    public Grid targetGrid;              // Grid padre de todos los Tilemaps

    [Header("Sala")]
    public int roomWidth = 30;
    public int roomHeight = 20;
    public int roomCount = 3;
    public int roomSpacing = 4;          // separación en unidades entre salas

    [Header("Bordes básicos")]
    public bool useFloor     = true;
    public bool useCeiling   = true;
    public bool useLeftWall  = true;
    public bool useRightWall = true;

    [Header("Ruido tipo 'cueva' (interior)")]
    public bool useCaveNoise = true;
    [Range(0, 100)]
    public int randomFillPercent = 45;
    [Range(0, 10)]
    public int smoothIterations = 5;

    [Header("Aberturas laterales (por sala)")]
    [Range(0, 8)]
    public int openingsPerSide = 1;      // nº de huecos POR LADO (si el lado está activo)
    [Range(1, 10)]
    public int openingHeight = 3;        // altura del hueco en tiles
    [Range(1, 5)]
    public int openingDepth = 2;         // cuánto entra hacia dentro el hueco (tiles despejados)

    [Header("Random")]
    public bool useRandomSeed = true;
    public string seed = "tirolei";

    [Header("Tiles")]
    public TileBase groundTile;

    [Header("Cámara (opcional)")]
    public Camera targetCamera;
    public float cameraPadding = 2f;

    [Header("Ruido de PLATAFORMAS internas")]
    public bool usePlatformNoise = false;          // activar / desactivar
    [Range(0, 100)]
    public int platformNoisePercent = 15;          // probabilidad de intentar crear plataforma
    [Range(1, 10)]
    public int platformMinLength = 2;              // longitud mínima de la plataforma
    [Range(1, 10)]
    public int platformMaxLength = 5;              // longitud máxima de la plataforma

    private System.Random prng;

    // ========= BOTONES DE INSPECTOR =========

    [ContextMenu("Generate Rooms (separate Tilemaps)")]
    public void GenerateRoomsAdditive()
    {
        if (!CheckSetup()) return;

        InitPrng();

        // calculamos el offset X inicial en función de lo que ya haya
        float startOffsetX = GetRightmostRoomEndX();

        for (int i = 0; i < roomCount; i++)
        {
            float offsetX = startOffsetX + i * (roomWidth + roomSpacing);
            CreateAndFillRoom(offsetX);
        }

        AutoCenterCameraOnAllRooms();
    }

    [ContextMenu("Clear All Generated Rooms")]
    public void ClearAllGeneratedRooms()
    {
        if (targetGrid == null) return;

        var children = targetGrid.GetComponentsInChildren<Transform>(true);
        foreach (var t in children)
        {
            if (t == targetGrid.transform) continue;
            if (t.name.StartsWith("Room_"))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                else
                    UnityEngine.Object.Destroy(t.gameObject);
#else
                UnityEngine.Object.DestroyImmediate(t.gameObject);
#endif
            }
        }
    }

    // ========= LÓGICA DE GENERACIÓN =========

    private bool CheckSetup()
    {
        if (targetGrid == null)
        {
            Debug.LogError("[ProceduralRoomGenerator] Asigna targetGrid (un Grid en la escena).");
            return false;
        }

        if (groundTile == null)
        {
            Debug.LogError("[ProceduralRoomGenerator] Asigna groundTile.");
            return false;
        }

        if (roomWidth <= 0 || roomHeight <= 0 || roomCount <= 0)
        {
            Debug.LogError("[ProceduralRoomGenerator] roomWidth, roomHeight y roomCount deben ser > 0.");
            return false;
        }

        return true;
    }

    private void InitPrng()
    {
        if (useRandomSeed)
            prng = new System.Random();
        else
            prng = new System.Random(seed.GetHashCode());
    }

    /// <summary>
    /// Devuelve el X local (en unidades) donde termina el Room más a la derecha.
    /// Lo usamos para seguir añadiendo salas sin pisar las anteriores.
    /// </summary>
    private float GetRightmostRoomEndX()
    {
        float maxEndX = 0f;
        bool foundAny = false;

        var tilemaps = targetGrid.GetComponentsInChildren<Tilemap>();
        foreach (var tm in tilemaps)
        {
            if (!tm.name.StartsWith("Room_")) continue;
            foundAny = true;

            tm.CompressBounds();
            var b = tm.localBounds;                 // bounds locales al Tilemap
            float endX = tm.transform.localPosition.x + b.max.x;
            if (endX > maxEndX)
                maxEndX = endX;
        }

        if (!foundAny)
            return 0f;

        return maxEndX + roomSpacing;
    }

    private void CreateAndFillRoom(float offsetX)
    {
        // 1) Crear GameObject Room_n bajo el Grid
        int index = GetNextRoomIndex();
        var roomGO = new GameObject($"Room_{index:D2}");
        roomGO.transform.SetParent(targetGrid.transform, false);
        roomGO.transform.localPosition = new Vector3(offsetX, 0f, 0f);

        // 2) Añadir Tilemap y TilemapRenderer
        var tilemap = roomGO.AddComponent<Tilemap>();
        var renderer = roomGO.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = 0;

        // 3) Generar mapa de esa sala
        int[,] roomMap = GenerateSingleRoom(roomWidth, roomHeight);

        // 4) Pintar en su Tilemap
        DrawRoomToTilemap(roomMap, tilemap);
    }

    private int GetNextRoomIndex()
    {
        int maxIndex = -1;

        var tilemaps = targetGrid.GetComponentsInChildren<Tilemap>(true);
        foreach (var tm in tilemaps)
        {
            if (!tm.name.StartsWith("Room_")) continue;

            string suffix = tm.name.Substring("Room_".Length);
            if (int.TryParse(suffix, out int idx))
            {
                if (idx > maxIndex) maxIndex = idx;
            }
        }

        return maxIndex + 1;
    }

    private int[,] GenerateSingleRoom(int width, int height)
    {
        int[,] map = new int[width, height];

        // --- 1) Todo vacío ---
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                map[x, y] = 0;
            }
        }

        // --- 2) Bordes básicos según flags ---
        if (useFloor)
        {
            for (int x = 0; x < width; x++)
                map[x, 0] = 1;
        }

        if (useCeiling)
        {
            for (int x = 0; x < width; x++)
                map[x, height - 1] = 1;
        }

        if (useLeftWall)
        {
            for (int y = 0; y < height; y++)
                map[0, y] = 1;
        }

        if (useRightWall)
        {
            for (int y = 0; y < height; y++)
                map[width - 1, y] = 1;
        }

        // --- 3) Ruido interior tipo "cueva" (opcional) ---
        if (useCaveNoise && randomFillPercent > 0)
        {
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    if (map[x, y] == 1)
                        continue;

                    map[x, y] = (prng.Next(0, 100) < randomFillPercent) ? 1 : 0;
                }
            }

            if (smoothIterations > 0)
            {
                for (int i = 0; i < smoothIterations; i++)
                    map = SmoothMapWithLockedBorders(map, width, height);
            }
        }

        // --- 4) Aberturas laterales (solo quitan tiles, no añaden grosor) ---
        CarveSideOpenings(map, width, height);

        // --- 5) PLATAFORMAS internas extra (opcional) ---
        if (usePlatformNoise)
            AddPlatformNoise(map, width, height);

        return map;
    }

    /// <summary>
    /// Suavizado tipo "cueva", pero sin tocar bordes exteriores.
    /// </summary>
    private int[,] SmoothMapWithLockedBorders(int[,] map, int width, int height)
    {
        int[,] newMap = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool isBorder =
                    (x == 0) || (x == width - 1) ||
                    (y == 0) || (y == height - 1);

                if (isBorder)
                {
                    newMap[x, y] = map[x, y];
                    continue;
                }

                int neighbourWallTiles = GetSurroundingWallCountNoOutsideWalls(map, width, height, x, y);

                if (neighbourWallTiles > 4)
                    newMap[x, y] = 1;
                else if (neighbourWallTiles < 4)
                    newMap[x, y] = 0;
                else
                    newMap[x, y] = map[x, y];
            }
        }

        return newMap;
    }

    private int GetSurroundingWallCountNoOutsideWalls(int[,] map, int width, int height, int gridX, int gridY)
    {
        int wallCount = 0;

        for (int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX++)
        {
            for (int neighbourY = gridY - 1; neighbourY <= gridY + 1; neighbourY++)
            {
                if (neighbourX >= 0 && neighbourX < width &&
                    neighbourY >= 0 && neighbourY < height)
                {
                    if (neighbourX == gridX && neighbourY == gridY)
                        continue;

                    wallCount += map[neighbourX, neighbourY];
                }
            }
        }

        return wallCount;
    }

    /// <summary>
    /// Crea agujeros en las paredes laterales activas.
    /// NO añade columnas sólidas hacia dentro (nada de bordes gordos).
    /// </summary>
    private void CarveSideOpenings(int[,] map, int width, int height)
    {
        if (openingsPerSide <= 0 || openingHeight <= 0)
            return;

        bool anySideActive = useLeftWall || useRightWall;
        if (!anySideActive)
            return;

        int interiorMinY = 1;
        int interiorMaxY = height - 2;
        int interiorHeight = interiorMaxY - interiorMinY + 1;
        if (interiorHeight <= 0)
            return;

        int effectiveHeight = Mathf.Clamp(openingHeight, 1, interiorHeight);
        int effectiveDepth  = Mathf.Clamp(openingDepth, 1, Mathf.Max(1, width / 4));

        // LEFT
        if (useLeftWall)
            CarveOpeningsOnSide(map, width, height, interiorMinY, interiorMaxY,
                                effectiveHeight, effectiveDepth,
                                openingsPerSide, isLeft: true);

        // RIGHT
        if (useRightWall)
            CarveOpeningsOnSide(map, width, height, interiorMinY, interiorMaxY,
                                effectiveHeight, effectiveDepth,
                                openingsPerSide, isLeft: false);
    }

    private void CarveOpeningsOnSide(
        int[,] map,
        int width,
        int height,
        int interiorMinY,
        int interiorMaxY,
        int effectiveHeight,
        int effectiveDepth,
        int openingsCount,
        bool isLeft)
    {
        float interiorHeight = interiorMaxY - interiorMinY + 1;
        float segmentSize = interiorHeight / (openingsCount + 1);

        for (int i = 0; i < openingsCount; i++)
        {
            float centerYFloat = interiorMinY + (i + 1) * segmentSize;
            int centerY = Mathf.RoundToInt(centerYFloat);

            int startY = centerY - effectiveHeight / 2;
            if (startY < interiorMinY)
                startY = interiorMinY;
            if (startY + effectiveHeight - 1 > interiorMaxY)
                startY = interiorMaxY - effectiveHeight + 1;

            int endY = startY + effectiveHeight - 1;

            for (int y = startY; y <= endY; y++)
            {
                if (y <= 0 || y >= height - 1)
                    continue;

                // Quitamos tiles de pared, no añadimos grosor
                if (isLeft)
                {
                    for (int dx = 0; dx < effectiveDepth; dx++)
                    {
                        int x = 0 + dx;
                        if (x >= width) break;
                        map[x, y] = 0;
                    }
                }
                else
                {
                    for (int dx = 0; dx < effectiveDepth; dx++)
                    {
                        int x = width - 1 - dx;
                        if (x < 0) break;
                        map[x, y] = 0;
                    }
                }
            }
        }
    }

    private void AddPlatformNoise(int[,] map, int width, int height)
    {
        int minY = 2;
        int maxY = height - 3;
        if (maxY <= minY)
            return;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (map[x, y] != 0)
                    continue;

                if (prng.Next(0, 100) >= platformNoisePercent)
                    continue;

                int length = prng.Next(platformMinLength, platformMaxLength + 1);
                int dir = (prng.NextDouble() < 0.5) ? -1 : 1;

                int startX = x;
                for (int i = 0; i < length; i++)
                {
                    int px = startX + i * dir;

                    if (px <= 0 || px >= width - 1)
                        break;

                    if (map[px, y] != 0)
                        break;

                    if (map[px, y - 1] == 0 && map[px, y + 1] == 0)
                        map[px, y] = 1;
                }
            }
        }
    }

    private void DrawRoomToTilemap(int[,] map, Tilemap tilemap)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        tilemap.ClearAllTiles();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (map[x, y] == 1)
                    tilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
        }
    }

    private void AutoCenterCameraOnAllRooms()
    {
        if (targetCamera == null || targetGrid == null) return;

        bool hasBounds = false;
        Bounds totalBounds = new Bounds();

        var tilemaps = targetGrid.GetComponentsInChildren<Tilemap>();
        foreach (var tm in tilemaps)
        {
            if (!tm.name.StartsWith("Room_")) continue;

            tm.CompressBounds();
            var b = tm.localBounds;
            b.center += tm.transform.position;

            if (!hasBounds)
            {
                totalBounds = b;
                hasBounds = true;
            }
            else
            {
                totalBounds.Encapsulate(b);
            }
        }

        if (!hasBounds) return;

        Vector3 center = totalBounds.center;
        Vector3 extents = totalBounds.extents;

        targetCamera.orthographic = true;
        targetCamera.transform.position = new Vector3(center.x, center.y, -10f);

        float vertExtent = extents.y;
        float horizExtent = extents.x / Mathf.Max(targetCamera.aspect, 0.0001f);
        float neededSize = Mathf.Max(vertExtent, horizExtent) + cameraPadding;
        targetCamera.orthographicSize = neededSize;
    }
}
