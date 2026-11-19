using UnityEngine;

/// <summary>
/// Hafta 7 – Mevcut, önceki ve global durumu tutan durum makinesi.
/// MonoBehaviour değildir; sahibi her karede Update'ini çağırır.
/// </summary>
public class StateMachine<T>
{
    readonly T owner;

    public State<T> Current { get; private set; }
    public State<T> Previous { get; private set; }
    public State<T> Global { get; set; }
    public bool LogTransitions { get; set; } = true;

    public StateMachine(T owner)
    {
        this.owner = owner;
    }

    // Her karede: önce global durum, sonra mevcut durum
    public void Update()
    {
        Global?.Execute(owner);
        Current?.Execute(owner);
    }

    public void ChangeState(State<T> next)
    {
        if (next == null || next == Current) return;

        if (LogTransitions)
            Debug.Log($"[{Time.time:0.0} sn] {Current?.Name ?? "-"} → {next.Name}");

        Previous = Current;          // 1. eskiyi sakla
        Current?.Exit(owner);        // 2. eskiden çık
        Current = next;              // 3. yeniyi geçerli yap
        Current.Enter(owner);        // 4. yeniye gir
    }

    public void RevertToPreviousState()
    {
        ChangeState(Previous);
    }

    public bool IsIn(State<T> state)
    {
        return Current == state;
    }
}
