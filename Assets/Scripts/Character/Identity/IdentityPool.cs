using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class IdentityPool
    {
        public const string PlayerName      = "KutanArcan";
        public const int    PlayerFlagIndex = 0;

        private const int DefaultFlagSlots = 4;

        private static readonly string[] NamePool =
        {
            "Blade",  "Cider",  "Nomad",  "Pixel",  "Rogue",
            "Sable",  "Twitch", "Vandal", "Wisp",   "Zephyr"
        };

        private readonly FlagCatalog   _catalog;
        private readonly System.Random _random = new();
        private readonly List<string>  _deck   = new(NamePool.Length);

        public IdentityPool(FlagCatalog catalog) => _catalog = catalog;

        public void Initialize()
        {
            _deck.Clear();
            _deck.AddRange(NamePool);

            for (int i = _deck.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
            }
        }

        public CharacterIdentity NextPlayer() => new(PlayerName, PlayerFlagIndex);

        public CharacterIdentity NextEnemy() => new(TakeName(), _random.Next(FlagSlots));

        public Sprite Flag(int index) => _catalog != null ? _catalog.Get(index) : null;

        public void Deinitialize() => _deck.Clear();

        private int FlagSlots
        {
            get
            {
                int count = _catalog != null ? _catalog.Count : 0;
                return count > 0 ? count : DefaultFlagSlots;
            }
        }

        private string TakeName()
        {
            if (_deck.Count == 0)
                return NamePool[_random.Next(NamePool.Length)];

            int last = _deck.Count - 1;
            string name = _deck[last];

            _deck.RemoveAt(last);

            return name;
        }
    }
}
