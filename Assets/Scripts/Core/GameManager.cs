using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Definitions")]
        [SerializeField] private CharacterDefinition _playerDefinition;
        [SerializeField] private CharacterDefinition _enemyDefinition;

        [Header("Prefabs")]
        [SerializeField] private SwordView _swordPrefab;
        [SerializeField] private SwordCollectibleView _collectiblePrefab;

        [Header("Feedback")]
        [SerializeField] private FeedbackConfig _feedbackConfig;
        [SerializeField] private AudioSource _audioSource;

        [Header("Arena")]
        [SerializeField] private ArenaView _arenaView;
        [SerializeField] private float _arenaWidth = 22f;
        [SerializeField] private float _arenaHeight = 16f;
        [SerializeField] private float _wallThickness = 0.5f;
        [SerializeField] private float _arenaMarginOffset = 4f;

        [Header("Spawn")]
        [SerializeField] private float _minCharacterSeparation = 3.5f;
        [SerializeField] private float _characterMargin = 1.5f;
        [SerializeField] private float _randomMargin = 1.5f;
        [SerializeField] private int _spawnSeed = 12345;

        [Header("Scene")]
        [SerializeField] private int _enemyCount = 8;

        [Header("Collectibles")]
        [SerializeField]
        private SwordCollectibleSpawnSettings _collectibleSettings = new()
        {
            SpawnInterval = 3f,
            MaxActive = 6,
            SwordAmount = 1
        };
        [SerializeField] private int _collectiblePrewarm = 8;

        [Header("Pooling")]
        [SerializeField] private int _swordPrewarm = 48;

        private PlayerInput _input;
        private Arena _arena;
        private SpawnMap _map;
        private CharacterRegistry _characters;
        private CharacterFactory _factory;
        private Pool<Sword> _swordPool;
        private InteractionResolver _resolver;
        private SwordCollectibleSpawner _collectibles;

        private AudioManager _audio;
        private ParticleManager _particles;
        private GameFeedback _feedback;

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

        /// <summary>Defeated → play again. Not a single Instantiate.</summary>
        public void Restart()
        {
            Deinitialize();
            Initialize();
        }

        // ================= CREATE =================
        // Every `new` line here can be handed over to a DI container.
        private void Compose()
        {
            DOTween.Init(recycleAllByDefault: false, useSafeMode: true, LogBehaviour.ErrorsOnly)
                   .SetCapacity(tweenersCapacity: 200, sequencesCapacity: 50);

            _input = new PlayerInput();

            // Arena first: ground/mask are sized, the fence is spawned, Min/Max become known
            _arena = new Arena(_arenaView, _arenaWidth, _arenaHeight, _wallThickness, _arenaMarginOffset);
            _map = new SpawnMap(_arena, _spawnSeed,
                                       _minCharacterSeparation, _characterMargin, _randomMargin);
            _characters = new CharacterRegistry();
            _resolver = new InteractionResolver();

            _audio = new AudioManager(_audioSource);
            _particles = new ParticleManager(_feedbackConfig);
            _feedback = new GameFeedback(_audio, _particles, _feedbackConfig);

            _swordPool = new Pool<Sword>(
                create: CreateSword,
                destroy: sword => Destroy(sword.View.gameObject),
                prewarm: _swordPrewarm);

            _collectibles = new SwordCollectibleSpawner(
                _collectiblePrefab, _resolver, _map, _collectibleSettings, _collectiblePrewarm);

            _resolver.AddRule(new SwordPickupRule(_feedback, _feedbackConfig));
            _resolver.AddRule(new SwordVsSwordRule(_feedback));
            _resolver.AddRule(new SwordVsCharacterRule());

            _factory = new CharacterFactory(_characters, _map, _input, _swordPool, _resolver,
                                            _feedback, _feedbackConfig,
                                            _playerDefinition, _enemyDefinition, _enemyCount, _collectibles, _arena);
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
            _map.Initialize(1 + _enemyCount);      // player + enemies share one polygon
            _audio.Initialize();
            _particles.Initialize();
            _collectibles.Initialize();
            _factory.Initialize();
        }

        // ================= DEINITIALIZE =================
        public void Deinitialize()
        {
            _factory.Deinitialize();
            _collectibles.Deinitialize();   // flying bubbles return to the pool at once
            _particles.Deinitialize();      // no blood splash left on screen
            _audio.Deinitialize();
            _characters.Deinitialize();
            _map.Deinitialize();
        }

        // ================= DISPOSE =================
        private void Dispose()
        {
            DOTween.KillAll();

            // Dispose order matters: the factory returns swords to the pool first,
            // then the pool destroys them.
            _factory?.Dispose();
            _collectibles?.Dispose();
            _particles?.Dispose();
            _swordPool?.Dispose();
            _resolver?.Dispose();
            _arena?.Dispose();               // destroys the fence pieces

            _input?.Disable();
            _input?.Dispose();

            _input = null;
            _factory = null;
            _particles = null;
            _audio = null;
            _feedback = null;
            _swordPool = null;
            _resolver = null;
            _characters = null;
            _map = null;
            _arena = null;
            _collectibles = null;
        }

        // ================= TICK =================
        private void Update()
        {
            // Deaths are triggered inside physics callbacks; we sweep after all of
            // this frame's physics steps have finished.
            DespawnPending();

            float dt = Time.deltaTime;

            _particles.Update(dt);
            _collectibles.Update(dt);

            IReadOnlyList<Character> active = _characters.Active;
            for (int i = 0; i < active.Count; i++)
                active[i].Update(dt);

#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.R)) Restart();   // reset demo
#endif
        }

        private void DespawnPending()
        {
            IReadOnlyList<Character> pending = _characters.PendingRemoval;
            if (pending.Count == 0) return;

            for (int i = 0; i < pending.Count; i++)
                _factory.Despawn(pending[i]);       // NOT Destroy — just deactivates

            _characters.ClearPending();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            IReadOnlyList<Character> active = _characters.Active;
            for (int i = 0; i < active.Count; i++)
                active[i].FixedUpdate(dt);
        }
    }
}
