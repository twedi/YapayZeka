using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(LineRenderer))]
public class EnemyChaser : MonoBehaviour
{
    [Header("Hedef")]
    public Transform target; // Inspector'dan Player objesini buraya sürükle

    private NavMeshAgent agent;
    private LineRenderer lineRenderer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        lineRenderer = GetComponent<LineRenderer>();

        // Çizgi (LineRenderer) için görsel ayarlar
        lineRenderer.startWidth = 0.15f;
        lineRenderer.endWidth = 0.15f;
        lineRenderer.positionCount = 0;

        // Çizgiye basit bir kırmızı materyal atayalım
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;
    }

    void Update()
    {
        if (target != null)
        {
            // Düşmanın hedefini oyuncunun pozisyonu olarak belirle
            agent.SetDestination(target.position);

            // Yolu çiz
            DrawPath();
        }
    }

    void DrawPath()
    {
        // NavMeshAgent bir yol hesaplayamadıysa çizgiyi gizle
        if (agent.path.corners.Length < 2)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        // Çizginin nokta sayısını, hesaplanan yolun köşe sayısına eşitle
        lineRenderer.positionCount = agent.path.corners.Length;

        // Noktaların pozisyonlarını çizgiye aktar
        lineRenderer.SetPositions(agent.path.corners);
    }
}