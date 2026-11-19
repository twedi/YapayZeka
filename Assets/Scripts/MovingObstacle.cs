using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Hafta 6 ödevi: hareketli engel. İki nokta arasında gidip gelir, durduğunda NavMesh'i oyar (Carve).
/// Carve Only Stationary açık: hareket ederken oyma yapılmaz, durunca TimeToStationary sonra oyulur.
/// </summary>
public class MovingObstacle : MonoBehaviour
{
    public Vector3 moveOffset = new Vector3(0f, 0f, 10f);
    public float speed = 3f;
    [Tooltip("Uçlarda bekleme süresi; bu sırada NavMesh oyulur.")]
    public float waitAtEnds = 4f;

    Vector3 a, b;
    bool toB = true;
    float wait;

    void Awake()
    {
        // Runtime bake bu küpü kalıcı duvar sanmasın; ağa etkisini yalnızca NavMeshObstacle belirlesin
        NavMeshRebaker.ExcludeFromBake(gameObject);

        NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = Vector3.one;          // ölçekle birlikte küpün kendisi
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.carvingMoveThreshold = 0.1f;
        obstacle.carvingTimeToStationary = 0.5f;

        a = transform.position;
        b = a + moveOffset;
    }

    void Update()
    {
        if (wait > 0f) { wait -= Time.deltaTime; return; }

        Vector3 target = toB ? b : a;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude < 0.0001f)
        {
            toB = !toB;
            wait = waitAtEnds;
        }
    }
}
