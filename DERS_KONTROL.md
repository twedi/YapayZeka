# Ders Konuları → Projedeki Karşılıkları

Tüm haftalar tek sahnede: `Assets/Scenes/SampleScene.unity`. Oynarken sol üstteki panel tuşları gösterir.

| Tuş | İş |
|---|---|
| WASD | Oyuncu hareketi |
| E | Kapıyı aç/kapat (yanındayken) |
| N | Ses çıkar (Hafta 7 ödevi) |
| M / J | Çamur maliyetini değiştir / zıplama bağlantısını aç-kapat (Hafta 6) |
| Sol tık, 1–4 | Izgara üzerinde DFS / BFS / Dijkstra / A* (Hafta 4) |
| T | Waypoint grafında A* + yol yumuşatma (Hafta 5) |
| C | Üst görünüm ↔ takip kamerası |

## Hafta 1–2 – Matematik ve Fizik
- `Assets/Scripts/Ders/Vector2D.cs`: Vector2D (Length, Dot, Cross, Normalize, Truncate) ve Euler ile hareket eden `Agent2D`.
- `Assets/Scripts/Ders/DersOdevleri.cs`: `IsInFov` ve `SideOf` (karekök/acos yok), `NisanAl` (kesin kestirim), `AtisAcisi`, `FuturePosition`, `TimeToCoverDistance`.
- Testleri çalıştırmak için menüden **YapayZeka > Hafta 1 Testleri** ve **YapayZeka > Hafta 2 Testleri** seçilir. Sonuçlar Console'a yazılır:
  - Hafta 1: 14 test, sınır açıları dahil.
  - Hafta 2: Euler hata tablosu, top soruları, 6 senaryoda yaklaşık ve kesin nişan yan yana, balistik açılar ve yorum.

## Hafta 3 – Arazi, Girdi, Hareket, Gözcü
- **Oyuncu** (`PlayerController.cs`): CharacterController ve New Input System kullanıyor; çaprazda hızlanma ve yerçekimi düzeltildi.
- **Kamera** (`Ders/CameraModes.cs`): Yumuşatma kare hızından bağımsız (`1 − e^(−kΔt)`). Takip modunda SphereCast ile duvarın içine girmiyor.
- **Gözcü** (`Ders/Gozcu.cs`, sahnedeki `Gozcu` objesi):
  - Görüş konisi nokta çarpımla hesaplanıyor, görüş hattı Linecast ile kontrol ediliyor.
  - Görmüyorsa yeşil, görüyorsa kırmızı. Oyuncunun sağında mı solunda mı olduğu konsola yazılıyor.
  - Menzil ve koni Gizmos ile çiziliyor.
  - Ek görevler: tarama, yükseklik tuzağı, takip.

## Hafta 4 – A*
- `Ders/GridAStar.cs` ve `Ders/MinHeap.cs`, sahnedeki `IzgaraAStar` objesi.
  - Arazinin üstüne 2 m'lik hücrelerden bir ızgara seriliyor; engeller her aramada yeniden okunuyor.
  - Sol tıkla oyuncudan tıklanan noktaya yol bulunuyor. Konsola yol uzunluğu, maliyet ve genişletilen düğüm sayısı yazılıyor.
  - Dağ Yolu fikri hücre maliyetine eklendi: eğim arttıkça maliyet artıyor, çamur ve sığ su alanları çarpan ekliyor.
- Unity NavMesh ile karşılaştırma için düşmanın NavMesh yolu sahnede ayrıca çiziliyor.

## Hafta 5 – Waypoint'ler ve Graflar
- **Devriye** (`PatrolRoute.cs`): `DevriyeRotasi` objesinin çocukları waypoint olarak kullanılıyor. Döngü ya da gidip gelme modu seçilebiliyor (`loop` kapalıysa gidip gelme).
- **Graf** (`Ders/WaypointGraph.cs`):
  - Kenarlar komşuluk listesiyle tutuluyor. Birbirini gören ve yakın düğümler arasında otomatik kuruluyor.
  - Grafa en yakın görünen düğümden biniliyor.
  - Grafta A* Öklid sezgiselle çalışıyor. Dijkstra'yla karşılaştırmalı genişletme sayısı konsola yazılıyor.
  - Yol yumuşatma (ek görev): A*'ın yolu gri, yumuşatılmış yol mavi çiziliyor.

## Hafta 6 – NavMesh
- **Alan maliyetleri:**
  - `Mud`: maliyet 20 (`Camur` objesi).
  - `SigSu`: maliyet 3 (`SigSu` objesi). İki alan da NavMeshModifierVolume ile işaretlendi (`MudZone.cs`).
  - Ek görev: bu alanlara giren yavaşlıyor.
- **Zıplama bağlantısı:** NavMeshLink (`ZiplamaLinki`) üzerinden parabolle atlanıyor (`EnemyAI.JumpAcrossLink`, `JumpLink.cs`).
- **Hareketli engel:** `HareketliEngel` objesi, Carve açık NavMeshObstacle kullanıyor (`MovingObstacle.cs`).
- **Çalışma anında bake:** Kapı açılıp kapanınca NavMesh yeniden bake ediliyor (`RisingDoor.cs`, `NavMeshRebaker.cs`).
- **Yol paneli** (`NavDebugPanel.cs`, PathDebugDraw + AreaCostToggle karşılığı):
  - Ekranda köşe sayısı, uzunluk, maliyet ve PathStatus gösteriliyor; yol durumu değişince konsola yazılıyor.
  - M ile çamur maliyeti, J ile bağlantı değiştiriliyor.

## Hafta 7 – Sonlu Durum Makineleri
- **FSM altyapısı:** `FSM/State.cs` ve `FSM/StateMachine.cs`. Mevcut, önceki ve global durumu tutuyor; her geçiş konsola yazılıyor.
- **Düşman** (`EnemyAI.cs`): Sadece veri ve yardımcı metotları tutuyor. Durumlar `EnemyStates.cs` içinde:
  - **Global:** Oyuncuyu görünce Kovala'ya geçirir.
  - **Devriye (yeşil):** Rotayı gezer.
  - **Kovala (kırmızı):** Oyuncuyu takip eder.
  - **Ara (sarı):** Son görülen noktaya gidip bakınır.
  - **Kapıda Bekle (gri):** Kapı önünde kapanırsa durur. Açılınca `RevertToPreviousState` ile önceki duruma döner.
  - **Araştır (mor, ödev):** N ile çıkarılan sese 8 m içindeyse gider, 3 sn bakınır, Devriye'ye döner. Kovala'dayken sesi yok sayar.
- **Ses:** `PlayerNoise.cs`, Input Actions'taki `MakeNoise` eylemini (N) kullanıyor.

### Araştır eklenmiş geçiş tablosu (ödev)
| Mevcut durum | Koşul | Sonraki durum |
|---|---|---|
| Herhangi biri (Kovala hariç) | Oyuncu görüldü | Kovala |
| Devriye | Kapı az önce kapandı ve kapıya yakın | Kapıda Bekle |
| Devriye | 8 m içinde ses duyuldu | Araştır |
| Kovala | Oyuncu `memoryDuration` sn'den uzun süredir görünmüyor | Ara |
| Ara | Kapı az önce kapandı ve kapıya yakın | Kapıda Bekle |
| Ara | 8 m içinde ses duyuldu | Araştır |
| Ara | Son görülen noktaya vardı ve süre doldu | Devriye |
| Araştır | Kapı az önce kapandı ve kapıya yakın | Kapıda Bekle |
| Araştır | Sese vardı ve 3 sn bakındı | Devriye |
| Kapıda Bekle | Kapı açıldı (ya da en fazla 8 sn bekledi) | Önceki durum |
