using UnityEngine;

/// <summary>
/// Düşmanın görüş sistemi.
///  - Görüş mesafesi + görüş açısı (koni) içinde mi?
///  - Arada duvar var mı? (Raycast ile görüş hattı kontrolü)
///  - Çok yakındaysa (arkasında bile olsa) fark eder.
/// Ayrıca görüş konisini oyunda zemine çizer; koni duvarlara çarpınca kesilir.
/// </summary>
public class EnemyVision : MonoBehaviour
{
    [Header("Görüş")]
    public float viewDistance = 18f;
    [Range(1f, 360f)] public float viewAngle = 90f;
    [Tooltip("Bu mesafenin içindeyse açıdan bağımsız olarak fark edilir (ensesinde durmak gibi).")]
    public float proximityRadius = 3f;
    [Tooltip("Gözün zeminden yüksekliği (dünya birimi).")]
    public float eyeHeight = 1.5f;
    [Tooltip("Görüşü engelleyen katmanlar. Oyuncunun katmanı da dahil olmalı.")]
    public LayerMask obstacleMask = ~0;

    [Header("Görüş Konisi Görseli")]
    public bool showCone = true;
    [Range(8, 120)] public int coneResolution = 48;
    public float coneHeightOffset = 0.1f;

    private Mesh coneMesh;
    private Transform coneTransform;
    private Vector3[] vertices;
    private Color[] colors;
    private Color coneColor = new Color(0.2f, 1f, 0.3f, 0.25f);

    /// <summary>Göz pozisyonu (raycast'lerin çıktığı nokta).</summary>
    public Vector3 EyePosition => GroundPosition + Vector3.up * eyeHeight;

    /// <summary>Düşmanın ayak hizası.</summary>
    public Vector3 GroundPosition { get; private set; }

    void Start()
    {
        UpdateGroundPosition();
        if (showCone) BuildCone();
    }

    void LateUpdate()
    {
        UpdateGroundPosition();
        if (coneMesh != null) UpdateCone();
    }

    void OnDestroy()
    {
        if (coneTransform != null) Destroy(coneTransform.gameObject);
    }

    void UpdateGroundPosition()
    {
        // Capsule'un altından zemini bul (kendi collider'ımızın içinden başladığımız için ona çarpmaz)
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f, obstacleMask, QueryTriggerInteraction.Ignore))
            GroundPosition = hit.point;
        else
            GroundPosition = transform.position;
    }

    /// <summary>Hedef şu an görülüyor mu?</summary>
    public bool CanSee(Transform target)
    {
        if (target == null) return false;

        Vector3 eye = EyePosition;
        Vector3 toTarget = target.position - eye;
        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        float distance = flat.magnitude;

        if (distance > viewDistance) return false;

        bool inProximity = distance <= proximityRadius;
        if (!inProximity && Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f)
            return false;

        // Görüş hattı: ilk çarptığımız şey hedefin kendisi mi?
        if (Physics.Raycast(eye, toTarget.normalized, out RaycastHit hit, toTarget.magnitude + 1f,
                obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == target || hit.transform.IsChildOf(target);
        }

        // Hiçbir şeye çarpmadı (hedefin collider'ı yoksa) -> arada engel yok sayılır
        return true;
    }

    public void SetConeColor(Color color) => coneColor = color;

    // ---------------------------------------------------------------- Koni mesh'i

    void BuildCone()
    {
        // Ayrı bir obje: düşmanın scale'inden etkilenmesin, vertex'ler dünya koordinatında
        GameObject go = new GameObject(name + "_GorusAlani");
        coneTransform = go.transform;

        coneMesh = new Mesh { name = "VisionCone" };
        coneMesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = coneMesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        int vCount = coneResolution + 2;
        vertices = new Vector3[vCount];
        colors = new Color[vCount];

        int[] tris = new int[coneResolution * 3];
        for (int i = 0; i < coneResolution; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        coneMesh.vertices = vertices;
        coneMesh.colors = colors;
        coneMesh.triangles = tris;
    }

    void UpdateCone()
    {
        Vector3 eye = EyePosition;
        Vector3 center = GroundPosition + Vector3.up * coneHeightOffset;
        float startAngle = -viewAngle * 0.5f;
        float step = viewAngle / coneResolution;

        vertices[0] = center;
        colors[0] = coneColor;

        Color edge = coneColor;
        edge.a *= 0.35f;

        for (int i = 0; i <= coneResolution; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, startAngle + step * i, 0f) * Flat(transform.forward);
            float dist = viewDistance;

            // Duvara çarpıyorsa koniyi orada kes
            if (Physics.Raycast(eye, dir, out RaycastHit hit, viewDistance, obstacleMask, QueryTriggerInteraction.Ignore))
                dist = hit.distance;

            vertices[i + 1] = center + dir * dist;
            colors[i + 1] = edge;
        }

        coneMesh.vertices = vertices;
        coneMesh.colors = colors;
        coneMesh.RecalculateBounds();
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    // ---------------------------------------------------------------- Gizmo (Editör)

    void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position;
        Vector3 fwd = Flat(transform.forward);

        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * fwd;
        Vector3 right = Quaternion.Euler(0, viewAngle * 0.5f, 0) * fwd;
        Gizmos.DrawLine(pos, pos + left * viewDistance);
        Gizmos.DrawLine(pos, pos + right * viewDistance);

        Vector3 prev = pos + left * viewDistance;
        for (int i = 1; i <= 20; i++)
        {
            Vector3 p = pos + Quaternion.Euler(0, -viewAngle * 0.5f + viewAngle * i / 20f, 0) * fwd * viewDistance;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(pos, proximityRadius);
    }
}
