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


    [Header("Aberturas laterales (por sala)")]
    [Range(0, 8)]
    public int openingsPerSide = 1;      // nº de huecos en cada lado (izq y dcha)
    [Range(1, 10)]
    public int openingHeight = 3;        // altura del hueco en tiles
    [Range(1, 5)]
    public int openingDepth = 2;         // cuántos tiles entra el pasillo hacia dentro


    [Header("Ruido inicial")]
    [Range(0, 100)]
    public int randomFillPercent = 45;

    [Header("Suavizado tipo 'cueva'")]
    [Range(0, 10)]
    public int smoothIterations = 5;

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

        // Ruido inicial + bordes llenos
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (prng.Next(0, 100) < randomFillPercent) ? 1 : 0;
                }
            }
        }

        // Suavizado
        for (int i = 0; i < smoothIterations; i++)
        {
            map = SmoothMap(map, width, height);
        }

        // Asegurar línea de suelo
        for (int x = 0; x < width; x++)
        {
            map[x, 0] = 1;
        }

        // Aberturas laterales por sala (independientes)
        CarveSideOpenings(map, width, height);

        // PLATAFORMAS internas extra (opcional)
        if (usePlatformNoise)
        {
            AddPlatformNoise(map, width, height);
        }

        return map;
    }


    private int[,] SmoothMap(int[,] map, int width, int height)
    {
        int[,] newMap = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(map, width, height, x, y);

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

    private int GetSurroundingWallCount(int[,] map, int width, int height, int gridX, int gridY)
    {
        int wallCount = 0;

        for (int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX++)
        {
            for (int neighbourY = gridY - 1; neighbourY <= gridY + 1; neighbourY++)
            {
                if (neighbourX >= 0 && neighbourX < width &&
                    neighbourY >= 0 && neighbourY < height)
                {
                    if (neighbourX != gridX || neighbourY != gridY)
                    {
                        wallCount += map[neighbourX, neighbourY];
                    }
                }
                else
                {
                    wallCount++; // fuera del mapa = muro
                }
            }
        }

        return wallCount;
    }

    /// <summary>
    /// openingsPerSide se interpreta como NÚMERO TOTAL de aberturas por sala.
    /// - Si openingsPerSide = 1 => exactamente 1 hueco (en un solo lado, aleatorio).
    /// - Si openingsPerSide = 2 => 2 huecos en total, alternando lados (izq/dcha).
    /// - Altura = openingHeight, profundidad = openingDepth (acotadas al tamaño real).
    /// </summary>
    private void CarveSideOpenings(int[,] map, int width, int height)
    {
        int totalOpenings = Mathf.Max(0, openingsPerSide);
        if (totalOpenings == 0 || openingHeight <= 0 || openingDepth <= 0)
            return;

        // Interior vertical: dejamos suelo (0) y techo (height-1) intactos
        int interiorMinY   = 1;
        int interiorMaxY   = height - 2; // inclusive
        int interiorHeight = interiorMaxY - interiorMinY + 1;
        if (interiorHeight <= 0)
            return;

        int effectiveHeight = Mathf.Clamp(openingHeight, 1, interiorHeight);
        int effectiveDepth  = Mathf.Clamp(openingDepth, 1, Mathf.Max(1, width / 4));

        // Aseguramos que caben totalOpenings huecos con al menos 1 tile de separación
        int gap = (interiorHeight - totalOpenings * effectiveHeight) / (totalOpenings + 1);
        while (gap < 1 && effectiveHeight > 1)
        {
            effectiveHeight--;
            gap = (interiorHeight - totalOpenings * effectiveHeight) / (totalOpenings + 1);
        }

        if (effectiveHeight <= 0)
            return;
        if (gap < 1) gap = 1;

        // 1) Paredes laterales sólidas (con profundidad)
        for (int y = interiorMinY; y <= interiorMaxY; y++)
        {
            for (int dx = 0; dx < effectiveDepth; dx++)
            {
                // lado izquierdo (0..effectiveDepth-1)
                if (dx < width)
                    map[dx, y] = 1;

                // lado derecho (width-1 .. width-effectiveDepth)
                int rx = width - 1 - dx;
                if (rx >= 0)
                    map[rx, y] = 1;
            }
        }

        // 2) Calculamos posiciones verticales de los centros de cada hueco
        float segmentSize = interiorHeight / (float)(totalOpenings + 1);
        for (int i = 0; i < totalOpenings; i++)
        {
            float centerYFloat = interiorMinY + (i + 1) * segmentSize;
            int centerY = Mathf.RoundToInt(centerYFloat);

            int startY = centerY - effectiveHeight / 2;
            if (startY < interiorMinY)
                startY = interiorMinY;
            if (startY + effectiveHeight - 1 > interiorMaxY)
                startY = interiorMaxY - effectiveHeight + 1;

            int endY = startY + effectiveHeight - 1;

            // 3) Elegimos en qué lado va este hueco
            bool openOnLeft;
            if (totalOpenings == 1)
            {
                // Uno solo: moneda al aire (o fijo a un lado si prefieres)
                if (prng == null)
                    openOnLeft = true;
                else
                    openOnLeft = prng.NextDouble() < 0.5;
            }
            else
            {
                // Varios: alternamos lados para repartirlos
                openOnLeft = (i % 2 == 0); // 0,2,4.. izquierda; 1,3,5.. derecha
            }

            // 4) Tallamos el hueco SOLO en el lado elegido
            for (int y = startY; y <= endY; y++)
            {
                if (y <= 0 || y >= height - 1)
                    continue;

                if (openOnLeft)
                {
                    // izquierda: despejamos 0..effectiveDepth-1
                    for (int dx = 0; dx < effectiveDepth; dx++)
                    {
                        if (dx < width)
                            map[dx, y] = 0;
                    }
                }
                else
                {
                    // derecha: despejamos width-1 .. width-effectiveDepth
                    for (int dx = 0; dx < effectiveDepth; dx++)
                    {
                        int rx = width - 1 - dx;
                        if (rx >= 0)
                            map[rx, y] = 0;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Añade "ruido" en forma de pequeñas plataformas flotantes.
    /// No toca bordes, ni el suelo, ni el techo.
    /// Controlado por platformNoisePercent y longitudes min/max.
    /// </summary>
    private void AddPlatformNoise(int[,] map, int width, int height)
    {
        // Zona vertical jugable para plataformas, lejos del suelo y del techo
        int minY = 2;              // por encima de las filas 0 y 1
        int maxY = height - 3;     // por debajo de height-1 y height-2

        if (maxY <= minY)
            return;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                // Solo intentamos crear plataforma donde ahora mismo es hueco
                if (map[x, y] != 0)
                    continue;

                // Probabilidad de empezar una plataforma aquí
                if (prng.Next(0, 100) >= platformNoisePercent)
                    continue;

                int length = prng.Next(platformMinLength, platformMaxLength + 1);
                int dir = (prng.NextDouble() < 0.5) ? -1 : 1; // izquierda o derecha

                int startX = x;
                for (int i = 0; i < length; i++)
                {
                    int px = startX + i * dir;

                    // Nos salimos de los límites laterales: paramos
                    if (px <= 0 || px >= width - 1)
                        break;

                    // Si hay muro ya, paramos la plataforma
                    if (map[px, y] != 0)
                        break;

                    // Opcional: que parezca flotante (hueco arriba y abajo)
                    if (map[px, y - 1] == 0 && map[px, y + 1] == 0)
                    {
                        map[px, y] = 1;
                    }
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
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
                }
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
