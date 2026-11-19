using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hafta 3–4 – Kamera: C ile sahnedeki sabit üst görünüm ↔ oyuncuyu izleyen takip kamerası.
/// Yumuşatma kare hızından bağımsız: t = 1 − e^(−k·Δt). Takipte kamera duvarın içine girmesin diye
/// oyuncudan kameraya SphereCast atılır, çarparsa mesafe kısaltılır (Hafta 3, alıştırma 7).
/// </summary>
public class CameraModes : MonoBehaviour
{
    public Transform target;
    public Vector3 followOffset = new Vector3(0f, 22f, -16f);
    public float smoothing = 6f;
    public LayerMask obstacles = ~0;

    bool follow;
    Vector3 topPos;
    Quaternion topRot;

    void Awake()
    {
        topPos = transform.position;   // sahnedeki konum = üst görünüm
        topRot = transform.rotation;
    }

    void LateUpdate()
    {
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) follow = !follow;
        if (target == null) return;

        Vector3 wantedPos;
        Quaternion wantedRot;
        if (follow)
        {
            Vector3 focus = target.position;
            Vector3 dir = followOffset.normalized;
            float dist = followOffset.magnitude;
            if (Physics.SphereCast(focus, 0.5f, dir, out RaycastHit hit, dist, obstacles, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target))
                dist = hit.distance;
            wantedPos = focus + dir * dist;
            wantedRot = Quaternion.LookRotation(focus - wantedPos);
        }
        else
        {
            wantedPos = topPos;
            wantedRot = topRot;
        }

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, wantedPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, wantedRot, t);
    }
}
