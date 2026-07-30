using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Definitions")]
        [SerializeField] private CharacterDefinition _playerDefinition;

        [Header("Scene")]
        [SerializeField] private SpawnMapView _spawnMapView;
        [SerializeField] private int _spawnSeed = 12345;

        private PlayerInput       _input;
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
            _factory    = new CharacterFactory(_characters, _map, _input, _playerDefinition);
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

            _input?.Disable();
            _input?.Dispose();

            _input      = null;
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
