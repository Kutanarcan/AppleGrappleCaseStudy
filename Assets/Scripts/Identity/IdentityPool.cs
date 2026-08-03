using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Hands out one identity per character, per round. Plain C# — no MonoBehaviour,
    /// no inspector wiring beyond the flag sprites themselves.
    ///
    /// Names are dealt from a shuffled deck rather than picked at random, so a table of
    /// enemies never shows the same name twice while the deck lasts.
    /// </summary>
    public sealed class IdentityPool
    {
        public const string PlayerName      = "KutanArcan";
        public const int    PlayerFlagIndex = 0;

        /// <summary>Fallback when no catalog is wired — the design calls for slots 0..3.</summary>
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

        // ================= INITIALIZE =================
        /// <summary>Reshuffles the deck so a restart deals a fresh table.</summary>
        public void Initialize()
        {
            _deck.Clear();
            _deck.AddRange(NamePool);

            // Fisher-Yates. Drawing from the tail then makes Take() an O(1) removal.
            for (int i = _deck.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
            }
        }

        public CharacterIdentity NextPlayer() => new(PlayerName, PlayerFlagIndex);

        public CharacterIdentity NextEnemy() => new(TakeName(), _random.Next(FlagSlots));

        /// <summary>Resolves the index a <see cref="CharacterIdentity"/> carries.</summary>
        public Sprite Flag(int index) => _catalog != null ? _catalog.Get(index) : null;

        // ================= DEINITIALIZE =================
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
            // More characters than names: the deck runs dry and repeats become unavoidable.
            if (_deck.Count == 0) return NamePool[_random.Next(NamePool.Length)];

            int last = _deck.Count - 1;
            string name = _deck[last];
            _deck.RemoveAt(last);
            return name;
        }
    }
}
