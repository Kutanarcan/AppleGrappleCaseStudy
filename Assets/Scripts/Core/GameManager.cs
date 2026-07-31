using System.Collections.Generic;
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
            _input      = new PlayerInput();
            _map        = new SpawnMap(_spawnMapView, _spawnSeed);
            _characters = new CharacterRegistry();

            _resolver = new InteractionResolver();
            _resolver.AddRule(new SwordVsSwordRule());
            _resolver.AddRule(new SwordVsCharacterRule());

            _swordPool = new Pool<Sword>(
                create:  CreateSword,
                destroy: sword => Destroy(sword.View.gameObject),
                prewarm: _swordPrewarm);

            _factory    = new CharacterFactory(_characters, _map, _input, _swordPool, _resolver,
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
            // Dispose order matters: the factory returns swords to the pool first,
            // then the pool destroys them.
            _factory?.Dispose();
            _swordPool?.Dispose();
            _resolver?.Dispose();

            _input?.Disable();
            _input?.Dispose();

            _input      = null;
            _factory    = null;
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
