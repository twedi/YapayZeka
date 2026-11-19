using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Hafta 6 – PathDebugDraw + AreaCostToggle'ın birleşimi (düşmanın üzerinde).
///  - Ekranın sol üstünde: durum, köşe sayısı, yol uzunluğu, alan maliyetli toplam, PathStatus.
///  - Yolun durumu değişince konsola yazar.
///  - M (Player/CycleMudCost): bu ajanın çamur maliyetini 1 → 2 → 5 → 10 → 20 diye döndürür.
///  - J (Player/ToggleLink): zıplama bağlantısını açar / kapatır.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NavDebugPanel : MonoBehaviour
{
    public NavMeshLink jumpLink;
    public string mudAreaName = "Mud";
    public float[] mudCosts = { 1f, 2f, 5f, 10f, 20f };
    public float sampleStep = 0.25f;

    NavMeshAgent agent;
    EnemyAI enemy;
    InputAction cycleMud, toggleLink;
    int mudArea, costIndex = 4;
    float length, cost;
    int cornerCount;
    NavMeshPathStatus lastStatus = NavMeshPathStatus.PathComplete;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemy = GetComponent<EnemyAI>();
        if (InputSystem.actions != null)
        {
            cycleMud = InputSystem.actions.FindAction("Player/CycleMudCost");
            toggleLink = InputSystem.actions.FindAction("Player/ToggleLink");
        }
        mudArea = NavMesh.GetAreaFromName(mudAreaName);
    }

    void Start()
    {
        // Başlangıçta MudZone'un verdiği maliyete en yakın kademeyi seç
        if (mudArea >= 0)
        {
            float current = agent.GetAreaCost(mudArea);
            for (int i = 0; i < mudCosts.Length; i++)
                if (Mathf.Approximately(mudCosts[i], current)) costIndex = i;
        }
    }

    void Update()
    {
        if (cycleMud != null && cycleMud.WasPressedThisFrame() && mudArea >= 0)
        {
            costIndex = (costIndex + 1) % mudCosts.Length;
            agent.SetAreaCost(mudArea, mudCosts[costIndex]);   // yalnızca bu ajan
            Debug.Log($"Çamur maliyeti: {mudCosts[costIndex]}");
        }

        if (toggleLink != null && toggleLink.WasPressedThisFrame() && jumpLink != null)
        {
            jumpLink.activated = !jumpLink.activated;
            Debug.Log($"Zıplama bağlantısı: {(jumpLink.activated ? "açık" : "kapalı")}");
        }

        if (agent.pathPending || !agent.hasPath) return;

        if (agent.pathStatus != lastStatus)
        {
            lastStatus = agent.pathStatus;
            Debug.Log($"Yol durumu: {lastStatus}");
        }

        Vector3[] c = agent.path.corners;
        cornerCount = c.Length;
        length = 0f;
        cost = 0f;
        for (int i = 0; i < c.Length - 1; i++)
        {
            float segLen = Vector3.Distance(c[i], c[i + 1]);
            length += segLen;
            cost += SegmentCost(c[i], c[i + 1], segLen);
        }
    }

    // g(rota) = Σ uzunluk × alan maliyeti (ders notu 5. bölüm)
    float SegmentCost(Vector3 a, Vector3 b, float segLen)
    {
        if (IsLink(a, b)) return segLen * agent.GetAreaCost(jumpLink.area);

        int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / sampleStep));
        float stepLen = segLen / steps;
        float total = 0f;
        for (int s = 0; s < steps; s++)
        {
            Vector3 p = Vector3.Lerp(a, b, (s + 0.5f) / steps);
            int area = 0;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
                area = AreaIndex(hit.mask);
            total += stepLen * agent.GetAreaCost(area);
        }
        return total;
    }

    bool IsLink(Vector3 a, Vector3 b)
    {
        if (jumpLink == null || !jumpLink.activated) return false;
        // Geniş link: uçlar link çizgisinin herhangi bir yerinde olabilir; link ekseninde mi bak
        Vector3 s = jumpLink.transform.position + jumpLink.transform.rotation * jumpLink.startPoint;
        Vector3 e = jumpLink.transform.position + jumpLink.transform.rotation * jumpLink.endPoint;
        Vector3 axis = e - s; axis.y = 0f;
        Vector3 seg = b - a; seg.y = 0f;
        if (seg.sqrMagnitude < 0.01f || axis.sqrMagnitude < 0.01f) return false;
        bool parallel = Mathf.Abs(Vector3.Dot(seg.normalized, axis.normalized)) > 0.95f;
        bool sameLength = Mathf.Abs(seg.magnitude - axis.magnitude) < 0.5f;
        return parallel && sameLength;
    }

    static int AreaIndex(int mask)
    {
        for (int i = 0; i < 32; i++)
            if ((mask & (1 << i)) != 0) return i;
        return 0;
    }

    void OnGUI()
    {
        string link = (jumpLink != null && jumpLink.activated) ? "açık" : "kapalı";
        string mud = mudArea >= 0 ? mudCosts[costIndex].ToString() : "-";
        string state = enemy != null ? enemy.StateName : "-";
        GUI.Box(new Rect(5, 5, 470, 82), GUIContent.none);
        GUI.Label(new Rect(10, 8, 460, 22), $"Durum: {state}    Yol: {agent.pathStatus}");
        GUI.Label(new Rect(10, 28, 460, 22), $"Köşe: {cornerCount}   Uzunluk: {length:F2} m   Maliyet: {cost:F2}");
        GUI.Label(new Rect(10, 48, 460, 22), $"[M] Çamur maliyeti: {mud}   [J] Bağlantı: {link}   [E] Kapı   [N] Ses");
        GUI.Label(new Rect(10, 66, 460, 22), $"[Sol tık] Izgara A*   [1-4] DFS/BFS/Dijkstra/A*   [T] Waypoint A*   [C] Kamera");
    }

    void OnDrawGizmos()
    {
        if (agent == null || !agent.hasPath) return;
        Gizmos.color = Color.yellow;
        foreach (Vector3 p in agent.path.corners)
            Gizmos.DrawSphere(p, 0.3f);
    }
}
