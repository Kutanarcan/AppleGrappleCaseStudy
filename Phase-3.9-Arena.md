# Faz 3.9 — Arena

**Faz 3.75 bitti, Faz 4'e geçmeden önce.**

Elle yerleştirilen spawn noktaları kalkıyor, yerine hesaplama geliyor.

## Değişim yüzeyi

| | Dosya |
|---|---|
| **Silinen (1)** | `Spawn/SpawnMapView.cs` |
| **Yeni (3)** | `Arena/Arena.cs`, `Arena/ArenaView.cs`, `Arena/ArenaBorderSpawner.cs` |
| **Düzenlenen (3)** | `Spawn/SpawnCategory.cs`, `Spawn/SpawnMap.cs`, `Core/GameManager.cs` |

**Dokunulmayanlar:** `CharacterFactory`, `SwordCollectibleSpawner`, `RoamDirectionProvider`
ve diğer her şey. `SpawnMap.Next(category)` API'si aynı kaldı — değişen tek şey
noktaların **nereden geldiği**.

---

## 0. Tasarım

### 0.1 İki hesaplama yöntemi

| Kategori | Yöntem |
|---|---|
| `Player`, `Enemy` | **Polygonal** — merkez etrafında düzgün çokgen |
| `Collectible`, `Prop` | **Random** — Min/Max içinde herhangi bir nokta |

`SpawnPick` enum'u siliniyor: yöntem kategoriye bağlı, ayrıca yapılandırılmıyor.
`Next()` içindeki switch yeterli.

Random'da üst üste binme sorun değil — collectible ve prop için kabul edilebilir.

### 0.2 Polygon yarıçapı N ile ölçekleniyor

Sabit yarıçap kullanırsan **oyun, kılıç halkaları birbirine girmiş halde başlar.**

N nokta, R yarıçaplı çemberde komşu mesafesi `2R·sin(π/N)`.
Bunu `MinCharacterSeparation`'a eşitleyen yarıçap:

```
R = MinCharacterSeparation / (2·sin(π/N))
```

`OrbitRadius = 1.5`, pay `0.5` → `MinCharacterSeparation = 3.5`:

| N | Gereken R |
|---|---|
| 1 | 0 (merkez) |
| 2 | 1.75 |
| 3 | 2.02 |
| **9** | **5.12** |

Arenaya sığmıyorsa clamp'lenir ve uyarı verilir.

**Bedava gelen:** eski plandaki *"`EnemyPoints.Length ≥ _enemyCount` olmalı"*
kısıtı kayboluyor. Polygon sonsuz slot üretiyor.

### 0.3 Boyutlandırma: scale

`SpriteRenderer` **Simple** modda. Boyut `renderer.size` ile değil
`transform.localScale` ile veriliyor:

```
scale = istenenBoyut / sprite.bounds.size
```

`sprite.bounds.size` = scale 1'deki dünya boyutu.

---

## 1. Yeni Dosyalar

### 1.1 `Arena/ArenaView.cs`

Sahne referanslarını tutan dummy MonoBehaviour.

```csharp
using UnityEngine;

/// <summary>
/// Zemin, mask, duvar collider'ları ve çit prefab referansları.
/// Hesap yapmaz — Arena ne derse onu uygular.
/// </summary>
public sealed class ArenaView : MonoBehaviour
{
    [Header("Zemin (SpriteRenderer — Simple)")]
    [SerializeField] private SpriteRenderer _ground;
    [SerializeField] private SpriteRenderer _mask;

    [Header("Duvar collider'ları (Rigidbody2D YOK)")]
    [SerializeField] private BoxCollider2D _wallTop;
    [SerializeField] private BoxCollider2D _wallBottom;
    [SerializeField] private BoxCollider2D _wallLeft;
    [SerializeField] private BoxCollider2D _wallRight;

    [Header("Çit prefabları (SpriteRenderer — Simple)")]
    [SerializeField] private Transform      _borderRoot;
    [SerializeField] private SpriteRenderer _cornerPrefab;
    [SerializeField] private SpriteRenderer _horizontalPrefab;
    [SerializeField] private SpriteRenderer _verticalPrefab;

    public Transform      BorderRoot       => _borderRoot != null ? _borderRoot : transform;
    public SpriteRenderer CornerPrefab     => _cornerPrefab;
    public SpriteRenderer HorizontalPrefab => _horizontalPrefab;
    public SpriteRenderer VerticalPrefab   => _verticalPrefab;

    /// <summary>
    /// Simple draw mode → boyut SCALE ile veriliyor.
    /// scale = istenenBoyut / sprite.bounds.size
    /// </summary>
    public void SetArenaSize(Vector2 size, float wallThickness)
    {
        ApplyScale(_ground, size);
        ApplyScale(_mask,   size);

        float hw = size.x * 0.5f;
        float hh = size.y * 0.5f;
        float t  = Mathf.Max(0.1f, wallThickness);

        // Collider'lar oyun alanının hemen DIŞINDA
        SetBox(_wallTop,    new Vector2(0f,  hh + t * 0.5f), new Vector2(size.x + 2f * t, t));
        SetBox(_wallBottom, new Vector2(0f, -hh - t * 0.5f), new Vector2(size.x + 2f * t, t));
        SetBox(_wallLeft,   new Vector2(-hw - t * 0.5f, 0f), new Vector2(t, size.y));
        SetBox(_wallRight,  new Vector2( hw + t * 0.5f, 0f), new Vector2(t, size.y));
    }

    private static void ApplyScale(SpriteRenderer renderer, Vector2 size)
    {
        if (renderer == null || renderer.sprite == null) return;

        Vector2 unit = renderer.sprite.bounds.size;      // scale 1'deki dünya boyutu
        if (unit.x <= 0.0001f || unit.y <= 0.0001f) return;

        renderer.transform.localPosition = Vector3.zero;
        renderer.transform.localScale    = new Vector3(size.x / unit.x, size.y / unit.y, 1f);
    }

    private static void SetBox(BoxCollider2D box, Vector2 center, Vector2 size)
    {
        if (box == null) return;

        box.offset = center;
        box.size   = size;
    }
}
```

---

### 1.2 `Arena/ArenaBorderSpawner.cs`

Çit parçalarını hesaplayıp üretiyor. CREATE fazında bir kez.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kenar uzunluğuna göre kaç çit parçası sığdığını hesaplar ve yerleştirir.
/// CREATE fazında bir kez çalışır, Dispose'a kadar durur.
/// </summary>
public sealed class ArenaBorderSpawner : IDisposable
{
    private readonly List<GameObject> _pieces = new(256);

    public ArenaBorderSpawner(ArenaView view, Arena arena)
    {
        if (view == null) return;

        Transform root = view.BorderRoot;

        // --- Köşe direkleri ---
        float inset = HalfWidth(view.CornerPrefab);

        Place(view.CornerPrefab, new Vector2(arena.Min.x, arena.Max.y), root);
        Place(view.CornerPrefab, new Vector2(arena.Max.x, arena.Max.y), root);
        Place(view.CornerPrefab, new Vector2(arena.Min.x, arena.Min.y), root);
        Place(view.CornerPrefab, new Vector2(arena.Max.x, arena.Min.y), root);

        // --- Yatay kenarlar (üst / alt) ---
        FillEdge(view.HorizontalPrefab, root,
                 arena.Min.x + inset, arena.Max.x - inset, arena.Max.y, horizontal: true);
        FillEdge(view.HorizontalPrefab, root,
                 arena.Min.x + inset, arena.Max.x - inset, arena.Min.y, horizontal: true);

        // --- Dikey kenarlar (sol / sağ) ---
        float vInset = HalfHeight(view.CornerPrefab);

        FillEdge(view.VerticalPrefab, root,
                 arena.Min.y + vInset, arena.Max.y - vInset, arena.Min.x, horizontal: false);
        FillEdge(view.VerticalPrefab, root,
                 arena.Min.y + vInset, arena.Max.y - vInset, arena.Max.x, horizontal: false);
    }

    /// <summary>
    /// Parça boyutuna göre adet hesaplar, sonra aralığı TAM dolduracak şekilde
    /// aralığı yeniden böler — kenarda boşluk veya taşma kalmaz.
    /// </summary>
    private void FillEdge(SpriteRenderer prefab, Transform root,
                          float from, float to, float fixedCoord, bool horizontal)
    {
        if (prefab == null || prefab.sprite == null) return;

        Vector2 unit = prefab.sprite.bounds.size;
        float step = horizontal ? unit.x : unit.y;
        if (step <= 0.0001f) return;

        float length = to - from;
        if (length <= 0f) return;

        int count = Mathf.Max(1, Mathf.RoundToInt(length / step));
        float spacing = length / count;

        for (int i = 0; i < count; i++)
        {
            float t = from + spacing * (i + 0.5f);

            Vector2 position = horizontal
                ? new Vector2(t, fixedCoord)
                : new Vector2(fixedCoord, t);

            Place(prefab, position, root);
        }
    }

    private void Place(SpriteRenderer prefab, Vector2 position, Transform root)
    {
        if (prefab == null) return;

        SpriteRenderer instance = UnityEngine.Object.Instantiate(prefab, root);
        instance.transform.position = position;

        _pieces.Add(instance.gameObject);
    }

    private static float HalfWidth(SpriteRenderer r)
        => r != null && r.sprite != null ? r.sprite.bounds.size.x * 0.5f : 0f;

    private static float HalfHeight(SpriteRenderer r)
        => r != null && r.sprite != null ? r.sprite.bounds.size.y * 0.5f : 0f;

    public void Dispose()
    {
        for (int i = 0; i < _pieces.Count; i++)
            if (_pieces[i] != null) UnityEngine.Object.Destroy(_pieces[i]);

        _pieces.Clear();
    }
}
```

---

### 1.3 `Arena/Arena.cs`

```csharp
using System;
using UnityEngine;

/// <summary>
/// Arena geometrisi. Width/Height verilir, zemin ve mask boyutlanır,
/// çitler üretilir. Center / Min / Max herkesin ortak referansı.
/// </summary>
public sealed class Arena : IDisposable
{
    private readonly ArenaBorderSpawner _border;

    public float   Width  { get; }
    public float   Height { get; }
    public Vector2 Center { get; }
    public Vector2 Min    { get; }
    public Vector2 Max    { get; }

    /// <summary>CREATE fazı — boyutlandırma ve çit üretimi burada.</summary>
    public Arena(ArenaView view, float width, float height, float wallThickness)
    {
        Width  = Mathf.Max(1f, width);
        Height = Mathf.Max(1f, height);
        Center = view != null ? (Vector2)view.transform.position : Vector2.zero;

        Vector2 half = new(Width * 0.5f, Height * 0.5f);
        Min = Center - half;
        Max = Center + half;

        view?.SetArenaSize(new Vector2(Width, Height), wallThickness);

        _border = new ArenaBorderSpawner(view, this);
    }

    public Vector2 ClampToBounds(Vector2 point, float margin = 0f)
    {
        return new Vector2(
            Mathf.Clamp(point.x, Min.x + margin, Max.x - margin),
            Mathf.Clamp(point.y, Min.y + margin, Max.y - margin));
    }

    public bool Contains(Vector2 point, float margin = 0f)
        => point.x >= Min.x + margin && point.x <= Max.x - margin
        && point.y >= Min.y + margin && point.y <= Max.y - margin;

    public void Dispose() => _border?.Dispose();
}
```

---

## 2. Düzenlenen Dosyalar

### 2.1 `Spawn/SpawnCategory.cs`

`SpawnPick` **silindi** — yöntem kategoriye bağlı.

```csharp
public enum SpawnCategory
{
    Player,
    Enemy,
    Collectible,
    Prop
}
```

---

### 2.2 `Spawn/SpawnMap.cs`

API aynı (`Next(category)`), içerik hesaplamaya döndü.

```csharp
using UnityEngine;

/// <summary>
/// Spawn noktalarını HESAPLAR — sahnede elle konan Transform yok.
///
///   Player / Enemy  → Polygonal (merkez etrafında düzgün çokgen)
///   Collectible/Prop → Random   (Min/Max içinde, üst üste binebilir)
/// </summary>
public sealed class SpawnMap
{
    private readonly Arena         _arena;
    private readonly System.Random _random;
    private readonly float         _minCharacterSeparation;
    private readonly float         _characterMargin;
    private readonly float         _randomMargin;

    private int _characterCount = 1;
    private int _characterCursor;

    /// <summary>CREATE fazı.</summary>
    public SpawnMap(Arena arena, int seed,
                    float minCharacterSeparation, float characterMargin, float randomMargin)
    {
        _arena                  = arena;
        _random                 = new System.Random(seed);
        _minCharacterSeparation = Mathf.Max(0.1f, minCharacterSeparation);
        _characterMargin        = Mathf.Max(0f, characterMargin);
        _randomMargin           = Mathf.Max(0f, randomMargin);
    }

    /// <summary>
    /// INITIALIZE fazı. Toplam karakter sayısı polygon'un kaç köşeli
    /// olacağını belirliyor — Player ve Enemy AYNI polygonu paylaşıyor.
    /// </summary>
    public void Initialize(int characterCount)
    {
        _characterCount  = Mathf.Max(1, characterCount);
        _characterCursor = 0;
    }

    public Vector2 Next(SpawnCategory category)
    {
        switch (category)
        {
            case SpawnCategory.Player:
            case SpawnCategory.Enemy:
                return NextPolygonal();

            default:
                return NextRandom();
        }
    }

    // ================= POLYGONAL =================
    private Vector2 NextPolygonal()
    {
        if (_characterCount <= 1) return _arena.Center;

        int index = _characterCursor % _characterCount;
        _characterCursor++;

        float radius = CharacterRadius(_characterCount);
        float angle  = index * (360f / _characterCount) * Mathf.Deg2Rad;

        return _arena.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    /// <summary>
    /// Komşu mesafesi = 2R·sin(π/N). Bunu MinCharacterSeparation'a eşitleyen R.
    /// Sığmıyorsa clamp'lenir — kılıç halkaları başlangıçta çakışabilir.
    /// </summary>
    private float CharacterRadius(int total)
    {
        float needed = _minCharacterSeparation / (2f * Mathf.Sin(Mathf.PI / total));
        float max    = Mathf.Min(_arena.Width, _arena.Height) * 0.5f - _characterMargin;

        if (max <= 0f)
        {
            Debug.LogWarning("SpawnMap: CharacterMargin arenadan büyük.");
            return 0f;
        }

        if (needed > max)
        {
            Debug.LogWarning(
                $"SpawnMap: {total} karakter için {needed:F2} yarıçap gerekiyor, {max:F2} var. " +
                $"Kılıç halkaları başlangıçta çakışabilir. " +
                $"Arena boyutunu artır veya MinCharacterSeparation'ı azalt.");
        }

        return Mathf.Min(needed, max);
    }

    // ================= RANDOM =================
    private Vector2 NextRandom()
    {
        Vector2 min = _arena.Min + Vector2.one * _randomMargin;
        Vector2 max = _arena.Max - Vector2.one * _randomMargin;

        return new Vector2(
            Mathf.Lerp(min.x, max.x, (float)_random.NextDouble()),
            Mathf.Lerp(min.y, max.y, (float)_random.NextDouble()));
    }

    public void Deinitialize() => _characterCursor = 0;
}
```

> **`_random` neden `Initialize`'da yeniden yaratılmıyor:** karakterler zaten
> polygonal ve deterministik — her retry'da aynı noktada. Collectible'ların
> her retry'da farklı dizilmesi isteniyor. Ayrıca yeniden yaratmak allocation demek.

---

### 2.3 `Core/GameManager.cs`

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

    [Header("Arena")]
    [SerializeField] private ArenaView _arenaView;
    [SerializeField] private float _arenaWidth     = 22f;
    [SerializeField] private float _arenaHeight    = 16f;
    [SerializeField] private float _wallThickness  = 0.5f;

    [Header("Spawn")]
    [SerializeField] private float _minCharacterSeparation = 3.5f;
    [SerializeField] private float _characterMargin        = 1.5f;
    [SerializeField] private float _randomMargin           = 1.5f;
    [SerializeField] private int   _spawnSeed              = 12345;

    [Header("Scene")]
    [SerializeField] private JoystickInput _joystick;
    [SerializeField] private int _enemyCount = 8;

    [Header("Pooling")]
    [SerializeField] private int _swordPrewarm = 48;

    private Arena               _arena;
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

        // Arena önce: zemin/mask boyutlanır, çitler üretilir, Min/Max belli olur
        _arena = new Arena(_arenaView, _arenaWidth, _arenaHeight, _wallThickness);
        _map   = new SpawnMap(_arena, _spawnSeed,
                              _minCharacterSeparation, _characterMargin, _randomMargin);

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
        _map.Initialize(1 + _enemyCount);      // player + düşmanlar aynı polygonda
        _audio.Initialize();
        _particles.Initialize();
        _factory.Initialize();
    }

    // ================= DEINITIALIZE =================
    public void Deinitialize()
    {
        _factory.Deinitialize();
        _particles.Deinitialize();
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
        _arena?.Dispose();

        _factory    = null;
        _particles  = null;
        _audio      = null;
        _feedback   = null;
        _swordPool  = null;
        _resolver   = null;
        _characters = null;
        _map        = null;
        _arena      = null;
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

> `CharacterFactory` imzası **değişmedi** — hâlâ `SpawnMap` alıyor,
> hâlâ `_map.Next(SpawnCategory.Player)` çağırıyor.

---

## 3. Unity Kurulumu

### 3.1 Sahne

```
Arena                 ← ArenaView.cs
├── Ground            SpriteRenderer (Simple)   sorting order -100
├── Mask              SpriteRenderer (Simple)   sorting order  -90
├── Border            (boş Transform — BorderRoot)
└── Walls             Layer: Environment
    ├── WallTop       BoxCollider2D
    ├── WallBottom    BoxCollider2D
    ├── WallLeft      BoxCollider2D
    └── WallRight     BoxCollider2D
```

`Walls` altında **Rigidbody2D yok** — static collider doğru seçim.

Çit prefabları (`Fence_Corner`, `Fence_Horizontal`, `Fence_Vertical`) `Assets` içinde,
`ArenaView` inspector'ından atanır. Sahnede kopyaları yok — runtime üretiliyor.

### 3.2 Sprite import

| Sprite | px | Mesh Type | PPU | Pivot |
|---|---|---|---|---|
| Ground | — | **Full Rect** | 64 | Center |
| Mask | — | **Full Rect** | 64 | Center |
| fence-connector-horizontal | 28×53 | Full Rect | 64 | **Bottom Center** |
| fence-connector-vertical | 18×28 | Full Rect | 64 | **Bottom Center** |
| fence-corner | 48×86 | Full Rect | 64 | **Bottom Center** |

PPU 64'te: yatay parça **0.44×0.83**, dikey **0.28×0.44**, köşe **0.75×1.34** birim.

> **Full Rect:** Tight mesh şeffaf kenarları kırpar, `sprite.bounds.size` değişir
> ve scale hesabı bozulur.

> **Pivot Bottom Center:** çit sprite'ları perspektifli. Direğin yere değdiği
> nokta pivot olunca kenar çizgisine yerleştirmek yeterli, ekstra offset gerekmez.

### 3.3 Parça sayısı

22×16 arena, PPU 64:

| Kenar | Parça |
|---|---|
| Üst / alt | ~50 ×2 |
| Sol / sağ | ~36 ×2 |
| Köşe | 4 |
| **Toplam** | **~176 GameObject** |

Hepsi CREATE fazında bir kez üretiliyor, statik duruyor, retry'da dokunulmuyor.
Çok gelirse PPU'yu düşür (32 → yarı yarıya azalır) veya çit sprite'ını genişlet.

### 3.4 Y-sorting

Alt duvar karakterlerin **önünde**, üst duvar **arkasında** görünmeli:

```
Project Settings → Graphics
  Transparency Sort Mode = Custom Axis
  Transparency Sort Axis = (0, 1, 0)
```

Düşük Y'deki sprite öne çizilir. Ground ve Mask sabit order ile (−100 / −90)
her zaman en altta kalır.

### 3.5 Layer Collision Matrix

|                 | Character | Sword | Collectible | Environment |
|-----------------|:---------:|:-----:|:-----------:|:-----------:|
| **Character**   |     ✔     |   ✔   |      ✔      |    **✔**    |
| **Sword**       |     ✔     |   ✔   |      ✘      |      ✘      |
| **Collectible** |     ✔     |   ✘   |      ✘      |      ✘      |
| **Environment** |   **✔**   |   ✘   |      ✘      |      —      |

Çit **görselleri** collider taşımıyor — fizik sadece 4 `BoxCollider2D`'de.

---

## 4. Kabul Testleri

| Test | Beklenen |
|---|---|
| **Boyut** | `_arenaWidth/_arenaHeight` değiştir → zemin, mask, çitler, collider'lar uyuyor |
| Zemin/mask | Aynı boyutta, mask üstte |
| **Çit tam oturuyor** | Kenarlarda boşluk veya taşma yok, köşelerde çakışma yok |
| Y-sorting | Alt duvar karakterin önünde, üst duvar arkasında |
| **Duvar fiziksel** | Karakter duvara çarpıyor, dışarı çıkamıyor |
| Kılıç | Kılıçlar duvara takılmıyor |
| N=1 | Karakter merkezde |
| N=2 | İki karakter yatay çizgide |
| N=3 | Üçgen |
| **N=9** | Düzgün 9-gen, kılıç halkaları çakışmıyor |
| **Sığmama uyarısı** | `_arenaWidth = 8` → konsolda uyarı |
| **Determinizm** | 10 kez R → karakterler **aynı** noktalarda |
| Random | Collectible'lar her retry'da farklı yerlerde |
| **Retry allocation** | 10 kez R → 0 `Instantiate`, 0 `Destroy` |
| Sızıntı | 20 kez R → Hierarchy obje sayısı sabit |
| Eski sistem | `SpawnMapView`, `SpawnPick` ara → **sıfır sonuç** |

---

## 5. Tuzaklar

| Tuzak | Belirti | Çözüm |
|---|---|---|
| Sprite `Mesh Type = Tight` | Scale hesabı yanlış, zemin boyutu tutmuyor | **Full Rect** |
| `drawMode = Sliced/Tiled` | `localScale` etkisiz görünüyor | **Simple** |
| Pivot Center | Çit yerin altında/üstünde kalıyor | **Bottom Center** |
| Y-sorting kapalı | Alt duvar karakterin arkasında | `Transparency Sort Axis = (0,1,0)` |
| Collider'a Rigidbody2D | Duvar itiliyor | Rigidbody **yok** |
| İnce duvar | Hızlı karakter duvardan geçiyor | `WallThickness ≥ MoveSpeed × fixedDeltaTime × 3` |
| `Character ↔ Environment` kapalı | Karakter duvarı geçiyor | Matriste **aç** |
| Sabit polygon yarıçapı | Oyun halkalar çakışık başlıyor | `MinSeparation / (2·sin(π/N))` |
| `_map.Initialize()` unutulmuş | Herkes merkezde doğuyor | `GameManager.Initialize`'da `1 + _enemyCount` |
| Çit prefabına collider | Yüzlerce gereksiz collider | Fizik sadece 4 kutuda |

---

## 6. İleri Fazlara Etki

### 6.1 Faz 4 — `SwordCollectibleSpawner`

**Değişiklik yok.** Hâlâ `_map.Next(SpawnCategory.Collectible)` çağırıyor,
artık Random hesaplaması dönüyor.

`Phase-3.75-RingPolish.md` Bölüm B olduğu gibi geçerli.
`GameManager.Compose` içinde spawner'a `_map` geçilmeye devam ediyor.

**İptal olan tek adım:** ana plandaki *"`CollectiblePoints` altına 8–12 boş
Transform koy"* — artık gerek yok.

### 6.2 Faz 5b — `RoamDirectionProvider` (opsiyonel)

Duvarlar geldiği için AI'ın rastgele hedefi arena dışına düşerse duvara yaslanıp
bekler. Tek satırlık düzeltme:

```csharp
// RoamDirectionProvider constructor'ına: Arena arena
// Update() içinde hedef seçiminde:
Vector2 raw = position + Random.insideUnitCircle * _settings.RoamRadius;
_destination = _arena.ClampToBounds(raw, margin: 1f);
```

`AIBrainProvider` ve `CharacterFactory` bir `Arena` referansı taşır.
Bu düzeltme yapılmazsa oyun çalışır ama AI'lar zamanla kenarlarda birikir.

### 6.3 Ana plan etkisi

| Bölüm | Durum |
|---|---|
| Faz 1 — `SpawnMapView.cs` | **Silindi** |
| Faz 1 — `SpawnCategory.cs` (`SpawnPick`) | `SpawnPick` silindi |
| Faz 1 — `SpawnMap.cs` | Bu dokümandaki hâli |
| Faz 1 — sahne kurulumu (`PlayerPoints` vb.) | **Geçersiz** — Arena hiyerarşisi |
| Faz 4 — `CollectiblePoints` Transform'ları | **Geçersiz** |
| `CharacterFactory` | **Değişmedi** |
| Diğer her şey | Değişmedi |
