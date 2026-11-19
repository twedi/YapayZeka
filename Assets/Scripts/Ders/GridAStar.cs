using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Hafta 4 – Kendi yol bulucumuz: arazinin üstüne serilmiş ızgarada DFS / BFS / Dijkstra / A*.
///  - Sol tık: oyuncunun bulunduğu hücreden tıklanan hücreye yol bulur, sarı çizgiyle çizer.
///  - 1 / 2 / 3 / 4: algoritma seçimi. Konsola yol uzunluğu, maliyet ve genişletilen düğüm sayısı yazılır.
///  - Hücre maliyeti (Dağ Yolu fikri): çamur gibi alanlarda MudZone maliyeti, eğimde ek maliyet.
///  - Engeller her aramada yeniden okunur; kapı kapanınca ızgara da bunu görür.
/// </summary>
public class GridAStar : MonoBehaviour
{
    public enum Algorithm { DFS, BFS, Dijkstra, AStar }

    [Header("Izgara")]
    public Vector3 origin = Vector3.zero;   // sol alt köşe
    public int width = 50, depth = 50;
    public float cellSize = 2f;
    [Tooltip("Engel kontrolünde kutunun zeminden yüksekliği.")]
    public float probeHeight = 2.5f;
    [Tooltip("Bu açıdan dik hücreler yürünemez; altındakiler eğime göre pahalılaşır.")]
    public float maxSlope = 45f;

    [Header("Arama")]
    public Algorithm algorithm = Algorithm.AStar;
    public Transform startFrom;              // oyuncu
    public Camera cam;
    public bool drawExpanded = true;

    [Header("Görsel")]
    public Color pathColor = new Color(1f, 0.9f, 0.1f);
    public float lineLift = 0.4f;

    bool[,] walkable;
    float[,] cellCost;
    readonly List<Vector2Int> path = new List<Vector2Int>();
    readonly HashSet<Vector2Int> expanded = new HashSet<Vector2Int>();
    LineRenderer line;
    Terrain terrain;

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    void Awake()
    {
        terrain = Terrain.activeTerrain;
        if (cam == null) cam = Camera.main;

        NavMeshRebaker.ExcludeFromBake(gameObject);
        line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        line.useWorldSpace = true;
        line.widthMultiplier = 0.3f;
        line.numCornerVertices = 2;
        line.startColor = line.endColor = pathColor;
        line.positionCount = 0;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) SetAlgorithm(Algorithm.DFS);
            if (kb.digit2Key.wasPressedThisFrame) SetAlgorithm(Algorithm.BFS);
            if (kb.digit3Key.wasPressedThisFrame) SetAlgorithm(Algorithm.Dijkstra);
            if (kb.digit4Key.wasPressedThisFrame) SetAlgorithm(Algorithm.AStar);
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || cam == null || startFrom == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore)) return;

        FindPath(WorldToCell(startFrom.position), WorldToCell(hit.point));
    }

    void SetAlgorithm(Algorithm a)
    {
        algorithm = a;
        Debug.Log($"Izgara algoritması: {a}");
    }

    // ---------------------------------------------------------------- Izgara

    public Vector2Int WorldToCell(Vector3 p)
    {
        Vector3 local = p - origin;
        return new Vector2Int(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(local.z / cellSize));
    }

    public Vector3 CellToWorld(Vector2Int c)
    {
        Vector3 p = origin + new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);
        p.y = GroundHeight(p);
        return p;
    }

    bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < depth;

    float GroundHeight(Vector3 p) =>
        terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : origin.y;

    float SlopeAt(Vector3 p)
    {
        if (terrain == null) return 0f;
        Vector3 local = p - terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        Vector3 n = terrain.terrainData.GetInterpolatedNormal(local.x / size.x, local.z / size.z);
        return Mathf.Acos(Mathf.Clamp(n.y, -1f, 1f)) * Mathf.Rad2Deg;    // Hafta 3: arccos(n·y)
    }

    /// <summary>Engelleri ve maliyetleri sahneden okur.</summary>
    void ScanGrid()
    {
        walkable = new bool[width, depth];
        cellCost = new float[width, depth];
        Vector3 half = new Vector3(cellSize * 0.45f, probeHeight * 0.5f, cellSize * 0.45f);
        Collider[] buffer = new Collider[16];

        for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
            {
                Vector3 p = CellToWorld(new Vector2Int(x, z));
                Vector3 center = p + Vector3.up * (0.3f + half.y);
                int n = Physics.OverlapBoxNonAlloc(center, half, buffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

                bool blocked = false;
                for (int i = 0; i < n && !blocked; i++)
                    blocked = IsObstacle(buffer[i]);

                float slope = SlopeAt(p);
                walkable[x, z] = !blocked && slope <= maxSlope;
                // Dağ Yolu: eğim arttıkça pahalılaşır (0° → 1, 45° → 2), çamur gibi alanlar çarpan ekler
                cellCost[x, z] = (1f + slope / maxSlope) * MudZone.CostAt(p);
            }
    }

    static bool IsObstacle(Collider c)
    {
        if (c is TerrainCollider) return false;
        if (c.GetComponentInParent<CharacterController>() != null) return false;   // oyuncu
        if (c.GetComponentInParent<NavMeshAgent>() != null) return false;          // düşman
        return true;
    }

    // ---------------------------------------------------------------- Arama

    public void FindPath(Vector2Int start, Vector2Int goal)
    {
        ScanGrid();
        path.Clear();
        expanded.Clear();
        line.positionCount = 0;

        if (!InBounds(start) || !InBounds(goal) || !walkable[goal.x, goal.y])
        {
            Debug.Log($"{goal}: engel ya da ızgara dışı");
            return;
        }
        walkable[start.x, start.y] = true;   // oyuncunun hücresi her zaman başlangıç olabilir

        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var g = new Dictionary<Vector2Int, float> { [start] = 0f };
        bool found = false;
        float t0 = Time.realtimeSinceStartup;

        if (algorithm == Algorithm.DFS || algorithm == Algorithm.BFS)
        {
            var frontier = new LinkedList<Vector2Int>();
            frontier.AddLast(start);
            var seen = new HashSet<Vector2Int> { start };
            while (frontier.Count > 0)
            {
                Vector2Int cur;
                if (algorithm == Algorithm.DFS) { cur = frontier.Last.Value; frontier.RemoveLast(); }   // yığın
                else { cur = frontier.First.Value; frontier.RemoveFirst(); }                              // kuyruk
                expanded.Add(cur);
                if (cur == goal) { found = true; break; }

                foreach (Vector2Int d in Dirs)
                {
                    Vector2Int nb = cur + d;
                    if (!InBounds(nb) || !walkable[nb.x, nb.y] || seen.Contains(nb)) continue;
                    seen.Add(nb);
                    parent[nb] = cur;
                    g[nb] = g[cur] + StepCost(nb);
                    frontier.AddLast(nb);
                }
            }
        }
        else
        {
            var open = new MinHeap<Vector2Int>();
            var closed = new HashSet<Vector2Int>();
            open.Push(start, H(start, goal));
            while (open.Count > 0)
            {
                Vector2Int cur = open.Pop();
                if (closed.Contains(cur)) continue;     // eski (daha pahalı) kayıt
                closed.Add(cur);
                expanded.Add(cur);
                if (cur == goal) { found = true; break; }

                foreach (Vector2Int d in Dirs)
                {
                    Vector2Int nb = cur + d;
                    if (!InBounds(nb) || !walkable[nb.x, nb.y] || closed.Contains(nb)) continue;
                    float newG = g[cur] + StepCost(nb);
                    if (g.TryGetValue(nb, out float oldG) && newG >= oldG) continue;
                    g[nb] = newG;
                    parent[nb] = cur;
                    open.Push(nb, newG + H(nb, goal));
                }
            }
        }

        float ms = (Time.realtimeSinceStartup - t0) * 1000f;
        if (!found)
        {
            Debug.Log($"{algorithm}: yol yok, {expanded.Count} düğüm genişletildi");
            return;
        }

        for (Vector2Int c = goal; ; c = parent[c])
        {
            path.Add(c);
            if (c == start) break;
        }
        path.Reverse();

        line.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
            line.SetPosition(i, CellToWorld(path[i]) + Vector3.up * lineLift);

        Debug.Log($"{algorithm}: {path.Count} hücre, maliyet {g[goal]:F1}, {expanded.Count} düğüm genişletildi ({ms:F2} ms)");
    }

    float StepCost(Vector2Int c) => cellSize * cellCost[c.x, c.y];

    // A*: Manhattan (dört yönlü harekette kabul edilebilir; hücre maliyeti en az 1). Dijkstra: h = 0.
    float H(Vector2Int a, Vector2Int b) =>
        algorithm == Algorithm.AStar ? (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y)) * cellSize : 0f;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
        Vector3 size = new Vector3(width * cellSize, 0.1f, depth * cellSize);
        Gizmos.DrawWireCube(origin + size * 0.5f, size);

        if (!drawExpanded || expanded.Count == 0) return;
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
        foreach (Vector2Int c in expanded)
            Gizmos.DrawCube(CellToWorld(c) + Vector3.up * 0.1f, new Vector3(cellSize * 0.9f, 0.05f, cellSize * 0.9f));
    }
}
