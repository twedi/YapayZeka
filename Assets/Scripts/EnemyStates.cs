using UnityEngine;

// Hafta 7 – Düşmanın durumları (State deseni). Her sınıfı ders notundaki 5.1 tablosuyla karşılaştırın:
//
// Durum         | Enter (bir kez)                       | Execute (her kare)                         | Exit (bir kez)
// Devriye       | Yeşil, sıradaki noktayı hedef al      | Kapı engel mi? Vardıysan sonrakini hedefle | –
// Kovala        | Kırmızı, hızlan, frenlemeyi kapat     | Takip et; görmüyorsan Ara                  | Hız/fren ayarlarını geri al
// Ara           | Sarı, son görülen noktayı hedef al    | Vardıysan bakın; süre dolduysa Devriye     | Dönüşü ajana geri ver
// Kapıda Bekle  | Gri, ajanı durdur                     | Kapı açıldıysa önceki duruma dön           | Ajanı yeniden yürüt
// Araştır (ödev)| Mor, sesin geldiği yeri hedef al      | Vardıysan 3 sn bakın, sonra Devriye        | Dönüşü ajana geri ver

/// <summary>Global durum: her karede mevcut durumdan ÖNCE çalışır.</summary>
public class EnemyGlobal : State<EnemyAI>
{
    public override void Execute(EnemyAI e)
    {
        if (!e.SeesTarget) return;               // son görülen konum EnemyAI.Update'te güncellendi
        if (!e.FSM.IsIn(e.Chase)) e.FSM.ChangeState(e.Chase);
    }
}

public class EnemyPatrol : State<EnemyAI>
{
    float waitTimer;

    public override string Name => "Devriye";

    public override void Enter(EnemyAI e)
    {
        e.baseSpeed = e.patrolSpeed;
        e.Agent.stoppingDistance = 0f;
        e.Agent.autoBraking = true;
        waitTimer = 0f;
        e.GoToCurrentWaypoint();
        e.ApplyStateVisuals(e.patrolColor, "DEVRİYE", true);
    }

    public override void Execute(EnemyAI e)
    {
        if (e.DoorBlocksMe()) { e.FSM.ChangeState(e.WaitAtDoor); return; }

        // Noktaya varmak bir geçiş değildir: durum değişmez, sıradaki nokta hedeflenir
        if (e.HasArrived(e.waypointTolerance))
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= e.waitAtWaypoint)
            {
                waitTimer = 0f;
                e.AdvanceWaypoint();
            }
        }
    }
}

public class EnemyChase : State<EnemyAI>
{
    public override string Name => "Kovala";

    public override void Enter(EnemyAI e)
    {
        e.baseSpeed = e.chaseSpeed;
        e.Agent.stoppingDistance = e.catchDistance * 0.8f;
        e.Agent.autoBraking = false;
        e.ResetChase();
        e.onPlayerSpotted?.Invoke();
        e.ApplyStateVisuals(e.chaseColor, "KOVALIYOR!", false);
    }

    public override void Execute(EnemyAI e)
    {
        if (e.target == null) { e.FSM.ChangeState(e.Patrol); return; }
        if (Time.time - e.lastSeenTime > e.memoryDuration) { e.FSM.ChangeState(e.Search); return; }
        e.ChaseTick();
    }

    public override void Exit(EnemyAI e)
    {
        // Enter'da değişen ayarları geri al
        e.baseSpeed = e.patrolSpeed;
        e.Agent.stoppingDistance = 0f;
        e.Agent.autoBraking = true;
    }
}

public class EnemySearch : State<EnemyAI>
{
    float lookTimer;   // her düşmanın kendi durum nesnesi olduğu için veri tutabilir

    public override string Name => "Ara";

    public override void Enter(EnemyAI e)
    {
        e.baseSpeed = e.searchSpeed;
        e.Agent.SetDestination(e.lastKnownPosition);
        lookTimer = e.searchDuration;            // her girişte süre sıfırdan başlar
        e.ApplyStateVisuals(e.searchColor, "ARIYOR ?", false);
    }

    public override void Execute(EnemyAI e)
    {
        if (e.DoorBlocksMe()) { e.FSM.ChangeState(e.WaitAtDoor); return; }
        if (!e.HasArrived(e.waypointTolerance)) return;

        e.LookAround();
        lookTimer -= Time.deltaTime;
        if (lookTimer <= 0f)
        {
            e.SelectClosestWaypoint();
            e.FSM.ChangeState(e.Patrol);
        }
    }

    public override void Exit(EnemyAI e)
    {
        e.Agent.updateRotation = true;           // LookAround kapatmıştı
    }
}

public class EnemyWaitAtDoor : State<EnemyAI>
{
    float waited;

    public override string Name => "KapıdaBekle";

    public override void Enter(EnemyAI e)
    {
        waited = 0f;
        e.Agent.isStopped = true;
        e.ApplyStateVisuals(e.waitColor, "KAPIDA BEKLİYOR", false);
    }

    public override void Execute(EnemyAI e)
    {
        waited += Time.deltaTime;
        bool timeout = e.maxDoorWait > 0f && waited >= e.maxDoorWait;
        if (e.door == null || e.door.IsOpen || timeout)
            e.FSM.RevertToPreviousState();       // Devriye, Ara ya da Araştır
    }

    public override void Exit(EnemyAI e)
    {
        e.Agent.isStopped = false;               // bu satır olmazsa düşman donar (ders notu 5.2)
    }
}

/// <summary>Hafta 7 ödevi: sesi duyan bekçi.</summary>
public class EnemyInvestigate : State<EnemyAI>
{
    float lookTimer;

    public override string Name => "Araştır";

    public override void Enter(EnemyAI e)
    {
        e.baseSpeed = e.searchSpeed;
        e.Agent.updateRotation = true;
        e.Agent.SetDestination(e.noisePosition);
        lookTimer = e.investigateLookTime;
        e.ApplyStateVisuals(e.investigateColor, "SES?", false);
    }

    public override void Execute(EnemyAI e)
    {
        if (e.DoorBlocksMe()) { e.FSM.ChangeState(e.WaitAtDoor); return; }
        if (!e.HasArrived(e.waypointTolerance)) return;

        e.LookAround();
        lookTimer -= Time.deltaTime;
        if (lookTimer <= 0f)
        {
            e.SelectClosestWaypoint();
            e.FSM.ChangeState(e.Patrol);
        }
    }

    public override void Exit(EnemyAI e)
    {
        e.Agent.updateRotation = true;
    }
}
