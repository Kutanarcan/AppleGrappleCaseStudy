using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Definitions")]
        [SerializeField] private CharacterDefinition _playerDefinition;

        [Header("Prefabs")]
        [SerializeField] private SwordView _swordPrefab;

        [Header("Scene")]
        [SerializeField] private SpawnMapView _spawnMapView;
        [SerializeField] private int _spawnSeed = 12345;

        [Header("Pooling")]
        [SerializeField] private int _swordPrewarm = 48;

        private PlayerInput       _input;
        private SpawnMap          _map;
        private CharacterRegistry _characters;
        private CharacterFactory  _factory;
        private Pool<Sword>       _swordPool;

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

            _swordPool = new Pool<Sword>(
                create:  CreateSword,
                destroy: sword => Destroy(sword.View.gameObject),
                prewarm: _swordPrewarm);

            _factory    = new CharacterFactory(_characters, _map, _input, _swordPool, _playerDefinition);
        }

        private Sword CreateSword()
        {
            SwordView view = Instantiate(_swordPrefab);
            view.gameObject.SetActive(false);
            return new Sword(view);
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

            _input?.Disable();
            _input?.Dispose();

            _input      = null;
            _factory    = null;
            _swordPool  = null;
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
            if (Input.GetKeyDown(KeyCode.R)) Restart();   // reset demo
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
}
