using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Devriye rotası. Bu objenin çocukları (child) sırasıyla waypoint olarak kullanılır.
/// Sahnede waypoint eklemek / taşımak / sıralamak için sadece çocuk objeleri düzenlemen yeterli.
///
/// Görselleştirme:
///  - Editörde (Scene view): Gizmo olarak noktalar, numaralar ve rota çizgisi.
///  - Oyunda (Game view): LineRenderer ile rota çizgisi + her waypoint için zemin işareti.
///    Düşmanın o an gittiği waypoint farklı renkte vurgulanır.
/// </summary>
public class PatrolRoute : MonoBehaviour
{
    [Header("Rota")]
    [Tooltip("Açıksa son noktadan sonra ilk noktaya döner (döngü). Kapalıysa ileri-geri (ping-pong) gider.")]
    public bool loop = true;

    [Tooltip("Oyun başında waypoint'leri en yakın NavMesh noktasına yapıştır.")]
    public bool snapToNavMesh = true;

    [Header("Oyun İçi Görselleştirme")]
    public bool showInGame = true;
    public Color routeColor = new Color(0.2f, 0.8f, 1f, 0.6f);
    public Color waypointColor = new Color(1f, 1f, 1f, 0.8f);
    public Color activeWaypointColor = new Color(1f, 0.85f, 0.1f, 1f);
    public float lineWidth = 0.25f;
    public float markerSize = 1.2f;
    [Tooltip("Çizgilerin zeminin içine gömülmemesi için yükseklik ofseti.")]
    public float heightOffset = 0.15f;

    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<Renderer> markers = new List<Renderer>();
    private LineRenderer routeLine;
    private MaterialPropertyBlock mpb;
    private int highlighted = -1;

    public int Count => points.Count;

    void Awake()
    {
        CachePoints();
        if (showInGame) BuildVisuals();
    }

    /// <summary>Çocuk objelerden waypoint pozisyonlarını toplar.</summary>
    void CachePoints()
    {
        points.Clear();
        foreach (Transform child in transform)
        {
            Vector3 p = child.position;
            if (snapToNavMesh && NavMesh.SamplePosition(p, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                p = hit.position;
            points.Add(p);
        }
    }

    public Vector3 GetPoint(int index) => points[index];

    /// <summary>Rotadaki bir sonraki indeksi verir (döngü veya ping-pong).</summary>
    public int NextIndex(int current, ref int direction)
    {
        if (points.Count <= 1) return 0;

        if (loop) return (current + 1) % points.Count;

        int next = current + direction;
        if (next >= points.Count || next < 0)
        {
            direction = -direction;
            next = current + direction;
        }
        return next;
    }

    /// <summary>Verilen pozisyona en yakın waypoint indeksini verir (kovalamacadan sonra rotaya dönüş için).</summary>
    public int ClosestIndex(Vector3 position)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            float d = (points[i] - position).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    // ---------------------------------------------------------------- Görsel

    void BuildVisuals()
    {
        if (points.Count == 0) return;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mpb = new MaterialPropertyBlock();

        // Rota çizgisi
        GameObject lineObj = new GameObject("RotaCizgisi");
        lineObj.transform.SetParent(transform, false);
        routeLine = lineObj.AddComponent<LineRenderer>();
        routeLine.sharedMaterial = mat;
        routeLine.useWorldSpace = true;
        routeLine.loop = loop;
        routeLine.widthMultiplier = lineWidth;
        routeLine.startColor = routeLine.endColor = routeColor;
        routeLine.numCornerVertices = 2;
        routeLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        routeLine.receiveShadows = false;
        routeLine.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            routeLine.SetPosition(i, points[i] + Vector3.up * heightOffset);

        // Waypoint işaretleri (zemine yatık ince silindirler)
        for (int i = 0; i < points.Count; i++)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "WaypointIsareti_" + i;
            Destroy(marker.GetComponent<Collider>()); // fizik / görüş raycast'lerini engellemesin
            marker.transform.SetParent(transform, true);
            marker.transform.position = points[i] + Vector3.up * heightOffset;
            marker.transform.localScale = new Vector3(markerSize, 0.02f, markerSize);

            Renderer r = marker.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            markers.Add(r);
            SetMarkerColor(i, waypointColor);
        }
    }

    /// <summary>Düşmanın şu an hedeflediği waypoint'i vurgular. -1 verilirse vurgu kalkar.</summary>
    public void Highlight(int index)
    {
        if (index == highlighted || markers.Count == 0) return;

        if (highlighted >= 0 && highlighted < markers.Count)
            SetMarkerColor(highlighted, waypointColor);

        highlighted = index;

        if (highlighted >= 0 && highlighted < markers.Count)
            SetMarkerColor(highlighted, activeWaypointColor);
    }

    /// <summary>Rotanın görünürlüğünü / rengini değiştirir (ör. kovalamaca sırasında soluklaştırmak için).</summary>
    public void SetRouteAlpha(float alpha)
    {
        if (routeLine == null) return;
        Color c = routeColor;
        c.a *= alpha;
        routeLine.startColor = routeLine.endColor = c;
    }

    void SetMarkerColor(int index, Color color)
    {
        // Sprites/Default shader'ı _Color ve _RendererColor kullanır
        markers[index].GetPropertyBlock(mpb);
        mpb.SetColor("_Color", color);
        mpb.SetColor("_RendererColor", color);
        markers[index].SetPropertyBlock(mpb);
    }

    // ---------------------------------------------------------------- Gizmo (Editör)

    void OnDrawGizmos()
    {
        int count = transform.childCount;
        if (count == 0) return;

        Gizmos.color = routeColor;
        for (int i = 0; i < count; i++)
        {
            Vector3 a = transform.GetChild(i).position;
            Gizmos.DrawSphere(a, 0.4f);

            bool hasNext = i + 1 < count;
            if (hasNext || loop)
            {
                Vector3 b = transform.GetChild(hasNext ? i + 1 : 0).position;
                Gizmos.DrawLine(a, b);

                // Yön oku
                Vector3 dir = (b - a);
                if (dir.sqrMagnitude > 0.01f)
                {
                    Vector3 mid = (a + b) * 0.5f;
                    Vector3 back = -dir.normalized * 0.8f;
                    Gizmos.DrawLine(mid, mid + Quaternion.Euler(0, 30, 0) * back);
                    Gizmos.DrawLine(mid, mid + Quaternion.Euler(0, -30, 0) * back);
                }
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(a + Vector3.up * 1f, i.ToString());
#endif
        }
    }
}
