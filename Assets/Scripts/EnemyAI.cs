using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Düşman yapay zekası – Hafta 7: State deseni ile sonlu durum makinesi (Buckland, Bölüm 2).
/// Durum sınıfları EnemyStates.cs içinde; bu sınıf yalnızca veri ve yardımcı metotları tutar.
///
///   GLOBAL   : oyuncu görüldü -> KOVALA (Kovala hariç her durumdan)
///   DEVRİYE  : kapı az önce önünde kapandı -> KAPIDA BEKLE
///   KOVALA   : oyuncu memoryDuration'dan uzun süredir görünmüyor -> ARA
///   ARA      : kapı az önce önünde kapandı -> KAPIDA BEKLE ; vardı + bakındı -> DEVRİYE
///   KAPIDA BEKLE : kapı açıldı (ya da bekleme süresi doldu) -> ÖNCEKİ DURUM
///   ARAŞTIR (ödev): ses duyuldu (Devriye/Ara'dayken, 8 m) -> sese git, 3 sn bakın -> DEVRİYE
///
/// Görsel geri bildirim:
///   - Gidilen yol (NavMesh path) LineRenderer ile çizilir, rengi duruma göre değişir.
///   - Görüş konisi (EnemyVision) ve gövde rengi duruma göre değişir.
///   - Kafasının üstünde durum yazısı çıkar.
///   - Devriye rotası (PatrolRoute) ve hedeflenen waypoint vurgulanır.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EnemyVision))]
public class EnemyAI : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Kovalanacak hedef (oyuncu).")]
    public Transform target;
    [Tooltip("Devriye rotası. Çocuk objeleri waypoint olarak kullanılır.")]
    public PatrolRoute patrolRoute;

    [Header("Devriye")]
    public float patrolSpeed = 4f;
    [Tooltip("Waypoint'e bu kadar yaklaşınca varmış sayılır.")]
    public float waypointTolerance = 0.75f;
    [Tooltip("Her waypoint'te bekleme süresi (saniye).")]
    public float waitAtWaypoint = 0.3f;

    [Header("Kovalama")]
    public float chaseSpeed = 9f;
    [Tooltip("Oyuncu gözden kaybolduktan sonra kaç saniye daha onun gerçek pozisyonuna gider (sezgi).")]
    public float memoryDuration = 1.5f;
    [Tooltip("Bu mesafeye girince oyuncu yakalanmış sayılır.")]
    public float catchDistance = 2.5f;
    [Tooltip("Hedefi her karede değil, bu aralıklarla yeniden hesapla (performans).")]
    public float repathInterval = 0.1f;

    [Header("Arama")]
    public float searchSpeed = 5.5f;
    [Tooltip("Son görülen noktaya varınca etrafa bakınma süresi.")]
    public float searchDuration = 4f;
    [Tooltip("Etrafa bakınırken dönme hızı (derece/sn).")]
    public float lookAroundSpeed = 120f;

    [Header("Zıplama (NavMeshLink)")]
    [Tooltip("Açıksa düşman sadece kovalarken / ararken zıplama linklerini kullanır; devriyede etrafından dolanır.")]
    public bool jumpOnlyWhenChasing = true;
    [Tooltip("Zıplama yayının tepe yüksekliği (dünya birimi).")]
    public float jumpHeight = 3f;
    [Tooltip("Zıplamanın süresi (sn).")]
    public float jumpDuration = 0.6f;
    [Tooltip("Project Settings > Navigation > Areas içindeki zıplama alanının adı.")]
    public string jumpAreaName = "Jump";
    [Tooltip("Link'in başlangıcına bu mesafeden yakınsa zıplama kararı kesinleşir; oyuncu sağa sola gitse de yol değiştirilmez.")]
    public float jumpCommitDistance = 4f;
    [Tooltip("İndikten sonra bu süre boyunca tekrar zıplamaz (duvarın üstünde ileri-geri sekmesin).")]
    public float jumpCooldown = 1f;

    [Header("Kapıda Bekle (Hafta 7)")]
    [Tooltip("Kapı (boşsa bu durum hiç kullanılmaz).")]
    public RisingDoor door;
    [Tooltip("Kapı bu mesafedeyken kapanırsa düşman durup bekler.")]
    public float doorStopDistance = 5f;
    [Tooltip("Kapı kapandıktan sonra kaç saniye içinde yakındaysa 'önümde kapandı' sayılır.")]
    public float doorReactTime = 1.5f;
    [Tooltip("En fazla bu kadar bekler, sonra önceki işine döner (0 = kapı açılana kadar).")]
    public float maxDoorWait = 8f;

    [Header("Ses / Araştır (Hafta 7 ödevi)")]
    [Tooltip("Bu mesafeden yakın sesleri duyar. Duvarlar sesi kesmez.")]
    public float hearingRange = 8f;
    [Tooltip("Sesin geldiği yerde bakınma süresi.")]
    public float investigateLookTime = 3f;

    [Header("Görsel")]
    public Color patrolColor = new Color(0.2f, 1f, 0.3f);
    public Color chaseColor = new Color(1f, 0.15f, 0.1f);
    public Color searchColor = new Color(1f, 0.8f, 0.1f);
    public Color waitColor = new Color(0.6f, 0.6f, 0.6f);
    public Color investigateColor = new Color(0.7f, 0.3f, 1f);
    public float pathLineWidth = 0.2f;
    public bool showStateLabel = true;
    public float labelHeight = 4f;

    [Header("Olaylar")]
    public UnityEvent onPlayerSpotted;
    public UnityEvent onPlayerCaught;

    // Durum nesneleri: her düşmanın kendi kopyası
    public readonly EnemyPatrol Patrol = new EnemyPatrol();
    public readonly EnemyChase Chase = new EnemyChase();
    public readonly EnemySearch Search = new EnemySearch();
    public readonly EnemyWaitAtDoor WaitAtDoor = new EnemyWaitAtDoor();
    public readonly EnemyInvestigate Investigate = new EnemyInvestigate();

    public StateMachine<EnemyAI> FSM { get; private set; }
    public NavMeshAgent Agent => agent;
    public bool SeesTarget { get; private set; }
    public string StateName => FSM?.Current?.Name ?? "-";

    private NavMeshAgent agent;
    private LineRenderer pathLine;
    private EnemyVision vision;
    private Renderer bodyRenderer;
    private MaterialPropertyBlock mpb;
    private TextMesh label;
    private Camera cam;

    private int waypointIndex;
    private int patrolDirection = 1;

    [HideInInspector] public Vector3 lastKnownPosition;
    [HideInInspector] public float lastSeenTime = -999f;
    [HideInInspector] public Vector3 noisePosition;
    private float repathTimer;
    private bool caught;
    [HideInInspector] public float baseSpeed;
    private bool jumping;
    private int jumpAreaMask;
    private float jumpCooldownUntil;

    public bool IsJumping => jumping;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        pathLine = GetComponent<LineRenderer>();
        vision = GetComponent<EnemyVision>();
        bodyRenderer = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        cam = Camera.main;

        // Link'leri Unity'nin düz "kayma" hareketiyle değil, kendi zıplama animasyonumuzla geçeceğiz
        agent.autoTraverseOffMeshLink = false;
        int jumpArea = NavMesh.GetAreaFromName(jumpAreaName);
        jumpAreaMask = jumpArea >= 0 ? 1 << jumpArea : 0;

        SetupPathLine();
        if (showStateLabel) CreateLabel();

        FSM = new StateMachine<EnemyAI>(this);
        FSM.Global = new EnemyGlobal();
    }

    void Start()
    {
        if (patrolRoute != null && patrolRoute.Count > 0)
            waypointIndex = patrolRoute.ClosestIndex(transform.position);

        FSM.ChangeState(Patrol);
    }

    void Update()
    {
        // Görüş zıplarken de çalışsın; yoksa indiğinde oyuncuyu "unutmuş" olur
        SeesTarget = vision.CanSee(target);
        if (SeesTarget)
        {
            lastKnownPosition = target.position;
            lastSeenTime = Time.time;
        }

        // Link'in başına geldiysek zıpla; zıplarken başka karar verme
        if (jumping) return;
        if (agent.isOnOffMeshLink)
        {
            StartCoroutine(JumpAcrossLink());
            return;
        }

        ApplyAreaMask();

        // Önce global durum, sonra mevcut durum (StateMachine.Update)
        FSM.Update();

        // Çamurdaysak yavaşla
        agent.speed = baseSpeed * MudZone.SpeedMultiplierAt(transform.position);

        DrawPath();
    }

    void OnEnable() => NavMeshRebaker.Rebaked += OnNavMeshRebaked;
    void OnDisable() => NavMeshRebaker.Rebaked -= OnNavMeshRebaked;

    /// <summary>
    /// NavMesh değişti (ör. kapı açıldı/kapandı): mevcut hedefe giden yolu yeniden hesapla.
    /// Kapı kapandıysa düşman etrafından dolanır, açıldıysa kestirmeden gider.
    /// </summary>
    void OnNavMeshRebaked()
    {
        if (jumping) return;

        if (!agent.isOnNavMesh)
        {
            // Kapı tam üstümüzde kapandıysa en yakın geçerli noktaya kay
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                return;
        }

        RefreshDestination();
    }

    /// <summary>Mevcut duruma göre hedefi yeniden ver (yol baştan hesaplanır).</summary>
    void RefreshDestination()
    {
        if (!agent.isOnNavMesh || FSM == null) return;

        if (FSM.IsIn(Patrol) && patrolRoute != null && patrolRoute.Count > 0)
            agent.SetDestination(patrolRoute.GetPoint(waypointIndex));
        else if (FSM.IsIn(Chase) && target != null)
            agent.SetDestination(target.position);
        else if (FSM.IsIn(Search))
            agent.SetDestination(lastKnownPosition);
        else if (FSM.IsIn(Investigate))
            agent.SetDestination(noisePosition);
    }

    void LateUpdate()
    {
        if (label != null)
        {
            label.transform.position = transform.position + Vector3.up * labelHeight;
            if (cam != null) label.transform.rotation = cam.transform.rotation; // hep kameraya baksın
        }
    }

    // ---------------------------------------------------------------- Durumların kullandığı yardımcılar

    /// <summary>Kovala durumu: hedefi takip et, yakalandıysa olayı tetikle.</summary>
    public void ChaseTick()
    {
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            // Zıplamak üzereysek yolu değiştirme; yoksa oyuncu sağa sola gittikçe
            // karar "zıpla" <-> "dolan" arasında gidip gelir ve düşman titrer
            if (!IsCommittedToJump())
                agent.SetDestination(target.position);
        }

        Vector3 diff = target.position - transform.position;
        diff.y = 0f;
        if (diff.magnitude <= catchDistance)
        {
            FaceTowards(target.position);
            if (!caught)
            {
                caught = true;
                Debug.Log($"{name} oyuncuyu yakaladı!");
                onPlayerCaught?.Invoke();
            }
        }
        else
        {
            caught = false;
        }
    }

    public void ResetChase()
    {
        repathTimer = 0f;
        caught = false;
    }

    /// <summary>Devriyede bir sonraki waypoint'e geç.</summary>
    public void AdvanceWaypoint()
    {
        if (patrolRoute == null || patrolRoute.Count == 0) return;
        waypointIndex = patrolRoute.NextIndex(waypointIndex, ref patrolDirection);
        GoToCurrentWaypoint();
    }

    /// <summary>Rotaya en yakın noktadan dön.</summary>
    public void SelectClosestWaypoint()
    {
        if (patrolRoute != null && patrolRoute.Count > 0)
            waypointIndex = patrolRoute.ClosestIndex(transform.position);
    }

    /// <summary>Olduğu yerde dönerek etrafa bakın.</summary>
    public void LookAround()
    {
        agent.updateRotation = false;
        transform.Rotate(0f, lookAroundSpeed * Time.deltaTime, 0f);
    }

    /// <summary>Kapı az önce kapandı ve düşman kapıya yakın mı? (Hafta 7: Kapıda Bekle)</summary>
    public bool DoorBlocksMe()
    {
        if (door == null || door.IsOpen) return false;
        if (Time.time - door.LastClosedTime > doorReactTime) return false;
        return door.DistanceTo(transform.position) < doorStopDistance;
    }

    /// <summary>
    /// Hafta 7 ödevi: oyuncu ses çıkardı. Devriye ya da Ara'dayken 8 m içindeyse Araştır'a geç.
    /// Kovala'dayken sesi görmezden gelir.
    /// </summary>
    public void HearNoise(Vector3 position)
    {
        Vector3 d = position - transform.position;
        d.y = 0f;
        if (d.magnitude > hearingRange) return;
        if (!(FSM.IsIn(Patrol) || FSM.IsIn(Search) || FSM.IsIn(Investigate))) return;

        noisePosition = position;
        if (FSM.IsIn(Investigate))
            Investigate.Enter(this);          // yeni ses: hedefi ve süreyi tazele
        else
            FSM.ChangeState(Investigate);
    }

    public void GoToCurrentWaypoint()
    {
        if (patrolRoute == null || patrolRoute.Count == 0) return;
        agent.SetDestination(patrolRoute.GetPoint(waypointIndex));
        patrolRoute.Highlight(waypointIndex);
    }

    public bool HasArrived(float tolerance)
    {
        if (agent.pathPending) return false;
        return agent.remainingDistance <= Mathf.Max(tolerance, agent.stoppingDistance);
    }

    // ---------------------------------------------------------------- Zıplama

    /// <summary>
    /// Off-mesh link üzerinde parabolik zıplama. Bittiğinde CompleteOffMeshLink ile
    /// ajanı link'in diğer ucuna bırakır ve normal yol takibi devam eder.
    /// </summary>
    IEnumerator JumpAcrossLink()
    {
        jumping = true;
        OffMeshLinkData data = agent.currentOffMeshLinkData;
        if (!data.valid)
        {
            agent.CompleteOffMeshLink();
            jumping = false;
            yield break;
        }

        // Transform ile NavMesh yüzeyi arasındaki dikey fark (baseOffset * scale)
        float yOffset = transform.position.y - agent.nextPosition.y;

        // ÖNEMLİ: Zıplarken transform'u biz hareket ettiriyoruz. updatePosition açık kalırsa
        // ajan, duvarın üstündeki transform'u NavMesh'e "yapıştırmaya" çalışır ve
        // düşman bir anda başka bir yere ışınlanır. Bu yüzden zıplama boyunca senkronu kapatıyoruz.
        agent.updatePosition = false;
        agent.updateRotation = false;

        Vector3 start = transform.position;
        Vector3 end = data.endPos + Vector3.up * yOffset;

        Vector3 flatDir = end - start;
        flatDir.y = 0f;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = flatDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flatDir) : startRot;

        string previousText = label != null ? label.text : null;
        if (label != null) label.text = "ZIPLIYOR!";
        pathLine.positionCount = 0;

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.05f, jumpDuration));
            float arc = jumpHeight * 4f * (t - t * t); // 0 -> tepe -> 0
            transform.position = Vector3.Lerp(start, end, t) + Vector3.up * arc;
            transform.rotation = Quaternion.Slerp(startRot, endRot, t * 4f);
            yield return null;
        }

        // Ajanı link'in diğer ucuna bırak, sonra transform ile senkronu geri aç.
        // NOT: agent.nextPosition kullanmıyoruz; o, ajanı NavMesh bağlantısı boyunca kaydırır
        // ve duvarın içinden geçemediği için ajan başladığı tarafta kalır -> düşman geri ışınlanır.
        // Warp ise ajanı doğrudan iniş noktasına koyar.
        agent.CompleteOffMeshLink();
        Vector3 landing = data.endPos;
        if (NavMesh.SamplePosition(data.endPos, out NavMeshHit landHit, 2f, NavMesh.AllAreas))
            landing = landHit.position;
        agent.Warp(landing);                       // Warp yolu sıfırlar, aşağıda yeniden veriyoruz
        transform.position = landing + Vector3.up * yOffset;
        agent.updatePosition = true;
        agent.updateRotation = true;

        if (label != null && previousText != null) label.text = previousText;

        // Hemen geri zıplamasın; kovalıyorsak yeni konumdan yolu tazele
        jumpCooldownUntil = Time.time + jumpCooldown;
        ApplyAreaMask();
        RefreshDestination();
        repathTimer = repathInterval;
        jumping = false;
    }

    /// <summary>
    /// Devriyede ve zıplama bekleme süresinde Jump alanını yasakla, diğer durumlarda serbest bırak.
    /// Serbestken A* zıplamanın maliyetini (link uzunluğu x Jump alan maliyeti)
    /// etrafından dolanmanın maliyetiyle karşılaştırır ve ucuz olanı seçer.
    /// </summary>
    void ApplyAreaMask()
    {
        if (FSM == null) return;
        bool calm = FSM.IsIn(Patrol) || FSM.IsIn(WaitAtDoor);
        bool allowJump = (!jumpOnlyWhenChasing || !calm) && Time.time >= jumpCooldownUntil;
        int mask = allowJump ? NavMesh.AllAreas : NavMesh.AllAreas & ~jumpAreaMask;
        if (agent.areaMask != mask) agent.areaMask = mask;
    }

    /// <summary>Yol bir zıplama link'inden geçiyor ve link'in başına çok yakın mıyız?</summary>
    bool IsCommittedToJump()
    {
        if (!agent.hasPath || agent.pathPending) return false;
        OffMeshLinkData next = agent.nextOffMeshLinkData;
        if (!next.valid) return false;
        Vector3 d = next.startPos - agent.nextPosition;
        d.y = 0f;
        return d.magnitude <= jumpCommitDistance;
    }

    // ---------------------------------------------------------------- Yardımcılar

    public void FaceTowards(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, agent.angularSpeed * Time.deltaTime);
    }

    // ---------------------------------------------------------------- Görsel

    void SetupPathLine()
    {
        pathLine.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        pathLine.useWorldSpace = true;
        pathLine.widthMultiplier = pathLineWidth;
        pathLine.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        pathLine.numCornerVertices = 2;
        pathLine.numCapVertices = 2;
        pathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pathLine.receiveShadows = false;
        pathLine.positionCount = 0;
    }

    void CreateLabel()
    {
        GameObject go = new GameObject(name + "_Durum");
        label = go.AddComponent<TextMesh>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        label.anchor = TextAnchor.LowerCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 64;
        label.characterSize = 0.3f;
        label.fontStyle = FontStyle.Bold;
    }

    /// <summary>Durumların Enter'ında çağrılır: renk, koni, yazı ve rota görünürlüğü.</summary>
    public void ApplyStateVisuals(Color c, string text, bool patrolling)
    {
        pathLine.startColor = c;
        pathLine.endColor = new Color(c.r, c.g, c.b, 0.3f);

        Color cone = c;
        cone.a = 0.25f;
        vision.SetConeColor(cone);

        if (bodyRenderer != null)
        {
            bodyRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", Color.Lerp(Color.white, c, 0.6f)); // URP Lit
            mpb.SetColor("_Color", Color.Lerp(Color.white, c, 0.6f));     // Built-in yedek
            bodyRenderer.SetPropertyBlock(mpb);
        }

        if (label != null)
        {
            label.text = text;
            label.color = c;
        }

        if (patrolRoute != null)
        {
            patrolRoute.SetRouteAlpha(patrolling ? 1f : 0.35f);
            if (!patrolling) patrolRoute.Highlight(-1);
        }
    }

    void DrawPath()
    {
        NavMeshPath path = agent.path;
        if (path == null || path.corners.Length < 2 || !agent.hasPath)
        {
            pathLine.positionCount = 0;
            return;
        }

        Vector3[] corners = path.corners;
        pathLine.positionCount = corners.Length;
        for (int i = 0; i < corners.Length; i++)
            pathLine.SetPosition(i, corners[i] + Vector3.up * 0.25f);

        // İlk nokta düşmanın kendisi olsun ki çizgi ondan çıksın
        pathLine.SetPosition(0, vision.GroundPosition + Vector3.up * 0.25f);
    }

    void OnDestroy()
    {
        if (label != null) Destroy(label.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || FSM == null) return;
        if (FSM.IsIn(Search) || FSM.IsIn(Chase))
        {
            Gizmos.color = searchColor;
            Gizmos.DrawWireSphere(lastKnownPosition, 0.6f);
        }
        if (FSM.IsIn(Investigate))
        {
            Gizmos.color = investigateColor;
            Gizmos.DrawWireSphere(noisePosition, 0.6f);
        }
        Gizmos.color = new Color(investigateColor.r, investigateColor.g, investigateColor.b, 0.3f);
        Gizmos.DrawWireSphere(transform.position, hearingRange);
    }
}
