using System;
using System.Collections.Generic;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class CharacterFactory : IDisposable
    {
        private readonly CharacterRegistry _registry;

        private readonly PlayerInput _input;

        private readonly Pool<Sword> _swordPool;

        private readonly InteractionResolver _resolver;

        private readonly IGameFeedback _feedback;
        private readonly FeedbackConfig _feedbackConfig;

        private readonly List<Character> _enemies = new(32);

        private readonly SwordCollectibleSpawner _collectibles;
        private readonly SpawnMap _map;
        private readonly Arena _arena;

        private readonly IdentityPool _identities;

        private readonly IScratchPainter _scratch;

        private Character _player;

        public Character Player => _player;

        public CharacterFactory(CharacterRegistry registry, SpawnMap map,
                                PlayerInput input, Pool<Sword> swordPool,
                                InteractionResolver resolver,
                                IGameFeedback feedback, FeedbackConfig feedbackConfig,
                                CharacterDefinition playerDefinition,
                                CharacterDefinition enemyDefinition, int enemyCount, SwordCollectibleSpawner collectibles, Arena arena,
                                IdentityPool identities, IScratchPainter scratch)

        {
            _registry = registry;
            _map = map;
            _input = input;
            _swordPool = swordPool;
            _resolver = resolver;
            _feedback = feedback;
            _feedbackConfig = feedbackConfig;
            _collectibles = collectibles;
            _arena = arena;
            _identities = identities;
            _scratch = scratch ?? NullScratchPainter.Instance;

            _player = CreateCharacter(playerDefinition);

            for (int i = 0; i < enemyCount; i++)
            {
                _enemies.Add(CreateCharacter(enemyDefinition));
            }
        }

        private Character CreateCharacter(CharacterDefinition definition)
        {
            CharacterView view = UnityEngine.Object.Instantiate(definition.ViewPrefab);

            var character = new Character(view, definition, _resolver, _feedback, _scratch);

            character.SetDirectionProvider(CreateProvider(definition, character));

            if (definition.HasSwordRing)
                character.AddAbility(new SwordRingAbility(_swordPool, _feedbackConfig, _scratch));

            return character;
        }

        private IDirectionProvider CreateProvider(CharacterDefinition definition, Character self)
        {
            switch (definition.BrainType)
            {
                case CharacterBrainType.Player:
                    return new PlayerInputDirectionProvider(_input.Player.Move);

                case CharacterBrainType.AI:
                    return new AngleAIDirectionProvider(self, _arena, _registry, _collectibles);

                default:
                    return NullDirectionProvider.Instance;
            }
        }

        public void Initialize()
        {
            Place(_player, SpawnCategoryType.Player);

            for (int i = 0; i < _enemies.Count; i++)
            {
                Place(_enemies[i], SpawnCategoryType.Enemy);
            }
        }

        private void Place(Character character, SpawnCategoryType category)
        {
            CharacterIdentity identity = category == SpawnCategoryType.Player
                ? _identities.NextPlayer()
                : _identities.NextEnemy();

            character.SetIdentity(identity, _identities.Flag(identity.FlagIndex));
            character.Initialize(_map.Next(category));

            _registry.Add(character);
        }

        public void Despawn(Character character)
        {
            if (!character.IsSpawned)
                return;

            _registry.Remove(character);

            character.Deinitialize();
        }

        public void Deinitialize()
        {
            Despawn(_player);

            for (int i = 0; i < _enemies.Count; i++)
            {
                Despawn(_enemies[i]);
            }
        }

        public void Dispose()
        {
            _player?.Dispose();
            _player = null;

            for (int i = 0; i < _enemies.Count; i++)
            {
                _enemies[i].Dispose();
            }

            _enemies.Clear();
        }
    }
}
