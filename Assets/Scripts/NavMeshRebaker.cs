using System;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Oyun sırasında NavMesh'i yeniden bake eder (kapı açılıp kapanınca vb.).
///
///  - NavMeshSurface.UpdateNavMesh asenkron çalışır, yani oyun donmaz.
///    Unity sadece değişen bölgedeki (tile) NavMesh'i yeniden hesaplar.
///  - Bake sürerken yeni bir istek gelirse sıraya alınır, bitince bir kez daha bake edilir.
///  - Bake bittiğinde <see cref="Rebaked"/> olayı tetiklenir; düşmanlar yollarını yeniden hesaplar.
///
/// Terrain üzerindeki NavMeshSurface ile aynı objeye eklenir.
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
public class NavMeshRebaker : MonoBehaviour
{
    public static NavMeshRebaker Instance { get; private set; }

    /// <summary>Her yeniden bake tamamlandığında çağrılır.</summary>
    public static event Action Rebaked;

    [Tooltip("Oyun başında bir kez bake et (çamur / kapı gibi sahne değişiklikleri editörde bake edilmemiş olsa bile uygulanır).")]
    public bool rebakeOnStart = true;
    [Tooltip("Başlangıç bake'i sıfırdan (BuildNavMesh) yapılsın. NavMeshSurface ayarları (tile boyutu vb.) " +
             "editörde bake edilen veriden farklı olsa bile doğru sonuç verir.")]
    public bool fullRebuildOnStart = true;
    public bool logRebakes = true;

    private NavMeshSurface surface;
    private AsyncOperation running;
    private bool pending;
    private float startTime;

    public bool IsBaking => running != null && !running.isDone;

    void Awake()
    {
        Instance = this;
        surface = GetComponent<NavMeshSurface>();
    }

    void Start()
    {
        if (!rebakeOnStart) return;

        if (fullRebuildOnStart)
        {
            startTime = Time.realtimeSinceStartup;
            surface.BuildNavMesh();
            OnBakeFinished();
        }
        else
        {
            RequestRebake();
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// NavMesh'i sahnenin O ANKİ haline göre yeniden bake etmeyi ister.
    /// Kaynaklar (objelerin pozisyonu vb.) çağrı anında toplanır.
    /// Bake o an meşgulse false döner ve istek sıraya alınır.
    /// </summary>
    public bool RequestRebake()
    {
        if (IsBaking)
        {
            pending = true;
            return false;
        }

        if (surface.navMeshData == null)
        {
            // Hiç bake edilmemişse ilk seferi senkron yap
            surface.BuildNavMesh();
            OnBakeFinished();
            return true;
        }

        startTime = Time.realtimeSinceStartup;
        running = surface.UpdateNavMesh(surface.navMeshData);
        running.completed += _ => OnBakeFinished();
        return true;
    }

    void OnBakeFinished()
    {
        running = null;
        if (logRebakes)
            Debug.Log($"NavMesh yeniden bake edildi ({(Time.realtimeSinceStartup - startTime) * 1000f:0} ms)");

        Rebaked?.Invoke();

        if (pending)
        {
            pending = false;
            RequestRebake();
        }
    }

    /// <summary>
    /// Bu objeyi (ve çocuklarını) NavMesh bake'inden hariç tutar.
    /// Oyuncu, çizgiler, işaretler gibi zemini "delmemesi" gereken objeler için.
    /// </summary>
    public static void ExcludeFromBake(GameObject go)
    {
        NavMeshModifier m = go.GetComponent<NavMeshModifier>();
        if (m == null) m = go.AddComponent<NavMeshModifier>();
        m.ignoreFromBuild = true;
        m.applyToChildren = true;
    }
}
