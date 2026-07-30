using System.Collections.Generic;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class CharacterRegistry
    {
        private readonly List<Character> _active = new(64);

        public IReadOnlyList<Character> Active => _active;

        public void Add(Character character)    => _active.Add(character);
        public void Remove(Character character) => _active.Remove(character);

        public void Deinitialize() => _active.Clear();
    }
}
