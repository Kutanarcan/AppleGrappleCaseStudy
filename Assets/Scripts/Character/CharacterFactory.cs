using System;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class CharacterFactory : IDisposable
    {
        private readonly CharacterRegistry _registry;
        private readonly SpawnMap          _map;
        private readonly PlayerInput       _input;

        private Character _player;

        // ================= CREATE =================
        // The roster is born here. After this no character is ever Instantiated.
        public CharacterFactory(CharacterRegistry registry, SpawnMap map,
                                PlayerInput input, CharacterDefinition playerDefinition)
        {
            _registry = registry;
            _map      = map;
            _input    = input;

            _player = CreateCharacter(playerDefinition);
        }

        private Character CreateCharacter(CharacterDefinition definition)
        {
            CharacterView view = UnityEngine.Object.Instantiate(definition.ViewPrefab);

            var character = new Character(view, definition);

            // The provider may reference the character → character first, then provider
            character.SetDirectionProvider(CreateProvider(definition, character));

            return character;
        }

        private IDirectionProvider CreateProvider(CharacterDefinition definition, Character self)
        {
            switch (definition.BrainType)
            {
                case CharacterBrainType.Player:
                    return new PlayerInputDirectionProvider(_input.Player.Move);

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

        /// <summary>Does not destroy — deactivates and waits until retry.</summary>
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
}
