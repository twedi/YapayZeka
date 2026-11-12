using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Zıplanabilir duvarın üzerinden geçen NavMeshLink'i yönetir ve görselleştirir.
///
/// Yol bulma bu link'i normal bir yol parçası gibi görür. Link'i kullanmanın maliyeti
/// "link uzunluğu x Jump alan maliyeti" olur. A* bunu duvarın etrafından dolanmanın
/// maliyetiyle karşılaştırır:
///   - Düşman duvarın ortasına yakınsa dolanmak pahalıdır, zıplar.
///   - Duvarın ucuna yakınsa dolanmak daha ucuzdur, zıplamaz.
/// Maliyeti artırdıkça düşman daha az zıplar.
/// </summary>
[RequireComponent(typeof(NavMeshLink))]
public class JumpLink : MonoBehaviour
{
    [Header("Maliyet")]
    [Tooltip("Project Settings > Navigation > Areas içindeki alan adı.")]
    public string areaName = "Jump";
    [Tooltip("Zıplamanın maliyet çarpanı. Normal zemin = 1.")]
    public float areaCost = 2f;

    [Header("Görsel")]
    public bool showInGame = true;
    public Color arcColor = new Color(0.3f, 0.7f, 1f, 0.7f);
    [Tooltip("Link genişliği boyunca kaç tane yay çizilsin.")]
    [Range(1, 9)] public int arcCount = 5;
    public float arcHeight = 2f;
    public Color wallColor = new Color(0.45f, 0.55f, 0.7f);
    [Tooltip("Rengi değiştirilecek duvar (opsiyonel).")]
    public Renderer wallRenderer;

    private NavMeshLink link;

    void Awake()
    {
        link = GetComponent<NavMeshLink>();

        int area = NavMesh.GetAreaFromName(areaName);
        if (area < 0)
        {
            Debug.LogWarning($"'{areaName}' adında NavMesh alanı yok.");
        }
        else
        {
            link.area = area;
            NavMesh.SetAreaCost(area, areaCost);
            foreach (NavMeshAgent agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
                agent.SetAreaCost(area, areaCost);
        }

        if (wallRenderer != null)
        {
            var mpb = new MaterialPropertyBlock();
            wallRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", wallColor);
            mpb.SetColor("_Color", wallColor);
            wallRenderer.SetPropertyBlock(mpb);
        }

        if (showInGame) BuildVisuals();
    }

    // Link noktaları dünya koordinatında (NavMeshLink de scale'i yok sayar)
    Vector3 WorldPoint(Vector3 local) => transform.position + transform.rotation * local;

    Vector3 WidthAxis()
    {
        Vector3 dir = link.endPoint - link.startPoint;
        dir.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
        return transform.rotation * side;
    }

    void BuildVisuals()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        Vector3 a = WorldPoint(link.startPoint);
        Vector3 b = WorldPoint(link.endPoint);
        Vector3 side = WidthAxis();

        for (int i = 0; i < arcCount; i++)
        {
            float f = arcCount == 1 ? 0f : (i / (float)(arcCount - 1) - 0.5f);
            Vector3 offset = side * (link.width * f);

            GameObject go = new GameObject("ZiplamaYayi_" + i);
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.12f;
            lr.startColor = lr.endColor = arcColor;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            const int segments = 16;
            lr.positionCount = segments + 1;
            for (int s = 0; s <= segments; s++)
            {
                float t = s / (float)segments;
                Vector3 p = Vector3.Lerp(a, b, t) + offset + Vector3.up * (0.15f + arcHeight * 4f * (t - t * t));
                lr.SetPosition(s, p);
            }
        }

        // Etiket
        GameObject labelGo = new GameObject(name + "_Etiket");
        TextMesh tm = labelGo.AddComponent<TextMesh>();
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelGo.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        tm.text = $"ZIPLAMA\nmaliyet x{areaCost:0.#}";
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 64;
        tm.characterSize = 0.18f;
        tm.color = new Color(0.75f, 0.9f, 1f);
        labelGo.transform.position = (a + b) * 0.5f + side * (link.width * 0.5f + 1.5f) + Vector3.up * 0.3f;
        labelGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void OnDrawGizmos()
    {
        NavMeshLink l = link != null ? link : GetComponent<NavMeshLink>();
        if (l == null) return;
        link = l;

        Vector3 a = WorldPoint(l.startPoint);
        Vector3 b = WorldPoint(l.endPoint);
        Vector3 side = WidthAxis();

        Gizmos.color = arcColor;
        for (int i = 0; i < 3; i++)
        {
            Vector3 offset = side * (l.width * (i * 0.5f - 0.5f));
            Vector3 prev = a + offset;
            for (int s = 1; s <= 12; s++)
            {
                float t = s / 12f;
                Vector3 p = Vector3.Lerp(a, b, t) + offset + Vector3.up * (arcHeight * 4f * (t - t * t));
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
