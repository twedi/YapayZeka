using System;

/// <summary>Hafta 1 ve 2 ödev fonksiyonları (Unity'den bağımsız, saf C#).</summary>
public static class DersOdevleri
{
    // ---------------------------------------------------------------- Hafta 1

    /// <summary>Hedef menzilde ve görüş konisinde mi? Karekök ve acos kullanmadan.</summary>
    public static bool IsInFov(Vector2D agent, Vector2D heading, Vector2D target, double fovDeg, double range)
    {
        Vector2D d = target - agent;
        double distSq = d.LengthSq();
        if (distSq > range * range) return false;            // kare mesafe
        if (distSq < 1e-12) return true;                     // tam üstünde

        // ileri · birim(d) ≥ cos(yarım açı)  ⇔  ileri · d ≥ cos · |d|  ⇔ (karelerle, işarete dikkat)
        double cosHalf = Math.Cos(fovDeg * 0.5 * Math.PI / 180.0);
        Vector2D h = heading.Normalized();
        double dot = h.Dot(d);
        if (cosHalf >= 0)
            return dot >= 0 && dot * dot >= cosHalf * cosHalf * distSq - 1e-9;   // sınır açı dahil
        return dot >= 0 || dot * dot <= cosHalf * cosHalf * distSq + 1e-9;   // koni 180°'den geniş
    }

    /// <summary>+1 sol, −1 sağ, 0 aynı doğrultu.</summary>
    public static int SideOf(Vector2D agent, Vector2D heading, Vector2D target)
    {
        double c = heading.Cross(target - agent);
        return c > 1e-9 ? 1 : (c < -1e-9 ? -1 : 0);
    }

    // ---------------------------------------------------------------- Hafta 2

    /// <summary>x = u·t + ½·a·t²</summary>
    public static double FuturePosition(double u, double a, double t) => u * t + 0.5 * a * t * t;

    /// <summary>v² = u² + 2·a·x ; ulaşamazsa −1.</summary>
    public static double TimeToCoverDistance(double u, double a, double x)
    {
        double v2 = u * u + 2 * a * x;
        if (v2 < 0) return -1;
        double v = Math.Sqrt(v2);
        if (Math.Abs(a) < 1e-12) return u > 0 ? x / u : -1;
        return (v - u) / a;
    }

    /// <summary>Yaklaşık kestirim (Buckland Pursuit): t ≈ |P| / (s + |V|).</summary>
    public static Vector2D NisanYaklasik(Vector2D kule, Vector2D hedef, Vector2D hedefHiz, double mermiSurati, out double sure)
    {
        Vector2D P = hedef - kule;
        sure = P.Length() / (mermiSurati + hedefHiz.Length());
        return hedef + hedefHiz * sure;
    }

    /// <summary>Kesin kestirim: (V·V − s²)t² + 2(P·V)t + P·P = 0, en küçük pozitif kök. Yetişemiyorsa false.</summary>
    public static bool NisanAl(Vector2D kule, Vector2D hedef, Vector2D hedefHiz, double mermiSurati,
                               out Vector2D nisan, out double sure)
    {
        Vector2D P = hedef - kule, V = hedefHiz;
        double a = V.Dot(V) - mermiSurati * mermiSurati;
        double b = 2 * P.Dot(V);
        double c = P.Dot(P);
        sure = -1;

        if (Math.Abs(a) < 1e-9)                               // doğrusal durum: hedef mermi kadar hızlı
        {
            if (Math.Abs(b) > 1e-9 && -c / b > 0) sure = -c / b;
        }
        else
        {
            double disc = b * b - 4 * a * c;
            if (disc >= 0)
            {
                double r = Math.Sqrt(disc);
                double t1 = (-b - r) / (2 * a), t2 = (-b + r) / (2 * a);
                double tMin = Math.Min(t1, t2), tMax = Math.Max(t1, t2);
                sure = tMin > 0 ? tMin : (tMax > 0 ? tMax : -1);
            }
        }

        if (sure <= 0) { nisan = hedef; return false; }
        nisan = hedef + V * sure;
        return true;
    }

    /// <summary>tan θ = (v² ± √(v⁴ − g(g·x² + 2·y·v²))) / (g·x). Menzil dışındaysa false. Açılar derece.</summary>
    public static bool AtisAcisi(double v, double x, double y, out double alcak, out double yuksek, double g = 9.81)
    {
        alcak = yuksek = double.NaN;
        double ic = v * v * v * v - g * (g * x * x + 2 * y * v * v);
        if (ic < 0 || x <= 0) return false;
        double r = Math.Sqrt(ic);
        alcak = Math.Atan((v * v - r) / (g * x)) * 180.0 / Math.PI;
        yuksek = Math.Atan((v * v + r) / (g * x)) * 180.0 / Math.PI;
        return true;
    }

    /// <summary>Uçuş süresi: t = x / (v·cos θ).</summary>
    public static double UcusSuresi(double v, double x, double aciDerece) =>
        x / (v * Math.Cos(aciDerece * Math.PI / 180.0));
}
