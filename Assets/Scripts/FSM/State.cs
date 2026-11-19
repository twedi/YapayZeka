/// <summary>
/// Hafta 7 – Tüm durumların atası (Buckland, Bölüm 2).
/// T: durumun sahibi olan sınıf (bu projede EnemyAI).
/// </summary>
public abstract class State<T>
{
    public virtual string Name => GetType().Name;

    public virtual void Enter(T owner) { }      // duruma girerken bir kez
    public abstract void Execute(T owner);      // durumdayken her karede
    public virtual void Exit(T owner) { }       // durumdan çıkarken bir kez
}
