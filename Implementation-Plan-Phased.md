# 2D RPG — Uygulama Planı (v9 — Fazlar, Kendi Kendine Yeter)

**Bu doküman tek başına yeterlidir.** Başka bir dosyaya referans vermez.
Her scriptin tam kodu, doğduğu fazda yazılıdır.

**Uygulama sırası zorunludur.** Fazlar sırayla yapılır; her fazın kabul testi
geçmeden sonrakine geçilmez.

- **Unity 6 / 2023.3+** varsayılmıştır. Daha eski sürümde `Rigidbody2D.linearVelocity`
  yerine `Rigidbody2D.velocity` kullan.
- Tüm C# sınıfları `namespace` içinde değildir (case study sadeliği).
- MonoBehaviour katmanı **dummy**'dir: referans tutar ve Unity mesajlarını
  yukarı taşır, mantık içermez.

---

## 0. Mimari Sözleşmeler

Bunlar fazlar boyunca **değişmez kurallardır**. Bir faz bunlardan birini
ihlal ediyorsa faz yanlış uygulanmıştır.

### 0.1 Dört fazlı yaşam döngüsü

```
Create   (constructor)   → nesneler doğar, prefab Instantiate edilir, havuzlar prewarm
Initialize()             → oyun durumu kurulur — HİÇBİR ŞEY Instantiate EDİLMEZ
Deinitialize()           → oyun durumu sıfırlanır — nesneler hayatta kalır
Dispose()                → nesneler yok edilir
```

| Olay | Çağrı |
|---|---|
| Level yüklendi | `Compose()` → `Initialize()` |
| Yenildik, tekrar oyna | `Deinitialize()` → `Initialize()` |
| Sahne değişiyor | `Deinitialize()` → `Dispose()` |

**Ölçülebilir kural:** `Deinitialize()` → `Initialize()` arasında
**0 `Instantiate`, 0 `Destroy`**. `Initialize()` gövdelerinde `Instantiate`
kelimesi geçemez.

`Destroy` yerine `Dispose` kullanılır: MonoBehaviour içinde `Destroy` adında bir
instance metodu `UnityEngine.Object.Destroy` statiğini gölgeler.

### 0.2 IDirectionProvider sözleşmesi

Player ile AI arasındaki **tek** fark budur.

- `Direction` **saf okumadır** — hesap yapmaz, yan etkisi yoktur.
  Aynı adımda iki kez okumak aynı değeri verir.
- Hesap yalnızca `Update()` içinde yapılır.
- `magnitude ∈ [0, 1]` — `normalized` **değil**, `ClampMagnitude`.
  Yarım itilen joystick yarım hızda gitmeli.
- Deadzone provider'ın içindedir; `MovementSimulator` deadzone bilmez.
- Yön **dünya uzayındadır**.
- AI yavaş yürüsün isteniyorsa `dir * 0.5f` döndürülür — simulator'a dokunulmaz.

### 0.3 Update / FixedUpdate ayrımı

| `Update` (frame rate) | `FixedUpdate` (fizik adımı) |
|---|---|
| Ölenleri süpür | `MovementSimulator` → `linearVelocity` |
| Joystick örnekleme | Ring açı birikimi + `MovePosition` |
| AI karar / repath | Sword nötrleme timer'ı |
| Collectible spawn + süpürme | Ability fizik etkileri |

Unity'nin frame içi sırası **FixedUpdate → Update → LateUpdate**.
`Update`'te örneklenen input **bir sonraki** frame'in `FixedUpdate`'inde tüketilir —
bir frame'lik gecikme kaçınılmazdır.

Bir frame'de 0 veya 2+ `FixedUpdate` çalışabilir. 2 çalıştığında aynı `Direction`
iki kez tüketilir — doğru davranıştır, çünkü yön bir *seviye*, bir olay değildir.

### 0.4 Katman ayrımı

```
Interaction katmanı              Combat katmanı
────────────────────             ─────────────────────
IInteractionEntity               ICombatant : IInteractionEntity
InteractionBody                  DamageInfo
InteractionReport                SwordVsSwordRule
IInteractionRule                 SwordVsCharacterRule
InteractionResolver
  → sadece Root self-check
```

Bağımlılık **tek yönlüdür**: Combat → Interaction, Collectibles → Interaction.
Combat ile Collectibles birbirini tanımaz.

`Interaction/` klasöründe `Damage`, `Sword`, `Character` kelimeleri geçemez.

### 0.5 Kimlik: Root

```csharp
IInteractionEntity Root { get; }
```

Kılıçta sahibi olan karakter; karakterde ve collectible'da kendisi.
**Zincirin en tepesi döner, bir üst halka değil.**

Resolver tek bir `ReferenceEquals(source.Root, target.Root)` yapar — bu ancak
herkes en tepeyi döndürürse doğru çalışır. Böylece int kimliğe ve kimlik
üretecine gerek kalmaz.

Oyun **free-for-all**: takım kavramı yoktur, herkes herkese saldırabilir.
Kendi kılıcından zarar görmemeyi `Root` karşılaştırması sağlar.

### 0.6 Sistem ayrımı: Sword ↔ Collectible

```
SwordCollectible ──(SwordPickupRule)──▶ CharacterStats.SwordCount
                                                  ▲
                                                  │ (SyncCount — poll)
                                        SwordRingAbility
```

İki sistem arasında **doğrudan referans yoktur.** Kılıç sisteminin tek sözleşmesi:
*"`SwordCount` stat'ı ne diyorsa o kadar kılıcım olsun."*

`SwordRingAbility`, `Sword`, `SwordView` dosyalarında `Collectible` kelimesi geçemez.

### 0.7 Fizik kurulumu

| Nesne | Body Type | Collider | Interpolate | Layer |
|---|---|---|---|---|
| Character | Dynamic | `isTrigger = false` | ✔ | `Character` |
| Sword | Kinematic | `isTrigger = true` | ✔ | `Sword` |
| Collectible | Kinematic | `isTrigger = true` | ✘ | `Collectible` |

Karakterde ayrıca `gravityScale = 0`, `freezeRotation = true`
(kodda `MovementSimulator` constructor'ı ayarlar).

**Layer Collision Matrix** (Faz 4 sonunda tam hâli):

|                 | Character | Sword | Collectible | Environment |
|-----------------|:---------:|:-----:|:-----------:|:-----------:|
| **Character**   |     ✔     |   ✔   |      ✔      |      ✔      |
| **Sword**       |     ✔     |   ✔   |      ✘      |      ✘      |
| **Collectible** |     ✔     |   ✘   |      ✘      |      ✘      |
| **Environment** |     ✔     |   ✘   |      ✘      |      —      |

`Physics2D.IgnoreCollision` **kullanılmaz** — N×M kurulum gerektirir ve havuzda
bayat referans bırakır.

### 0.8 Fizik tuzakları

| Tuzak | Belirti | Çözüm |
|---|---|---|
| `useFullKinematicContacts = false` | Sword×Sword trigger'ı **hiç** tetiklenmez | `true` |
| Rigidbody2D'siz collider'ı transform ile oynatmak | Her frame static collider ağacı yeniden kurulur | Kinematic RB + `MovePosition` |
| Karakter `Interpolate`, kılıç değil | Ring görsel olarak geride sürüklenir | İkisinde de açık |
| `body.position + offset` | Ring `velocity * dt` kadar geride | `PredictedPosition(dt)` |
| Child Rigidbody2D nesting | `attachedRigidbody` zinciri kopar | Root seviye kinematic RB |
| Ring açısını `Update`'te ilerletmek | İki rate karışır, titreme | Açı `FixedUpdate`'te |
| İnce collider + yüksek açısal hız | Tünelleme | Collider'ı sprite'tan geniş tut |
| Aktif objeye `Body.position` yazmak | Interpolasyon lekesi | `transform.position` → `SetActive` → `Body.position` |

---

## 1. Faz Haritası

| Faz | Kapsam | Build sonunda ne çalışıyor | Yeni | Düzenlenen |
|---|---|---|:---:|:---:|
| **1** | Hareket + yaşam döngüsü | Player joystick'le hareket ediyor, R spawn'a döndürüyor | 15 | 0 |
| **2** | Sword Ring (çarpışmasız) | Kılıçlar dönüyor, sayı değişince yeniden diziliyor | 5 | 5 |
| **3** | Etkileşim + Combat + sabit düşmanlar | Kılıçlar nötrleşiyor, karakterler ölüyor, R hepsini geri getiriyor | 8 | 7 |
| **4** | Collectible | Kılıç toplayınca ring büyüyor | 4 | 1 |
| **5a** | AI — sadece Chase | Düşmanlar kovalıyor | 7 | 3 |
| **5b** | AI — tam karar mekanizması | Roam / Flee / SeekCollectible + histerezis | 1 | 2 |

**Toplam: 40 dosya, 18 düzenleme.**

### Faz kuralları

1. Her faz **çalışan bir build** bırakır. Yarım kalan sistem yok.
2. Hiçbir faz bir öncekini yeniden yazmaz — sadece ekler veya nokta atışı düzenler.
3. Kabul testi geçmeden sonraki faza geçilmez.
4. §9'daki matris atlama kontrolüdür.

---

## 2. FAZ 1 — Hareket + Yaşam Döngüsü

**Hedef:** oyunun iskeleti ve retry'da 0 allocation iddiası ilk günden ayakta.

### 2.1 Klasör yapısı (Faz 1 sonu)

```
Scripts/
├── Core/
│   └── GameManager.cs
├── Spawn/
│   ├── SpawnCategory.cs
│   ├── SpawnMapView.cs
│   └── SpawnMap.cs
├── Character/
│   ├── Character.cs
│   ├── CharacterStats.cs
│   ├── CharacterDefinition.cs
│   ├── CharacterView.cs
│   ├── CharacterRegistry.cs
│   ├── CharacterFactory.cs
│   └── MovementSimulator.cs
└── Input/
    ├── JoystickInput.cs
    ├── IDirectionProvider.cs
    ├── JoystickDirectionProvider.cs
    └── NullDirectionProvider.cs
```

### 2.2 Spawn/SpawnCategory.cs

```csharp
public enum SpawnCategory
{
    Player,
    Enemy,
    Collectible,
    Prop        // bugün kullanılmıyor — map'in yerini ayırıyor
}

public enum SpawnPick
{
    /// <summary>Sırayla dolaş. Retry'da cursor sıfırlanır → aynı düzen.</summary>
    Sequential,

    /// <summary>Shuffle bag: tüm noktalar bir tur dağıtılır, sonra yeniden karışır.</summary>
    Random
}
```

> **Player ve Enemy neden ayrı kategori:** player merkezde, düşmanlar çevrede
> doğmalı. Tek kategoride olsalardı "index 0 player'ındır" gibi sihirli bir
> kural gerekirdi.

> **Karakterler neden `Sequential`:** cursor `Initialize`'da sıfırlandığı için
> her retry'da her düşman aynı noktasına gidiyor. Seed'e gerek kalmıyor.

### 2.3 Spawn/SpawnMapView.cs

```csharp
using UnityEngine;

public sealed class SpawnMapView : MonoBehaviour
{
    [SerializeField] private Transform[] _playerPoints;
    [SerializeField] private Transform[] _enemyPoints;
    [SerializeField] private Transform[] _collectiblePoints;
    [SerializeField] private Transform[] _propPoints;

    public Transform[] GetPoints(SpawnCategory category) => category switch
    {
        SpawnCategory.Player      => _playerPoints,
        SpawnCategory.Enemy       => _enemyPoints,
        SpawnCategory.Collectible => _collectiblePoints,
        SpawnCategory.Prop        => _propPoints,
        _                         => System.Array.Empty<Transform>()
    };

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawCategory(_playerPoints,      Color.green,  0.45f);
        DrawCategory(_enemyPoints,       Color.red,    0.40f);
        DrawCategory(_collectiblePoints, Color.cyan,   0.30f);
        DrawCategory(_propPoints,        Color.yellow, 0.25f);
    }

    private static void DrawCategory(Transform[] points, Color color, float radius)
    {
        if (points == null) return;

        Gizmos.color = color;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;
            Gizmos.DrawWireSphere(points[i].position, radius);
        }
    }
#endif
}
```

### 2.4 Spawn/SpawnMap.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SpawnMap
{
    private sealed class Bucket
    {
        public Vector2[] Points;
        public SpawnPick Pick;
        public int[]     Bag;      // Random için karıştırılmış indeks listesi
        public int       Cursor;
    }

    private readonly Dictionary<SpawnCategory, Bucket> _buckets = new(4);

    // CREATE'te bir kez üretiliyor. Initialize'da YENİDEN YARATILMIYOR:
    // karakterler Sequential olduğu için retry'da düzen zaten aynı,
    // collectible'lar ise her retry'da farklı dizilsin istiyoruz.
    private readonly System.Random _random;

    /// <summary>CREATE fazı — Transform'lar Vector2'ye kopyalanır, view'a bağ kalmaz.</summary>
    public SpawnMap(SpawnMapView view, int seed)
    {
        _random = new System.Random(seed);

        AddBucket(view, SpawnCategory.Player,      SpawnPick.Sequential);
        AddBucket(view, SpawnCategory.Enemy,       SpawnPick.Sequential);
        AddBucket(view, SpawnCategory.Collectible, SpawnPick.Random);
        AddBucket(view, SpawnCategory.Prop,        SpawnPick.Random);
    }

    private void AddBucket(SpawnMapView view, SpawnCategory category, SpawnPick pick)
    {
        Transform[] transforms = view != null
            ? view.GetPoints(category)
            : Array.Empty<Transform>();

        int count = transforms?.Length ?? 0;
        var points = new Vector2[count];
        var bag    = new int[count];

        for (int i = 0; i < count; i++)
        {
            points[i] = transforms[i] != null ? (Vector2)transforms[i].position : Vector2.zero;
            bag[i]    = i;
        }

        _buckets[category] = new Bucket { Points = points, Pick = pick, Bag = bag, Cursor = 0 };
    }

    public int Count(SpawnCategory category) => _buckets[category].Points.Length;

    /// <summary>INITIALIZE fazı — cursor sıfırlanır, Random bucket'lar karıştırılır.</summary>
    public void Initialize()
    {
        foreach (Bucket bucket in _buckets.Values)
        {
            bucket.Cursor = 0;
            if (bucket.Pick == SpawnPick.Random) Shuffle(bucket);
        }
    }

    public Vector2 Next(SpawnCategory category)
    {
        Bucket bucket = _buckets[category];

        if (bucket.Points.Length == 0)
        {
            Debug.LogWarning($"SpawnMap: '{category}' için nokta tanımlı değil.");
            return Vector2.zero;
        }

        if (bucket.Pick == SpawnPick.Sequential)
        {
            Vector2 point = bucket.Points[bucket.Cursor];
            bucket.Cursor = (bucket.Cursor + 1) % bucket.Points.Length;
            return point;
        }

        // Shuffle bag: tüm noktalar bir tur dağıtılmadan hiçbiri tekrar etmez
        if (bucket.Cursor >= bucket.Bag.Length) Shuffle(bucket);
        return bucket.Points[bucket.Bag[bucket.Cursor++]];
    }

    private void Shuffle(Bucket bucket)
    {
        int[] bag = bucket.Bag;

        for (int i = bag.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        bucket.Cursor = 0;
    }

    public void Deinitialize()
    {
        foreach (Bucket bucket in _buckets.Values)
            bucket.Cursor = 0;
    }
}
```

> **Shuffle bag neden:** collectible aynı anda birkaç tane aktif olabiliyor.
> Düz `Random` ikisini aynı noktaya koyabilir — kırılmaz ama çirkin görünür.

### 2.5 Input/JoystickInput.cs

Sahnedeki UI joystick. `Canvas` + `EventSystem` gerektirir.
Editörde klavye ile de test edilebilsin diye fallback var.

```csharp
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class JoystickInput : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform _background;
    [SerializeField] private RectTransform _handle;
    [SerializeField] private float _handleRange = 60f;
    [SerializeField] private bool  _keyboardFallback = true;

    private Vector2 _value;

    /// <summary>Ham joystick vektörü, magnitude 0..1. Deadzone UYGULANMAZ.</summary>
    public Vector2 Value
    {
        get
        {
#if UNITY_EDITOR
            if (_keyboardFallback && _value.sqrMagnitude < 0.0001f)
            {
                var keyboard = new Vector2(
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical"));

                if (keyboard.sqrMagnitude > 0.0001f)
                    return Vector2.ClampMagnitude(keyboard, 1f);
            }
#endif
            return _value;
        }
    }

    public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

    public void OnDrag(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _background, eventData.position, eventData.pressEventCamera,
                out Vector2 local))
            return;

        _value = Vector2.ClampMagnitude(local / _handleRange, 1f);
        _handle.anchoredPosition = _value * _handleRange;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _value = Vector2.zero;
        _handle.anchoredPosition = Vector2.zero;
    }
}
```

> Deadzone burada **değil**, `JoystickDirectionProvider`'da uygulanır.
> Bu sınıf ham veriyi verir.

### 2.6 Input/IDirectionProvider.cs

```csharp
using UnityEngine;

public interface IDirectionProvider
{
    /// <summary>
    /// SAF OKUMA. Hesap yapmaz, yan etkisi yoktur, bedavadır.
    /// Aynı adımda iki kez okumak aynı değeri verir.
    /// Değer yalnızca Update() içinde güncellenir.
    /// Magnitude 0..1 — normalize DEĞİL, ClampMagnitude.
    /// </summary>
    Vector2 Direction { get; }

    /// <summary>Round başı — biriken state sıfırlanır (timer, hedef, yön).</summary>
    void Initialize();

    void Update(float deltaTime);
    void Deinitialize();
}
```

### 2.7 Input/JoystickDirectionProvider.cs

```csharp
using UnityEngine;

public sealed class JoystickDirectionProvider : IDirectionProvider
{
    private readonly JoystickInput _joystick;
    private readonly float _deadzone;

    private Vector2 _direction;

    public JoystickDirectionProvider(JoystickInput joystick, float deadzone = 0.15f)
    {
        _joystick = joystick;
        _deadzone = deadzone;
    }

    public Vector2 Direction => _direction;

    public void Initialize()   => _direction = Vector2.zero;
    public void Deinitialize() => _direction = Vector2.zero;

    public void Update(float deltaTime)
    {
        Vector2 raw = _joystick.Value;
        float mag = raw.magnitude;

        if (mag < _deadzone)
        {
            _direction = Vector2.zero;
            return;
        }

        // Deadzone'u yeniden ölçekle ki eşik geçildiğinde hızda sıçrama olmasın
        float scaled = Mathf.InverseLerp(_deadzone, 1f, mag);
        _direction = raw / mag * scaled;
    }
}
```

### 2.8 Input/NullDirectionProvider.cs

```csharp
using UnityEngine;

public sealed class NullDirectionProvider : IDirectionProvider
{
    // Stateless olduğu için paylaşılabilir ve reset'ten etkilenmez.
    public static readonly NullDirectionProvider Instance = new();

    public Vector2 Direction => Vector2.zero;

    public void Initialize() { }
    public void Update(float deltaTime) { }
    public void Deinitialize() { }
}
```

> Stun / diyalog / ölüm → `SetDirectionProvider(NullDirectionProvider.Instance)`.
> Possession, AI devralma → provider swap. Tek satır.

### 2.9 Character/CharacterStats.cs (Faz 1)

```csharp
public sealed class CharacterStats
{
    public float MoveSpeed;
    public float Acceleration;
    public float Deceleration;
    public float MaxHealth;

    /// <summary>
    /// INITIALIZE fazı. Yeni nesne ÜRETMİYOR — alanların üzerine yazıyor.
    /// Retry'da allocation olmaması için gerekli.
    /// </summary>
    public void ResetFrom(CharacterDefinition def)
    {
        MoveSpeed    = def.MoveSpeed;
        Acceleration = def.Acceleration;
        Deceleration = def.Deceleration;
        MaxHealth    = def.MaxHealth;
    }
}
```

> **Tuzak:** SO'yu runtime'da doğrudan okuyup üzerine yazarsan asset'i kalıcı
> değiştirirsin — editörde fark etmezsin, build'de sıfırlanır. `ResetFrom` SO'dan
> **okuyup** runtime kopyasına yazıyor, SO'ya asla dokunmuyor.

### 2.10 Character/CharacterDefinition.cs (Faz 1)

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
    public CharacterBrainType BrainType = CharacterBrainType.Player;

    [Header("Movement")]
    public float MoveSpeed    = 5f;
    public float Acceleration = 40f;
    public float Deceleration = 60f;
    public float MaxHealth    = 100f;
}
```

### 2.11 Character/CharacterView.cs (Faz 1)

```csharp
using UnityEngine;

// Faz 3'te taban sınıf InteractionBody olacak.
public sealed class CharacterView : MonoBehaviour
{
    [SerializeField] private Rigidbody2D    _body;
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private Animator       _animator;

    public Rigidbody2D Body     => _body;
    public Animator    Animator => _animator;

    public void SetTint(Color color) => _sprite.color = color;
}
```

### 2.12 Character/MovementSimulator.cs

Input'un ne olduğunu bilmez. Yön alır, hız üretir. **Faz 1'de final hâlindedir.**

```csharp
using UnityEngine;

public sealed class MovementSimulator
{
    private const float ExternalDecay = 8f;

    private readonly Rigidbody2D    _body;
    private readonly CharacterStats _stats;

    private Vector2 _direction;
    private Vector2 _velocity;
    private Vector2 _externalVelocity;   // knockback / dash kanalı

    public Vector2 Position => _body.position;
    public Vector2 Velocity => _velocity;

    /// <summary>Bu fizik adımından SONRAKİ pozisyon. Ring bunu merkez alır.</summary>
    public Vector2 PredictedPosition(float deltaTime)
        => _body.position + _body.linearVelocity * deltaTime;

    /// <summary>CREATE fazı — Rigidbody ayarları bir kez yapılıyor.</summary>
    public MovementSimulator(Rigidbody2D body, CharacterStats stats)
    {
        _body  = body;
        _stats = stats;

        _body.gravityScale   = 0f;
        _body.freezeRotation = true;
        _body.interpolation  = RigidbodyInterpolation2D.Interpolate;
    }

    public void Initialize()   => ResetMotion();
    public void Deinitialize() => ResetMotion();

    private void ResetMotion()
    {
        _direction        = Vector2.zero;
        _velocity         = Vector2.zero;
        _externalVelocity = Vector2.zero;

        if (_body != null) _body.linearVelocity = Vector2.zero;
    }

    public void SetDirection(Vector2 direction)
        => _direction = Vector2.ClampMagnitude(direction, 1f);

    public void AddImpulse(Vector2 impulse) => _externalVelocity += impulse;

    public void FixedUpdate(float deltaTime)
    {
        Vector2 target = _direction * _stats.MoveSpeed;

        float rate = _direction.sqrMagnitude > 0.0001f
            ? _stats.Acceleration
            : _stats.Deceleration;

        _velocity = Vector2.MoveTowards(_velocity, target, rate * deltaTime);

        // Tek hesap noktası — modifier gelirse burası genişler
        _body.linearVelocity = _velocity + _externalVelocity;

        _externalVelocity = Vector2.MoveTowards(
            _externalVelocity, Vector2.zero, ExternalDecay * deltaTime);
    }
}
```

### 2.13 Character/Character.cs (Faz 1)

```csharp
using System;
using UnityEngine;

public sealed class Character : IDisposable
{
    private readonly CharacterView       _view;
    private readonly CharacterDefinition _definition;

    private IDirectionProvider _directionProvider;

    /// <summary>Sahada mı? Roster'da bekleyenler için false.</summary>
    public bool IsSpawned { get; private set; }

    public CharacterView     View     => _view;
    public CharacterStats    Stats    { get; }
    public MovementSimulator Movement { get; }

    public Vector2 Position => Movement.Position;

    // ================= CREATE =================
    public Character(CharacterView view, CharacterDefinition definition)
    {
        _view       = view;
        _definition = definition;

        Stats    = new CharacterStats();
        Movement = new MovementSimulator(view.Body, Stats);

        _directionProvider = NullDirectionProvider.Instance;
        _view.gameObject.SetActive(false);
    }

    public void SetDirectionProvider(IDirectionProvider provider)
        => _directionProvider = provider ?? NullDirectionProvider.Instance;

    // ================= INITIALIZE =================
    public void Initialize(Vector2 position)
    {
        if (IsSpawned) return;

        Stats.ResetFrom(_definition);

        // transform önce, sonra aktifleştir, sonra body — interpolasyon lekesi olmasın
        _view.transform.position = position;
        _view.gameObject.SetActive(true);
        _view.Body.position = position;
        _view.Body.rotation = 0f;

        Movement.Initialize();
        _directionProvider.Initialize();

        IsSpawned = true;
    }

    // ================= TICK =================
    public void Update(float deltaTime)
    {
        if (!IsSpawned) return;

        _directionProvider.Update(deltaTime);          // yön burada HESAPLANIR
    }

    public void FixedUpdate(float deltaTime)
    {
        if (!IsSpawned) return;

        Movement.SetDirection(_directionProvider.Direction);   // saf okuma
        Movement.FixedUpdate(deltaTime);
    }

    // ================= DEINITIALIZE =================
    public void Deinitialize()
    {
        if (!IsSpawned) return;
        IsSpawned = false;

        _directionProvider.Deinitialize();
        Movement.Deinitialize();

        _view.gameObject.SetActive(false);
    }

    // ================= DISPOSE =================
    public void Dispose()
    {
        Deinitialize();

        _directionProvider = null;

        if (_view != null) UnityEngine.Object.Destroy(_view.gameObject);
    }
}
```

### 2.14 Character/CharacterRegistry.cs (Faz 1)

```csharp
using System.Collections.Generic;

public sealed class CharacterRegistry
{
    private readonly List<Character> _active = new(64);

    public IReadOnlyList<Character> Active => _active;

    public void Add(Character character)    => _active.Add(character);
    public void Remove(Character character) => _active.Remove(character);

    public void Deinitialize() => _active.Clear();
}
```

### 2.15 Character/CharacterFactory.cs (Faz 1)

```csharp
using System;
using UnityEngine;

public sealed class CharacterFactory : IDisposable
{
    private readonly CharacterRegistry _registry;
    private readonly SpawnMap          _map;
    private readonly JoystickInput     _joystick;

    private Character _player;

    // ================= CREATE =================
    // Roster burada doğuyor. Bundan sonra hiçbir karakter Instantiate edilmiyor.
    public CharacterFactory(CharacterRegistry registry, SpawnMap map,
                            JoystickInput joystick, CharacterDefinition playerDefinition)
    {
        _registry = registry;
        _map      = map;
        _joystick = joystick;

        _player = CreateCharacter(playerDefinition);
    }

    private Character CreateCharacter(CharacterDefinition definition)
    {
        CharacterView view = UnityEngine.Object.Instantiate(definition.ViewPrefab);

        var character = new Character(view, definition);

        // Provider karakteri referans alabilir → önce karakter, sonra provider
        character.SetDirectionProvider(CreateProvider(definition, character));

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
    public void Initialize() => Place(_player, SpawnCategory.Player);

    private void Place(Character character, SpawnCategory category)
    {
        character.Initialize(_map.Next(category));
        _registry.Add(character);
    }

    /// <summary>Yok etmiyor — deaktive ediyor, retry'a kadar bekliyor.</summary>
    public void Despawn(Character character)
    {
        if (!character.IsSpawned) return;

        _registry.Remove(character);
        character.Deinitialize();
    }

    // ================= DEINITIALIZE =================
    public void Deinitialize() => Despawn(_player);

    // ================= DISPOSE =================
    public void Dispose()
    {
        _player?.Dispose();
        _player = null;
    }
}
```

### 2.16 Core/GameManager.cs (Faz 1)

```csharp
using System.Collections.Generic;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    [Header("Definitions")]
    [SerializeField] private CharacterDefinition _playerDefinition;

    [Header("Scene")]
    [SerializeField] private SpawnMapView  _spawnMapView;
    [SerializeField] private JoystickInput _joystick;
    [SerializeField] private int _spawnSeed = 12345;

    private SpawnMap          _map;
    private CharacterRegistry _characters;
    private CharacterFactory  _factory;

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

    /// <summary>Yenildik → tekrar oyna. Tek bir Instantiate yok.</summary>
    public void Restart()
    {
        Deinitialize();
        Initialize();
    }

    // ================= CREATE =================
    // Buradaki her `new` satırı bir DI container'a devredilebilir.
    private void Compose()
    {
        _map        = new SpawnMap(_spawnMapView, _spawnSeed);
        _characters = new CharacterRegistry();
        _factory    = new CharacterFactory(_characters, _map, _joystick, _playerDefinition);
    }

    // ================= INITIALIZE =================
    public void Initialize()
    {
        _map.Initialize();
        _factory.Initialize();
    }

    // ================= DEINITIALIZE =================
    public void Deinitialize()
    {
        _factory.Deinitialize();
        _characters.Deinitialize();
        _map.Deinitialize();
    }

    // ================= DISPOSE =================
    private void Dispose()
    {
        _factory?.Dispose();

        _factory    = null;
        _characters = null;
        _map        = null;
    }

    // ================= TICK =================
    private void Update()
    {
        float dt = Time.deltaTime;

        IReadOnlyList<Character> active = _characters.Active;
        for (int i = 0; i < active.Count; i++)
            active[i].Update(dt);

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R)) Restart();   // reset demosu
#endif
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        IReadOnlyList<Character> active = _characters.Active;
        for (int i = 0; i < active.Count; i++)
            active[i].FixedUpdate(dt);
    }
}
```

### 2.17 Sahne ve asset kurulumu

**Sahne:**
```
GameManager        → GameManager.cs
SpawnMap           → SpawnMapView.cs
 ├── PlayerPoints/      (en az 1 boş Transform)
 ├── EnemyPoints/       (şimdilik boş)
 ├── CollectiblePoints/ (şimdilik boş)
 └── PropPoints/        (şimdilik boş)
Canvas
 └── Joystick       → JoystickInput.cs (Image + handle child)
EventSystem
Main Camera
```

**Player prefab (`Player_View`):** root'ta
`Rigidbody2D` (Dynamic) + `CircleCollider2D` (`isTrigger = false`)
+ `SpriteRenderer` + `CharacterView`. Layer: `Character`.

**Asset:** `Player_Definition` (`CharacterDefinition`),
`BrainType = Player`, `ViewPrefab = Player_View`.

**Layer:** Project Settings → Tags and Layers → `Character` ekle.

### 2.18 Kabul testleri

| Test | Beklenen |
|---|---|
| Hareket | Joystick'le sekiz yöne akıcı hareket |
| Analog | Joystick'i yarım it → yarım hızda gidiyor |
| Deadzone | Merkeze yakın küçük hareketler yön üretmiyor |
| Restart | R → player spawn noktasına dönüyor, hızı sıfırlanıyor |
| **Allocation** | Profiler açık, 10 kez R → **0 `Instantiate`, 0 `Destroy`** |
| Gizmo | Scene view'da spawn noktaları renkli görünüyor |
| Nokta yok | `PlayerPoints`'i boşalt → uyarı logu, çökme yok |
| Dispose | Play'den çık → konsolda uyarı yok |

---

## 3. FAZ 2 — Sword Ring (Çarpışmasız)

**Hedef:** ring geometrisi ve havuz mekaniği tek başına doğrulanıyor.
Kılıçların collider'ı var ama kimse dinlemiyor.

### 3.1 Core/Pool.cs (YENİ)

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class Pool<T> : IDisposable where T : class
{
    private readonly Stack<T>  _idle;
    private readonly Func<T>   _create;
    private readonly Action<T> _destroy;

    public int IdleCount => _idle.Count;

    /// <summary>CREATE fazı — prewarm burada Instantiate ediyor.</summary>
    public Pool(Func<T> create, Action<T> destroy, int prewarm = 0)
    {
        _create  = create;
        _destroy = destroy;
        _idle    = new Stack<T>(Mathf.Max(prewarm, 4));

        for (int i = 0; i < prewarm; i++)
            _idle.Push(_create());
    }

    public T Rent() => _idle.Count > 0 ? _idle.Pop() : _create();

    public void Return(T item) => _idle.Push(item);

    /// <summary>DISPOSE fazı — havuzdaki BOŞTA olanları yok eder.</summary>
    public void Dispose()
    {
        while (_idle.Count > 0)
            _destroy(_idle.Pop());
    }
}
```

> Havuzun `Initialize`/`Deinitialize`'ı yok — havuzun oyun durumu yok.
> Retry'da havuz olduğu gibi kalır, sadece kirada olanlar geri döner.

### 3.2 Abilities/IAbility.cs (YENİ)

```csharp
public interface IAbility
{
    /// <summary>Round başı. Ability CREATE'te takılır, burada sadece kurulur.</summary>
    void Initialize(Character owner);

    void Update(float deltaTime);
    void FixedUpdate(float deltaTime);

    /// <summary>Round sonu veya ölüm. Kaynaklar (kılıçlar) havuza iade edilir.</summary>
    void Deinitialize();
}
```

### 3.3 Abilities/SwordView.cs (YENİ, Faz 2)

```csharp
using UnityEngine;

// Faz 3'te taban sınıf InteractionBody olacak ve OnTriggerEnter2D eklenecek.
public sealed class SwordView : MonoBehaviour
{
    [SerializeField] private Rigidbody2D    _body;
    [SerializeField] private Collider2D     _hitCollider;
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private Color          _neutralizedTint = new(1f, 1f, 1f, 0.35f);

    public Rigidbody2D Body => _body;

    private void Reset()
    {
        _body = GetComponent<Rigidbody2D>();
        _body.bodyType      = RigidbodyType2D.Kinematic;
        _body.interpolation = RigidbodyInterpolation2D.Interpolate;
        _body.useFullKinematicContacts = true;    // Faz 3'te ŞART olacak, şimdiden doğru ayarla
    }

    public void SetNeutralized(bool value)
    {
        _hitCollider.enabled = !value;
        _sprite.color = value ? _neutralizedTint : Color.white;
    }
}
```

### 3.4 Abilities/Sword.cs (YENİ, Faz 2)

Nötrleme mantığı **tam** — sadece combat'a bağlı değil.
Faz 3'te `: ICombatant`, `Root` ve resolver bağlantısı eklenecek.

```csharp
using UnityEngine;

public enum SwordState { Active, Neutralized }

public sealed class Sword
{
    private readonly SwordView _view;

    private Character _owner;
    private float _recoveryTimer;

    /// <summary>CREATE fazı — havuzun create fonksiyonu çağırıyor.</summary>
    public Sword(SwordView view) => _view = view;

    public SwordState State { get; private set; }
    public SwordView  View  => _view;

    public float Damage             => _owner != null ? _owner.Stats.SwordDamage        : 0f;
    public float NeutralizeDuration => _owner != null ? _owner.Stats.NeutralizeDuration : 1f;

    public void Initialize(Character owner)
    {
        _owner = owner;
        State  = SwordState.Active;
        _recoveryTimer = 0f;

        _view.gameObject.SetActive(true);
        _view.SetNeutralized(false);
    }

    public void FixedUpdate(float deltaTime)
    {
        if (State != SwordState.Neutralized) return;

        _recoveryTimer -= deltaTime;
        if (_recoveryTimer > 0f) return;

        State = SwordState.Active;
        _view.SetNeutralized(false);          // collider tekrar açılır
    }

    public void Neutralize(float duration)
    {
        if (State == SwordState.Neutralized)
        {
            _recoveryTimer = Mathf.Max(_recoveryTimer, duration);   // refresh, stack değil
            return;
        }

        State = SwordState.Neutralized;
        _recoveryTimer = duration;
        _view.SetNeutralized(true);           // collider kapanır
    }

    public void MoveTo(Vector2 position, float angleRad)
    {
        _view.Body.MovePosition(position);
        _view.Body.MoveRotation(angleRad * Mathf.Rad2Deg - 90f);     // sprite yukarı bakıyorsa
    }

    public void Deinitialize()
    {
        _view.Body.linearVelocity = Vector2.zero;
        _view.SetNeutralized(false);
        _view.gameObject.SetActive(false);

        _owner = null;
        State  = SwordState.Active;
        _recoveryTimer = 0f;
    }
}
```

### 3.5 Abilities/SwordRingAbility.cs (YENİ, final)

```csharp
using System.Collections.Generic;
using UnityEngine;

public sealed class SwordRingAbility : IAbility
{
    private readonly List<Sword> _swords = new(8);
    private readonly Pool<Sword> _pool;

    private Character _owner;
    private float _currentAngle;

    public SwordRingAbility(Pool<Sword> pool) => _pool = pool;

    public int ActiveSwordCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _swords.Count; i++)
                if (_swords[i].State == SwordState.Active) count++;
            return count;
        }
    }

    public void Initialize(Character owner)
    {
        _owner = owner;
        _currentAngle = 0f;
        SyncCount();
    }

    public void Update(float deltaTime) { }

    public void FixedUpdate(float deltaTime)
    {
        SyncCount();                                    // stat değiştiyse yakalar
        if (_swords.Count == 0) return;

        _currentAngle = Mathf.Repeat(
            _currentAngle + _owner.Stats.OrbitAngularSpeed * deltaTime, 360f);

        float radius = _owner.Stats.OrbitRadius;
        float step   = 360f / _swords.Count;

        // KRİTİK: bu fizik adımından SONRAKİ merkez.
        // body.position kullanırsan ring hareket halinde velocity*dt kadar geride kalır.
        Vector2 center = _owner.Movement.PredictedPosition(deltaTime);

        for (int i = 0; i < _swords.Count; i++)
        {
            float rad = (_currentAngle + i * step) * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

            _swords[i].FixedUpdate(deltaTime);          // nötrleme timer'ı
            _swords[i].MoveTo(center + offset, rad);
        }
    }

    /// <summary>
    /// Tek doğruluk kaynağı: açı biriktir, slotu hesapla.
    ///   angle_i = _currentAngle + i * (360 / count)
    /// Kılıç ekle/çıkar = sadece count değişir, aralık kendiliğinden yeniden dağılır.
    /// </summary>
    private void SyncCount()
    {
        int desired = Mathf.Clamp(_owner.Stats.SwordCount, 0, _owner.Stats.MaxSwordCount);

        while (_swords.Count < desired)
        {
            Sword sword = _pool.Rent();               // prewarm sayesinde Instantiate yok
            sword.Initialize(_owner);
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
        // Kılıç kendini iade etmez — ability topluca iade eder
        for (int i = 0; i < _swords.Count; i++)
            ReturnSword(_swords[i]);

        _swords.Clear();
        _owner = null;
        _currentAngle = 0f;
    }
}
```

### 3.6 DÜZENLE: Character/CharacterStats.cs

Alanlar ekle:
```csharp
public int   SwordCount;
public int   MaxSwordCount;
public float OrbitRadius;
public float OrbitAngularSpeed;      // derece / saniye
public float SwordDamage;
public float NeutralizeDuration;
```

`ResetFrom` gövdesine ekle:
```csharp
SwordCount         = def.SwordCount;
MaxSwordCount      = def.MaxSwordCount;
OrbitRadius        = def.OrbitRadius;
OrbitAngularSpeed  = def.OrbitAngularSpeed;
SwordDamage        = def.SwordDamage;
NeutralizeDuration = def.NeutralizeDuration;
```

### 3.7 DÜZENLE: Character/CharacterDefinition.cs

`[Header("Brain")]` bloğuna ekle:
```csharp
public bool HasSwordRing = true;
```

Yeni blok ve `OnValidate` ekle:
```csharp
[Header("Sword Ring")]
public int   SwordCount         = 3;
public int   MaxSwordCount      = 12;
public float OrbitRadius        = 1.5f;
public float OrbitAngularSpeed  = 120f;
public float SwordDamage        = 10f;
public float NeutralizeDuration = 1.5f;

private void OnValidate()
{
    MaxSwordCount = Mathf.Max(MaxSwordCount, SwordCount);
}
```

### 3.8 DÜZENLE: Character/Character.cs

Alan ve metot ekle:
```csharp
private readonly List<IAbility> _abilities = new(2);   // using System.Collections.Generic;

/// <summary>CREATE fazı — ability bir kez takılır.</summary>
public void AddAbility(IAbility ability) => _abilities.Add(ability);
```

`Initialize()` sonuna, `IsSpawned = true;` satırından **önce**:
```csharp
for (int i = 0; i < _abilities.Count; i++)
    _abilities[i].Initialize(this);
```

`Update()` sonuna:
```csharp
for (int i = 0; i < _abilities.Count; i++)
    _abilities[i].Update(deltaTime);
```

`FixedUpdate()` sonuna (ring hareketten **sonra**):
```csharp
for (int i = 0; i < _abilities.Count; i++)
    _abilities[i].FixedUpdate(deltaTime);
```

`Deinitialize()` içine, `IsSpawned = false;` satırından hemen sonra:
```csharp
for (int i = _abilities.Count - 1; i >= 0; i--)
    _abilities[i].Deinitialize();
```

`Dispose()` içine:
```csharp
_abilities.Clear();
```

### 3.9 DÜZENLE: Character/CharacterFactory.cs

Alan ve constructor parametresi ekle:
```csharp
private readonly Pool<Sword> _swordPool;

// constructor imzasına: Pool<Sword> swordPool
// constructor gövdesine: _swordPool = swordPool;
```

`CreateCharacter` içine, `SetDirectionProvider`'dan sonra:
```csharp
if (definition.HasSwordRing)
    character.AddAbility(new SwordRingAbility(_swordPool));
```

### 3.10 DÜZENLE: Core/GameManager.cs

Serialized alanlar ekle:
```csharp
[Header("Prefabs")]
[SerializeField] private SwordView _swordPrefab;

[Header("Pooling")]
[SerializeField] private int _swordPrewarm = 48;
```

Alan ekle:
```csharp
private Pool<Sword> _swordPool;
```

`Compose()` içine, factory'den **önce**:
```csharp
_swordPool = new Pool<Sword>(
    create:  CreateSword,
    destroy: sword => Destroy(sword.View.gameObject),
    prewarm: _swordPrewarm);
```

Yeni metot:
```csharp
private Sword CreateSword()
{
    SwordView view = Instantiate(_swordPrefab);
    view.gameObject.SetActive(false);
    return new Sword(view);
}
```

Factory constructor çağrısına `_swordPool` parametresi ekle.

`Dispose()` içine, `_factory?.Dispose();` satırından **sonra**:
```csharp
_swordPool?.Dispose();
_swordPool = null;
```

> **Dispose sırası kritik:** `_factory.Dispose()` önce çalışmalı ki kılıçlar
> havuza dönsün, sonra `_swordPool.Dispose()` onları yok etsin.

### 3.11 Sahne ve asset kurulumu

**Sword prefab (`Sword_View`):** root'ta
`Rigidbody2D` (**Kinematic**, `Interpolate`, `useFullKinematicContacts = true`)
+ `BoxCollider2D` (**`isTrigger = true`**, sprite'tan biraz geniş)
+ `SpriteRenderer` + `SwordView`. Layer: `Sword`.

**Layer:** `Sword` ekle. Matris ayarı Faz 3'te anlam kazanacak ama şimdi yap:
`Sword ↔ Environment` kapalı.

`GameManager` inspector'ında `_swordPrefab` ata.

### 3.12 Kabul testleri

| Test | Beklenen |
|---|---|
| Dizilim | 3 kılıç 120° aralıklı |
| Yeniden dizilim | Debug'dan `Stats.SwordCount` 3→5 → beş kılıç 72° aralıklı |
| Sıfır | `SwordCount = 0` → kılıç yok, hata yok |
| Clamp | `SwordCount = 99` → `MaxSwordCount` kadar |
| **Ring gecikmesi** | Tam hızda koş → kılıçlar arkada **sürüklenmiyor** |
| Titreme | `Time.timeScale = 0.2` → ring akıcı, zıplamıyor |
| Prewarm | Play'e bas → Hierarchy'de 48 deaktif kılıç |
| Restart | R → ring resetleniyor, **0 `Instantiate`** |
| Dispose | Play'den çık → kılıç kalıntısı yok |

---

## 4. FAZ 3 — Etkileşim + Combat + Düşmanlar

**Hedef:** çarpışma, hasar, ölüm ve restart döngüsü.
Düşmanlar sabit kukla (`BrainType.None`), sıfır AI kodu.

**En yoğun faz** — 8 dosya doğuyor, 7 dosya düzenleniyor.

### 4.1 Interaction/IInteractionEntity.cs (YENİ)

```csharp
public interface IInteractionEntity
{
    /// <summary>
    /// Etkileşim grubunun KÖKÜ. Kılıçta sahibi olan karakter;
    /// karakterde ve collectible'da kendisi.
    ///
    /// SÖZLEŞME: zincirin en tepesi döner, bir üst halka değil.
    /// Resolver tek bir ReferenceEquals yapıyor, zincir yürümüyor — bu ancak
    /// herkes en tepeyi döndürürse doğru çalışır. İleride sword → shield → character
    /// gibi bir zincir kurulursa kılıç yine KARAKTERİ döndürmeli, kalkanı değil.
    ///
    /// Böylece int kimliğe ve kimlik üretecine gerek kalmıyor;
    /// referans karşılaştırması yetiyor.
    /// </summary>
    IInteractionEntity Root { get; }
}
```

### 4.2 Interaction/InteractionBody.cs (YENİ)

```csharp
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class InteractionBody : MonoBehaviour
{
    private InteractionResolver _resolver;

    public IInteractionEntity Entity { get; private set; }

    public void Bind(IInteractionEntity entity, InteractionResolver resolver)
    {
        Entity    = entity;
        _resolver = resolver;
    }

    public void Unbind()
    {
        Entity    = null;
        _resolver = null;
    }

    /// <summary>Türevler trigger mesajlarını buraya yönlendirir.</summary>
    protected void ReportContact(Collider2D other)
    {
        if (Entity == null || _resolver == null) return;

        Rigidbody2D otherRigidbody = other.attachedRigidbody;
        if (otherRigidbody == null) return;
        if (!otherRigidbody.TryGetComponent(out InteractionBody otherBody)) return;
        if (otherBody.Entity == null) return;

        _resolver.Resolve(Entity, otherBody.Entity, other.ClosestPoint(transform.position));
    }
}
```

> **Kısıt:** collider ya `InteractionBody` ile aynı GameObject'te olmalı, ya da
> `attachedRigidbody` o GameObject'e çıkmalı. Child'a **kendi Rigidbody2D'si olan**
> bir collider koyarsan zincir kopar.

> **Sözlük neden yok:** `attachedRigidbody` zaten root'a çıkan cached bir referans,
> hiyerarşi gezmiyor.

### 4.3 Interaction/InteractionReport.cs (YENİ)

```csharp
using UnityEngine;

public readonly struct InteractionReport
{
    public readonly IInteractionEntity Source;
    public readonly IInteractionEntity Target;
    public readonly Vector2 Point;

    public InteractionReport(IInteractionEntity source, IInteractionEntity target, Vector2 point)
    {
        Source = source;
        Target = target;
        Point  = point;
    }
}
```

### 4.4 Interaction/IInteractionRule.cs (YENİ)

```csharp
public interface IInteractionRule
{
    /// <summary>
    /// Kural uygulandıysa true. Tip eşleşmezse veya durum uygun değilse false —
    /// resolver sıradaki kurala geçer.
    /// </summary>
    bool TryApply(in InteractionReport report);
}
```

### 4.5 Interaction/InteractionResolver.cs (YENİ)

```csharp
using System.Collections.Generic;
using UnityEngine;

public sealed class InteractionResolver
{
    private readonly List<IInteractionRule> _rules = new(4);

    /// <summary>CREATE fazında doldurulur — kuralların oyun durumu yok.</summary>
    public void AddRule(IInteractionRule rule) => _rules.Add(rule);

    /// <summary>
    /// Trigger callback'inden doğrudan çağrılır — anında çözülür.
    /// Kuyruk yok: callback içinde yaptığımız tek şey collider kapatmak,
    /// sayı değiştirmek ve listeye eklemek. Hiçbir nesne yok edilmiyor.
    /// </summary>
    public void Resolve(IInteractionEntity source, IInteractionEntity target, Vector2 point)
    {
        if (source == null || target == null) return;
        if (source.Root == null || target.Root == null) return;

        // EVRENSEL filtre: hiçbir şey kendi parçasıyla etkileşmez.
        // Kendi kılıcım kendime ve diğer kılıçlarıma değmiyor.
        // Oyun free-for-all olduğu için başka bir eleme yok; takımlar
        // eklenirse o kontrol BURAYA DEĞİL, combat kurallarının içine gider.
        if (ReferenceEquals(source.Root, target.Root)) return;

        var report = new InteractionReport(source, target, point);

        for (int i = 0; i < _rules.Count; i++)
            if (_rules[i].TryApply(report)) return;
    }

    public void Dispose() => _rules.Clear();
}
```

> **Dedup neden gerekmiyor:** iki kılıç değdiğinde A→B ve B→A olmak üzere iki çağrı
> gelir. İlki ikisini de nötrler; ikincisi `State != Active` guard'ına takılır.
> Aynı mantık hasarda da geçerli: hedef ilk vuruşta ölürse ikinci çağrı
> `!IsAlive` ile eleniyor.

### 4.6 Combat/ICombatant.cs (YENİ)

```csharp
using UnityEngine;

/// <summary>
/// Savaşa katılan entity. BUGÜN ÜYESİZ — bir etiket (marker).
///
/// Boş olması eksiklik değil, işi bu: Collectible, kapı, checkpoint gibi şeyler
/// bunu implement etmez, dolayısıyla combat kurallarına TİP OLARAK giremezler.
///
/// Takımlar geri geldiğinde (co-op, birbirine saldırmayan düşman sürüsü)
/// `int TeamId` buraya eklenir ve başka hiçbir yer değişmez.
/// </summary>
public interface ICombatant : IInteractionEntity
{
}

public readonly struct DamageInfo
{
    public readonly float      Amount;
    public readonly ICombatant Source;
    public readonly Vector2    Point;

    public DamageInfo(float amount, ICombatant source, Vector2 point)
    {
        Amount = amount;
        Source = source;
        Point  = point;
    }
}
```

### 4.7 Combat/SwordVsSwordRule.cs (YENİ)

```csharp
public sealed class SwordVsSwordRule : IInteractionRule
{
    public bool TryApply(in InteractionReport report)
    {
        // Kendi kılıcım kendi diğer kılıcıma değemez — resolver Root ile eledi.
        // Buraya gelen her çift zaten farklı karakterlere ait.
        if (report.Source is not Sword a) return false;
        if (report.Target is not Sword b) return false;

        // Bu guard aynı zamanda dedup görevi görüyor: B→A çağrısı geldiğinde
        // ikisi de zaten Neutralized olduğu için no-op.
        if (a.State != SwordState.Active) return false;
        if (b.State != SwordState.Active) return false;

        a.Neutralize(a.NeutralizeDuration);
        b.Neutralize(b.NeutralizeDuration);
        // VFX kıvılcım + SFX çınlama — hasar YOK

        return true;
    }
}
```

### 4.8 Combat/SwordVsCharacterRule.cs (YENİ)

```csharp
public sealed class SwordVsCharacterRule : IInteractionRule
{
    public bool TryApply(in InteractionReport report)
    {
        if (report.Source is not Sword sword)      return false;
        if (report.Target is not Character target) return false;

        if (sword.State != SwordState.Active) return false;   // nötrken vuramaz
        if (!target.IsAlive)                  return false;   // aynı adımda ikinci vuruşu eler

        target.ReceiveDamage(new DamageInfo(sword.Damage, sword, report.Point));
        return true;
    }
}
```

> **Vuruş ritmi:** sadece `OnTriggerEnter2D` kullanıyoruz. Kılıç dönerken hedefe
> girer, bir kez vurur, çıkar, bir sonraki turda tekrar girer. Ritmi fizik bedava
> veriyor — cooldown tablosuna gerek yok.

### 4.9 DÜZENLE: Character/CharacterView.cs

Taban sınıfı değiştir:
```csharp
public sealed class CharacterView : InteractionBody     // MonoBehaviour → InteractionBody
```

> Trigger metodu **eklenmiyor**. Tespit kaynak tarafında: kılıç ve collectible
> raporluyor, karakter sadece `attachedRigidbody` üzerinden çözülüyor.

### 4.10 DÜZENLE: Abilities/SwordView.cs

Taban sınıfı değiştir ve trigger ekle:
```csharp
public sealed class SwordView : InteractionBody         // MonoBehaviour → InteractionBody

// Sadece Enter — Stay YOK. Kılıç dönüyor: girer, vurur, çıkar, tekrar girer.
private void OnTriggerEnter2D(Collider2D other) => ReportContact(other);
```

### 4.11 DÜZENLE: Abilities/Sword.cs

Arayüz, alan, constructor, property ekle:
```csharp
public sealed class Sword : ICombatant                  // + arayüz

private readonly InteractionResolver _resolver;

public Sword(SwordView view, InteractionResolver resolver)   // + parametre
{
    _view     = view;
    _resolver = resolver;
}

public IInteractionEntity Root => _owner;               // kökü sahibi olan karakter
```

`Initialize()` sonuna:
```csharp
_view.Bind(this, _resolver);
```

`Deinitialize()` başına:
```csharp
_view.Unbind();
```

### 4.12 DÜZENLE: Character/Character.cs

Arayüz, alanlar, property'ler ekle:
```csharp
public sealed class Character : ICombatant, IDisposable   // + arayüz

private readonly InteractionResolver _resolver;
private float _health;

public IInteractionEntity Root => this;
public bool IsAlive => IsSpawned && _health > 0f;
public event Action<Character> Died;
```

Constructor'a parametre ekle:
```csharp
public Character(CharacterView view, CharacterDefinition definition,
                 InteractionResolver resolver)
{
    // ... mevcut satırlar ...
    _resolver = resolver;
}
```

`Initialize()` içine, `Stats.ResetFrom(_definition);` satırından hemen sonra:
```csharp
_health = Stats.MaxHealth;
```

`Initialize()` içine, `_view.Body.rotation = 0f;` satırından sonra:
```csharp
_view.Bind(this, _resolver);
```

`Update()` ve `FixedUpdate()` guard'ını **değiştir**:
```csharp
if (!IsSpawned) return;   →   if (!IsAlive) return;
```

Yeni metot ekle:
```csharp
public void ReceiveDamage(in DamageInfo info)
{
    if (!IsAlive) return;

    _health = Mathf.Max(0f, _health - info.Amount);
    if (_health > 0f) return;

    Movement.SetDirection(Vector2.zero);
    Died?.Invoke(this);        // registry kuyruğa alır, GameManager süpürür
}
```

`Deinitialize()` içine, `_view.gameObject.SetActive(false);` satırından **önce**:
```csharp
_view.Unbind();
```

`Dispose()` içine:
```csharp
Died = null;
```

### 4.13 DÜZENLE: Character/CharacterRegistry.cs

Tam yeni hâli:
```csharp
using System.Collections.Generic;

public sealed class CharacterRegistry
{
    private readonly List<Character> _active  = new(64);
    private readonly List<Character> _pending = new(8);

    public IReadOnlyList<Character> Active         => _active;
    public IReadOnlyList<Character> PendingRemoval => _pending;

    public void Add(Character character)
    {
        _active.Add(character);
        character.Died += OnDied;
    }

    public void Remove(Character character)
    {
        character.Died -= OnDied;
        _active.Remove(character);
    }

    private void OnDied(Character character)
    {
        if (_pending.Contains(character)) return;   // aynı adımda çift ölüm sinyali
        _pending.Add(character);
    }

    public void ClearPending() => _pending.Clear();

    public void Deinitialize()
    {
        for (int i = 0; i < _active.Count; i++)
            _active[i].Died -= OnDied;

        _active.Clear();
        _pending.Clear();
    }
}
```

### 4.14 DÜZENLE: Character/CharacterFactory.cs

Alanlar ekle:
```csharp
private readonly InteractionResolver _resolver;
private readonly List<Character> _enemies = new(32);   // using System.Collections.Generic;
```

Constructor imzasına ekle:
```csharp
InteractionResolver resolver, CharacterDefinition enemyDefinition, int enemyCount
```

Constructor gövdesine ekle (player oluşturmadan **önce** `_resolver` ataması,
**sonra** düşman döngüsü):
```csharp
_resolver = resolver;

// ... _player = CreateCharacter(playerDefinition); ...

for (int i = 0; i < enemyCount; i++)
    _enemies.Add(CreateCharacter(enemyDefinition));
```

`CreateCharacter` içinde:
```csharp
var character = new Character(view, definition, _resolver);   // + resolver
```

`Initialize()` sonuna:
```csharp
for (int i = 0; i < _enemies.Count; i++)
    Place(_enemies[i], SpawnCategory.Enemy);
```

`Deinitialize()` sonuna:
```csharp
for (int i = 0; i < _enemies.Count; i++)
    Despawn(_enemies[i]);
```

`Dispose()` sonuna:
```csharp
for (int i = 0; i < _enemies.Count; i++)
    _enemies[i].Dispose();

_enemies.Clear();
```

### 4.15 DÜZENLE: Core/GameManager.cs

Serialized alanlar ekle:
```csharp
[SerializeField] private CharacterDefinition _enemyDefinition;   // Definitions bloğuna
[SerializeField] private int _enemyCount = 8;                    // Scene bloğuna
```

Alan ekle:
```csharp
private InteractionResolver _resolver;
```

`Compose()` içine, havuzdan **önce**:
```csharp
_resolver = new InteractionResolver();
_resolver.AddRule(new SwordVsSwordRule());
_resolver.AddRule(new SwordVsCharacterRule());
```

`CreateSword()` içinde:
```csharp
return new Sword(view, _resolver);          // + resolver
```

Factory constructor çağrısına ekle: `_resolver, _enemyDefinition, _enemyCount`.

`Update()` **başına**:
```csharp
// Ölümler fizik callback'lerinde tetikleniyor; bu frame'in tüm fizik
// adımları bittikten sonra süpürüyoruz.
DespawnPending();
```

Yeni metot:
```csharp
private void DespawnPending()
{
    IReadOnlyList<Character> pending = _characters.PendingRemoval;
    if (pending.Count == 0) return;

    for (int i = 0; i < pending.Count; i++)
        _factory.Despawn(pending[i]);       // Destroy DEĞİL — deaktive eder

    _characters.ClearPending();
}
```

`Dispose()` içine:
```csharp
_resolver?.Dispose();
_resolver = null;
```

### 4.16 Sahne ve asset kurulumu

**Layer Collision Matrix:**

|                 | Character | Sword | Environment |
|-----------------|:---------:|:-----:|:-----------:|
| **Character**   |     ✔     |   ✔   |      ✔      |
| **Sword**       |     ✔     |   ✔   |      ✘      |
| **Environment** |     ✔     |   ✘   |      —      |

**İki düşman definition'ı oluştur:**

| Asset | `BrainType` | `HasSwordRing` | Amaç |
|---|---|---|---|
| `Enemy_Dummy` | `None` | **false** | Saf kum torbası — hasar ve ölümü izole test |
| `Enemy_Ringed` | `None` | true | Kılıç-kılıç nötrlemeyi test |

> **Neden ayrı dummy:** iki karakterin de ring'i varsa `OrbitRadius = 1.5` ile
> kılıçlar ~3.0 mesafede çarpışıyor, gövde teması ~1.5'te. Yani yaklaşınca önce
> ring'ler nötrleşiyor ve gövdeye ulaşmak için toparlanmayı beklemek gerekiyor.
> İyi bir mekanik ama *test ederken* can sıkıcı.

`EnemyPoints` altına en az `_enemyCount` kadar boş Transform koy.

### 4.17 Kabul testleri — SIRAYLA

**Bu sıra önemli.** Her adım bir sonrakinin ön koşulu.

| # | Test | Beklenen |
|---|---|---|
| 1 | **Trigger ateşliyor mu** | `SwordView.OnTriggerEnter2D`'ye geçici `Debug.Log` koy → kılıç kuklaya değince log geliyor |
| 2 | Ownership | Kendi kılıcın kendine ve diğer kılıçlarına **değmiyor** (log yok) |
| 3 | Hasar | `Enemy_Dummy`'ye gir → canı düşüyor |
| 4 | Ölüm | Dummy ölünce **deaktif** oluyor, Hierarchy'de duruyor (silinmiyor) |
| 5 | Nötrleme | `Enemy_Ringed`'e yaklaş → iki taraf da sönükleşiyor, süre sonunda toparlanıyor |
| 6 | Vuruş ritmi | Kılıç dönüp tekrar girince yeniden vuruyor |
| 7 | Restart | R → **herkes** geri geliyor, canlar dolu, kılıçlar tam |
| 8 | **Allocation** | 10 kez R → **0 `Instantiate`, 0 `Destroy`** |
| 9 | Çift ölüm | İki kılıç aynı anda öldürücü vuruş → `Died` bir kez tetikleniyor |
| 10 | Dispose | Play'den çık → uyarı yok, kalıntı yok |

> **Adım 1'i atlama.** `useFullKinematicContacts` yanlışsa hiçbir şey ateşlemez
> ve sen resolver'ı suçlarsın. Bu tek adım en çok saat kurtaran şeydir.

### 4.18 Bu fazın tuzakları

| Tuzak | Belirti | Çözüm |
|---|---|---|
| `useFullKinematicContacts = false` | Sword×Sword **hiç** tetiklenmez | `true` |
| Collider `isTrigger` değil | Trigger yerine collision | Kılıç trigger, karakter değil |
| Child'da ayrı Rigidbody2D | `attachedRigidbody` zinciri kopar | Root seviye |
| `Bind` unutulmuş | Rapor üretilmiyor | `Initialize` içinde `Bind` |
| `Unbind` unutulmuş | Ölü karakter hâlâ hedef | `Deinitialize` içinde `Unbind` |
| Ölümde `Destroy` çağırmak | Retry'da yeniden `Instantiate` | `Despawn` sadece deaktive eder |
| `Died` aboneliği sızıntısı | Retry'da çift tetikleme | `Remove` içinde `-=` |

---

## 5. FAZ 4 — Collectible

**Hedef:** ring toplayarak büyüyor. Mimarinin "additive" iddiasının kanıtı.

### 5.1 Collectibles/SwordCollectible.cs (YENİ)

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

    /// <summary>CREATE fazı — havuzun create fonksiyonu çağırıyor.</summary>
    public SwordCollectible(SwordCollectibleView view, InteractionResolver resolver)
    {
        _view     = view;
        _resolver = resolver;
    }

    public IInteractionEntity Root => this;      // kökü kendisi, kimseye ait değil

    public int  SwordAmount { get; private set; }
    public bool IsConsumed  { get; private set; }

    public SwordCollectibleView View => _view;
    public Vector2 Position => _view.Body.position;

    public void Initialize(Vector2 position, int swordAmount)
    {
        SwordAmount = swordAmount;
        IsConsumed  = false;

        _view.transform.position = position;
        _view.gameObject.SetActive(true);
        _view.Body.position = position;

        _view.SetPickupEnabled(true);
        _view.Bind(this, _resolver);
    }

    public void Consume()
    {
        if (IsConsumed) return;

        IsConsumed = true;
        _view.SetPickupEnabled(false);            // collider kapanır → rapor üretmez
    }

    public void Deinitialize()
    {
        _view.Unbind();
        _view.SetPickupEnabled(false);
        _view.gameObject.SetActive(false);

        IsConsumed  = false;
        SwordAmount = 0;
    }
}
```

### 5.2 Collectibles/SwordCollectibleView.cs (YENİ)

```csharp
using UnityEngine;

public sealed class SwordCollectibleView : InteractionBody
{
    [SerializeField] private Rigidbody2D    _body;
    [SerializeField] private Collider2D     _pickupCollider;
    [SerializeField] private SpriteRenderer _sprite;

    public Rigidbody2D Body => _body;

    private void Reset()
    {
        _body = GetComponent<Rigidbody2D>();
        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.useFullKinematicContacts = true;
        // Interpolate gerekmiyor — collectible hareket etmiyor
    }

    // Raporu COLLECTIBLE veriyor; CharacterView'a dokunmuyoruz.
    private void OnTriggerEnter2D(Collider2D other) => ReportContact(other);

    public void SetPickupEnabled(bool value)
    {
        _pickupCollider.enabled = value;
        _sprite.enabled = value;
    }
}
```

### 5.3 Collectibles/SwordCollectibleSpawner.cs (YENİ)

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
        SweepConsumed();

        _timer -= deltaTime;
        if (_timer > 0f) return;

        _timer = _settings.SpawnInterval;
        if (_active.Count >= _settings.MaxActive) return;

        Spawn();
    }

    public SwordCollectible Spawn()
    {
        SwordCollectible collectible = _pool.Rent();
        collectible.Initialize(_map.Next(SpawnCategory.Collectible), _settings.SwordAmount);

        _active.Add(collectible);
        return collectible;
    }

    private void SweepConsumed()
    {
        _sweep.Clear();

        for (int i = 0; i < _active.Count; i++)
            if (_active[i].IsConsumed) _sweep.Add(_active[i]);

        for (int i = 0; i < _sweep.Count; i++)
            Recycle(_sweep[i]);
    }

    private void Recycle(SwordCollectible collectible)
    {
        _active.Remove(collectible);
        collectible.Deinitialize();
        _pool.Return(collectible);
    }

    /// <summary>Menzilde collectible yoksa null.</summary>
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

> **Süpürme neden `Update`'te:** tüketme trigger callback'inde oluyor (fizikten sonra).
> Bir sonraki `Update`'te süpürmek bir frame gecikme demek ama collectible zaten
> görünmez ve collider'ı kapalı — görsel sorun yok.

### 5.4 Collectibles/SwordPickupRule.cs (YENİ)

```csharp
using UnityEngine;

public sealed class SwordPickupRule : IInteractionRule
{
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
        return true;
    }
}
```

> Kural yaptığı tek şey **bir sayıyı artırmak.** `SwordRingAbility`'ye, `Sword`'e,
> `Pool<Sword>`'a hiçbir referansı yok.

### 5.5 DÜZENLE: Core/GameManager.cs

Serialized alanlar ekle:
```csharp
[SerializeField] private SwordCollectibleView _collectiblePrefab;              // Prefabs bloğuna

[Header("Collectibles")]
[SerializeField] private SwordCollectibleSpawnSettings _collectibleSettings;

[SerializeField] private int _collectiblePrewarm = 8;                          // Pooling bloğuna
```

Alan ekle:
```csharp
private SwordCollectibleSpawner _collectibles;
```

`Compose()` içine, kural kayıtlarından **önce** (resolver oluştuktan sonra):
```csharp
_collectibles = new SwordCollectibleSpawner(
    _collectiblePrefab, _resolver, _map, _collectibleSettings, _collectiblePrewarm);
```

Kural listesine ekle:
```csharp
_resolver.AddRule(new SwordPickupRule());
```

`Initialize()` sonuna:
```csharp
_collectibles.Initialize();
```

`Deinitialize()` içine, `_factory.Deinitialize();` satırından **sonra**:
```csharp
_collectibles.Deinitialize();
```

`Update()` içine, karakter döngüsünden **önce**:
```csharp
_collectibles.Update(dt);
```

`Dispose()` içine, `_factory?.Dispose();` **sonrası**, `_swordPool?.Dispose();` **öncesi**:
```csharp
_collectibles?.Dispose();
_collectibles = null;
```

### 5.6 Değişmeyenler — bu fazın asıl mesajı

| Dosya | Neden değişmedi |
|---|---|
| `SwordRingAbility.cs` | `SwordCount`'u poll ediyor, sayıyı kimin değiştirdiğini bilmiyor |
| `Sword.cs` | — |
| `SwordView.cs` | — |
| `Character.cs` | — |
| `CharacterStats.cs` | `SwordCount` / `MaxSwordCount` Faz 2'de zaten eklenmişti |
| `CharacterFactory.cs` | — |

**Ring büyümesi için sıfır satır kod yazıldı.**

### 5.7 Sahne ve asset kurulumu

`CollectiblePoints` altına 8-12 boş Transform koy.

**Collectible prefab (`SwordCollectible_View`):** root'ta
`Rigidbody2D` (Kinematic, `useFullKinematicContacts = true`, `Interpolate` **kapalı**)
+ `CircleCollider2D` (**trigger**) + `SpriteRenderer` + `SwordCollectibleView`.
Layer: `Collectible`.

**Layer matrise ekle:**

|                 | Character | Sword | Collectible |
|-----------------|:---------:|:-----:|:-----------:|
| **Collectible** |     ✔     | **✘** |      ✘      |

> `Sword ↔ Collectible` **kapalı**: açık bırakırsan dönen kılıçlar collectible'ın
> üzerinden geçtikçe rapor üretir, hiçbir kural eşleşmez, boşa iş olur.

> **Prefab uyarısı:** bubble içindeki kılıç görseli **düz `SpriteRenderer`** olsun —
> `SwordView` prefab'ını dekorasyon olarak içine koyma. Koyarsan child rigidbody
> nesting'e girersin ve `Sword` layer'ında hayalet collider dolaşır.

### 5.8 Kabul testleri

| Test | Beklenen |
|---|---|
| Toplama | Üstünden geç → ring bir kılıç büyüyor |
| Yeniden dizilim | 3→4 → dört kılıç **eşit aralıklı** |
| Doluluk | `MaxSwordCount`'a ulaş → collectible **sahada kalıyor** |
| Shuffle bag | Collectible'lar üst üste doğmuyor |
| Limit | Aynı anda en fazla `MaxActive` kadar |
| Kılıç teması | Dönen kılıç collectible'ı **tetiklemiyor** (layer kapalı) |
| Restart | R → sahadaki collectible'lar temizleniyor, `SwordCount` base'e dönüyor |
| **Allocation** | 10 kez R → **0 `Instantiate`, 0 `Destroy`** |

---

## 6. FAZ 5a — AI (Sadece Chase)

**Hedef:** arena canlanıyor. Karar mekanizması yok, tek davranış: kovala.

### 6.1 AI/TargetFinder.cs (YENİ)

```csharp
using System.Collections.Generic;

public sealed class TargetFinder
{
    private readonly CharacterRegistry _characters;

    public TargetFinder(CharacterRegistry characters) => _characters = characters;

    /// <summary>Hedef bulunamazsa null.</summary>
    public Character FindNearest(Character self)
    {
        Character best = null;
        float bestSqr = float.MaxValue;

        IReadOnlyList<Character> active = _characters.Active;
        for (int i = 0; i < active.Count; i++)
        {
            Character candidate = active[i];

            if (candidate == self)  continue;
            if (!candidate.IsAlive) continue;

            float sqr = (candidate.Position - self.Position).sqrMagnitude;
            if (sqr >= bestSqr) continue;

            bestSqr = sqr;
            best    = candidate;
        }

        return best;
    }
}
```

> **Performans:** AI başına O(n), saniyede 4 kez. 50 düşmanda sorunsuz.
> Oyun free-for-all olduğu için takım filtresi yok.

### 6.2 AI/AIBlackboard.cs (YENİ)

```csharp
public sealed class AIBlackboard
{
    public Character        Self;
    public Character        Target;        // decider'ın gördüğü hedef
    public SwordCollectible Collectible;   // menzildeki en yakın pickup
    public AIStateId        State;

    /// <summary>Round başı — Self korunur, geri kalan sıfırlanır.</summary>
    public void Reset()
    {
        Target      = null;
        Collectible = null;
        State       = AIStateId.Roam;
    }
}
```

> Karar veren ile hareket eden aynı veriyi görmeli. Decider "hedef X zayıf, kovala"
> diyorsa chase state'i X'i kovalamalı — yeniden sorgulayıp Y bulmamalı.
> Blackboard sorguları da tekilleştiriyor: `FindNearest` (O(n)) ve collectible
> araması (O(m)) karar aralığında **bir kez** çalışıyor, her frame değil.

### 6.3 AI/AISettings.cs (YENİ)

```csharp
using UnityEngine;

public readonly struct AISettings
{
    public readonly float AggroEnterRadius;
    public readonly float AggroExitRadius;        // > Enter → histerezis
    public readonly float CollectibleSeekRadius;
    public readonly int   SwordAdvantageMargin;   // kararsız bölge genişliği
    public readonly float DecisionInterval;
    public readonly float RoamRadius;
    public readonly float RoamRepathInterval;

    public AISettings(float aggroEnter, float aggroExit, float seekRadius,
                      int margin, float decisionInterval,
                      float roamRadius, float roamRepath)
    {
        AggroEnterRadius      = aggroEnter;
        AggroExitRadius       = Mathf.Max(aggroExit, aggroEnter);
        CollectibleSeekRadius = seekRadius;
        SwordAdvantageMargin  = Mathf.Max(1, margin);
        DecisionInterval      = decisionInterval;
        RoamRadius            = roamRadius;
        RoamRepathInterval    = roamRepath;
    }

    public static AISettings CreateFrom(CharacterDefinition def) => new(
        def.AggroEnterRadius,
        def.AggroExitRadius,
        def.CollectibleSeekRadius,
        def.SwordAdvantageMargin,
        def.DecisionInterval,
        def.RoamRadius,
        def.RoamRepathInterval);
}
```

### 6.4 AI/IAIDecider.cs (YENİ)

```csharp
public enum AIStateId
{
    Roam,
    Chase,
    Flee,
    SeekCollectible
}

public interface IAIDecider
{
    AIStateId Decide(AIBlackboard blackboard, in AISettings settings);
}
```

### 6.5 AI/DefaultAIDecider.cs (YENİ, Faz 5a)

```csharp
public sealed class DefaultAIDecider : IAIDecider
{
    // Faz 5b'de tam karar mantığı gelecek: histerezis, kılıç avantajı,
    // roam ve collectible dalları.
    public AIStateId Decide(AIBlackboard bb, in AISettings settings)
        => bb.Target != null && bb.Target.IsAlive
            ? AIStateId.Chase
            : AIStateId.Roam;
}
```

### 6.6 AI/States/SeekDirectionProvider.cs (YENİ, final)

Chase, Flee ve SeekCollectible tek sınıfta. Fark sadece **neye** ve **hangi işaretle**.

```csharp
using UnityEngine;

public enum SeekSubject { Enemy, Collectible }

public sealed class SeekDirectionProvider : IDirectionProvider
{
    private readonly AIBlackboard _blackboard;
    private readonly SeekSubject  _subject;
    private readonly bool         _approach;
    private readonly float        _speedScale;

    private Vector2 _direction;

    public SeekDirectionProvider(AIBlackboard blackboard, SeekSubject subject,
                                 bool approach, float speedScale = 1f)
    {
        _blackboard = blackboard;
        _subject    = subject;
        _approach   = approach;
        _speedScale = speedScale;
    }

    public Vector2 Direction => _direction;

    public void Initialize()   => _direction = Vector2.zero;
    public void Deinitialize() => _direction = Vector2.zero;

    public void Update(float deltaTime)
    {
        if (!TryGetPoint(out Vector2 point))
        {
            _direction = Vector2.zero;
            return;
        }

        Vector2 delta = point - _blackboard.Self.Position;

        // Tam üst üste bindiysek rastgele bir yön seç — sıfır vektöre bölme yok
        if (delta.sqrMagnitude < 0.0001f)
            delta = Random.insideUnitCircle.normalized;

        // Yaklaşırken ClampMagnitude: varışa yakın yavaşlar, titremez.
        // Kaçarken normalized: yakınken de tam hız — Clamp kullanılsa
        // düşman yanındayken yavaş kaçardı.
        _direction = _approach
            ? Vector2.ClampMagnitude(delta, 1f) * _speedScale
            : -delta.normalized * _speedScale;
    }

    private bool TryGetPoint(out Vector2 point)
    {
        point = default;

        switch (_subject)
        {
            case SeekSubject.Enemy:
                Character target = _blackboard.Target;
                if (target == null || !target.IsAlive) return false;
                point = target.Position;
                return true;

            case SeekSubject.Collectible:
                SwordCollectible collectible = _blackboard.Collectible;
                if (collectible == null || collectible.IsConsumed) return false;
                point = collectible.Position;
                return true;

            default:
                return false;
        }
    }
}
```

### 6.7 AI/AIBrainProvider.cs (YENİ, Faz 5a)

```csharp
using System.Collections.Generic;
using UnityEngine;

public sealed class AIBrainProvider : IDirectionProvider
{
    private readonly AIBlackboard _blackboard = new();
    private readonly Dictionary<AIStateId, IDirectionProvider> _states;

    private readonly IAIDecider              _decider;
    private readonly TargetFinder            _targetFinder;
    private readonly SwordCollectibleSpawner _collectibles;
    private readonly AISettings              _settings;

    private IDirectionProvider _current;
    private float _decisionTimer;

    public Vector2 Direction => _current.Direction;

    // ================= CREATE =================
    public AIBrainProvider(Character self, IAIDecider decider,
                           TargetFinder targetFinder, SwordCollectibleSpawner collectibles,
                           in AISettings settings)
    {
        _decider      = decider;
        _targetFinder = targetFinder;
        _collectibles = collectibles;
        _settings     = settings;

        _blackboard.Self = self;

        // Faz 5b'de Roam / Flee / SeekCollectible eklenecek.
        _states = new Dictionary<AIStateId, IDirectionProvider>(4)
        {
            [AIStateId.Chase] = new SeekDirectionProvider(
                                    _blackboard, SeekSubject.Enemy, approach: true)
        };

        _current = _states[AIStateId.Chase];
    }

    // ================= INITIALIZE =================
    public void Initialize()
    {
        _blackboard.Reset();
        _blackboard.State = AIStateId.Chase;     // Faz 5b'de bu satır kalkacak
        _decisionTimer = 0f;
        _current = _states[AIStateId.Chase];

        foreach (IDirectionProvider state in _states.Values)
            state.Initialize();
    }

    public void Update(float deltaTime)
    {
        _decisionTimer -= deltaTime;

        if (_decisionTimer <= 0f)
        {
            _decisionTimer = _settings.DecisionInterval;

            RefreshBlackboard();

            AIStateId next = _decider.Decide(_blackboard, _settings);

            // Kayıtlı olmayan state'ler yok sayılıyor — Faz 5b'de guard kalkacak.
            if (next != _blackboard.State && _states.ContainsKey(next))
            {
                _blackboard.State = next;
                _current = _states[next];
            }
        }

        // Karar aralıklı, hareket her frame — hedef pozisyonu canlı okunduğu için
        // kovalama akıcı kalıyor.
        _current.Update(deltaTime);
    }

    private void RefreshBlackboard()
    {
        _blackboard.Target = _targetFinder.FindNearest(_blackboard.Self);

        _blackboard.Collectible = _collectibles.FindNearest(
            _blackboard.Self.Position, _settings.CollectibleSeekRadius);
    }

    // ================= DEINITIALIZE =================
    public void Deinitialize()
    {
        _blackboard.Reset();
        _decisionTimer = 0f;

        foreach (IDirectionProvider state in _states.Values)
            state.Deinitialize();
    }
}
```

### 6.8 DÜZENLE: Character/CharacterDefinition.cs

Yeni blok ekle:
```csharp
[Header("AI (BrainType = AI ise geçerli)")]
public float AggroEnterRadius      = 6f;
public float AggroExitRadius       = 8f;
public float CollectibleSeekRadius = 12f;
public int   SwordAdvantageMargin  = 1;
public float DecisionInterval      = 0.25f;
public float RoamRadius            = 5f;
public float RoamRepathInterval    = 2f;
```

`OnValidate()` içine ekle:
```csharp
AggroExitRadius      = Mathf.Max(AggroExitRadius, AggroEnterRadius);
SwordAdvantageMargin = Mathf.Max(1, SwordAdvantageMargin);
```

### 6.9 DÜZENLE: Character/CharacterFactory.cs

Alanlar ekle:
```csharp
private readonly TargetFinder            _targetFinder;
private readonly SwordCollectibleSpawner _collectibles;
```

Constructor imzasına ekle:
```csharp
TargetFinder targetFinder, SwordCollectibleSpawner collectibles
```

Constructor gövdesine, karakter üretiminden **önce** ekle:
```csharp
_targetFinder = targetFinder;
_collectibles = collectibles;
```

`CreateProvider` switch'ine ekle:
```csharp
case CharacterBrainType.AI:
    return new AIBrainProvider(
        self,
        new DefaultAIDecider(),
        _targetFinder,
        _collectibles,
        AISettings.CreateFrom(definition));
```

### 6.10 DÜZENLE: Core/GameManager.cs

`Compose()` içine, factory'den **önce**:
```csharp
var targetFinder = new TargetFinder(_characters);
```

Factory constructor çağrısına ekle: `targetFinder, _collectibles`.

### 6.11 Asset kurulumu

`Enemy_Ringed` definition'ında `BrainType` → **`AI`**.
`Enemy_Dummy`'yi `None` bırak (regresyon testi için).

### 6.12 Kabul testleri

| Test | Beklenen |
|---|---|
| Kovalama | Düşmanlar player'a doğru geliyor |
| Free-for-all | Düşmanlar **birbirini de** kovalıyor |
| Hedef değişimi | En yakın düşmanı öldür → hedef otomatik değişiyor |
| Ölü hedef | Hedef ölünce AI duruyor (Roam yok henüz) |
| Akıcılık | Karar 0.25sn'de bir ama hareket her frame — kovalama takılmıyor |
| Restart | R → hepsi başlangıç noktasında, hedefler sıfırlanmış |
| **Allocation** | 10 kez R → **0 `Instantiate`, 0 `Destroy`** |
| Performans | Profiler'da `TargetFinder` görünmüyor |

---

## 7. FAZ 5b — AI (Tam Karar Mekanizması)

**Hedef:** kılıç avantajına göre kovala/kaç, hedefsizken dolaş, collectible ara.

### 7.1 AI/States/RoamDirectionProvider.cs (YENİ, final)

```csharp
using UnityEngine;

public sealed class RoamDirectionProvider : IDirectionProvider
{
    private const float ArrivalThresholdSqr = 0.25f;
    private const float RoamSpeedScale      = 0.5f;    // dolaşırken yürü

    private readonly AIBlackboard _blackboard;
    private readonly AISettings   _settings;

    private Vector2 _destination;
    private Vector2 _direction;
    private float   _timer;
    private bool    _hasDestination;

    public RoamDirectionProvider(AIBlackboard blackboard, in AISettings settings)
    {
        _blackboard = blackboard;
        _settings   = settings;
    }

    public Vector2 Direction => _direction;

    public void Initialize()   => Reset();
    public void Deinitialize() => Reset();

    private void Reset()
    {
        _hasDestination = false;
        _direction      = Vector2.zero;
        _timer          = 0f;
    }

    public void Update(float deltaTime)
    {
        Vector2 position = _blackboard.Self.Position;
        _timer -= deltaTime;

        bool needsNewDestination =
            !_hasDestination ||
            _timer <= 0f ||
            (_destination - position).sqrMagnitude < ArrivalThresholdSqr;

        if (needsNewDestination)
        {
            _destination    = position + Random.insideUnitCircle * _settings.RoamRadius;
            _timer          = _settings.RoamRepathInterval;
            _hasDestination = true;
        }

        // magnitude < 1 → MovementSimulator yavaş yürüsün.
        // "AI yavaş yürüsün → dir * 0.5f" sözleşmesi (§0.2) burada kullanılıyor.
        _direction = Vector2.ClampMagnitude(_destination - position, 1f) * RoamSpeedScale;
    }
}
```

### 7.2 DEĞİŞTİR: AI/DefaultAIDecider.cs

Faz 5a'daki iki satırlık gövdeyi **tamamen değiştir**:

```csharp
public sealed class DefaultAIDecider : IAIDecider
{
    public AIStateId Decide(AIBlackboard bb, in AISettings settings)
    {
        Character self   = bb.Self;
        Character target = bb.Target;

        bool engaged = bb.State == AIStateId.Chase || bb.State == AIStateId.Flee;

        if (target != null && target.IsAlive)
        {
            // HİSTEREZİS: angaje olduğumuzda daha geniş yarıçapla ölçüyoruz ki
            // sınırda gidip gelen bir hedef state'i her karar tick'inde çevirmesin.
            float radius = engaged ? settings.AggroExitRadius : settings.AggroEnterRadius;
            float sqr = (target.Position - self.Position).sqrMagnitude;

            if (sqr <= radius * radius)
            {
                int mine   = self.Stats.SwordCount;
                int theirs = target.Stats.SwordCount;
                int margin = settings.SwordAdvantageMargin;

                if (mine >= theirs + margin) return AIStateId.Chase;
                if (mine <= theirs - margin) return AIStateId.Flee;

                // Kararsız bölge: zaten angaje isek kararı KORU (titreme engeli),
                // değilsek saldırgan davran.
                return engaged ? bb.State : AIStateId.Chase;
            }
        }

        if (bb.Collectible != null && !bb.Collectible.IsConsumed)
            return AIStateId.SeekCollectible;

        return AIStateId.Roam;
    }
}
```

> **Neden histerezis şart:** `SwordAdvantageMargin` olmadan iki karakterin kılıç
> sayısı eşitlendiğinde AI her karar tick'inde Chase↔Flee arasında salınır ve
> yerinde titrer. Yarıçap histerezisi aynı sorunu mesafe ekseninde çözer.

### 7.3 DÜZENLE: AI/AIBrainProvider.cs

`_states` sözlüğüne üç kayıt ekle:
```csharp
_states = new Dictionary<AIStateId, IDirectionProvider>(4)
{
    [AIStateId.Roam]  = new RoamDirectionProvider(_blackboard, settings),
    [AIStateId.Chase] = new SeekDirectionProvider(
                            _blackboard, SeekSubject.Enemy, approach: true),
    [AIStateId.Flee]  = new SeekDirectionProvider(
                            _blackboard, SeekSubject.Enemy, approach: false),
    [AIStateId.SeekCollectible] = new SeekDirectionProvider(
                            _blackboard, SeekSubject.Collectible, approach: true)
};

_current = _states[AIStateId.Roam];      // Chase → Roam
```

`Initialize()` içinde:
```csharp
// SİL: _blackboard.State = AIStateId.Chase;   (Reset() zaten Roam yazıyor)
_current = _states[AIStateId.Roam];      // Chase → Roam
```

`Update()` içindeki geçici guard'ı **kaldır**:
```csharp
// ESKİ: if (next != _blackboard.State && _states.ContainsKey(next))
if (next != _blackboard.State)
{
    _blackboard.State = next;
    _current = _states[next];
}
```

### 7.4 Kabul testleri

| Test | Beklenen |
|---|---|
| Kaçma | Kılıç toplayıp güçlen → zayıf AI'lar kaçıyor |
| Dönüş | AI collectible toplayıp güçlenince **geri dönüp saldırıyor** |
| Roam | Menzilde kimse yokken AI amaçsız dolaşıyor (yavaş) |
| Collectible arama | Hedefsiz AI en yakın collectible'a gidiyor |
| **Titreme yok** | İki karakter eşit kılıçla → biri kararında kalıyor, salınmıyor |
| **Sınır titremesi yok** | AI aggro sınırında ileri-geri gitmiyor |
| Restart | R → hepsi Roam'da başlıyor |
| **Allocation** | 10 kez R → **0 `Instantiate`, 0 `Destroy`** |

### 7.5 Ayar turu

Bu faz **kod değil ayar** işi. Sırayla:

1. `SwordAdvantageMargin` — 1 ile başla; AI çok kararsızsa artır
2. `AggroExitRadius` — `Enter`'ın ~1.3 katı iyi bir başlangıç
3. `DecisionInterval` — 0.25 akıcı; 0.5 daha "düşünen" ama tepkisiz
4. `RoamRadius` / `RoamRepathInterval` — dolaşma ne kadar geniş

### 7.6 Opsiyonel tek satır

"Kaçarken collectible topla" — `DefaultAIDecider` içinde:
```csharp
if (mine <= theirs - margin)
    return bb.Collectible != null ? AIStateId.SeekCollectible : AIStateId.Flee;
```

> Dikkat: collectible kovalayanın **arkasındaysa** AI düşmana doğru koşar.
> `CollectibleSeekRadius`'u daraltmak gerekebilir — kodladıktan sonra
> hissederek ayarlanacak bir şey.

---

## 8. Nihai Klasör Yapısı

```
Scripts/
├── Core/
│   ├── Pool.cs                                  [Faz 2]
│   └── GameManager.cs                           [Faz 1, düz: 2,3,4,5a]
├── Spawn/
│   ├── SpawnCategory.cs                         [Faz 1]
│   ├── SpawnMapView.cs                          [Faz 1]
│   └── SpawnMap.cs                              [Faz 1]
├── Interaction/
│   ├── IInteractionEntity.cs                    [Faz 3]
│   ├── InteractionBody.cs                       [Faz 3]
│   ├── InteractionReport.cs                     [Faz 3]
│   ├── IInteractionRule.cs                      [Faz 3]
│   └── InteractionResolver.cs                   [Faz 3]
├── Combat/
│   ├── ICombatant.cs                            [Faz 3]
│   ├── SwordVsSwordRule.cs                      [Faz 3]
│   └── SwordVsCharacterRule.cs                  [Faz 3]
├── Character/
│   ├── Character.cs                             [Faz 1, düz: 2,3]
│   ├── CharacterStats.cs                        [Faz 1, düz: 2]
│   ├── CharacterDefinition.cs                   [Faz 1, düz: 2,5a]
│   ├── CharacterView.cs                         [Faz 1, düz: 3]
│   ├── CharacterRegistry.cs                     [Faz 1, düz: 3]
│   ├── CharacterFactory.cs                      [Faz 1, düz: 2,3,5a]
│   └── MovementSimulator.cs                     [Faz 1]
├── Input/
│   ├── JoystickInput.cs                         [Faz 1]
│   ├── IDirectionProvider.cs                    [Faz 1]
│   ├── JoystickDirectionProvider.cs             [Faz 1]
│   └── NullDirectionProvider.cs                 [Faz 1]
├── AI/
│   ├── TargetFinder.cs                          [Faz 5a]
│   ├── AIBlackboard.cs                          [Faz 5a]
│   ├── AISettings.cs                            [Faz 5a]
│   ├── IAIDecider.cs                            [Faz 5a]
│   ├── DefaultAIDecider.cs                      [Faz 5a, düz: 5b]
│   ├── AIBrainProvider.cs                       [Faz 5a, düz: 5b]
│   └── States/
│       ├── SeekDirectionProvider.cs             [Faz 5a]
│       └── RoamDirectionProvider.cs             [Faz 5b]
├── Abilities/
│   ├── IAbility.cs                              [Faz 2]
│   ├── SwordRingAbility.cs                      [Faz 2]
│   ├── Sword.cs                                 [Faz 2, düz: 3]
│   └── SwordView.cs                             [Faz 2, düz: 3]
└── Collectibles/
    ├── SwordCollectible.cs                      [Faz 4]
    ├── SwordCollectibleView.cs                  [Faz 4]
    ├── SwordCollectibleSpawner.cs               [Faz 4]
    └── SwordPickupRule.cs                       [Faz 4]
```

---

## 9. İzlenebilirlik Matrisi

**Atlama kontrolü buradan yapılır.** 40 dosyanın hepsi listede.

| Dosya | Doğar | Düzenlenir | Final |
|---|:---:|:---:|:---:|
| `Core/Pool.cs` | 2 | — | **2** |
| `Core/GameManager.cs` | 1 | 2, 3, 4, 5a | **5a** |
| `Spawn/SpawnCategory.cs` | 1 | — | **1** |
| `Spawn/SpawnMapView.cs` | 1 | — | **1** |
| `Spawn/SpawnMap.cs` | 1 | — | **1** |
| `Interaction/IInteractionEntity.cs` | 3 | — | **3** |
| `Interaction/InteractionBody.cs` | 3 | — | **3** |
| `Interaction/InteractionReport.cs` | 3 | — | **3** |
| `Interaction/IInteractionRule.cs` | 3 | — | **3** |
| `Interaction/InteractionResolver.cs` | 3 | — | **3** |
| `Combat/ICombatant.cs` | 3 | — | **3** |
| `Combat/SwordVsSwordRule.cs` | 3 | — | **3** |
| `Combat/SwordVsCharacterRule.cs` | 3 | — | **3** |
| `Character/Character.cs` | 1 | 2, 3 | **3** |
| `Character/CharacterStats.cs` | 1 | 2 | **2** |
| `Character/CharacterDefinition.cs` | 1 | 2, 5a | **5a** |
| `Character/CharacterView.cs` | 1 | 3 | **3** |
| `Character/CharacterRegistry.cs` | 1 | 3 | **3** |
| `Character/CharacterFactory.cs` | 1 | 2, 3, 5a | **5a** |
| `Character/MovementSimulator.cs` | 1 | — | **1** |
| `Input/JoystickInput.cs` | 1 | — | **1** |
| `Input/IDirectionProvider.cs` | 1 | — | **1** |
| `Input/JoystickDirectionProvider.cs` | 1 | — | **1** |
| `Input/NullDirectionProvider.cs` | 1 | — | **1** |
| `AI/TargetFinder.cs` | 5a | — | **5a** |
| `AI/AIBlackboard.cs` | 5a | — | **5a** |
| `AI/AISettings.cs` | 5a | — | **5a** |
| `AI/IAIDecider.cs` | 5a | — | **5a** |
| `AI/DefaultAIDecider.cs` | 5a | 5b | **5b** |
| `AI/AIBrainProvider.cs` | 5a | 5b | **5b** |
| `AI/States/SeekDirectionProvider.cs` | 5a | — | **5a** |
| `AI/States/RoamDirectionProvider.cs` | 5b | — | **5b** |
| `Abilities/IAbility.cs` | 2 | — | **2** |
| `Abilities/SwordRingAbility.cs` | 2 | — | **2** |
| `Abilities/Sword.cs` | 2 | 3 | **3** |
| `Abilities/SwordView.cs` | 2 | 3 | **3** |
| `Collectibles/SwordCollectible.cs` | 4 | — | **4** |
| `Collectibles/SwordCollectibleView.cs` | 4 | — | **4** |
| `Collectibles/SwordCollectibleSpawner.cs` | 4 | — | **4** |
| `Collectibles/SwordPickupRule.cs` | 4 | — | **4** |

**Doğum sayısı:** 15 + 5 + 8 + 4 + 7 + 1 = **40** ✓
**Düzenleme sayısı:** 0 + 5 + 7 + 1 + 3 + 2 = **18** ✓

### Faz sonu doğrulama

| Faz | Doğması gereken | Düzenlenmesi gereken |
|---|---|---|
| 1 | 15 dosya | — |
| 2 | 5 dosya | `CharacterStats`, `CharacterDefinition`, `Character`, `CharacterFactory`, `GameManager` |
| 3 | 8 dosya | `CharacterView`, `SwordView`, `Sword`, `Character`, `CharacterRegistry`, `CharacterFactory`, `GameManager` |
| 4 | 4 dosya | `GameManager` |
| 5a | 7 dosya | `CharacterDefinition`, `CharacterFactory`, `GameManager` |
| 5b | 1 dosya | `DefaultAIDecider`, `AIBrainProvider` |

Sayılar tutmuyorsa ya bir yapı atlanmış ya mimariden sapılmıştır.

---

## 10. Her Fazda Geçerli Regresyon Testi

Bu testler **her fazın sonunda** tekrar edilir. Biri bozulursa faz bitmemiştir.

| Test | Nasıl |
|---|---|
| **Retry allocation** | Profiler açık, 10 kez R → 0 `Instantiate`, 0 `Destroy` |
| **Retry düzeni** | Karakterler her retry'da aynı noktalarda |
| **Sızıntı** | 20 kez R → Hierarchy obje sayısı sabit, `Pool.IdleCount` sabit |
| **Grep** (Faz 3'ten itibaren) | `Initialize()` gövdelerinde `Instantiate` ara → sıfır sonuç |
| **Katman** (Faz 4'ten itibaren) | `Interaction/` içinde `Damage`, `Sword`, `Character` ara → sıfır sonuç |
| **Sistem** (Faz 4'ten itibaren) | `SwordRingAbility`, `Sword`, `SwordView` içinde `Collectible` ara → sıfır sonuç |

---

## 11. Kapsam Dışı

Aşağıdakiler **hiçbir faza dahil değildir.** Sinyali görülünce ayrıca ele alınır.

| Sinyal | Eklenecek |
|---|---|
| İki modifier aynı stat'ı büküyor | `Stat` + `ModifierHandle` |
| Ability kendini kaldırıyor (süreli buff) | `IExpirable` + sweep deseni |
| Ability'ye buton lazım (dash, attack) | `ICommandSource` |
| Sahibinden uzun yaşayan entity (mermi) | Bağımsız tick listesi |
| Takım gerektiren mod (co-op, sürü) | `ICombatant`'a `int TeamId` + kurallara guard |
| İkinci hasar alabilen tip | `IDamageable` |
| İkinci hedef seçim stratejisi | `ITargetSelector` |
| İkinci collectible türü | Ortak `ICollectible` + generic spawner |
| AI kişilikleri (Passive/Aggressive/Smart/Dumb) | `IAIDecider` implementasyonları |
| Gerçek map sistemi | `SpawnMapView` yerine veri kaynaklı besleyici |
| Ölen düşman geri gelsin | Respawn timer'ı — roster zaten hazır |
| Dinamik dalgalar | `Pool<Character>` |
| DI container | `Compose()` içindeki `new` satırları silinir |
| Addressables | `Compose()` önüne async bootstrap |
| 500+ entity | Spatial hash |

**Faz planına özel notlar:**

- `Enemy_Dummy` definition'ı Faz 3'ten sonra da durabilir — hasar/ölüm
  regresyonunu izole test etmek için faydalı.
- Faz 5a'daki `ContainsKey` guard'ı ve `_blackboard.State = AIStateId.Chase`
  satırı **geçicidir**, 5b'de kaldırılır.

---

## Ek A — Düzenlenen Dosyaların NİHAİ Hâli

Yukarıdaki `DÜZENLE` bölümleri adım adım ne ekleneceğini anlatıyor.
Bu ek, o dosyaların **tüm fazlar bittikten sonraki tam hâlini** veriyor.

**Kullanımı:** bir fazı bitirdikten sonra, o fazda `Final` sütunu işaretli olan
dosyayı buradaki kodla karşılaştır. Farklıysa diff yanlış uygulanmıştır.

| Dosya | Hangi fazdan sonra bu hâli almalı |
|---|---|
| `CharacterStats.cs` | Faz 2 |
| `Character.cs` | Faz 3 |
| `CharacterView.cs` | Faz 3 |
| `CharacterRegistry.cs` | Faz 3 |
| `Sword.cs` | Faz 3 |
| `SwordView.cs` | Faz 3 |
| `CharacterDefinition.cs` | Faz 5a |
| `CharacterFactory.cs` | Faz 5a |
| `GameManager.cs` | Faz 5a |
| `DefaultAIDecider.cs` | Faz 5b |
| `AIBrainProvider.cs` | Faz 5b |

---

### A.1 `Character/CharacterStats.cs` — final (Faz 2 sonrası)

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
    public float NeutralizeDuration;

    /// <summary>
    /// INITIALIZE fazı. Yeni nesne ÜRETMİYOR — alanların üzerine yazıyor.
    /// Retry'da allocation olmaması için gerekli. SO'dan okur, SO'ya asla yazmaz.
    /// </summary>
    public void ResetFrom(CharacterDefinition def)
    {
        MoveSpeed          = def.MoveSpeed;
        Acceleration       = def.Acceleration;
        Deceleration       = def.Deceleration;
        MaxHealth          = def.MaxHealth;

        SwordCount         = def.SwordCount;
        MaxSwordCount      = def.MaxSwordCount;
        OrbitRadius        = def.OrbitRadius;
        OrbitAngularSpeed  = def.OrbitAngularSpeed;
        SwordDamage        = def.SwordDamage;
        NeutralizeDuration = def.NeutralizeDuration;
    }
}
```

---

### A.2 `Character/CharacterView.cs` — final (Faz 3 sonrası)

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

    public Rigidbody2D Body     => _body;
    public Animator    Animator => _animator;

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

### A.3 `Character/Character.cs` — final (Faz 3 sonrası)

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

    private IDirectionProvider _directionProvider;
    private float _health;

    public IInteractionEntity Root => this;      // kökü kendisi

    /// <summary>Sahada mı? Roster'da bekleyen ölü karakterler için false.</summary>
    public bool IsSpawned { get; private set; }
    public bool IsAlive => IsSpawned && _health > 0f;

    public CharacterView     View     => _view;
    public CharacterStats    Stats    { get; }
    public MovementSimulator Movement { get; }

    public Vector2 Position => Movement.Position;

    public event Action<Character> Died;

    // ================= CREATE =================
    public Character(CharacterView view, CharacterDefinition definition,
                     InteractionResolver resolver)
    {
        _view       = view;
        _definition = definition;
        _resolver   = resolver;

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

### A.4 `Character/CharacterRegistry.cs` — final (Faz 3 sonrası)

```csharp
using System.Collections.Generic;

/// <summary>
/// Sadece bookkeeping. Roster'ı TUTMUYOR — o factory'nin işi.
/// Bu liste "şu an sahada olanlar".
/// </summary>
public sealed class CharacterRegistry
{
    private readonly List<Character> _active  = new(64);
    private readonly List<Character> _pending = new(8);

    public IReadOnlyList<Character> Active         => _active;
    public IReadOnlyList<Character> PendingRemoval => _pending;

    public void Add(Character character)
    {
        _active.Add(character);
        character.Died += OnDied;
    }

    public void Remove(Character character)
    {
        character.Died -= OnDied;
        _active.Remove(character);
    }

    private void OnDied(Character character)
    {
        if (_pending.Contains(character)) return;   // aynı adımda çift ölüm sinyali
        _pending.Add(character);
    }

    public void ClearPending() => _pending.Clear();

    public void Deinitialize()
    {
        for (int i = 0; i < _active.Count; i++)
            _active[i].Died -= OnDied;

        _active.Clear();
        _pending.Clear();
    }
}
```

---

### A.5 `Abilities/SwordView.cs` — final (Faz 3 sonrası)

```csharp
using UnityEngine;

public sealed class SwordView : InteractionBody
{
    [SerializeField] private Rigidbody2D    _body;
    [SerializeField] private Collider2D     _hitCollider;
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private Color          _neutralizedTint = new(1f, 1f, 1f, 0.35f);

    public Rigidbody2D Body => _body;

    private void Reset()
    {
        _body = GetComponent<Rigidbody2D>();
        _body.bodyType      = RigidbodyType2D.Kinematic;
        _body.interpolation = RigidbodyInterpolation2D.Interpolate;
        _body.useFullKinematicContacts = true;    // kinematic↔kinematic için ŞART

        _hitCollider = GetComponent<Collider2D>();
        _sprite      = GetComponentInChildren<SpriteRenderer>();
    }

    // Sadece Enter — Stay YOK. Kılıç dönüyor: girer, vurur, çıkar, tekrar girer.
    private void OnTriggerEnter2D(Collider2D other) => ReportContact(other);

    public void SetNeutralized(bool value)
    {
        if (_hitCollider != null) _hitCollider.enabled = !value;
        if (_sprite != null) _sprite.color = value ? _neutralizedTint : Color.white;
    }
}
```

---

### A.6 `Abilities/Sword.cs` — final (Faz 3 sonrası)

```csharp
using UnityEngine;

public enum SwordState { Active, Neutralized }

public sealed class Sword : ICombatant
{
    private readonly SwordView           _view;
    private readonly InteractionResolver _resolver;

    private Character _owner;
    private float _recoveryTimer;

    /// <summary>CREATE fazı — havuzun create fonksiyonu çağırıyor.</summary>
    public Sword(SwordView view, InteractionResolver resolver)
    {
        _view     = view;
        _resolver = resolver;
    }

    public IInteractionEntity Root => _owner;      // kökü sahibi olan karakter

    public SwordState State { get; private set; }
    public SwordView  View  => _view;

    public float Damage             => _owner != null ? _owner.Stats.SwordDamage        : 0f;
    public float NeutralizeDuration => _owner != null ? _owner.Stats.NeutralizeDuration : 1f;

    public void Initialize(Character owner)
    {
        _owner = owner;
        State  = SwordState.Active;
        _recoveryTimer = 0f;

        _view.gameObject.SetActive(true);
        _view.SetNeutralized(false);
        _view.Bind(this, _resolver);
    }

    public void FixedUpdate(float deltaTime)
    {
        if (State != SwordState.Neutralized) return;

        _recoveryTimer -= deltaTime;
        if (_recoveryTimer > 0f) return;

        State = SwordState.Active;
        _view.SetNeutralized(false);          // collider tekrar açılır
    }

    public void Neutralize(float duration)
    {
        if (State == SwordState.Neutralized)
        {
            _recoveryTimer = Mathf.Max(_recoveryTimer, duration);   // refresh, stack değil
            return;
        }

        State = SwordState.Neutralized;
        _recoveryTimer = duration;
        _view.SetNeutralized(true);           // collider kapanır → rapor üretmez
    }

    public void MoveTo(Vector2 position, float angleRad)
    {
        _view.Body.MovePosition(position);
        _view.Body.MoveRotation(angleRad * Mathf.Rad2Deg - 90f);   // sprite yukarı bakıyorsa
    }

    public void Deinitialize()
    {
        _view.Unbind();
        _view.Body.linearVelocity = Vector2.zero;
        _view.SetNeutralized(false);
        _view.gameObject.SetActive(false);

        _owner = null;
        State  = SwordState.Active;
        _recoveryTimer = 0f;
    }
}
```

---

### A.7 `Character/CharacterDefinition.cs` — final (Faz 5a sonrası)

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
    public int   SwordCount         = 3;
    public int   MaxSwordCount      = 12;
    public float OrbitRadius        = 1.5f;
    public float OrbitAngularSpeed  = 120f;
    public float SwordDamage        = 10f;
    public float NeutralizeDuration = 1.5f;

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
        AggroExitRadius      = Mathf.Max(AggroExitRadius, AggroEnterRadius);
        SwordAdvantageMargin = Mathf.Max(1, SwordAdvantageMargin);
    }
}
```

---

### A.8 `Character/CharacterFactory.cs` — final (Faz 5a sonrası)

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Roster'ın sahibi. CREATE'te üretir, INITIALIZE'da yerleştirir, DISPOSE'da yok eder.
/// Ölüm = yok etme DEĞİL, deaktive etme.
/// </summary>
public sealed class CharacterFactory : IDisposable
{
    private readonly CharacterRegistry       _registry;
    private readonly InteractionResolver     _resolver;
    private readonly SpawnMap                _map;
    private readonly JoystickInput           _joystick;
    private readonly Pool<Sword>             _swordPool;
    private readonly TargetFinder            _targetFinder;
    private readonly SwordCollectibleSpawner _collectibles;

    private readonly List<Character> _enemies = new(32);
    private Character _player;

    // ================= CREATE =================
    // Roster burada doğuyor. Bundan sonra hiçbir karakter Instantiate edilmiyor.
    public CharacterFactory(CharacterRegistry registry, InteractionResolver resolver,
                            SpawnMap map, JoystickInput joystick, Pool<Sword> swordPool,
                            TargetFinder targetFinder, SwordCollectibleSpawner collectibles,
                            CharacterDefinition playerDefinition,
                            CharacterDefinition enemyDefinition, int enemyCount)
    {
        _registry     = registry;
        _resolver     = resolver;
        _map          = map;
        _joystick     = joystick;
        _swordPool    = swordPool;
        _targetFinder = targetFinder;
        _collectibles = collectibles;

        _player = CreateCharacter(playerDefinition);

        for (int i = 0; i < enemyCount; i++)
            _enemies.Add(CreateCharacter(enemyDefinition));
    }

    private Character CreateCharacter(CharacterDefinition definition)
    {
        CharacterView view = UnityEngine.Object.Instantiate(definition.ViewPrefab);

        var character = new Character(view, definition, _resolver);

        // Provider karakteri referans alabilir → önce karakter, sonra provider
        character.SetDirectionProvider(CreateProvider(definition, character));

        if (definition.HasSwordRing)
            character.AddAbility(new SwordRingAbility(_swordPool));

        return character;
    }

    private IDirectionProvider CreateProvider(CharacterDefinition definition, Character self)
    {
        switch (definition.BrainType)
        {
            case CharacterBrainType.Player:
                return new JoystickDirectionProvider(_joystick);

            case CharacterBrainType.AI:
                return new AIBrainProvider(
                    self,
                    new DefaultAIDecider(),
                    _targetFinder,
                    _collectibles,
                    AISettings.CreateFrom(definition));

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

### A.9 `Core/GameManager.cs` — final (Faz 5a sonrası)

```csharp
using System.Collections.Generic;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    [Header("Definitions")]
    [SerializeField] private CharacterDefinition _playerDefinition;
    [SerializeField] private CharacterDefinition _enemyDefinition;

    [Header("Prefabs")]
    [SerializeField] private SwordView            _swordPrefab;
    [SerializeField] private SwordCollectibleView _collectiblePrefab;

    [Header("Scene")]
    [SerializeField] private SpawnMapView  _spawnMapView;
    [SerializeField] private JoystickInput _joystick;
    [SerializeField] private int _enemyCount = 8;
    [SerializeField] private int _spawnSeed  = 12345;

    [Header("Collectibles")]
    [SerializeField] private SwordCollectibleSpawnSettings _collectibleSettings;

    [Header("Pooling")]
    [SerializeField] private int _swordPrewarm       = 48;
    [SerializeField] private int _collectiblePrewarm = 8;

    private SpawnMap                _map;
    private CharacterRegistry       _characters;
    private InteractionResolver     _resolver;
    private Pool<Sword>             _swordPool;
    private SwordCollectibleSpawner _collectibles;
    private CharacterFactory        _factory;

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

    /// <summary>Yenildik → tekrar oyna. Tek bir Instantiate yok.</summary>
    public void Restart()
    {
        Deinitialize();
        Initialize();
    }

    // ================= CREATE =================
    // Buradaki her `new` satırı bir DI container'a devredilebilir.
    private void Compose()
    {
        _map        = new SpawnMap(_spawnMapView, _spawnSeed);
        _characters = new CharacterRegistry();
        _resolver   = new InteractionResolver();

        _swordPool = new Pool<Sword>(
            create:  CreateSword,
            destroy: sword => Destroy(sword.View.gameObject),
            prewarm: _swordPrewarm);

        _collectibles = new SwordCollectibleSpawner(
            _collectiblePrefab, _resolver, _map, _collectibleSettings, _collectiblePrewarm);

        // Kurallar DIŞARIDAN kaydediliyor — interaction katmanı combat'ı tanımıyor.
        _resolver.AddRule(new SwordVsSwordRule());
        _resolver.AddRule(new SwordVsCharacterRule());
        _resolver.AddRule(new SwordPickupRule());

        var targetFinder = new TargetFinder(_characters);

        _factory = new CharacterFactory(
            _characters, _resolver, _map, _joystick, _swordPool,
            targetFinder, _collectibles,
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
        _map.Initialize();            // cursor sıfırla, shuffle bag karıştır
        _factory.Initialize();        // roster noktalara yerleşir, stat'lar base'e döner
        _collectibles.Initialize();   // timer sıfır, sahada collectible yok
    }

    // ================= DEINITIALIZE =================
    public void Deinitialize()
    {
        _factory.Deinitialize();      // karakterler deaktif, kılıçlar havuza
        _collectibles.Deinitialize(); // collectible'lar havuza
        _characters.Deinitialize();
        _map.Deinitialize();
    }

    // ================= DISPOSE =================
    private void Dispose()
    {
        // Sıra önemli: factory/spawner önce havuzlara iade etsin, sonra havuz boşalsın.
        _factory?.Dispose();
        _collectibles?.Dispose();
        _swordPool?.Dispose();
        _resolver?.Dispose();

        _factory      = null;
        _collectibles = null;
        _swordPool    = null;
        _resolver     = null;
        _characters   = null;
        _map          = null;
    }

    // ================= TICK =================
    private void Update()
    {
        // Ölümler fizik callback'lerinde tetikleniyor; bu frame'in tüm fizik
        // adımları bittikten sonra süpürüyoruz.
        DespawnPending();

        float dt = Time.deltaTime;

        _collectibles.Update(dt);

        IReadOnlyList<Character> active = _characters.Active;
        for (int i = 0; i < active.Count; i++)
            active[i].Update(dt);

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R)) Restart();   // reset demosu
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
            _factory.Despawn(pending[i]);       // Destroy DEĞİL — deaktive eder

        _characters.ClearPending();
    }
}
```

---

### A.10 `AI/DefaultAIDecider.cs` — final (Faz 5b sonrası)

```csharp
public sealed class DefaultAIDecider : IAIDecider
{
    public AIStateId Decide(AIBlackboard bb, in AISettings settings)
    {
        Character self   = bb.Self;
        Character target = bb.Target;

        bool engaged = bb.State == AIStateId.Chase || bb.State == AIStateId.Flee;

        if (target != null && target.IsAlive)
        {
            // HİSTEREZİS: angaje olduğumuzda daha geniş yarıçapla ölçüyoruz ki
            // sınırda gidip gelen bir hedef state'i her karar tick'inde çevirmesin.
            float radius = engaged ? settings.AggroExitRadius : settings.AggroEnterRadius;
            float sqr = (target.Position - self.Position).sqrMagnitude;

            if (sqr <= radius * radius)
            {
                int mine   = self.Stats.SwordCount;
                int theirs = target.Stats.SwordCount;
                int margin = settings.SwordAdvantageMargin;

                if (mine >= theirs + margin) return AIStateId.Chase;
                if (mine <= theirs - margin) return AIStateId.Flee;

                // Kararsız bölge: zaten angaje isek kararı KORU (titreme engeli),
                // değilsek saldırgan davran.
                return engaged ? bb.State : AIStateId.Chase;
            }
        }

        if (bb.Collectible != null && !bb.Collectible.IsConsumed)
            return AIStateId.SeekCollectible;

        return AIStateId.Roam;
    }
}
```

---

### A.11 `AI/AIBrainProvider.cs` — final (Faz 5b sonrası)

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kendisi de bir IDirectionProvider. Composite pattern —
/// Direction'ı saf okuma yaptığımız için sorunsuz iç içe geçiyor.
/// </summary>
public sealed class AIBrainProvider : IDirectionProvider
{
    private readonly AIBlackboard _blackboard = new();
    private readonly Dictionary<AIStateId, IDirectionProvider> _states;

    private readonly IAIDecider              _decider;
    private readonly TargetFinder            _targetFinder;
    private readonly SwordCollectibleSpawner _collectibles;
    private readonly AISettings              _settings;

    private IDirectionProvider _current;
    private float _decisionTimer;

    public Vector2 Direction => _current.Direction;

    // ================= CREATE =================
    public AIBrainProvider(Character self, IAIDecider decider,
                           TargetFinder targetFinder, SwordCollectibleSpawner collectibles,
                           in AISettings settings)
    {
        _decider      = decider;
        _targetFinder = targetFinder;
        _collectibles = collectibles;
        _settings     = settings;

        _blackboard.Self = self;

        _states = new Dictionary<AIStateId, IDirectionProvider>(4)
        {
            [AIStateId.Roam]  = new RoamDirectionProvider(_blackboard, settings),
            [AIStateId.Chase] = new SeekDirectionProvider(
                                    _blackboard, SeekSubject.Enemy, approach: true),
            [AIStateId.Flee]  = new SeekDirectionProvider(
                                    _blackboard, SeekSubject.Enemy, approach: false),
            [AIStateId.SeekCollectible] = new SeekDirectionProvider(
                                    _blackboard, SeekSubject.Collectible, approach: true)
        };

        _current = _states[AIStateId.Roam];
    }

    // ================= INITIALIZE =================
    public void Initialize()
    {
        _blackboard.Reset();                          // State = Roam yazıyor
        _decisionTimer = 0f;
        _current = _states[AIStateId.Roam];

        foreach (IDirectionProvider state in _states.Values)
            state.Initialize();
    }

    public void Update(float deltaTime)
    {
        _decisionTimer -= deltaTime;

        if (_decisionTimer <= 0f)
        {
            _decisionTimer = _settings.DecisionInterval;

            RefreshBlackboard();

            AIStateId next = _decider.Decide(_blackboard, _settings);
            if (next != _blackboard.State)
            {
                _blackboard.State = next;
                _current = _states[next];
            }
        }

        // Karar aralıklı, hareket her frame — hedef pozisyonu canlı okunduğu için
        // kovalama akıcı kalıyor.
        _current.Update(deltaTime);
    }

    private void RefreshBlackboard()
    {
        _blackboard.Target = _targetFinder.FindNearest(_blackboard.Self);

        _blackboard.Collectible = _collectibles.FindNearest(
            _blackboard.Self.Position, _settings.CollectibleSeekRadius);
    }

    // ================= DEINITIALIZE =================
    public void Deinitialize()
    {
        _blackboard.Reset();
        _decisionTimer = 0f;

        foreach (IDirectionProvider state in _states.Values)
            state.Deinitialize();
    }
}
```

---

## Ek B — Uygulayıcı İçin Kontrol Listesi

Bir agent'a veya kendine bu dokümanı verirken:

1. **Fazları atlama.** Her fazın kabul testi geçmeden sonrakine geçme.
2. **`DÜZENLE` bölümleri diff'tir.** Uyguladıktan sonra Ek A'daki nihai hâlle
   karşılaştır (o dosya için `Final` fazı geldiyse).
3. **`Initialize()` gövdelerine asla `Instantiate` yazma.** Tek kural bu.
4. **Ölümde `Destroy` çağırma.** `Despawn` sadece `SetActive(false)` yapar.
5. **`Dispose` sırası:** önce factory/spawner (havuza iade), sonra havuz (yok et).
6. **Layer matrisini elle ayarla** — kod bunu yapmaz.
7. **Prefab'larda `Reset()` metodu** doğru ayarları yazar ama sadece component
   ilk eklendiğinde çalışır; var olan prefab'larda elle doğrula.
