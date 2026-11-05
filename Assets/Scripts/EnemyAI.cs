using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Düşman yapay zekası (basit durum makinesi):
///
///   DEVRİYE  --(oyuncuyu gördü)-->  KOVALAMA
///   KOVALAMA --(gözden kaybetti, hafıza süresi doldu)--> ARAMA (son görülen yere gider, etrafa bakar)
///   ARAMA    --(oyuncuyu tekrar gördü)--> KOVALAMA
///   ARAMA    --(arama süresi doldu)--> DEVRİYE (en yakın waypoint'ten devam eder)
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
    public enum State { Patrol, Chase, Search }

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

    [Header("Görsel")]
    public Color patrolColor = new Color(0.2f, 1f, 0.3f);
    public Color chaseColor = new Color(1f, 0.15f, 0.1f);
    public Color searchColor = new Color(1f, 0.8f, 0.1f);
    public float pathLineWidth = 0.2f;
    public bool showStateLabel = true;
    public float labelHeight = 4f;

    [Header("Olaylar")]
    public UnityEvent onPlayerSpotted;
    public UnityEvent onPlayerCaught;

    public State CurrentState { get; private set; } = State.Patrol;

    private NavMeshAgent agent;
    private LineRenderer pathLine;
    private EnemyVision vision;
    private Renderer bodyRenderer;
    private MaterialPropertyBlock mpb;
    private TextMesh label;
    private Camera cam;

    private int waypointIndex;
    private int patrolDirection = 1;
    private float waitTimer;

    private Vector3 lastKnownPosition;
    private float lastSeenTime = -999f;
    private float searchTimer;
    private float repathTimer;
    private bool caught;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        pathLine = GetComponent<LineRenderer>();
        vision = GetComponent<EnemyVision>();
        bodyRenderer = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        cam = Camera.main;

        SetupPathLine();
        if (showStateLabel) CreateLabel();
    }

    void Start()
    {
        if (patrolRoute != null && patrolRoute.Count > 0)
            waypointIndex = patrolRoute.ClosestIndex(transform.position);

        EnterState(State.Patrol);
    }

    void Update()
    {
        bool seesTarget = vision.CanSee(target);
        if (seesTarget)
        {
            lastKnownPosition = target.position;
            lastSeenTime = Time.time;
        }

        switch (CurrentState)
        {
            case State.Patrol: UpdatePatrol(seesTarget); break;
            case State.Chase:  UpdateChase(seesTarget);  break;
            case State.Search: UpdateSearch(seesTarget); break;
        }

        DrawPath();
    }

    void LateUpdate()
    {
        if (label != null)
        {
            label.transform.position = transform.position + Vector3.up * labelHeight;
            if (cam != null) label.transform.rotation = cam.transform.rotation; // hep kameraya baksın
        }
    }

    // ---------------------------------------------------------------- Durumlar

    void EnterState(State newState)
    {
        CurrentState = newState;
        repathTimer = 0f;

        switch (newState)
        {
            case State.Patrol:
                agent.speed = patrolSpeed;
                agent.stoppingDistance = 0f;
                agent.autoBraking = true;
                agent.updateRotation = true;
                waitTimer = 0f;
                GoToCurrentWaypoint();
                break;

            case State.Chase:
                agent.speed = chaseSpeed;
                agent.stoppingDistance = catchDistance * 0.8f;
                agent.autoBraking = false;
                agent.updateRotation = true;
                caught = false;
                onPlayerSpotted?.Invoke();
                break;

            case State.Search:
                agent.speed = searchSpeed;
                agent.stoppingDistance = 0f;
                agent.autoBraking = true;
                agent.updateRotation = true;
                searchTimer = searchDuration;
                agent.SetDestination(lastKnownPosition);
                break;
        }

        ApplyStateVisuals();
    }

    void UpdatePatrol(bool seesTarget)
    {
        if (seesTarget)
        {
            EnterState(State.Chase);
            return;
        }

        if (patrolRoute == null || patrolRoute.Count == 0) return;

        if (HasArrived(waypointTolerance))
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitAtWaypoint)
            {
                waitTimer = 0f;
                waypointIndex = patrolRoute.NextIndex(waypointIndex, ref patrolDirection);
                GoToCurrentWaypoint();
            }
        }
    }

    void UpdateChase(bool seesTarget)
    {
        if (target == null)
        {
            EnterState(State.Patrol);
            return;
        }

        bool remembers = Time.time - lastSeenTime <= memoryDuration;

        if (!seesTarget && !remembers)
        {
            // Oyuncuyu kaybettik: son gördüğümüz yere git ve ara
            EnterState(State.Search);
            return;
        }

        // Görüyorsak (veya yeni kaybettiysek) oyuncunun gerçek pozisyonuna git
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            agent.SetDestination(target.position);
        }

        // Yakaladı mı?
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

    void UpdateSearch(bool seesTarget)
    {
        if (seesTarget)
        {
            EnterState(State.Chase);
            return;
        }

        if (!HasArrived(waypointTolerance)) return;

        // Son görülen noktadayız: etrafa bakın
        agent.updateRotation = false;
        transform.Rotate(0f, lookAroundSpeed * Time.deltaTime, 0f);

        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            // Bulamadık, rotaya en yakın noktadan geri dön
            if (patrolRoute != null && patrolRoute.Count > 0)
                waypointIndex = patrolRoute.ClosestIndex(transform.position);
            EnterState(State.Patrol);
        }
    }

    // ---------------------------------------------------------------- Yardımcılar

    void GoToCurrentWaypoint()
    {
        if (patrolRoute == null || patrolRoute.Count == 0) return;
        agent.SetDestination(patrolRoute.GetPoint(waypointIndex));
        patrolRoute.Highlight(waypointIndex);
    }

    bool HasArrived(float tolerance)
    {
        if (agent.pathPending) return false;
        return agent.remainingDistance <= Mathf.Max(tolerance, agent.stoppingDistance);
    }

    void FaceTowards(Vector3 point)
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

    void ApplyStateVisuals()
    {
        Color c;
        string text;
        switch (CurrentState)
        {
            case State.Chase:  c = chaseColor;  text = "KOVALIYOR!"; break;
            case State.Search: c = searchColor; text = "ARIYOR ?";   break;
            default:           c = patrolColor; text = "DEVRİYE";    break;
        }

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
            patrolRoute.SetRouteAlpha(CurrentState == State.Patrol ? 1f : 0.35f);
            if (CurrentState != State.Patrol) patrolRoute.Highlight(-1);
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
        if (!Application.isPlaying) return;
        if (CurrentState == State.Search || CurrentState == State.Chase)
        {
            Gizmos.color = searchColor;
            Gizmos.DrawWireSphere(lastKnownPosition, 0.6f);
        }
    }
}
