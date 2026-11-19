using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Hafta 5 – Waypoint grafı ve grafta A*.
///  - Düğümler: bu objenin çocukları (DevriyeRotasi'ndaki WP_xx noktaları).
///  - Kenarlar: birbirini gören (Linecast) ve maxEdgeLength'ten yakın her çift. Komşuluk listesiyle tutulur.
///  - T: düşmana en yakın görünen düğümden oyuncuya en yakın görünen düğüme A* (Öklid sezgisel).
///    Konsola yol, uzunluk, A* ve Dijkstra'nın genişlettiği düğüm sayısı yazılır.
///    Gri çizgi: A*'ın yolu, mavi çizgi: görüş hattıyla yumuşatılmış yol (ödevin ek görevi).
/// </summary>
public class WaypointGraph : MonoBehaviour
{
    public float maxEdgeLength = 22f;
    [Tooltip("Görüş ışınlarının zeminden yüksekliği.")]
    public float rayHeight = 1.5f;
    public Transform npc;       // düşman
    public Transform player;
    public float showSeconds = 6f;

    Transform[] nodes = new Transform[0];
    readonly List<List<int>> neighbors = new List<List<int>>();   // komşuluk listesi
    LineRenderer rawLine, smoothLine;
    float hideTime;

    void Awake()
    {
        rawLine = MakeLine("WaypointA*_Yol", new Color(0.8f, 0.8f, 0.8f, 0.9f));
        smoothLine = MakeLine("WaypointA*_Yumusak", new Color(0.2f, 0.5f, 1f, 0.9f));
    }

    void Start() => BuildEdges();

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame) RunTest();
        if (hideTime > 0f && Time.time > hideTime)
        {
            rawLine.positionCount = 0;
            smoothLine.positionCount = 0;
            hideTime = 0f;
        }
    }

    [ContextMenu("Kenarları Kur")]
    public void BuildEdges()
    {
        var list = new List<Transform>();
        foreach (Transform c in transform)
            if (c.GetComponent<Renderer>() == null && c.GetComponent<LineRenderer>() == null) list.Add(c);   // görselleri atla
        nodes = list.ToArray();

        neighbors.Clear();
        int edgeCount = 0;
        for (int a = 0; a < nodes.Length; a++)
        {
            var n = new List<int>();
            for (int b = 0; b < nodes.Length; b++)
            {
                if (a == b) continue;
                Vector3 pa = nodes[a].position, pb = nodes[b].position;
                if (Vector3.Distance(pa, pb) <= maxEdgeLength && CanSee(pa, pb)) n.Add(b);
            }
            edgeCount += n.Count;
            neighbors.Add(n);
        }
        Debug.Log($"Waypoint grafı: {nodes.Length} düğüm, {edgeCount / 2} kenar (yönsüz)");
    }

    /// <summary>Arada engel var mı? Oyuncu ve ajanların gövdesi engel sayılmaz.</summary>
    public bool CanSee(Vector3 from, Vector3 to)
    {
        from.y += rayHeight; to.y += rayHeight;
        Vector3 d = to - from;
        foreach (RaycastHit h in Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider is TerrainCollider) continue;
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (h.collider.GetComponentInParent<NavMeshAgent>() != null) continue;
            return false;
        }
        return true;
    }

    /// <summary>Grafa binme: görünen en yakın düğüm (duvar arkasındaki sayılmaz).</summary>
    public int Nearest(Vector3 p)
    {
        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < nodes.Length; i++)
        {
            float d = (nodes[i].position - p).sqrMagnitude;
            if (d < bestD && CanSee(p, nodes[i].position)) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>Grafta A* (useHeuristic=false ise Dijkstra). expandedCount: genişletilen düğüm sayısı.</summary>
    public List<int> FindPath(int start, int goal, bool useHeuristic, out int expandedCount)
    {
        expandedCount = 0;
        var g = new Dictionary<int, float> { [start] = 0f };
        var parent = new Dictionary<int, int>();
        var closed = new HashSet<int>();
        var open = new MinHeap<int>();
        open.Push(start, H(start, goal, useHeuristic));

        while (open.Count > 0)
        {
            int cur = open.Pop();
            if (closed.Contains(cur)) continue;
            closed.Add(cur);
            expandedCount++;
            if (cur == goal)
            {
                var path = new List<int>();
                for (int c = goal; ; c = parent[c]) { path.Add(c); if (c == start) break; }
                path.Reverse();
                return path;
            }

            foreach (int next in neighbors[cur])                     // dört yön değil, komşu listesi
            {
                if (closed.Contains(next)) continue;
                float newG = g[cur] + Vector3.Distance(nodes[cur].position, nodes[next].position);   // maliyet = uzaklık
                if (g.TryGetValue(next, out float old) && newG >= old) continue;
                g[next] = newG;
                parent[next] = cur;
                open.Push(next, newG + H(next, goal, useHeuristic));
            }
        }
        return null;
    }

    float H(int a, int b, bool use) => use ? Vector3.Distance(nodes[a].position, nodes[b].position) : 0f;   // Öklid

    void RunTest()
    {
        if (npc == null || player == null || nodes.Length == 0) return;

        int start = Nearest(npc.position);
        int goal = Nearest(player.position);
        if (start < 0 || goal < 0) { Debug.Log("Waypoint A*: görünen düğüm yok"); return; }

        List<int> path = FindPath(start, goal, true, out int astarExp);
        FindPath(start, goal, false, out int dijkstraExp);
        if (path == null) { Debug.Log("Waypoint A*: yol yok"); return; }

        // NPC → ilk düğüm → ... → son düğüm → oyuncu
        var points = new List<Vector3> { npc.position };
        foreach (int i in path) points.Add(nodes[i].position);
        points.Add(player.position);

        List<Vector3> smooth = Smooth(points);
        string names = string.Join(" → ", path.ConvertAll(i => nodes[i].name));
        Debug.Log($"Waypoint A*: {names}\n  A* genişletilen: {astarExp}, Dijkstra: {dijkstraExp}, " +
                  $"yol: {Length(points):F1} m, yumuşatılmış: {Length(smooth):F1} m");

        SetLine(rawLine, points);
        SetLine(smoothLine, smooth);
        hideTime = Time.time + showSeconds;
    }

    /// <summary>Yol yumuşatma: her noktadan görülebilen en uzak noktaya düz git, aradakileri at.</summary>
    List<Vector3> Smooth(List<Vector3> pts)
    {
        var result = new List<Vector3> { pts[0] };
        int i = 0;
        while (i < pts.Count - 1)
        {
            int far = i + 1;
            for (int j = pts.Count - 1; j > i + 1; j--)
                if (CanSee(pts[i], pts[j])) { far = j; break; }
            result.Add(pts[far]);
            i = far;
        }
        return result;
    }

    static float Length(List<Vector3> pts)
    {
        float l = 0f;
        for (int i = 0; i < pts.Count - 1; i++) l += Vector3.Distance(pts[i], pts[i + 1]);
        return l;
    }

    LineRenderer MakeLine(string n, Color c)
    {
        GameObject go = new GameObject(n);
        NavMeshRebaker.ExcludeFromBake(go);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.35f;
        lr.startColor = lr.endColor = c;
        lr.positionCount = 0;
        return lr;
    }

    static void SetLine(LineRenderer lr, List<Vector3> pts)
    {
        lr.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++) lr.SetPosition(i, new Vector3(pts[i].x, 0.6f, pts[i].z));
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || neighbors.Count != nodes.Length) return;
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        for (int a = 0; a < nodes.Length; a++)
            foreach (int b in neighbors[a])
                if (a < b) Gizmos.DrawLine(nodes[a].position + Vector3.up, nodes[b].position + Vector3.up);
    }
}
