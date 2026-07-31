# Faz 3.75 — Ring Polish + Faz 4 Hazırlığı

**Faz 3.5 bitti, Faz 4'e geçmeden önce.**

Bu doküman iki bölümden oluşuyor:

| Bölüm | Ne zaman |
|---|---|
| **A — Ring interpolasyonu** | **Şimdi uygula.** 2 dosya değişiyor. |
| **B — Collect girişi** | **Faz 4'e geçtiğinde uygula.** Orijinal plandaki Faz 4'ün ilgili kısımlarını EZER. |

---

## 0. Teşhis: snap neden oluyor

Mevcut formül: `angle_i = _phase + i * (360 / count)`

Bir kılıç iptal olunca **iki şey birden anında** değişiyor:

**1. `step` zıplıyor.** 5 kılıçta 72°, 4 kılıçta 90°. Kalan her kılıcın hedefi
aynı karede değişiyor.

**2. `i` kayıyor.** `RemoveAt(...)` sonrası index 3'teki kılıç index 2 oluyor —
hedefi bir tam slot birden atlıyor.

İkisi üst üste binince kılıç yarım tur teleport edebiliyor. Teleport ettiği yer
rakibin kılıcının üstüyse anında ikinci çarpışma → gördüğün "çok hızlı collide"
tam olarak bu.

### 0.1 Dünya uzayında Lerp neden çalışmaz

`Lerp(currentWorldPos, targetWorldPos, t)` yaparsan karakter hareket ederken
hedef her kare kaçar, kılıç sürekli geride kalır ve asla yakalayamaz.
Klasik "hareketli hedefe lerp" hatası.

### 0.2 Çözüm: interpolasyonu polar-local uzayda yap

**Pozisyonu değil, açı ofsetini yumuşat.**

```
pos = center + polar(_phase + offset) * radius
```

`center` her kare canlı okunuyor (`PredictedPosition`) → kılıç **her zaman**
karakterin etrafında, takip gecikmesi matematiksel olarak imkânsız.
Yumuşayan tek şey `offset` ve `radius`, ikisi de karakterin hareketinden bağımsız.

### 0.3 Tek mekanizma, üç senaryo

| Senaryo | Ne değişiyor |
|---|---|
| Çarpışma sonrası yeniden dizilim | `offset` hedefi değişir, `radius` sabit |
| Collect girişi | `radius` dışarıdan içeri iner, `offset` slota oturur |
| `OrbitRadius` powerup'ı | Bedava yumuşak — ekstra kod yok |

"Collect'te tween oynatalım" ihtiyacı ayrı bir sistem gerektirmiyor;
aynı yakınsamanın farklı başlangıç değeri.

---

# BÖLÜM A — Ring İnterpolasyonu (şimdi uygula)

**Değişen: 2 dosya.** `Sword.cs`, `SwordView.cs`, `SwordVsSwordRule.cs`
ve diğer her şey **aynı kalıyor** — `Detach` imzası değişmedi.

---

## A.1 `Character/CharacterDefinition.cs`

Ring ayarları eklendi (`Sword Ring` bloğuna iki alan).

```csharp
using UnityEngine;

public enum CharacterBrainType
{
    Player,
    AI,
    None        // eğitim kuklası, sabit hedef
}

[CreateAssetMenu(menuName = "Game/Character Definition")]
public sealed class CharacterDefinition : ScriptableObject
{
    [Header("Prefab")]
    public CharacterView ViewPrefab;

    [Header("Brain")]
    public CharacterBrainType BrainType = CharacterBrainType.AI;
    public bool               HasSwordRing = true;

    [Header("Movement")]
    public float MoveSpeed    = 5f;
    public float Acceleration = 40f;
    public float Deceleration = 60f;
    public float MaxHealth    = 100f;

    [Header("Sword Ring")]
    public int   SwordCount        = 3;
    public int   MaxSwordCount     = 12;
    public float OrbitRadius       = 1.5f;
    public float OrbitAngularSpeed = 120f;
    public float SwordDamage       = 10f;

    [Header("Sword Ring — Interpolasyon")]
    [Tooltip("Kılıç kaybedince kalanların yeni yerine oturma süresi.")]
    public float RingSettleTime = 0.18f;

    [Tooltip("Yeni kılıcın içeri spiral çizme süresi.")]
    public float RingEntryTime = 0.35f;

    [Tooltip("Yeni kılıç OrbitRadius'un kaç katından başlasın.")]
    public float RingEntryRadiusScale = 2.5f;

    [Header("Feedback")]
    public AudioClip HitClip;                       // PlayerHit / EnemyHit
    [Range(0f, 1f)] public float HitVolume = 0.9f;
    public Color FlashColor    = Color.red;
    public float FlashDuration = 0.14f;

    [Header("AI (BrainType = AI ise geçerli)")]
    public float AggroEnterRadius      = 6f;
    public float AggroExitRadius       = 8f;
    public float CollectibleSeekRadius = 12f;
    public int   SwordAdvantageMargin  = 1;
    public float DecisionInterval      = 0.25f;
    public float RoamRadius            = 5f;
    public float RoamRepathInterval    = 2f;

    private void OnValidate()
    {
        MaxSwordCount        = Mathf.Max(MaxSwordCount, SwordCount);
        FlashDuration        = Mathf.Max(0.02f, FlashDuration);
        RingSettleTime       = Mathf.Max(0.01f, RingSettleTime);
        RingEntryTime        = Mathf.Max(0.01f, RingEntryTime);
        RingEntryRadiusScale = Mathf.Max(1f, RingEntryRadiusScale);
        AggroExitRadius      = Mathf.Max(AggroExitRadius, AggroEnterRadius);
        SwordAdvantageMargin = Mathf.Max(1, SwordAdvantageMargin);
    }
}
```

> **Neden `Offset` ve `Radius` ayrı süreler:** `Radius` sadece girişte değişiyor,
> `Offset` sadece yeniden dizilimde. Ayrı tutunca ekstra state olmadan
> "giriş yavaş, dizilim hızlı" ayarı yapılabiliyor.

---

## A.2 `Abilities/SwordRingAbility.cs`

Tam yeni hâl. `_swords` listesi → `_slots`.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tek sözleşmesi: "SwordCount stat'ı ne diyorsa o kadar kılıcım olsun."
///
/// Konumlandırma POLAR-LOCAL uzayda yapılıyor:
///     pos = center + polar(_phase + offset) * radius
/// center her kare canlı okunduğu için kılıç karakteri asla kaybetmiyor;
/// yumuşayan tek şey offset ve radius.
/// </summary>
public sealed class SwordRingAbility : IAbility
{
    /// <summary>
    /// struct — List içinde yaşıyor, allocation yok.
    /// Mutasyon: kopyala → değiştir → geri yaz.
    /// </summary>
    private struct SwordSlot
    {
        public Sword Sword;
        public int   SlotIndex;
        public float Offset;      // derece, _phase'e göre
        public float OffsetVel;
        public float Radius;
        public float RadiusVel;
    }

    // Sıralama karşılaştırıcısı static — Sort() her çağrıda delegate ayırmasın
    private static readonly Comparison<SwordSlot> ByOffset =
        (a, b) => Mathf.Repeat(a.Offset, 360f).CompareTo(Mathf.Repeat(b.Offset, 360f));

    private readonly List<SwordSlot> _slots    = new(12);
    private readonly List<Sword>     _detached = new(4);
    private readonly Pool<Sword>     _pool;
    private readonly FeedbackConfig  _config;

    private Character _owner;
    private float _phase;

    public SwordRingAbility(Pool<Sword> pool, FeedbackConfig config)
    {
        _pool   = pool;
        _config = config;
    }

    public int ActiveSwordCount => _slots.Count;

    // ================= INITIALIZE =================
    public void Initialize(Character owner)
    {
        _owner = owner;
        _phase = 0f;

        _slots.Clear();
        SyncCount();
        SnapAll();          // round başında spiral yok — retry görüntüsü sabit kalsın
    }

    public void Update(float deltaTime) { }

    // ================= TICK =================
    public void FixedUpdate(float deltaTime)
    {
        SweepDetached(deltaTime);
        SyncCount();

        if (_slots.Count == 0) return;

        _phase = Mathf.Repeat(_phase + _owner.Stats.OrbitAngularSpeed * deltaTime, 360f);

        float step        = 360f / _slots.Count;
        float radius      = _owner.Stats.OrbitRadius;
        float settleTime  = _owner.Definition.RingSettleTime;
        float entryTime   = _owner.Definition.RingEntryTime;

        // KRİTİK: bu fizik adımından SONRAKİ merkez.
        Vector2 center = _owner.Movement.PredictedPosition(deltaTime);

        for (int i = 0; i < _slots.Count; i++)
        {
            SwordSlot slot = _slots[i];

            float targetOffset = slot.SlotIndex * step;

            // SmoothDampAngle en kısa yolu kendi seçiyor, kritik sönümlü → overshoot yok
            slot.Offset = Mathf.SmoothDampAngle(
                slot.Offset, targetOffset, ref slot.OffsetVel, settleTime, Mathf.Infinity, deltaTime);

            slot.Radius = Mathf.SmoothDamp(
                slot.Radius, radius, ref slot.RadiusVel, entryTime, Mathf.Infinity, deltaTime);

            _slots[i] = slot;                       // struct → geri yaz

            float rad = (_phase + slot.Offset) * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * slot.Radius;

            slot.Sword.MoveTo(center + offset, rad);
        }
    }

    // ================= İPTAL =================
    /// <summary>
    /// Kural SwordCount'a DEĞİL buraya çağırmalı: sayacı doğrudan düşürürsen
    /// SyncCount listenin SONUNDAKİ kılıcı atar, çarpışan kılıç ring'de kalır.
    /// </summary>
    public void Detach(Sword sword, float verticalSign)
    {
        if (_owner == null) return;

        int index = IndexOf(sword);
        if (index < 0) return;

        SwordSlot slot = _slots[index];
        _slots.RemoveAt(index);
        ReassignSlots();

        _owner.Stats.SwordCount = Mathf.Max(0, _owner.Stats.SwordCount - 1);

        // Dışa doğru yön slotun açısından — pozisyon interpolasyon yüzünden gecikmiş olabilir
        float rad = (_phase + slot.Offset) * Mathf.Deg2Rad;
        Vector2 outward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        Vector2 direction = (outward + Vector2.up * verticalSign * _config.SwordThrowVerticalBias)
                            .normalized;

        sword.BeginDetach(direction, _config);
        _detached.Add(sword);
    }

    private void SweepDetached(float deltaTime)
    {
        for (int i = _detached.Count - 1; i >= 0; i--)
        {
            _detached[i].TickDetach(deltaTime);
            if (!_detached[i].IsDetachFinished) continue;

            ReturnSword(_detached[i]);
            _detached.RemoveAt(i);
        }
    }

    // ================= SLOT YÖNETİMİ =================
    private void SyncCount()
    {
        int desired = Mathf.Clamp(_owner.Stats.SwordCount, 0, _owner.Stats.MaxSwordCount);
        if (desired == _slots.Count) return;

        while (_slots.Count < desired) AddSlot();
        while (_slots.Count > desired) RemoveLastSlot();

        ReassignSlots();
    }

    private void AddSlot()
    {
        Sword sword = _pool.Rent();               // prewarm sayesinde Instantiate yok
        sword.Initialize(_owner, this);

        _slots.Add(new SwordSlot
        {
            Sword     = sword,
            SlotIndex = _slots.Count,
            Offset    = FindEntryOffset(),        // en boş aralığa gir
            OffsetVel = 0f,
            Radius    = _owner.Stats.OrbitRadius * _owner.Definition.RingEntryRadiusScale,
            RadiusVel = 0f
        });
    }

    private void RemoveLastSlot()
    {
        int last = _slots.Count - 1;
        ReturnSword(_slots[last].Sword);
        _slots.RemoveAt(last);
    }

    /// <summary>
    /// Yeni kılıç en geniş açı boşluğunun ortasına girer.
    /// Böylece hem görsel olarak doğru yere gelir hem de sıralamayı bozmaz.
    /// </summary>
    private float FindEntryOffset()
    {
        if (_slots.Count == 0) return 0f;
        if (_slots.Count == 1) return Mathf.Repeat(_slots[0].Offset + 180f, 360f);

        float bestMid = 0f;
        float bestGap = -1f;

        for (int i = 0; i < _slots.Count; i++)
        {
            float a = Mathf.Repeat(_slots[i].Offset, 360f);
            float b = Mathf.Repeat(_slots[(i + 1) % _slots.Count].Offset, 360f);
            float gap = Mathf.Repeat(b - a, 360f);

            if (gap <= bestGap) continue;

            bestGap = gap;
            bestMid = Mathf.Repeat(a + gap * 0.5f, 360f);
        }

        return bestMid;
    }

    /// <summary>
    /// SADECE sayı değiştiğinde çağrılır. Her kare çağırırsan iki kılıç
    /// birbirini geçtiğinde hedefleri takas olur ve salınım başlar.
    ///
    /// Mevcut ofsete göre sıralayıp yeniden numaralamak, hiçbir kılıcın
    /// çapraz geçmemesini ve herkesin minimum yolu almasını garanti eder.
    /// </summary>
    private void ReassignSlots()
    {
        _slots.Sort(ByOffset);

        for (int i = 0; i < _slots.Count; i++)
        {
            SwordSlot slot = _slots[i];
            slot.SlotIndex = i;
            _slots[i] = slot;
        }
    }

    /// <summary>Round başı — interpolasyon olmadan doğrudan yerine koy.</summary>
    private void SnapAll()
    {
        if (_slots.Count == 0) return;

        float step   = 360f / _slots.Count;
        float radius = _owner.Stats.OrbitRadius;

        for (int i = 0; i < _slots.Count; i++)
        {
            SwordSlot slot = _slots[i];
            slot.Offset    = slot.SlotIndex * step;
            slot.OffsetVel = 0f;
            slot.Radius    = radius;
            slot.RadiusVel = 0f;
            _slots[i] = slot;
        }
    }

    private int IndexOf(Sword sword)
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i].Sword == sword) return i;

        return -1;
    }

    private void ReturnSword(Sword sword)
    {
        sword.Deinitialize();
        _pool.Return(sword);
    }

    // ================= DEINITIALIZE =================
    public void Deinitialize()
    {
        // Uçmakta olan kılıçlar animasyonu beklemeden havuza döner
        for (int i = 0; i < _detached.Count; i++)
            ReturnSword(_detached[i]);
        _detached.Clear();

        for (int i = 0; i < _slots.Count; i++)
            ReturnSword(_slots[i].Sword);
        _slots.Clear();

        _owner = null;
        _phase = 0f;
    }
}
```

---

## A.3 Bölüm A Kabul Testleri

| Test | Beklenen |
|---|---|
| **Snap yok** | Kılıç iptal olunca kalanlar **kayarak** yeni yerine gidiyor, teleport yok |
| **Zincir makul** | Çarpışmalar art arda olabiliyor ama "bir anda buhar oldu" hissi yok |
| **Çapraz geçiş yok** | Yeniden dizilimde iki kılıç birbirinin içinden geçmiyor |
| **Takip** | Tam hızda koşarken kılıçlar karakterin etrafında, geride kalmıyor |
| **Round başı** | Play/R → kılıçlar spiral çizmeden doğrudan yerinde |
| Tek kılıç | `SwordCount = 1` → tek kılıç düzgün dönüyor |
| Sıfır | `SwordCount = 0` → hata yok |
| Salınım yok | Dizilim oturduktan sonra kılıçlar titremiyor |
| Allocation | Oyun sırasında GC Alloc **0 B/frame** |
| Retry | 10 kez R → 0 `Instantiate`, 0 `Destroy` |

**Ayar turu:** `RingSettleTime` çok yumuşak gelirse düşür (0.10–0.12).
Hâlâ "sünüyor" hissi varsa `Mathf.SmoothDampAngle` yerine
`Mathf.MoveTowardsAngle(current, target, speed * dt)` — sabit hız, kesin varış.

---

# BÖLÜM B — Collect Girişi (Faz 4'e geçince uygula)

> **Bu bölüm orijinal `Implementation-Plan-Phased.md` içindeki Faz 4'ün
> ilgili dosyalarını EZER.** Faz 4'ü uygularken önce oradaki dosyaları oluştur,
> sonra aşağıdakilerle değiştir.

## B.0 Seçilen tasarım: "C — iki ayrı görsel, tek algı"

Üç seçenek vardı:

| Seçenek | Nasıl | Sorun |
|---|---|---|
| A — Genel giriş | Kılıç ring'de dışarıdan spiral çizerek belirir | Baloncukla bağı zayıf |
| B — Baloncuktan uçarak | Pickup kuralı ring'i bulup `AddAt(point)` çağırır | `TryGetAbility<T>()` gerekir, kural ring'i tanır → **decoupling bozulur** |
| **C — İkisi birden** | Baloncuk karaktere doğru büzülerek uçar **+** kılıç ring'e genel girer | — |

**C seçildi.** Göz ikisini tek olay olarak okuyor:
baloncuk merkeze gidiyor, kılıç dışarıdan merkeze yaklaşıyor → "o şey bu oldu".

Kazanç: `SwordRingAbility` collectible'ı hâlâ **hiç bilmiyor**.
`SwordPickupRule` bir sayıyı artırıyor, ring `SyncCount` ile yakalıyor,
spiral giriş Bölüm A'daki `Radius` yakınsamasından **bedava** geliyor.

---

## B.1 `Feedback/FeedbackConfig.cs`

Collect uçuş ayarları eklendi.

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Feedback Config")]
public sealed class FeedbackConfig : ScriptableObject
{
    [Header("Sesler (karaktere özel olmayanlar)")]
    public AudioClip SwordClashClip;
    public AudioClip CollectClip;

    [Range(0f, 1f)] public float SwordClashVolume = 0.8f;
    [Range(0f, 1f)] public float CollectVolume    = 0.7f;

    [Header("Partiküller")]
    public ParticleView BloodSplashPrefab;
    public ParticleView SwordClashPrefab;
    public int ParticlePrewarm = 8;

    [Header("Kılıç savrulması")]
    public float SwordThrowDistance     = 3.5f;
    public float SwordThrowDuration     = 1.0f;
    public float SwordThrowSpin         = 720f;   // derece
    public float SwordThrowVerticalBias = 1.2f;   // yukarı/aşağı itiş şiddeti

    [Header("Collectible toplanma")]
    [Tooltip("Baloncuğun karaktere uçma süresi. Ring giriş süresinden kısa olmalı.")]
    public float CollectFlyDuration = 0.25f;
    public float CollectEndScale    = 0.15f;

    private void OnValidate()
    {
        SwordThrowDuration = Mathf.Max(0.05f, SwordThrowDuration);
        SwordThrowDistance = Mathf.Max(0f, SwordThrowDistance);
        ParticlePrewarm    = Mathf.Max(0, ParticlePrewarm);
        CollectFlyDuration = Mathf.Max(0.05f, CollectFlyDuration);
        CollectEndScale    = Mathf.Clamp(CollectEndScale, 0.01f, 1f);
    }
}
```

> **`CollectFlyDuration < RingEntryTime` olmalı.** Baloncuk kılıçtan önce
> varmalı ki algı "baloncuk girdi → kılıç oldu" sırasını izlesin.
> Varsayılanlar: 0.25 < 0.35 ✓

---

## B.2 `Collectibles/SwordCollectibleView.cs`

Uçuş animasyonu eklendi. DOTween disiplini kılıçtakiyle aynı.

```csharp
using DG.Tweening;
using UnityEngine;

public sealed class SwordCollectibleView : InteractionBody
{
    [SerializeField] private Rigidbody2D    _body;
    [SerializeField] private Collider2D     _pickupCollider;
    [SerializeField] private SpriteRenderer _sprite;

    private Sequence _consumeSequence;
    private Color    _baseColor;
    private Vector3  _baseScale;

    public Rigidbody2D Body => _body;

    private void Awake()
    {
        _baseScale = transform.localScale;
        if (_sprite != null) _baseColor = _sprite.color;
    }

    // Raporu COLLECTIBLE veriyor; CharacterView'a dokunmuyoruz.
    private void OnTriggerEnter2D(Collider2D other) => ReportContact(other);

    public void SetPickupEnabled(bool value)
    {
        if (_pickupCollider != null) _pickupCollider.enabled = value;
    }

    public void SetVisible(bool value)
    {
        if (_sprite != null) _sprite.enabled = value;
    }

    /// <summary>Karaktere doğru büzülerek uçar. Sadece görsel.</summary>
    public void PlayConsume(Vector2 target, float duration, float endScale)
    {
        KillConsume();

        _consumeSequence = DOTween.Sequence()
            .Append(transform.DOMove(target, duration).SetEase(Ease.InQuad))
            .Join(transform.DOScale(_baseScale * endScale, duration).SetEase(Ease.InQuad))
            // Havuzlanmış nesne: KillOnDestroy işe yaramaz, KillOnDisable şart.
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        if (_sprite != null)
        {
            _consumeSequence.Join(_sprite
                .DOFade(0f, duration * 0.5f)
                .SetDelay(duration * 0.5f));
        }
    }

    public void KillConsume()
    {
        if (_consumeSequence == null) return;

        Sequence s = _consumeSequence;
        _consumeSequence = null;
        s.Kill(false);                 // complete: false → OnComplete tetiklenmez
    }

    /// <summary>Havuza dönerken görsel durumu tamamen sıfırla.</summary>
    public void ResetVisual()
    {
        KillConsume();
        transform.localScale = _baseScale;
        if (_sprite != null)
        {
            _sprite.color   = _baseColor;
            _sprite.enabled = true;
        }
    }
}
```

**Prefab ayarları — elle:**

| Component | Ayar | Değer |
|---|---|---|
| Rigidbody2D | Body Type | Kinematic |
| Rigidbody2D | Use Full Kinematic Contacts | ✔ |
| Rigidbody2D | Interpolate | None (hareket etmiyor) |
| Collider2D | Is Trigger | ✔ |
| GameObject | Layer | `Collectible` |

---

## B.3 `Collectibles/SwordCollectible.cs`

`Consume` artık hedef ve süre alıyor, timer eklendi — kılıçtaki
`_detachTimer` deseninin aynısı.

```csharp
using UnityEngine;

/// <summary>
/// Savaşçı DEĞİL — sadece bir etkileşim nesnesi.
/// Herhangi bir karakter üstünden geçtiğinde toplanır.
/// </summary>
public sealed class SwordCollectible : IInteractionEntity
{
    private readonly SwordCollectibleView _view;
    private readonly InteractionResolver  _resolver;

    private float _consumeTimer;

    /// <summary>CREATE fazı — havuzun create fonksiyonu çağırıyor.</summary>
    public SwordCollectible(SwordCollectibleView view, InteractionResolver resolver)
    {
        _view     = view;
        _resolver = resolver;
    }

    public IInteractionEntity Root => this;      // kökü kendisi, kimseye ait değil

    public int  SwordAmount { get; private set; }
    public bool IsConsumed  { get; private set; }

    /// <summary>Uçuş animasyonu bitti mi? Spawner buna bakıp havuza iade ediyor.</summary>
    public bool IsConsumeFinished => IsConsumed && _consumeTimer <= 0f;

    public SwordCollectibleView View => _view;
    public Vector2 Position => _view.Body.position;

    public void Initialize(Vector2 position, int swordAmount)
    {
        SwordAmount   = swordAmount;
        IsConsumed    = false;
        _consumeTimer = 0f;

        _view.transform.position = position;
        _view.gameObject.SetActive(true);
        _view.Body.position = position;

        _view.ResetVisual();
        _view.SetPickupEnabled(true);
        _view.Bind(this, _resolver);
    }

    /// <summary>
    /// Toplandı. Collider HEMEN kapanıyor (çift pickup olmasın),
    /// görsel ise karaktere doğru uçmaya devam ediyor.
    /// </summary>
    public void Consume(Vector2 flyTarget, float duration, float endScale)
    {
        if (IsConsumed) return;

        IsConsumed    = true;
        _consumeTimer = duration;

        _view.Unbind();                        // artık etkileşime girmiyor
        _view.SetPickupEnabled(false);
        _view.PlayConsume(flyTarget, duration, endScale);
    }

    public void TickConsume(float deltaTime)
    {
        if (!IsConsumed) return;
        _consumeTimer -= deltaTime;
    }

    public void Deinitialize()
    {
        _view.Unbind();
        _view.ResetVisual();                   // tween kill + scale/alpha sıfır
        _view.SetPickupEnabled(false);
        _view.gameObject.SetActive(false);

        IsConsumed    = false;
        SwordAmount   = 0;
        _consumeTimer = 0f;
    }
}
```

---

## B.4 `Collectibles/SwordCollectibleSpawner.cs`

Sweep artık timer'a bakıyor.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct SwordCollectibleSpawnSettings
{
    public float SpawnInterval;      // 3f
    public int   MaxActive;          // 6
    public int   SwordAmount;        // 1
}

public sealed class SwordCollectibleSpawner : IDisposable
{
    private readonly List<SwordCollectible> _active = new(16);
    private readonly List<SwordCollectible> _sweep  = new(8);

    private readonly Pool<SwordCollectible>        _pool;
    private readonly SpawnMap                      _map;
    private readonly SwordCollectibleSpawnSettings _settings;

    private float _timer;

    // ================= CREATE =================
    public SwordCollectibleSpawner(SwordCollectibleView prefab, InteractionResolver resolver,
                                   SpawnMap map, SwordCollectibleSpawnSettings settings,
                                   int prewarm)
    {
        _map      = map;
        _settings = settings;

        _pool = new Pool<SwordCollectible>(
            create: () =>
            {
                SwordCollectibleView view = UnityEngine.Object.Instantiate(prefab);
                view.gameObject.SetActive(false);
                return new SwordCollectible(view, resolver);
            },
            destroy: collectible => UnityEngine.Object.Destroy(collectible.View.gameObject),
            prewarm: prewarm);
    }

    // ================= INITIALIZE =================
    public void Initialize()
    {
        _timer = 0f;
        _active.Clear();
    }

    /// <summary>Fizik değil — Update'te sürülür.</summary>
    public void Update(float deltaTime)
    {
        SweepConsumed(deltaTime);

        _timer -= deltaTime;
        if (_timer > 0f) return;

        _timer = _settings.SpawnInterval;
        if (CountAvailable() >= _settings.MaxActive) return;

        Spawn();
    }

    public SwordCollectible Spawn()
    {
        SwordCollectible collectible = _pool.Rent();
        collectible.Initialize(_map.Next(SpawnCategory.Collectible), _settings.SwordAmount);

        _active.Add(collectible);
        return collectible;
    }

    /// <summary>Uçmakta olanlar "sahada" sayılmaz — limit onları saymaz.</summary>
    private int CountAvailable()
    {
        int n = 0;
        for (int i = 0; i < _active.Count; i++)
            if (!_active[i].IsConsumed) n++;

        return n;
    }

    private void SweepConsumed(float deltaTime)
    {
        _sweep.Clear();

        for (int i = 0; i < _active.Count; i++)
        {
            _active[i].TickConsume(deltaTime);
            if (_active[i].IsConsumeFinished) _sweep.Add(_active[i]);
        }

        for (int i = 0; i < _sweep.Count; i++)
            Recycle(_sweep[i]);
    }

    private void Recycle(SwordCollectible collectible)
    {
        _active.Remove(collectible);
        collectible.Deinitialize();
        _pool.Return(collectible);
    }

    /// <summary>Menzilde collectible yoksa null. Uçmakta olanlar hedef değil.</summary>
    public SwordCollectible FindNearest(Vector2 from, float maxRadius)
    {
        SwordCollectible best = null;
        float bestSqr = maxRadius * maxRadius;

        for (int i = 0; i < _active.Count; i++)
        {
            SwordCollectible candidate = _active[i];
            if (candidate.IsConsumed) continue;

            float sqr = (candidate.Position - from).sqrMagnitude;
            if (sqr >= bestSqr) continue;

            bestSqr = sqr;
            best    = candidate;
        }

        return best;
    }

    // ================= DEINITIALIZE =================
    public void Deinitialize()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            _active[i].Deinitialize();
            _pool.Return(_active[i]);
        }

        _active.Clear();
        _sweep.Clear();
        _timer = 0f;
    }

    // ================= DISPOSE =================
    public void Dispose() => _pool.Dispose();
}
```

> **`CountAvailable()` neden gerekli:** uçmakta olan collectible hâlâ `_active`
> listesinde. Ham `_active.Count` kullanırsan limit dolu görünür ve
> yeni collectible doğmaz.

---

## B.5 `Collectibles/SwordPickupRule.cs`

Uçuş hedefi ve süre geçiriliyor.

```csharp
using UnityEngine;

public sealed class SwordPickupRule : IInteractionRule
{
    private readonly IGameFeedback  _feedback;
    private readonly FeedbackConfig _config;

    public SwordPickupRule(IGameFeedback feedback, FeedbackConfig config)
    {
        _feedback = feedback;
        _config   = config;
    }

    public bool TryApply(in InteractionReport report)
    {
        if (report.Source is not SwordCollectible collectible) return false;
        if (report.Target is not Character character)          return false;

        if (collectible.IsConsumed) return false;
        if (!character.IsAlive)     return false;

        int before = character.Stats.SwordCount;
        int after  = Mathf.Min(before + collectible.SwordAmount, character.Stats.MaxSwordCount);

        // Ring dolu → collectible TÜKETİLMEZ, sahada kalır, başkası alabilir
        if (after == before) return false;

        // Kural HÂLÂ sadece bir sayı artırıyor.
        // Ring'i, Sword'ü, Pool'u tanımıyor — spiral giriş SyncCount'tan bedava geliyor.
        character.Stats.SwordCount = after;

        collectible.Consume(character.Position,
                            _config.CollectFlyDuration,
                            _config.CollectEndScale);

        _feedback.Collected(report.Point);
        return true;
    }
}
```

---

## B.6 `Core/GameManager.cs` — kural kaydı

Faz 4'te eklenecek satır artık iki parametreli:

```csharp
_resolver.AddRule(new SwordPickupRule(_feedback, _feedbackConfig));
```

Geri kalan `GameManager` değişikliği orijinal Faz 4 planındaki gibi
(`_collectibles` alanı, `Compose`/`Initialize`/`Deinitialize`/`Dispose`/`Update`).

---

## B.7 Bölüm B Kabul Testleri

| Test | Beklenen |
|---|---|
| **Algı** | Baloncuk karaktere uçuyor, kılıç dışarıdan spiral çizip yerine oturuyor |
| **Sıra** | Baloncuk kılıçtan **önce** varıyor (`CollectFlyDuration < RingEntryTime`) |
| **Boşluk** | Yeni kılıç en geniş aralığa giriyor, mevcut kılıçlar az kayıyor |
| **Çift pickup yok** | Uçarken ikinci karakter aynı baloncuğu alamıyor |
| **Limit** | Uçmakta olanlar limiti doldurmuyor, yeni collectible doğuyor |
| **AI hedeflemesi** | AI uçmakta olan collectible'ı hedeflemiyor |
| Ring dolu | `MaxSwordCount`'ta collectible sahada kalıyor, uçmuyor |
| **Restart (uçarken)** | Baloncuk uçarken R → anında havuza dönüyor, ekranda kalmıyor |
| Allocation | 10 kez R → 0 `Instantiate`, 0 `Destroy` |

---

## Tuzaklar

| Tuzak | Belirti | Çözüm |
|---|---|---|
| Her kare `ReassignSlots()` | Kılıçlar salınıyor, hedefler takas oluyor | Sadece sayı değişince |
| `SnapAll()` unutulmuş | Round başında kılıçlar spiral çizerek geliyor | `Initialize` sonunda çağır |
| `_slots[i] = slot` unutulmuş | Struct kopyası değişiyor, ekranda hiçbir şey olmuyor | Geri yaz |
| `Sort` içinde lambda | Her çağrıda delegate allocation | `static readonly Comparison<T>` |
| Dünya uzayında Lerp | Karakter hareket edince kılıçlar geride kalıyor | Polar-local: `center` canlı, `offset` yumuşak |
| `CountAvailable` yerine `_active.Count` | Uçanlar limiti dolduruyor, spawn duruyor | Tüketilmemişleri say |
| `CollectFlyDuration > RingEntryTime` | Kılıç önce beliriyor, algı bozuluyor | Uçuş daha kısa olmalı |
| `ResetVisual` unutulmuş | Havuzdan gelen baloncuk küçük/şeffaf | `Initialize` ve `Deinitialize`'da |

---

## Faz 4 Uygulama Sırası (özet)

1. Orijinal plandaki Faz 4 dosyalarını oluştur
   (`SwordCollectible`, `SwordCollectibleView`, `SwordCollectibleSpawner`, `SwordPickupRule`)
2. **Bu dokümanın B.1–B.5'iyle değiştir**
3. `GameManager`'a spawner + kural ekle (B.6)
4. `CollectiblePoints` altına 8–12 boş Transform
5. Layer matrisine `Collectible` satırını ekle (`Sword ↔ Collectible` **kapalı**)
6. B.7 kabul testleri
