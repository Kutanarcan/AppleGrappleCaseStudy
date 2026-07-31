using System.Collections.Generic;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class CharacterRegistry
    {
        private readonly List<Character> _active  = new(64);
        private readonly List<Character> _pending = new(8);

        public IReadOnlyList<Character> Active         => _active;
        public IReadOnlyList<Character> PendingRemoval => _pending;

        public void Add(Character character)
        {
            _active.Add(character);
            character.Died += OnDied;
        }

        public void Remove(Character character)
        {
            character.Died -= OnDied;
            _active.Remove(character);
        }

        private void OnDied(Character character)
        {
            if (_pending.Contains(character)) return;   // double death signal in the same step
            _pending.Add(character);
        }

        public void ClearPending() => _pending.Clear();

        public void Deinitialize()
        {
            for (int i = 0; i < _active.Count; i++)
                _active[i].Died -= OnDied;

            _active.Clear();
            _pending.Clear();
        }
    }
}
