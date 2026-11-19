using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hafta 7 ödevi: oyuncu N'ye (Player/MakeNoise eylemi) basınca bulunduğu yerde ses çıkarır.
/// Sahnedeki tüm düşmanlara haber verir; mesafe ve durum kontrolünü EnemyAI.HearNoise yapar.
/// </summary>
public class PlayerNoise : MonoBehaviour
{
    public float ringDuration = 0.6f;
    public Color ringColor = new Color(0.7f, 0.3f, 1f, 0.9f);

    InputAction noiseAction;
    LineRenderer ring;
    float ringTimer;
    Vector3 ringCenter;
    float ringRadius;

    void Awake()
    {
        noiseAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/MakeNoise") : null;
        if (noiseAction == null)
            Debug.LogWarning("PlayerNoise: 'Player/MakeNoise' eylemi bulunamadı (InputSystem_Actions).");

        GameObject go = new GameObject("SesHalkasi");
        NavMeshRebaker.ExcludeFromBake(go);
        ring = go.AddComponent<LineRenderer>();
        ring.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.widthMultiplier = 0.15f;
        ring.positionCount = 0;
    }

    void Update()
    {
        if (noiseAction != null && noiseAction.WasPressedThisFrame())
            MakeNoise();

        if (ringTimer > 0f)
        {
            ringTimer -= Time.deltaTime;
            float k = 1f - Mathf.Clamp01(ringTimer / ringDuration);
            DrawRing(ringRadius * k, new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a * (1f - k)));
            if (ringTimer <= 0f) ring.positionCount = 0;
        }
    }

    public void MakeNoise()
    {
        Vector3 p = transform.position;
        Debug.Log($"Ses çıkarıldı: {p}");

        float maxRange = 0f;
        foreach (EnemyAI e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            e.HearNoise(p);
            maxRange = Mathf.Max(maxRange, e.hearingRange);
        }

        ringCenter = p;
        ringRadius = maxRange > 0f ? maxRange : 8f;
        ringTimer = ringDuration;
    }

    void DrawRing(float radius, Color c)
    {
        const int n = 48;
        ring.positionCount = n;
        ring.startColor = ring.endColor = c;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            ring.SetPosition(i, ringCenter + new Vector3(Mathf.Cos(a) * radius, -1.5f, Mathf.Sin(a) * radius));
        }
    }
}
