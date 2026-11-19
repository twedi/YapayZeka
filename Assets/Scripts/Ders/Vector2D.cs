using System;

/// <summary>Hafta 1–2 – Buckland'ın Vector2D yapısının C# hali.</summary>
public struct Vector2D
{
    public double X, Y;

    public Vector2D(double x, double y) { X = x; Y = y; }

    public double Length() => Math.Sqrt(X * X + Y * Y);
    public double LengthSq() => X * X + Y * Y;
    public double Dot(Vector2D v) => X * v.X + Y * v.Y;
    public double Cross(Vector2D v) => X * v.Y - Y * v.X;          // > 0: v solda, < 0: sağda
    public Vector2D Normalized() { double l = Length(); return l > 1e-9 ? this * (1 / l) : this; }
    public Vector2D Truncate(double max) { double l = Length(); return l > max ? this * (max / l) : this; }
    public double Distance(Vector2D v) => (v - this).Length();
    public double DistanceSq(Vector2D v) => (v - this).LengthSq();

    public static Vector2D operator +(Vector2D a, Vector2D b) => new Vector2D(a.X + b.X, a.Y + b.Y);
    public static Vector2D operator -(Vector2D a, Vector2D b) => new Vector2D(a.X - b.X, a.Y - b.Y);
    public static Vector2D operator *(Vector2D a, double k) => new Vector2D(a.X * k, a.Y * k);

    public override string ToString() => $"({X:0.##}, {Y:0.##})";
}

/// <summary>Hafta 2 – Euler entegrasyonuyla hareket eden ajan (Vehicle::Update).</summary>
public class Agent2D
{
    public Vector2D Position, Velocity, Heading = new Vector2D(0, 1);
    public double Mass = 1, MaxSpeed = double.MaxValue, MaxForce = double.MaxValue;

    public void Update(Vector2D kuvvet, double dt)
    {
        Vector2D ivme = kuvvet.Truncate(MaxForce) * (1.0 / Mass);   // a = F / m
        Velocity = (Velocity + ivme * dt).Truncate(MaxSpeed);       // v += a·Δt, sınırla
        Position = Position + Velocity * dt;                        // p += v·Δt
        if (Velocity.LengthSq() > 1e-8) Heading = Velocity.Normalized();
    }
}
