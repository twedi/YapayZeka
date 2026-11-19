using UnityEngine;

/// <summary>
/// Hafta 3 ödevi – Gözcü. Sabit duran bir kapsül; oyuncuyu görünce kırmızı, görmezken yeşil olur.
///  - Görüş konisi: kare mesafe ≤ menzil² ve ileri · birim(yön) ≥ cos(açı/2)  (Hafta 1, karekök/acos yok)
///  - Görüş hattı: gözden oyuncuya Linecast; araya engel girerse görmez.
///  - Görünce sağda mı solda mı olduğunu konsola yazar (çapraz çarpım, Unity'de Cross(ileri, yön).y > 0 → sağ).
/// Ek görevler: tarama (±60°, 30°/s), yükseklik tuzağı (dikey ±30°), takip (2 sn görmezse taramaya döner).
/// </summary>
public class Gozcu : MonoBehaviour
{
    public Transform oyuncu;
    public float menzil = 15f;
    public float gorusAcisi = 90f;          // koninin tamamı (derece)
    public float gozYuksekligi = 0.6f;      // pivottan yukarı
    public LayerMask engeller = ~0;

    [Header("Ek görevler")]
    public bool tarama = true;
    public float taramaAcisi = 60f;
    public float taramaHizi = 30f;          // derece/sn
    public bool yukseklikTuzagi = true;
    public float dikeyAci = 30f;            // ±
    public bool takip = true;
    public float takipBirakmaSuresi = 2f;

    Renderer govde;
    MaterialPropertyBlock mpb;
    Quaternion baslangic;
    float taramaYonu = 1f;
    float sonGorulme = -999f;
    bool oncekiGoruyor;

    void Awake()
    {
        govde = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        baslangic = transform.rotation;
        BurunEkle();
    }

    void Update()
    {
        if (oyuncu == null) return;

        Vector3 goz = transform.position + Vector3.up * gozYuksekligi;
        bool goruyor = GorusKonisinde(goz, oyuncu.position) && GorusHattiAcik(goz, oyuncu.position);
        Renk(goruyor ? Color.red : Color.green);

        if (goruyor)
        {
            sonGorulme = Time.time;
            if (!oncekiGoruyor) Debug.Log($"Gözcü oyuncuyu gördü: {SagSol(oyuncu.position)}");
        }
        oncekiGoruyor = goruyor;

        if (takip && Time.time - sonGorulme < takipBirakmaSuresi)
        {
            Vector3 d = oyuncu.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 180f * Time.deltaTime);
        }
        else if (tarama)
        {
            Quaternion hedef = baslangic * Quaternion.Euler(0f, taramaAcisi * taramaYonu, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, hedef, taramaHizi * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, hedef) < 0.5f) taramaYonu = -taramaYonu;
        }
    }

    bool GorusKonisinde(Vector3 goz, Vector3 hedef)
    {
        Vector3 yon = hedef - goz;
        Vector3 yatay = new Vector3(yon.x, 0f, yon.z);
        if (yatay.sqrMagnitude > menzil * menzil) return false;                 // kare mesafe
        if (yatay.sqrMagnitude < 0.0001f) return true;

        Vector3 ileri = transform.forward; ileri.y = 0f; ileri.Normalize();
        float cosYari = Mathf.Cos(gorusAcisi * 0.5f * Mathf.Deg2Rad);
        if (Vector3.Dot(ileri, yatay.normalized) < cosYari) return false;       // nokta çarpım ≥ cos eşiği

        if (yukseklikTuzagi)
        {
            // Dikey açı: tan(dikey) = |dy| / yatay mesafe  →  |dy| ≤ tan(dikeyAci) · yatay
            float tan = Mathf.Tan(dikeyAci * Mathf.Deg2Rad);
            if (Mathf.Abs(yon.y) > tan * yatay.magnitude) return false;
        }
        return true;
    }

    bool GorusHattiAcik(Vector3 goz, Vector3 hedef)
    {
        if (!Physics.Linecast(goz, hedef, out RaycastHit hit, engeller, QueryTriggerInteraction.Ignore))
            return true;
        return hit.transform == oyuncu || hit.transform.IsChildOf(oyuncu);   // ilk çarpılan oyuncuysa açık
    }

    string SagSol(Vector3 hedef)
    {
        Vector3 yon = hedef - transform.position; yon.y = 0f;
        float y = Vector3.Cross(transform.forward, yon).y;
        return y > 0f ? "SAĞDA" : (y < 0f ? "SOLDA" : "tam önünde");
    }

    void Renk(Color c)
    {
        if (govde == null) return;
        govde.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", c);
        mpb.SetColor("_Color", c);
        govde.SetPropertyBlock(mpb);
    }

    void BurunEkle()
    {
        GameObject burun = GameObject.CreatePrimitive(PrimitiveType.Cube);
        burun.name = "Burun";
        Destroy(burun.GetComponent<Collider>());
        burun.transform.SetParent(transform, false);
        burun.transform.localPosition = new Vector3(0f, 0.3f, 0.55f);
        burun.transform.localScale = new Vector3(0.25f, 0.25f, 0.3f);
    }

    void OnDrawGizmos()
    {
        Vector3 goz = transform.position + Vector3.up * gozYuksekligi;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, menzil);
        Vector3 sol = Quaternion.AngleAxis(-gorusAcisi / 2f, Vector3.up) * transform.forward;
        Vector3 sag = Quaternion.AngleAxis(gorusAcisi / 2f, Vector3.up) * transform.forward;
        Gizmos.DrawLine(goz, goz + sol * menzil);
        Gizmos.DrawLine(goz, goz + sag * menzil);
    }
}
