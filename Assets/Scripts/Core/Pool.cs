using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class Pool<T> : IDisposable where T : class
    {
        private readonly Stack<T> _idle;
        private readonly Func<T> _create;
        private readonly Action<T> _destroy;

        public int IdleCount => _idle.Count;

        public Pool(Func<T> create, Action<T> destroy, int prewarm = 0)
        {
            _create = create;
            _destroy = destroy;
            _idle = new Stack<T>(Mathf.Max(prewarm, 4));

            for (int i = 0; i < prewarm; i++)
                _idle.Push(_create());
        }

        public T Rent() => _idle.Count > 0 ? _idle.Pop() : _create();

        public void Return(T item) => _idle.Push(item);

        public void Dispose()
        {
            while (_idle.Count > 0)
                _destroy(_idle.Pop());
        }
    }
}
