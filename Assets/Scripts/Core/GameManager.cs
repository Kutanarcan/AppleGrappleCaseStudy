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
        [SerializeField] private Camera _camera;

        [Header("Identity")]
        [SerializeField] private FlagCatalog _flagCatalog;

        [Header("Scratch")]
        [SerializeField] private MonoBehaviour _scratchPainter;

        [Header("Arena")]
        [SerializeField] private ArenaView _arenaView;
        [SerializeField] private ArenaConfig _arenaConfig;

        [Header("Spawn")]
        [SerializeField] private SpawnConfig _spawnConfig;

        [Header("Collectibles")]
        [SerializeField] private CollectibleConfig _collectibleConfig;

        [Header("Props")]
        [SerializeField] private PropView _propPrefab;
        [SerializeField] private PropConfig _propConfig;

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
        private PropSpawner _props;
        private IdentityPool _identities;
        private IScratchPainter _scratch;

        private AudioManager _audio;
        private ParticleManager _particles;
        private ScreenShakeEffect _screenShake;
        private GameFeedback _feedback;

        private bool _quitting;

        private void Awake()
        {
            Compose();
            Initialize();
        }

        private void OnApplicationQuit() => _quitting = true;

        private void OnDestroy()
        {
            if (_quitting)
                return;

            Deinitialize();
            Dispose();
        }

        public void Restart()
        {
            Deinitialize();
            Initialize();
        }

        private void Compose()
        {
            DOTween.Init(recycleAllByDefault: false, useSafeMode: true, LogBehaviour.ErrorsOnly)
                   .SetCapacity(tweenersCapacity: 200, sequencesCapacity: 50);

            _input = new PlayerInput();

            _arena = new Arena(_arenaView, _arenaConfig.Width, _arenaConfig.Height,
                                          _arenaConfig.WallThickness, _arenaConfig.MarginOffset);

            _map = new SpawnMap(_arena, _spawnConfig.Seed,
                                       _spawnConfig.MinCharacterSeparation,
                                       _spawnConfig.CharacterMargin,
                                       _spawnConfig.RandomMargin);

            _characters = new CharacterRegistry();
            _resolver = new InteractionResolver();

            _audio = new AudioManager(_audioSource);
            _particles = new ParticleManager(_feedbackConfig);

            Camera camera = _camera != null ? _camera : Camera.main;

            _screenShake = new ScreenShakeEffect(camera != null ? camera.transform : null);
            _feedback = new GameFeedback(_audio, _particles, _screenShake, _feedbackConfig);

            _swordPool = new Pool<Sword>(
                create: CreateSword,
                destroy: sword => Destroy(sword.View.gameObject),
                prewarm: _swordPrewarm);

            _collectibles = new SwordCollectibleSpawner(
                _collectiblePrefab, _resolver, _map,
                _collectibleConfig.Settings, _collectibleConfig.Prewarm);

            _props = new PropSpawner(_propPrefab, _map, _propConfig.Settings, _spawnConfig.Seed);

            _resolver.AddRule(new SwordPickupRule(_feedback, _feedbackConfig));
            _resolver.AddRule(new SwordVsSwordRule(_feedback));
            _resolver.AddRule(new SwordVsCharacterRule());

            _identities = new IdentityPool(_flagCatalog);

            _scratch = _scratchPainter as IScratchPainter;
            _scratch ??= NullScratchPainter.Instance;

            _factory = new CharacterFactory(_characters, _map, _input, _swordPool, _resolver,
                                            _feedback, _feedbackConfig,
                                            _playerDefinition, _enemyDefinition, _spawnConfig.EnemyCount, _collectibles, _arena,
                                            _identities, _scratch);
        }

        private Sword CreateSword()
        {
            SwordView view = Instantiate(_swordPrefab);
            view.gameObject.SetActive(false);
            return new Sword(view, _resolver);
        }

        public void Initialize()
        {
            _map.Initialize(1 + _spawnConfig.EnemyCount);
            _props.Initialize();
            _audio.Initialize();
            _particles.Initialize();
            _collectibles.Initialize();
            _identities.Initialize();
            _factory.Initialize();
        }

        public void Deinitialize()
        {
            _factory.Deinitialize();
            _identities.Deinitialize();
            _collectibles.Deinitialize();
            _props.Deinitialize();
            _particles.Deinitialize();
            _screenShake.Reset();
            _scratch.ClearAll();
            _audio.Deinitialize();
            _characters.Deinitialize();
            _map.Deinitialize();
        }

        private void Dispose()
        {
            DOTween.KillAll();

            _factory?.Dispose();
            _collectibles?.Dispose();
            _props?.Dispose();
            _particles?.Dispose();
            _swordPool?.Dispose();
            _resolver?.Dispose();
            _arena?.Dispose();

            _input?.Disable();
            _input?.Dispose();

            _input = null;
            _factory = null;
            _particles = null;
            _screenShake = null;
            _scratch = null;
            _audio = null;
            _feedback = null;
            _swordPool = null;
            _resolver = null;
            _characters = null;
            _map = null;
            _arena = null;
            _collectibles = null;
            _props = null;
            _identities = null;
        }

        private void Update()
        {
            DespawnPending();

            float dt = Time.deltaTime;

            _particles.Update(dt);
            _collectibles.Update(dt);

            IReadOnlyList<Character> active = _characters.Active;

            for (int i = 0; i < active.Count; i++)
            {
                active[i].Update(dt);
            }

#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.R)) Restart();
#endif
        }

        private void DespawnPending()
        {
            IReadOnlyList<Character> pending = _characters.PendingRemoval;
            if (pending.Count == 0) return;

            for (int i = 0; i < pending.Count; i++)
            {
                _factory.Despawn(pending[i]);
            }

            _characters.ClearPending();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            IReadOnlyList<Character> active = _characters.Active;

            for (int i = 0; i < active.Count; i++)
            {
                active[i].FixedUpdate(dt);
            }
        }
    }
}
