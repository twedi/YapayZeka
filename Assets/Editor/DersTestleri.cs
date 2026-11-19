using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hafta 1–2 ödev testleri. Menü: YapayZeka > Hafta 1 Testleri / Hafta 2 Testleri.
/// Sonuçlar Console'a yazılır (Play moduna gerek yok).
/// </summary>
public static class DersTestleri
{
    static Vector2D V(double x, double y) => new Vector2D(x, y);

    [MenuItem("YapayZeka/Hafta 1 Testleri (IsInFov, SideOf)")]
    public static void Hafta1()
    {
        var sb = new StringBuilder("<b>Hafta 1 – IsInFov / SideOf</b>\n");
        // ajan, ileri, hedef, fov, menzil, beklenen
        var fov = new (Vector2D a, Vector2D h, Vector2D t, double fov, double r, bool exp, string ad)[]
        {
            (V(0,0), V(0,1), V(1,3), 90, 10, true,  "P1 (1,3): koni içinde"),
            (V(0,0), V(0,1), V(4,1), 90, 10, false, "P2 (4,1): koni dışında"),
            (V(0,0), V(0,1), V(1,1), 90, 10, true,  "Sınır: tam 45° (dahil)"),
            (V(0,0), V(0,1), V(0,10), 90, 10, true, "Sınır: tam menzilde (dahil)"),
            (V(0,0), V(0,1), V(0,10.01), 90, 10, false, "Menzil dışı"),
            (V(0,0), V(0,1), V(0,-3), 90, 10, false, "Tam arkada"),
            (V(2,2), V(1,0), V(5,2.5), 30, 5, true,  "Kaydırılmış ajan, dar koni"),
            (V(0,0), V(0,1), V(-3,-1), 270, 10, true, "Geniş koni (270°), arka-yan"),
        };
        int ok = 0;
        foreach (var c in fov)
        {
            bool r = DersOdevleri.IsInFov(c.a, c.h, c.t, c.fov, c.r);
            ok += r == c.exp ? 1 : 0;
            sb.AppendLine($"{(r == c.exp ? "✔" : "✘")} IsInFov {c.ad}: {r}");
        }

        var side = new (Vector2D a, Vector2D h, Vector2D t, int exp, string ad)[]
        {
            (V(0,0), V(0,1), V(1,3), -1, "(1,3) sağda"),
            (V(0,0), V(0,1), V(-3,1), 1, "(−3,1) solda"),
            (V(0,0), V(0,1), V(0,5), 0, "aynı doğrultu"),
            (V(0,0), V(1,0), V(2,2), 1, "ileri (1,0), hedef (2,2) solda"),
            (V(5,5), V(0,-1), V(6,0), 1, "aşağı bakan ajan, hedef solda"),
            (V(0,0), V(0,1), V(0,-5), 0, "tam arkada (doğrultu)"),
        };
        foreach (var c in side)
        {
            int r = DersOdevleri.SideOf(c.a, c.h, c.t);
            ok += r == c.exp ? 1 : 0;
            sb.AppendLine($"{(r == c.exp ? "✔" : "✘")} SideOf {c.ad}: {r}");
        }

        // Laboratuvar soruları
        sb.AppendLine($"\nA(2,1)–B(8,9) mesafe: {V(2,1).Distance(V(8,9)):0.##}");
        sb.AppendLine($"(6,8) büyüklük {V(6,8).Length():0.##}, birim {V(6,8).Normalized()}");
        double aci = System.Math.Acos(V(1,0).Dot(V(2,2).Normalized())) * 180 / System.Math.PI;
        sb.AppendLine($"İleri (1,0), hedef (2,2) açı: {aci:0.##}°");
        sb.AppendLine($"İleri (0,1), hedef yönü (−3,1): {(V(0,1).Cross(V(-3,1)) > 0 ? "solda" : "sağda")}");
        sb.AppendLine($"20 m/s, 60°: vx = {20 * System.Math.Cos(System.Math.PI / 3):0.##}, vy = {20 * System.Math.Sin(System.Math.PI / 3):0.##}");
        sb.Insert(0, $"[{ok}/{fov.Length + side.Length} test geçti] ");
        Debug.Log(sb.ToString());
    }

    [MenuItem("YapayZeka/Hafta 2 Testleri (Akıllı Kule)")]
    public static void Hafta2()
    {
        var sb = new StringBuilder("<b>Hafta 2 – Euler, top, akıllı kule</b>\n");

        // Euler hata tablosu: m = 2, F = (10, 0), 1 s, gerçek 2.5 m
        foreach (double dt in new[] { 0.5, 0.1, 0.02 })
        {
            var ag = new Agent2D { Mass = 2 };
            for (int i = 0; i < System.Math.Round(1 / dt); i++) ag.Update(V(10, 0), dt);
            sb.AppendLine($"Euler Δt={dt}: x = {ag.Position.X:0.###} m (hata {ag.Position.X - 2.5:0.###})");
        }
        sb.AppendLine($"(6,8).Truncate(5) = {V(6,8).Truncate(5)}");

        // Top: u = 10, a = −2
        sb.AppendLine($"Top 3 sn sonra: {DersOdevleri.FuturePosition(10, -2, 3):0.##} m, " +
                      $"16 m: {DersOdevleri.TimeToCoverDistance(10, -2, 16):0.##} sn, " +
                      $"30 m: {DersOdevleri.TimeToCoverDistance(10, -2, 30)}");

        // Akıllı kule: 6 senaryo, yaklaşık ve kesin yan yana
        var s = new (string ad, Vector2D hedef, Vector2D hiz, double mermi)[]
        {
            ("Yandan geçen",      V(10, 0), V(0, 3),   15),
            ("Uzaklaşan",         V(10, 0), V(5, 0),   15),
            ("Yaklaşan",          V(10, 0), V(-5, 0),  15),
            ("Duran",             V(10, 0), V(0, 0),   15),
            ("Mermiden hızlı kaçan", V(10, 0), V(20, 0), 15),
            ("Çapraz, uzak",      V(30, 10), V(-3, 4), 20),
        };
        sb.AppendLine("\nSenaryo | Yaklaşık nişan | Kesin nişan | Fark");
        foreach (var c in s)
        {
            Vector2D yak = DersOdevleri.NisanYaklasik(V(0, 0), c.hedef, c.hiz, c.mermi, out double ty);
            bool kesinVar = DersOdevleri.NisanAl(V(0, 0), c.hedef, c.hiz, c.mermi, out Vector2D kes, out double tk);
            string kesStr = kesinVar ? $"{kes} t={tk:0.###}" : "yetişemez";
            string fark = kesinVar ? $"{yak.Distance(kes):0.##} m" : "-";
            sb.AppendLine($"{c.ad} | {yak} t={ty:0.###} | {kesStr} | {fark}");
        }

        // Balistik
        sb.AppendLine("\nAtış açısı (v, x, y) | alçak | yüksek | uçuş");
        foreach (var (v, x, y) in new[] { (20.0, 30.0, 0.0), (20.0, 30.0, 5.0), (20.0, 45.0, 0.0), (25.0, 40.0, 0.0) })
        {
            if (DersOdevleri.AtisAcisi(v, x, y, out double lo, out double hi))
                sb.AppendLine($"({v}, {x}, {y}) | {lo:0.#}° | {hi:0.#}° | " +
                              $"{DersOdevleri.UcusSuresi(v, x, lo):0.##} / {DersOdevleri.UcusSuresi(v, x, hi):0.##} sn");
            else
                sb.AppendLine($"({v}, {x}, {y}) | menzil dışı");
        }

        sb.AppendLine("\nYorum: Yaklaşık yöntem hedefin hızını mermi süratine 'ekleyerek' süreyi tahmin eder; " +
                      "hedef yandan geçerken ya da uzaklaşırken süreyi kısa bulur ve mermi hedefin arkasına düşer. " +
                      "Her karede yeniden hesaplanan takipte bu hata kapanır ama tek atışlık mermide ıska demektir. " +
                      "Hedef mermiden hızlı kaçıyorsa kesin yöntem 'yetişemez' der, yaklaşık yöntem yine de boşa ateş ettirir.");
        Debug.Log(sb.ToString());
    }
}
