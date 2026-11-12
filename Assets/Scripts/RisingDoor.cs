using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Yerden çıkan kapı. Oyuncu yanındayken E'ye basınca açılır / kapanır.
/// Her durum değişiminde NavMesh yeniden bake edilir, böylece düşman yolunu yeniden hesaplar.
///
///  KAPANIRKEN (yerden yükselirken): Bake, kapı hareket etmeye BAŞLARKEN yapılır.
///      Kapı bir anlığına "tam kapalı" pozisyona konup kaynaklar toplanır, sonra animasyon başlar.
///      Böylece düşman kapı yükselirken içinden geçmeye çalışmaz.
///  AÇILIRKEN (yere gömülürken): Bake, kapı tamamen yere girdikten SONRA yapılır.
///      Yol ancak kapı gerçekten açıldığında açılır.
///
/// Sahnede kapıyı KAPALI (yükselmiş) haliyle yerleştir; açık hali otomatik hesaplanır.
/// </summary>
[RequireComponent(typeof(NavMeshModifier))]
public class RisingDoor : MonoBehaviour
{
    [Header("Etkileşim")]
    public Transform player;
    [Tooltip("Oyuncu kapıya bu mesafedeyken E ile kullanabilir.")]
    public float interactDistance = 4f;

    [Header("Hareket")]
    [Tooltip("Oyun başladığında kapı açık mı (yerin altında)?")]
    public bool startOpen = true;
    [Tooltip("Açık/kapalı arası geçiş süresi (sn).")]
    public float moveDuration = 0.8f;
    [Tooltip("Açıkken kapının tepesi zeminin ne kadar altında kalsın.")]
    public float sinkMargin = 0.3f;

    [Header("Görsel")]
    public Color closedColor = new Color(0.9f, 0.45f, 0.1f);
    public Color promptColor = Color.white;
    public float promptHeight = 2f;

    public bool IsOpen { get; private set; }
    public bool IsMoving { get; private set; }

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private NavMeshModifier modifier;
    private Collider doorCollider;
    private Bounds footprint;          // kapının zemin üzerindeki alanı (etkileşim mesafesi için)
    private TextMesh prompt;
    private Camera cam;

    void Awake()
    {
        modifier = GetComponent<NavMeshModifier>();
        doorCollider = GetComponent<Collider>();
        cam = Camera.main;

        closedPosition = transform.position;
        float height = transform.lossyScale.y;
        openPosition = closedPosition - Vector3.up * (height + sinkMargin);

        Renderer r = GetComponent<Renderer>();
        if (r != null)
        {
            footprint = r.bounds;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", closedColor);
            mpb.SetColor("_Color", closedColor);
            r.SetPropertyBlock(mpb);
        }
        else
        {
            footprint = new Bounds(closedPosition, transform.lossyScale);
        }

        // Başlangıç durumunu animasyonsuz uygula
        IsOpen = startOpen;
        transform.position = IsOpen ? openPosition : closedPosition;
        ApplyNavMeshState(IsOpen);

        CreatePrompt();
    }

    void Update()
    {
        bool inRange = PlayerInRange();

        if (inRange && !IsMoving && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            Toggle();

        UpdatePrompt(inRange);
    }

    public void Toggle()
    {
        if (IsMoving) return;

        // Oyuncu kapının tam üstündeyse kapatma (içinde sıkışmasın)
        if (IsOpen && PlayerOnDoor())
        {
            Debug.Log("Kapının üstündeyken kapatamazsın!");
            return;
        }

        StartCoroutine(IsOpen ? CloseRoutine() : OpenRoutine());
    }

    IEnumerator CloseRoutine()
    {
        IsMoving = true;
        IsOpen = false;

        // 1) Kapıyı bir anlığına tam kapalı pozisyona koy ve bake'i başlat (kaynaklar şu an toplanır)
        transform.position = closedPosition;
        ApplyNavMeshState(false);
        bool bakedNow = NavMeshRebaker.Instance != null && NavMeshRebaker.Instance.RequestRebake();

        // 2) Görsel olarak yerden yükselt
        yield return Move(openPosition, closedPosition);

        // Bake o an meşgul olduğu için sıraya alındıysa, kapı yerine oturunca tekrar bake et
        if (!bakedNow && NavMeshRebaker.Instance != null) NavMeshRebaker.Instance.RequestRebake();

        IsMoving = false;
    }

    IEnumerator OpenRoutine()
    {
        IsMoving = true;

        // 1) Yere gömül
        yield return Move(closedPosition, openPosition);

        // 2) Tamamen açılınca NavMesh'ten çıkar ve bake et
        IsOpen = true;
        ApplyNavMeshState(true);
        if (NavMeshRebaker.Instance != null) NavMeshRebaker.Instance.RequestRebake();

        IsMoving = false;
    }

    IEnumerator Move(Vector3 from, Vector3 to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, moveDuration);
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.position = to;
    }

    void ApplyNavMeshState(bool open)
    {
        // Açıkken kapı bake'e hiç dahil edilmez, kapalıyken engel olarak dahil edilir
        modifier.ignoreFromBuild = open;
        if (doorCollider != null) doorCollider.enabled = !open;
    }

    bool PlayerInRange()
    {
        if (player == null) return false;
        Vector3 p = player.position;
        p.y = footprint.center.y;
        return Vector3.Distance(footprint.ClosestPoint(p), p) <= interactDistance;
    }

    bool PlayerOnDoor()
    {
        if (player == null) return false;
        Vector3 p = player.position;
        p.y = footprint.center.y;
        float radius = 1f;
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) radius = cc.radius * Mathf.Max(player.lossyScale.x, player.lossyScale.z) + 0.2f;
        return Vector3.Distance(footprint.ClosestPoint(p), p) < radius;
    }

    // ---------------------------------------------------------------- Görsel

    void CreatePrompt()
    {
        // Ayrı kök obje: kapının NavMeshModifier'ından ve scale'inden etkilenmesin
        GameObject go = new GameObject(name + "_Ipucu");
        prompt = go.AddComponent<TextMesh>();
        prompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<MeshRenderer>().sharedMaterial = prompt.font.material;
        prompt.anchor = TextAnchor.LowerCenter;
        prompt.alignment = TextAlignment.Center;
        prompt.fontSize = 64;
        prompt.characterSize = 0.25f;
        prompt.color = promptColor;
        go.transform.position = closedPosition + Vector3.up * (transform.lossyScale.y * 0.5f + promptHeight);
        go.SetActive(false);
    }

    void UpdatePrompt(bool inRange)
    {
        if (prompt == null) return;
        prompt.gameObject.SetActive(inRange);
        if (!inRange) return;

        prompt.text = IsMoving ? "..." : (IsOpen ? "[E] Kapıyı Kapat" : "[E] Kapıyı Aç");
        if (cam != null) prompt.transform.rotation = cam.transform.rotation;
    }

    void OnDestroy()
    {
        if (prompt != null) Destroy(prompt.gameObject);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.matrix = Matrix4x4.TRS(Application.isPlaying ? closedPosition : transform.position,
            transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
