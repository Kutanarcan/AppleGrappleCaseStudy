# Faz 3.5 — Extra (Feedback + Kılıç İptali)

**Faz 3 bitti, Faz 4'e geçmeden önce.** Ana plandan ayrı bir ek.

Bu doküman kendi kendine yeter — oluşturulan ve düzenlenen her dosya tam koduyla var.

**Bağımlılık:** DOTween (Demigiant). Package Manager veya Asset Store'dan kur,
`Tools → Demigiant → DOTween Utility Panel → Setup DOTween` çalıştır.

---

## 0. Kapsam

| # | Konu | Tür |
|---|---|---|
| 1 | AudioManager — tek `AudioSource`, `PlaySFX` | Sunum |
| 2 | CharacterFlash — hasar alınca renk atması | Sunum |
| 3 | ParticleManager — havuzlu, merkezi partikül | Sunum |
| 4 | **Kılıç iptali + savrulma** | **Oyun mantığı** |

**Yeni: 7 dosya. Düzenlenen: 10 dosya.**

### 0.1 Merkezi karar: tek cephe

Üç sunum sistemi de combat kurallarından tetikleniyor. Kurallara üç ayrı referans
vermek yerine tek bir arayüz:

```
IGameFeedback
  ├─ SwordClash(point)        → clash sesi + clash partikülü
  ├─ CharacterHit(char, pt)   → hit sesi + kan partikülü
  └─ Collected(point)         → collect sesi
```

Kural tek satır yazar. "Hangi ses, hangi partikül" kararı kuralın değil,
feedback'in işi.

### 0.2 Nötrleme kaldırılıyor

Kılıçlar artık geçici devre dışı kalmıyor, **kalıcı olarak iptal oluyor**.
Dolayısıyla şunlar **siliniyor**, kabuk bırakılmıyor:

- `SwordState.Neutralized` → `SwordState.Detached`
- `Sword._recoveryTimer` mantığı → `_detachTimer`
- `Sword.Neutralize()` → `BeginDetach()`
- `CharacterStats.NeutralizeDuration` → **silindi**
- `CharacterDefinition.NeutralizeDuration` → **silindi**
- `SwordView.SetNeutralized()` → `SetDetached()`
- `SwordRingAbility.FixedUpdate` içindeki `_swords[i].FixedUpdate(dt)` → **silindi**
  (aktif kılıcın artık tick'lenecek durumu yok)

Oyun döngüsü de netleşiyor: savaşta kılıç kaybediyorsun, collectible ile topluyorsun.
Faz 4'ün varlık sebebi buradan geliyor.

### 0.3 DOTween disiplini — üç kural

Havuzlanmış nesnelerde DOTween'in varsayılanları **yanlış** çalışır:

| Kural | Neden |
|---|---|
| `SetLink(go, LinkBehaviour.KillOnDisable)` | Varsayılan `KillOnDestroy`. Bizim nesneler yok edilmiyor, `SetActive(false)` oluyor — varsayılan link hiç tetiklenmez. |
| Handle sakla, `Deinitialize`'da `Kill()` | Link ikinci savunma hattı. Asıl temizlik bizim yaşam döngümüzde. |
| `Kill(complete: false)` | `OnComplete` tetiklenmesin. Havuza iadeyi tween değil, ability'nin sweep'i yapıyor. |

---

## 1. Yeni Dosyalar

Klasör: `Scripts/Feedback/` ve `Scripts/Effects/`

---

### `Feedback/ParticleId.cs`

```csharp
public enum ParticleId
{
    BloodSplash,
    SwordClash
}
```

---

### `Feedback/IGameFeedback.cs`

```csharp
using UnityEngine;

/// <summary>
/// Combat kurallarının sunuma açılan TEK kapısı.
/// Kurallar ses/partikül/flash'ı ayrı ayrı tanımaz.
/// </summary>
public interface IGameFeedback
{
    void SwordClash(Vector2 point);
    void CharacterHit(Character target, Vector2 point);
    void Collected(Vector2 point);
}
```

---

### `Feedback/FeedbackConfig.cs`

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

    private void OnValidate()
    {
        SwordThrowDuration = Mathf.Max(0.05f, SwordThrowDuration);
        SwordThrowDistance = Mathf.Max(0f, SwordThrowDistance);
        ParticlePrewarm    = Mathf.Max(0, ParticlePrewarm);
    }
}
```

---

### `Feedback/AudioManager.cs`

```csharp
using UnityEngine;

/// <summary>
/// Tek AudioSource, PlayOneShot. Case study için yeterli.
/// Tick almıyor — çağır ve unut.
/// </summary>
public sealed class AudioManager
{
    private readonly AudioSource _source;

    public AudioManager(AudioSource source) => _source = source;

    public void Initialize() => Stop();

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || _source == null) return;
        _source.PlayOneShot(clip, volume);
    }

    public void Deinitialize() => Stop();

    private void Stop()
    {
        if (_source != null) _source.Stop();
    }
}
```

---

### `Feedback/ParticleView.cs`

```csharp
using UnityEngine;

/// <summary>
/// Dummy MonoBehaviour. Prefab ayarları:
///   Play On Awake = false, Looping = false, Stop Action = None
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class ParticleView : MonoBehaviour
{
    [SerializeField] private ParticleSystem _system;

    /// <summary>Efektin toplam ömrü — havuza ne zaman döneceğini belirler.</summary>
    public float Duration { get; private set; }

    private void Reset() => _system = GetComponent<ParticleSystem>();

    private void Awake()
    {
        if (_system == null) _system = GetComponent<ParticleSystem>();
        CacheDuration();
    }

    private void CacheDuration()
    {
        ParticleSystem.MainModule main = _system.main;
        Duration = main.duration + main.startLifetime.constantMax;
    }

    public void Play(Vector2 position, float rotationDeg)
    {
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);

        gameObject.SetActive(true);
        _system.Clear(true);
        _system.Play(true);
    }

    public void StopAndClear()
    {
        _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _system.Clear(true);
        gameObject.SetActive(false);
    }
}
```

---

### `Feedback/ParticleManager.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prefab başına bir havuz. Süresi dolan partikülü sweep ile iade eder —
/// SwordCollectibleSpawner.SweepConsumed() ile aynı desen.
///
/// OnParticleSystemStopped callback'i yerine sayaç kullanıyoruz:
/// deterministik, reset'te temiz, tick disiplinine uyuyor.
/// </summary>
public sealed class ParticleManager : IDisposable
{
    private sealed class Entry
    {
        public ParticleView View;
        public ParticleId   Id;
        public float        Remaining;
    }

    private readonly Dictionary<ParticleId, Pool<ParticleView>> _pools = new(2);
    private readonly List<Entry> _active = new(32);
    private readonly List<Entry> _sweep  = new(8);

    // ================= CREATE =================
    public ParticleManager(FeedbackConfig config)
    {
        AddPool(ParticleId.BloodSplash, config.BloodSplashPrefab, config.ParticlePrewarm);
        AddPool(ParticleId.SwordClash,  config.SwordClashPrefab,  config.ParticlePrewarm);
    }

    private void AddPool(ParticleId id, ParticleView prefab, int prewarm)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"ParticleManager: '{id}' prefab'ı atanmamış.");
            return;
        }

        _pools[id] = new Pool<ParticleView>(
            create: () =>
            {
                ParticleView view = UnityEngine.Object.Instantiate(prefab);
                view.gameObject.SetActive(false);
                return view;
            },
            destroy: view => UnityEngine.Object.Destroy(view.gameObject),
            prewarm: prewarm);
    }

    // ================= INITIALIZE =================
    public void Initialize() => _active.Clear();

    public void Play(ParticleId id, Vector2 position, float rotationDeg = 0f)
    {
        if (!_pools.TryGetValue(id, out Pool<ParticleView> pool)) return;

        ParticleView view = pool.Rent();
        view.Play(position, rotationDeg);

        _active.Add(new Entry { View = view, Id = id, Remaining = view.Duration });
    }

    public void Update(float deltaTime)
    {
        if (_active.Count == 0) return;

        _sweep.Clear();

        for (int i = 0; i < _active.Count; i++)
        {
            _active[i].Remaining -= deltaTime;
            if (_active[i].Remaining <= 0f) _sweep.Add(_active[i]);
        }

        for (int i = 0; i < _sweep.Count; i++)
            Recycle(_sweep[i]);
    }

    private void Recycle(Entry entry)
    {
        _active.Remove(entry);
        entry.View.StopAndClear();
        _pools[entry.Id].Return(entry.View);
    }

    // ================= DEINITIALIZE =================
    // Bu olmazsa restart sonrası ekranda eski kan lekeleri kalır.
    public void Deinitialize()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            Recycle(_active[i]);

        _active.Clear();
        _sweep.Clear();
    }

    // ================= DISPOSE =================
    public void Dispose()
    {
        foreach (Pool<ParticleView> pool in _pools.Values)
            pool.Dispose();

        _pools.Clear();
    }
}
```

---

### `Feedback/GameFeedback.cs`

```csharp
using UnityEngine;

public sealed class GameFeedback : IGameFeedback
{
    private readonly AudioManager    _audio;
    private readonly ParticleManager _particles;
    private readonly FeedbackConfig  _config;

    public GameFeedback(AudioManager audio, ParticleManager particles, FeedbackConfig config)
    {
        _audio     = audio;
        _particles = particles;
        _config    = config;
    }

    public void SwordClash(Vector2 point)
    {
        _audio.PlaySFX(_config.SwordClashClip, _config.SwordClashVolume);
        _particles.Play(ParticleId.SwordClash, point);
    }

    public void CharacterHit(Character target, Vector2 point)
    {
        if (target == null) return;

        _audio.PlaySFX(target.Definition.HitClip, target.Definition.HitVolume);

        // Kan sıçraması vuruş yönüne baksın:
        // InteractionReport'a Normal eklemeye gerek yok, karakterden dışa doğru hesapla.
        Vector2 outward = point - target.Position;
        float angle = outward.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg
            : 0f;

        _particles.Play(ParticleId.BloodSplash, point, angle);
    }

    public void Collected(Vector2 point)
        => _audio.PlaySFX(_config.CollectClip, _config.CollectVolume);
}
```

---

### `Effects/FlashEffect.cs`

```csharp
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Hasar alınca renk atması. Plain C# — view dummy kalıyor.
///
/// Roster kullandığımız için reset ZORUNLU: karakter flash ortasında ölürse
/// ve tint sıfırlanmazsa retry'da kırmızı doğar.
/// </summary>
public sealed class FlashEffect
{
    private readonly SpriteRenderer _sprite;
    private readonly Color _baseColor;

    private Tween _tween;

    public FlashEffect(SpriteRenderer sprite)
    {
        _sprite    = sprite;
        _baseColor = sprite != null ? sprite.color : Color.white;
    }

    public void Play(Color flashColor, float duration)
    {
        if (_sprite == null) return;

        Kill();

        _tween = _sprite
            .DOColor(flashColor, duration * 0.5f)
            .SetLoops(2, LoopType.Yoyo)
            .SetLink(_sprite.gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(RestoreColor);
    }

    /// <summary>Initialize ve Deinitialize'da çağrılır.</summary>
    public void Reset()
    {
        Kill();
        RestoreColor();
    }

    private void Kill()
    {
        if (_tween == null) return;

        Tween t = _tween;
        _tween = null;                 // OnKill → RestoreColor tekrar Kill'e girmesin
        t.Kill(false);                 // complete: false → OnComplete tetiklenmez
    }

    private void RestoreColor()
    {
        if (_sprite != null) _sprite.color = _baseColor;
    }
}
```

---

## 2. Düzenlenen Dosyalar

Aşağıdakiler **tam yeni hâlleridir** — dosyayı bu içerikle değiştir.

---

### `Character/CharacterStats.cs`

`NeutralizeDuration` **silindi**.

```csharp
public sealed class CharacterStats
{
    public float MoveSpeed;
    public float Acceleration;
    public float Deceleration;
    public float MaxHealth;

    public int   SwordCount;
    public int   MaxSwordCount;
    public float OrbitRadius;
    public float OrbitAngularSpeed;      // derece / saniye
    public float SwordDamage;

    /// <summary>
    /// INITIALIZE fazı. Yeni nesne ÜRETMİYOR — alanların üzerine yazıyor.
    /// Retry'da allocation olmaması için gerekli. SO'dan okur, SO'ya asla yazmaz.
    /// </summary>
    public void ResetFrom(CharacterDefinition def)
    {
        MoveSpeed         = def.MoveSpeed;
        Acceleration      = def.Acceleration;
        Deceleration      = def.Deceleration;
        MaxHealth         = def.MaxHealth;

        SwordCount        = def.SwordCount;
        MaxSwordCount     = def.MaxSwordCount;
        OrbitRadius       = def.OrbitRadius;
        OrbitAngularSpeed = def.OrbitAngularSpeed;
        SwordDamage       = def.SwordDamage;
    }
}
```

---

### `Character/CharacterDefinition.cs`

`NeutralizeDuration` silindi, ses ve flash eklendi.

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
        AggroExitRadius      = Mathf.Max(AggroExitRadius, AggroEnterRadius);
        SwordAdvantageMargin = Mathf.Max(1, SwordAdvantageMargin);
    }
}
```

> **Not:** `PlayerHit` ve `EnemyHit` kodda ayrı kavram değil — ikisi de
> `HitClip`. Fark sadece hangi asset'in atandığı. Player/Enemy ayrımı
> koddan tamamen çıkıyor.

---

### `Character/CharacterView.cs`

Flash'ın erişebilmesi için `Sprite` açıldı.

```csharp
using UnityEngine;

/// <summary>
/// Trigger mesajı YÖNLENDİRMİYOR. Tespit kaynak tarafında yapılıyor:
/// kılıç ve collectible raporluyor, karakter sadece attachedRigidbody
/// üzerinden çözülüyor.
/// </summary>
public sealed class CharacterView : InteractionBody
{
    [SerializeField] private Rigidbody2D    _body;
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private Animator       _animator;

    public Rigidbody2D    Body     => _body;
    public SpriteRenderer Sprite   => _sprite;
    public Animator       Animator => _animator;

    public void SetTint(Color color)
    {
        if (_sprite != null) _sprite.color = color;
    }

    private void Reset()
    {
        _body   = GetComponent<Rigidbody2D>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
    }
}
```

---

### `Character/Character.cs`

`_feedback` + `FlashEffect` + `Definition` property eklendi.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class Character : ICombatant, IDisposable
{
    private readonly List<IAbility>      _abilities = new(2);
    private readonly CharacterView       _view;
    private readonly CharacterDefinition _definition;
    private readonly InteractionResolver _resolver;
    private readonly IGameFeedback       _feedback;
    private readonly FlashEffect         _flash;

    private IDirectionProvider _directionProvider;
    private float _health;

    public IInteractionEntity Root => this;      // kökü kendisi

    /// <summary>Sahada mı? Roster'da bekleyen ölü karakterler için false.</summary>
    public bool IsSpawned { get; private set; }
    public bool IsAlive => IsSpawned && _health > 0f;

    public CharacterView       View       => _view;
    public CharacterDefinition Definition => _definition;
    public CharacterStats      Stats      { get; }
    public MovementSimulator   Movement   { get; }

    public Vector2 Position => Movement.Position;

    public event Action<Character> Died;

    // ================= CREATE =================
    public Character(CharacterView view, CharacterDefinition definition,
                     InteractionResolver resolver, IGameFeedback feedback)
    {
        _view       = view;
        _definition = definition;
        _resolver   = resolver;
        _feedback   = feedback;
        _flash      = new FlashEffect(view.Sprite);

        Stats    = new CharacterStats();
        Movement = new MovementSimulator(view.Body, Stats);

        _directionProvider = NullDirectionProvider.Instance;
        _view.gameObject.SetActive(false);
    }

    /// <summary>CREATE fazı — provider ve ability'ler bir kez takılır.</summary>
    public void SetDirectionProvider(IDirectionProvider provider)
        => _directionProvider = provider ?? NullDirectionProvider.Instance;

    public void AddAbility(IAbility ability) => _abilities.Add(ability);

    // ================= INITIALIZE =================
    public void Initialize(Vector2 position)
    {
        if (IsSpawned) return;

        Stats.ResetFrom(_definition);
        _health = Stats.MaxHealth;

        // transform önce, sonra aktifleştir, sonra body —
        // aktif objeye doğrudan Body.position yazmak interpolasyon lekesi bırakır.
        _view.transform.position = position;
        _view.gameObject.SetActive(true);
        _view.Body.position = position;
        _view.Body.rotation = 0f;

        _view.Bind(this, _resolver);
        _flash.Reset();

        Movement.Initialize();
        _directionProvider.Initialize();

        for (int i = 0; i < _abilities.Count; i++)
            _abilities[i].Initialize(this);

        IsSpawned = true;
    }

    // ================= TICK =================
    public void Update(float deltaTime)
    {
        if (!IsAlive) return;

        _directionProvider.Update(deltaTime);          // yön burada HESAPLANIR

        for (int i = 0; i < _abilities.Count; i++)
            _abilities[i].Update(deltaTime);
    }

    public void FixedUpdate(float deltaTime)
    {
        if (!IsAlive) return;

        Movement.SetDirection(_directionProvider.Direction);   // saf okuma
        Movement.FixedUpdate(deltaTime);

        for (int i = 0; i < _abilities.Count; i++)
            _abilities[i].FixedUpdate(deltaTime);              // ring hareketten SONRA
    }

    public void ReceiveDamage(in DamageInfo info)
    {
        if (!IsAlive) return;

        _health = Mathf.Max(0f, _health - info.Amount);

        _flash.Play(_definition.FlashColor, _definition.FlashDuration);
        _feedback.CharacterHit(this, info.Point);

        if (_health > 0f) return;

        Movement.SetDirection(Vector2.zero);
        Died?.Invoke(this);        // registry kuyruğa alır, GameManager süpürür
    }

    // ================= DEINITIALIZE =================
    // Ölünce de round sonunda da aynı yol. Yok etmiyor — deaktive ediyor.
    public void Deinitialize()
    {
        if (!IsSpawned) return;
        IsSpawned = false;

        for (int i = _abilities.Count - 1; i >= 0; i--)
            _abilities[i].Deinitialize();              // kılıçlar havuza döner

        _flash.Reset();                                // tint kırmızı kalmasın
        _directionProvider.Deinitialize();
        Movement.Deinitialize();

        _view.Unbind();
        _view.gameObject.SetActive(false);
    }

    // ================= DISPOSE =================
    public void Dispose()
    {
        Deinitialize();

        _abilities.Clear();
        _directionProvider = null;
        Died = null;

        if (_view != null) UnityEngine.Object.Destroy(_view.gameObject);
    }
}
```

---

### `Abilities/SwordView.cs`

`SetNeutralized` → `SetDetached`, savrulma tween'i eklendi.

```csharp
using DG.Tweening;
using UnityEngine;

public sealed class SwordView : InteractionBody
{
    [SerializeField] private Rigidbody2D    _body;
    [SerializeField] private Collider2D     _hitCollider;
    [SerializeField] private SpriteRenderer _sprite;

    private Sequence _throwSequence;
    private Color    _baseColor;

    public Rigidbody2D Body => _body;

    private void Awake()
    {
        if (_sprite != null) _baseColor = _sprite.color;
    }

    // Sadece Enter — Stay YOK. Kılıç dönüyor: girer, vurur, çıkar, tekrar girer.
    private void OnTriggerEnter2D(Collider2D other) => ReportContact(other);

    /// <summary>
    /// Savrulan kılıç sadece görsel: collider kapalı, fizik simülasyonu kapalı.
    /// simulated=false olmadan transform tween'i rigidbody ile çakışır.
    /// </summary>
    public void SetDetached(bool value)
    {
        if (_hitCollider != null) _hitCollider.enabled = !value;
        if (_body != null) _body.simulated = !value;
    }

    public void PlayThrow(Vector2 direction, float distance, float duration, float spin)
    {
        KillThrow();

        Vector3 target = transform.position + (Vector3)(direction * distance);

        _throwSequence = DOTween.Sequence()
            .Append(transform.DOMove(target, duration).SetEase(Ease.OutQuad))
            .Join(transform.DORotate(new Vector3(0f, 0f, spin), duration, RotateMode.LocalAxisAdd)
                           .SetEase(Ease.Linear))
            // Havuzlanmış nesne: KillOnDestroy işe yaramaz, KillOnDisable şart.
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        if (_sprite != null)
        {
            _throwSequence.Join(_sprite
                .DOFade(0f, duration * 0.45f)
                .SetDelay(duration * 0.55f));
        }
    }

    public void KillThrow()
    {
        if (_throwSequence == null) return;

        Sequence s = _throwSequence;
        _throwSequence = null;
        s.Kill(false);                 // complete: false → OnComplete tetiklenmez
    }

    /// <summary>Havuza dönerken görsel durumu tamamen sıfırla.</summary>
    public void ResetVisual()
    {
        KillThrow();
        transform.rotation = Quaternion.identity;
        if (_sprite != null) _sprite.color = _baseColor;
    }
}
```

---

### `Abilities/Sword.cs`

Nötrleme yerine iptal. `Ring` geri referansı eklendi.

```csharp
using UnityEngine;

public enum SwordState { Active, Detached }

public sealed class Sword : ICombatant
{
    private readonly SwordView           _view;
    private readonly InteractionResolver _resolver;

    private Character _owner;
    private float _detachTimer;

    /// <summary>CREATE fazı — havuzun create fonksiyonu çağırıyor.</summary>
    public Sword(SwordView view, InteractionResolver resolver)
    {
        _view     = view;
        _resolver = resolver;
    }

    public IInteractionEntity Root => _owner;      // kökü sahibi olan karakter

    public SwordState State { get; private set; }
    public SwordView  View  => _view;

    /// <summary>
    /// Hangi halkaya ait? İptal edilirken sayaç yerine HALKA çağrılmalı,
    /// yoksa SyncCount yanlış kılıcı havuza atar.
    /// </summary>
    public SwordRingAbility Ring { get; private set; }

    public bool    IsActive        => State == SwordState.Active;
    public bool    IsDetachFinished => State == SwordState.Detached && _detachTimer <= 0f;
    public Vector2 Position        => _view.Body.position;

    public float Damage => _owner != null ? _owner.Stats.SwordDamage : 0f;

    public void Initialize(Character owner, SwordRingAbility ring)
    {
        _owner = owner;
        Ring   = ring;
        State  = SwordState.Active;
        _detachTimer = 0f;

        _view.gameObject.SetActive(true);
        _view.ResetVisual();
        _view.SetDetached(false);
        _view.Bind(this, _resolver);
    }

    /// <summary>
    /// Kalıcı iptal. Kılıç halkadan çıkar, savrulur, süre sonunda havuza döner.
    /// Collider hemen kapanır → bir daha rapor üretmez.
    /// </summary>
    public void BeginDetach(Vector2 direction, FeedbackConfig config)
    {
        if (State == SwordState.Detached) return;

        State = SwordState.Detached;
        _detachTimer = config.SwordThrowDuration;

        _view.Unbind();                 // artık etkileşime girmiyor
        _view.SetDetached(true);
        _view.PlayThrow(direction,
                        config.SwordThrowDistance,
                        config.SwordThrowDuration,
                        config.SwordThrowSpin);
    }

    public void TickDetach(float deltaTime)
    {
        if (State != SwordState.Detached) return;
        _detachTimer -= deltaTime;
    }

    public void MoveTo(Vector2 position, float angleRad)
    {
        _view.Body.MovePosition(position);
        _view.Body.MoveRotation(angleRad * Mathf.Rad2Deg - 90f);   // sprite yukarı bakıyorsa
    }

    public void Deinitialize()
    {
        _view.Unbind();
        _view.ResetVisual();            // tween kill + rotasyon/alpha sıfır
        _view.SetDetached(false);
        _view.Body.linearVelocity = Vector2.zero;
        _view.gameObject.SetActive(false);

        _owner = null;
        Ring   = null;
        State  = SwordState.Active;
        _detachTimer = 0f;
    }
}
```

---

### `Abilities/SwordRingAbility.cs`

`Detach()` + `_detached` listesi + sweep.

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tek sözleşmesi: "SwordCount stat'ı ne diyorsa o kadar kılıcım olsun."
/// Sayıyı kimin değiştirdiğini bilmiyor — collectible, level-up, iptal,
/// hepsi aynı kapıdan giriyor.
/// </summary>
public sealed class SwordRingAbility : IAbility
{
    private readonly List<Sword> _swords   = new(8);
    private readonly List<Sword> _detached = new(4);
    private readonly Pool<Sword> _pool;
    private readonly FeedbackConfig _config;

    private Character _owner;
    private float _currentAngle;

    public SwordRingAbility(Pool<Sword> pool, FeedbackConfig config)
    {
        _pool   = pool;
        _config = config;
    }

    public int ActiveSwordCount => _swords.Count;

    public void Initialize(Character owner)
    {
        _owner = owner;
        _currentAngle = 0f;
        SyncCount();
    }

    public void Update(float deltaTime) { }

    public void FixedUpdate(float deltaTime)
    {
        SweepDetached(deltaTime);
        SyncCount();                                    // stat değiştiyse yakalar

        if (_swords.Count == 0) return;

        _currentAngle = Mathf.Repeat(
            _currentAngle + _owner.Stats.OrbitAngularSpeed * deltaTime, 360f);

        float radius = _owner.Stats.OrbitRadius;
        float step   = 360f / _swords.Count;

        // KRİTİK: bu fizik adımından SONRAKİ merkez.
        Vector2 center = _owner.Movement.PredictedPosition(deltaTime);

        for (int i = 0; i < _swords.Count; i++)
        {
            float rad = (_currentAngle + i * step) * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

            _swords[i].MoveTo(center + offset, rad);
        }
    }

    /// <summary>
    /// Kılıç iptali. Kural SwordCount'a DEĞİL buraya çağırmalı:
    /// sayacı doğrudan düşürürsen SyncCount listenin SONUNDAKİ kılıcı atar,
    /// çarpışan kılıç ise ring'de kalır.
    /// </summary>
    public void Detach(Sword sword, float verticalSign)
    {
        if (_owner == null) return;
        if (!_swords.Remove(sword)) return;

        _owner.Stats.SwordCount = Mathf.Max(0, _owner.Stats.SwordCount - 1);

        Vector2 outward = sword.Position - _owner.Position;
        if (outward.sqrMagnitude < 0.0001f) outward = Vector2.right;
        outward.Normalize();

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

    /// <summary>
    /// Tek doğruluk kaynağı: açı biriktir, slotu hesapla.
    ///   angle_i = _currentAngle + i * (360 / count)
    /// </summary>
    private void SyncCount()
    {
        int desired = Mathf.Clamp(_owner.Stats.SwordCount, 0, _owner.Stats.MaxSwordCount);

        while (_swords.Count < desired)
        {
            Sword sword = _pool.Rent();               // prewarm sayesinde Instantiate yok
            sword.Initialize(_owner, this);
            _swords.Add(sword);
        }

        while (_swords.Count > desired)
        {
            int last = _swords.Count - 1;
            ReturnSword(_swords[last]);
            _swords.RemoveAt(last);
        }
    }

    private void ReturnSword(Sword sword)
    {
        sword.Deinitialize();
        _pool.Return(sword);
    }

    public void Deinitialize()
    {
        // Uçmakta olan kılıçlar animasyonu beklemeden havuza döner
        for (int i = 0; i < _detached.Count; i++)
            ReturnSword(_detached[i]);
        _detached.Clear();

        for (int i = 0; i < _swords.Count; i++)
            ReturnSword(_swords[i]);
        _swords.Clear();

        _owner = null;
        _currentAngle = 0f;
    }
}
```

---

### `Combat/SwordVsSwordRule.cs`

Nötrleme yerine iptal + feedback.

```csharp
using UnityEngine;

public sealed class SwordVsSwordRule : IInteractionRule
{
    private readonly IGameFeedback _feedback;

    public SwordVsSwordRule(IGameFeedback feedback) => _feedback = feedback;

    public bool TryApply(in InteractionReport report)
    {
        // Kendi kılıcım kendi diğer kılıcıma değemez — resolver Root ile eledi.
        if (report.Source is not Sword a) return false;
        if (report.Target is not Sword b) return false;

        // Bu guard aynı zamanda dedup görevi görüyor: B→A çağrısı geldiğinde
        // ikisi de zaten Detached olduğu için no-op.
        if (!a.IsActive || !b.IsActive) return false;
        if (a.Ring == null || b.Ring == null) return false;

        // Deterministik yön: yukarıdaki kılıç yukarı gider.
        // Rastgele veya "ilk raporlanan yukarı" yapay görünür.
        float aSign = a.Position.y >= b.Position.y ? 1f : -1f;

        a.Ring.Detach(a,  aSign);
        b.Ring.Detach(b, -aSign);

        _feedback.SwordClash((a.Position + b.Position) * 0.5f);
        return true;
    }
}
```

---

### `Collectibles/SwordPickupRule.cs`

Feedback eklendi.

```csharp
using UnityEngine;

public sealed class SwordPickupRule : IInteractionRule
{
    private readonly IGameFeedback _feedback;

    public SwordPickupRule(IGameFeedback feedback) => _feedback = feedback;

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

        character.Stats.SwordCount = after;
        collectible.Consume();

        _feedback.Collected(report.Point);
        return true;
    }
}
```

> `SwordVsCharacterRule` **değişmiyor** — `Character.ReceiveDamage` zaten
> flash ve feedback'i kendi içinde tetikliyor.

---

### `Character/CharacterFactory.cs`

`_feedback` ve `_feedbackConfig` eklendi.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterFactory : IDisposable
{
    private readonly CharacterRegistry       _registry;
    private readonly InteractionResolver     _resolver;
    private readonly SpawnMap                _map;
    private readonly JoystickInput           _joystick;
    private readonly Pool<Sword>             _swordPool;
    private readonly IGameFeedback           _feedback;
    private readonly FeedbackConfig          _feedbackConfig;

    private readonly List<Character> _enemies = new(32);
    private Character _player;

    // ================= CREATE =================
    public CharacterFactory(CharacterRegistry registry, InteractionResolver resolver,
                            SpawnMap map, JoystickInput joystick, Pool<Sword> swordPool,
                            IGameFeedback feedback, FeedbackConfig feedbackConfig,
                            CharacterDefinition playerDefinition,
                            CharacterDefinition enemyDefinition, int enemyCount)
    {
        _registry       = registry;
        _resolver       = resolver;
        _map            = map;
        _joystick       = joystick;
        _swordPool      = swordPool;
        _feedback       = feedback;
        _feedbackConfig = feedbackConfig;

        _player = CreateCharacter(playerDefinition);

        for (int i = 0; i < enemyCount; i++)
            _enemies.Add(CreateCharacter(enemyDefinition));
    }

    private Character CreateCharacter(CharacterDefinition definition)
    {
        CharacterView view = UnityEngine.Object.Instantiate(definition.ViewPrefab);

        var character = new Character(view, definition, _resolver, _feedback);
        character.SetDirectionProvider(CreateProvider(definition, character));

        if (definition.HasSwordRing)
            character.AddAbility(new SwordRingAbility(_swordPool, _feedbackConfig));

        return character;
    }

    private IDirectionProvider CreateProvider(CharacterDefinition definition, Character self)
    {
        switch (definition.BrainType)
        {
            case CharacterBrainType.Player:
                return new JoystickDirectionProvider(_joystick);

            default:
                return NullDirectionProvider.Instance;
        }
    }

    // ================= INITIALIZE =================
    public void Initialize()
    {
        Place(_player, SpawnCategory.Player);

        for (int i = 0; i < _enemies.Count; i++)
            Place(_enemies[i], SpawnCategory.Enemy);
    }

    private void Place(Character character, SpawnCategory category)
    {
        character.Initialize(_map.Next(category));
        _registry.Add(character);
    }

    /// <summary>Ölüm. Yok etmiyor — deaktive ediyor, retry'a kadar bekliyor.</summary>
    public void Despawn(Character character)
    {
        if (!character.IsSpawned) return;

        _registry.Remove(character);
        character.Deinitialize();
    }

    // ================= DEINITIALIZE =================
    public void Deinitialize()
    {
        Despawn(_player);

        for (int i = 0; i < _enemies.Count; i++)
            Despawn(_enemies[i]);
    }

    // ================= DISPOSE =================
    public void Dispose()
    {
        _player?.Dispose();
        _player = null;

        for (int i = 0; i < _enemies.Count; i++)
            _enemies[i].Dispose();

        _enemies.Clear();
    }
}
```

---

### `Core/GameManager.cs`

Feedback sistemleri komposizyona eklendi.

```csharp
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    [Header("Definitions")]
    [SerializeField] private CharacterDefinition _playerDefinition;
    [SerializeField] private CharacterDefinition _enemyDefinition;

    [Header("Prefabs")]
    [SerializeField] private SwordView _swordPrefab;

    [Header("Feedback")]
    [SerializeField] private FeedbackConfig _feedbackConfig;
    [SerializeField] private AudioSource    _audioSource;

    [Header("Scene")]
    [SerializeField] private SpawnMapView  _spawnMapView;
    [SerializeField] private JoystickInput _joystick;
    [SerializeField] private int _enemyCount = 8;
    [SerializeField] private int _spawnSeed  = 12345;

    [Header("Pooling")]
    [SerializeField] private int _swordPrewarm = 48;

    private SpawnMap            _map;
    private CharacterRegistry   _characters;
    private InteractionResolver _resolver;
    private Pool<Sword>         _swordPool;
    private CharacterFactory    _factory;

    private AudioManager    _audio;
    private ParticleManager _particles;
    private GameFeedback    _feedback;

    private void Awake()
    {
        Compose();
        Initialize();
    }

    private void OnDestroy()
    {
        Deinitialize();
        Dispose();
    }

    public void Restart()
    {
        Deinitialize();
        Initialize();
    }

    // ================= CREATE =================
    private void Compose()
    {
        DOTween.Init(recycleAllByDefault: false, useSafeMode: true, LogBehaviour.ErrorsOnly)
               .SetCapacity(tweenersCapacity: 200, sequencesCapacity: 50);

        _map        = new SpawnMap(_spawnMapView, _spawnSeed);
        _characters = new CharacterRegistry();
        _resolver   = new InteractionResolver();

        _audio     = new AudioManager(_audioSource);
        _particles = new ParticleManager(_feedbackConfig);
        _feedback  = new GameFeedback(_audio, _particles, _feedbackConfig);

        _swordPool = new Pool<Sword>(
            create:  CreateSword,
            destroy: sword => Destroy(sword.View.gameObject),
            prewarm: _swordPrewarm);

        _resolver.AddRule(new SwordVsSwordRule(_feedback));
        _resolver.AddRule(new SwordVsCharacterRule());

        _factory = new CharacterFactory(
            _characters, _resolver, _map, _joystick, _swordPool,
            _feedback, _feedbackConfig,
            _playerDefinition, _enemyDefinition, _enemyCount);
    }

    private Sword CreateSword()
    {
        SwordView view = Instantiate(_swordPrefab);
        view.gameObject.SetActive(false);
        return new Sword(view, _resolver);
    }

    // ================= INITIALIZE =================
    public void Initialize()
    {
        _map.Initialize();
        _audio.Initialize();
        _particles.Initialize();
        _factory.Initialize();
    }

    // ================= DEINITIALIZE =================
    public void Deinitialize()
    {
        _factory.Deinitialize();
        _particles.Deinitialize();      // ekranda kan lekesi kalmasın
        _audio.Deinitialize();
        _characters.Deinitialize();
        _map.Deinitialize();
    }

    // ================= DISPOSE =================
    private void Dispose()
    {
        DOTween.KillAll();

        _factory?.Dispose();
        _particles?.Dispose();
        _swordPool?.Dispose();
        _resolver?.Dispose();

        _factory    = null;
        _particles  = null;
        _audio      = null;
        _feedback   = null;
        _swordPool  = null;
        _resolver   = null;
        _characters = null;
        _map        = null;
    }

    // ================= TICK =================
    private void Update()
    {
        DespawnPending();

        float dt = Time.deltaTime;

        _particles.Update(dt);

        IReadOnlyList<Character> active = _characters.Active;
        for (int i = 0; i < active.Count; i++)
            active[i].Update(dt);

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R)) Restart();
#endif
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        IReadOnlyList<Character> active = _characters.Active;
        for (int i = 0; i < active.Count; i++)
            active[i].FixedUpdate(dt);
    }

    private void DespawnPending()
    {
        IReadOnlyList<Character> pending = _characters.PendingRemoval;
        if (pending.Count == 0) return;

        for (int i = 0; i < pending.Count; i++)
            _factory.Despawn(pending[i]);

        _characters.ClearPending();
    }
}
```

> **Faz 4'te ekleneceği yer:** `SwordCollectibleSpawner` ve
> `_resolver.AddRule(new SwordPickupRule(_feedback))`.

---

## 3. Unity Kurulumu

### 3.1 Sahne

```
GameManager       → GameManager.cs
  ├─ FeedbackConfig  (SO referansı)
  └─ AudioSource     (aşağıdaki objeye referans)
SFX               → AudioSource   (Play On Awake = false, Loop = false)
SpawnMap          → SpawnMapView.cs
Canvas/Joystick   → JoystickInput.cs
```

### 3.2 Kılıç prefab ayarları — ELLE

`SwordView.Reset()` kaldırıldı, bu ayarlar artık otomatik yazılmıyor.
Zaten `Reset()` yalnızca component **ilk eklendiğinde** çalışıyordu; var olan
bir prefab'da hiç tetiklenmiyordu — yani yanlış güven veriyordu.

| Component | Ayar | Değer |
|---|---|---|
| Rigidbody2D | Body Type | **Kinematic** |
| Rigidbody2D | Interpolate | **Interpolate** |
| Rigidbody2D | Use Full Kinematic Contacts | **✔ (şart)** |
| Collider2D | Is Trigger | **✔** |
| Collider2D | Boyut | Sprite'tan biraz geniş (tünelleme) |
| GameObject | Layer | `Sword` |

`SwordView` alanları (`_body`, `_hitCollider`, `_sprite`) inspector'dan elle atanır.

> `Use Full Kinematic Contacts` kapalıysa Sword×Sword trigger'ı **hiç**
> tetiklenmez — kılıçlar birbirinden geçer, iptal mekaniği hiç çalışmaz.

### 3.3 Partikül prefab ayarları

Her iki prefab (`BloodSplash`, `SwordClash`) için:

| Ayar | Değer | Neden |
|---|---|---|
| Play On Awake | **false** | Havuzda beklerken oynamasın |
| Looping | **false** | Süre hesabı anlamsızlaşır |
| Stop Action | **None** | Kendini yok etmesin, havuz yönetiyor |
| Sorting Layer | Karakterlerin üstü | Görünsün |

Prefab'a `ParticleView` component'i ekle.

### 3.4 Ses asset'leri

| Klip | Nereye |
|---|---|
| SwordClash | `FeedbackConfig.SwordClashClip` |
| Collect | `FeedbackConfig.CollectClip` |
| PlayerHit | Player `CharacterDefinition.HitClip` |
| EnemyHit | Enemy `CharacterDefinition.HitClip` |

Import: kısa SFX oldukları için `Load Type: Decompress on Load`,
`Compression Format: PCM` veya `ADPCM` — gecikmesiz çalar.

### 3.5 DOTween

Package Manager veya Asset Store → kur →
`Tools → Demigiant → DOTween Utility Panel → Setup DOTween`.

---

## 4. Kabul Testleri

### 4.1 Ses

| Test | Beklenen |
|---|---|
| Clash sesi | İki kılıç çarpışınca çalıyor |
| Hit sesi | Player ve düşman farklı klip çalıyor |
| Restart | R → çalan ses kesiliyor |

### 4.2 Flash

| Test | Beklenen |
|---|---|
| Hasar | Vurulan karakter renk atıp geri dönüyor |
| **Flash ortasında ölüm** | Karakter deaktif oluyor, retry'da **normal renkte** doğuyor |
| Üst üste hasar | İkinci vuruş flash'ı baştan başlatıyor, renk takılı kalmıyor |

### 4.3 Partikül

| Test | Beklenen |
|---|---|
| Clash | Çarpışma noktasında oynuyor |
| Kan | Vuruş noktasında, karakterden dışa doğru |
| Havuz | Play'e bas → Hierarchy'de prewarm kadar deaktif partikül |
| Sweep | Efekt bitince otomatik deaktif oluyor |
| **Restart** | R → ekranda **eski partikül kalmıyor** |
| Allocation | 10 kez R → 0 `Instantiate` |

### 4.4 Kılıç iptali

| Test | Beklenen |
|---|---|
| **Doğru kılıç gidiyor** | Çarpışan kılıç savruluyor, ring'deki başka kılıç değil |
| Sayaç | İki tarafın da `SwordCount`'u 1 azalıyor |
| Yeniden dizilim | Kalan kılıçlar eşit aralıklı yeniden diziliyor |
| Yön | Biri yukarı biri aşağı, dönerek uzaklaşıyor |
| Süre | ~1 saniyede kayboluyor |
| **Çarpışmıyor** | Savrulan kılıç kimseye hasar vermiyor, tetiklemiyor |
| Sıfırlanma | `SwordCount = 0` olunca hata yok, ring boş |
| **Restart (uçarken)** | Kılıç uçarken R → anında havuza dönüyor, ekranda kalmıyor |
| Allocation | 10 kez R → 0 `Instantiate` |

### 4.5 Regresyon (her fazda olduğu gibi)

| Test | Nasıl |
|---|---|
| Retry allocation | Profiler açık, 10 kez R → 0 `Instantiate`, 0 `Destroy` |
| Retry düzeni | Karakterler aynı noktalarda |
| Sızıntı | 20 kez R → Hierarchy obje sayısı sabit, `Pool.IdleCount` sabit |
| Grep | `Initialize()` gövdelerinde `Instantiate` ara → sıfır sonuç |
| **Nötrleme kalıntısı** | Projede `Neutralize` ara → **sıfır sonuç** |

---

## 5. Tuzaklar

| Tuzak | Belirti | Çözüm |
|---|---|---|
| `SetLink(go)` varsayılanı | Havuzdan gelen nesnede eski tween devam ediyor | `LinkBehaviour.KillOnDisable` |
| Tween handle saklanmamış | Restart sonrası hayalet animasyon | Handle sakla, `Deinitialize`'da `Kill(false)` |
| `Kill(true)` kullanmak | `OnComplete` tetikleniyor, çift havuz iadesi | `Kill(false)` |
| Rigidbody varken transform tween | Fizik/transform çakışması, uyarı | `rb.simulated = false` |
| Sayacı doğrudan düşürmek | **Yanlış kılıç kayboluyor** | `Ring.Detach(sword, sign)` |
| `Unbind` unutmak | Savrulan kılıç hâlâ hasar veriyor | `BeginDetach` içinde `Unbind` |
| Partikül `Stop Action = Destroy` | Havuzdaki nesne yok oluyor | `None` |
| Partikül `Play On Awake` | Havuzda beklerken oynuyor | `false` |
| Kılıç prefab ayarı eksik | Kılıçlar birbirinden geçiyor | §3.2 tablosunu elle uygula |
| `_sprite.color` sıfırlanmamış | Retry'da kırmızı/şeffaf karakter | `FlashEffect.Reset()`, `SwordView.ResetVisual()` |

---

## 6. Faz 4'e Etkisi

Ana plandaki Faz 4 (Collectible) iki küçük değişiklikle uygulanır:

1. `SwordPickupRule` constructor'ı artık `IGameFeedback` alıyor →
   `_resolver.AddRule(new SwordPickupRule(_feedback));`
2. `SwordCollectibleSpawner` kurulumu `GameManager.Compose`'a eklenir
   (ana plandaki gibi).

Geri kalan her şey aynı. `SwordRingAbility` collectible'ı hâlâ bilmiyor —
`SwordCount` üzerinden buluşuyorlar.
