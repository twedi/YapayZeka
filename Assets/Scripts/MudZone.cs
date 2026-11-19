using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Çamur alanı.
///
///  - Aynı objedeki NavMeshModifierVolume, kapladığı NavMesh bölgesini "Mud" alanı olarak işaretler.
///  - "Mud" alanının geçiş maliyeti yüksektir (varsayılan 10). A* yol bulma maliyeti
///    mesafe x alan maliyeti olarak hesapladığı için düşman çamurun içinden geçmek yerine
///    etrafından dolanmayı tercih eder. Hedef çamurun içindeyse mecburen girer.
///  - Çamura giren (düşman veya oyuncu) gerçekten de yavaşlar.
/// </summary>
[RequireComponent(typeof(NavMeshModifierVolume))]
public class MudZone : MonoBehaviour
{
    private static readonly List<MudZone> zones = new List<MudZone>();

    [Header("Yol Bulma")]
    [Tooltip("Project Settings > Navigation > Areas içindeki 'Mud' alanının adı.")]
    public string areaName = "Mud";
    [Tooltip("Çamurdan geçmenin maliyeti. Normal zemin = 1. Ne kadar yüksekse düşman o kadar uzaktan dolanır.")]
    public float areaCost = 20f;

    [Header("Fiziksel Etki")]
    [Tooltip("Çamurun içindeyken hız çarpanı.")]
    [Range(0.1f, 1f)] public float speedMultiplier = 0.4f;

    [Header("Görsel")]
    public Color mudColor = new Color(0.36f, 0.24f, 0.12f);
    public bool showLabel = true;
    [Tooltip("Etiketteki ad (boşsa 'ÇAMUR'). Örn. ikinci alan için 'SIĞ SU'.")]
    public string labelText = "";

    private Bounds bounds;

    void Awake()
    {
        NavMeshModifierVolume volume = GetComponent<NavMeshModifierVolume>();

        int area = NavMesh.GetAreaFromName(areaName);
        if (area < 0)
        {
            Debug.LogWarning($"'{areaName}' adında NavMesh alanı yok. Project Settings > Navigation > Areas'tan ekle.");
        }
        else
        {
            volume.area = area;
            NavMesh.SetAreaCost(area, areaCost);
            // Sahnedeki ajanlar kendi maliyet tablolarını tutar, onları da güncelle
            foreach (NavMeshAgent agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
                agent.SetAreaCost(area, areaCost);
        }

        // Görsel zemin (düz küp) bake'e dahil olmasın; alanı sadece volume belirlesin
        NavMeshRebaker.ExcludeFromBake(gameObject);

        Renderer r = GetComponent<Renderer>();
        if (r != null)
        {
            bounds = r.bounds;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", mudColor);
            mpb.SetColor("_Color", mudColor);
            r.SetPropertyBlock(mpb);
        }
        else
        {
            bounds = new Bounds(transform.position, transform.lossyScale);
        }

        if (showLabel) CreateLabel();
    }

    void OnEnable()
    {
        zones.Add(this);
        NavMeshRebaker.Rebaked += CheckBaked;
    }

    void OnDisable()
    {
        zones.Remove(this);
        NavMeshRebaker.Rebaked -= CheckBaked;
    }

    /// <summary>Bake'ten sonra çamurun NavMesh'e gerçekten işlenip işlenmediğini kontrol eder.</summary>
    void CheckBaked()
    {
        int area = NavMesh.GetAreaFromName(areaName);
        if (area < 0) return;

        bool ok = NavMesh.SamplePosition(bounds.center, out NavMeshHit hit, 3f, 1 << area);
        if (ok)
            Debug.Log($"{name}: NavMesh'te '{areaName}' alanı aktif (maliyet x{NavMesh.GetAreaCost(area)}).");
        else
            Debug.LogWarning($"{name}: NavMesh'te '{areaName}' alanı bulunamadı! NavMeshSurface'i yeniden bake et.");
    }

    bool Contains(Vector3 position)
    {
        position.y = bounds.center.y;
        return bounds.Contains(position);
    }

    /// <summary>Verilen pozisyondaki hız çarpanı (çamurdaysa &lt; 1).</summary>
    public static float SpeedMultiplierAt(Vector3 position)
    {
        float m = 1f;
        foreach (MudZone z in zones)
            if (z.Contains(position)) m = Mathf.Min(m, z.speedMultiplier);
        return m;
    }

    /// <summary>Hafta 4 ızgara A*'ı için: bu noktadaki hücre maliyeti (alan yoksa 1).</summary>
    public static float CostAt(Vector3 position)
    {
        float c = 1f;
        foreach (MudZone z in zones)
            if (z.Contains(position)) c = Mathf.Max(c, z.areaCost);
        return c;
    }

    void CreateLabel()
    {
        GameObject go = new GameObject(name + "_Etiket");
        TextMesh t = go.AddComponent<TextMesh>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
        string title = string.IsNullOrEmpty(labelText) ? "ÇAMUR" : labelText;
        t.text = $"{title}\nmaliyet x{areaCost:0}";
        t.anchor = TextAnchor.MiddleCenter;
        t.alignment = TextAlignment.Center;
        t.fontSize = 64;
        t.characterSize = 0.2f;
        t.color = new Color(1f, 0.85f, 0.6f);
        go.transform.position = bounds.center + Vector3.up * 0.3f;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // zemine yatık, yukarıdan okunur
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.55f, 0.35f, 0.15f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, Vector3.one);
    }
}
