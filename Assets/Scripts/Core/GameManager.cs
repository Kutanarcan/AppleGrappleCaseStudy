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

        [Header("Feedback")]
        [SerializeField] private FeedbackConfig _feedbackConfig;
        [SerializeField] private AudioSource    _audioSource;

        [Header("Scene")]
        [SerializeField] private SpawnMapView _spawnMapView;
        [SerializeField] private int _spawnSeed = 12345;
        [SerializeField] private int _enemyCount = 8;

        [Header("Pooling")]
        [SerializeField] private int _swordPrewarm = 48;

        private PlayerInput         _input;
        private SpawnMap            _map;
        private CharacterRegistry   _characters;
        private CharacterFactory    _factory;
        private Pool<Sword>         _swordPool;
        private InteractionResolver _resolver;

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

            _input      = new PlayerInput();
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

            _factory = new CharacterFactory(_characters, _map, _input, _swordPool, _resolver,
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

#if UNITY_EDITOR
            // Fewer enemy points than enemies means SpawnMap wraps and stacks enemies
            // on the same spot — their sword rings overlap and cancel each other out.
            int enemyPoints = _map.Count(SpawnCategory.Enemy);
            if (enemyPoints < _enemyCount)
                Debug.LogWarning($"SpawnMap: {enemyPoints} enemy point(s) for {_enemyCount} " +
                                 "enemies — enemies will stack and lose their swords on spawn.");
#endif

            _factory.Initialize();
        }

        // ================= DEINITIALIZE =================
        public void Deinitialize()
        {
            _factory.Deinitialize();
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
            _particles?.Dispose();
            _swordPool?.Dispose();
            _resolver?.Dispose();

            _input?.Disable();
            _input?.Dispose();

            _input      = null;
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
            // Deaths are triggered inside physics callbacks; we sweep after all of
            // this frame's physics steps have finished.
            DespawnPending();

            float dt = Time.deltaTime;

            _particles.Update(dt);

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
