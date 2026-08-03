using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public sealed class SwordRingAbility : IAbility
    {
        private struct SwordSlot
        {
            public Sword Sword;
            public int SlotIndex;
            public float Offset;
            public float OffsetVel;
            public float Radius;
            public float RadiusVel;
            public float Scale;
            public float ScaleVel;
        }

        private static readonly Comparison<SwordSlot> ByOffset = (a, b) => Mathf.Repeat(a.Offset, 360f).CompareTo(Mathf.Repeat(b.Offset, 360f));

        private readonly List<SwordSlot> _slots = new(36);
        private readonly List<Sword> _detached = new(12);
        private readonly Pool<Sword> _pool;
        private readonly FeedbackConfig _config;
        private readonly IScratchPainter _scratch;

        private Character _owner;
        private float _phase;
        private float _targetPhase;

        public SwordRingAbility(Pool<Sword> pool, FeedbackConfig config, IScratchPainter scratch)
        {
            _pool = pool;
            _config = config;
            _scratch = scratch ?? NullScratchPainter.Instance;
        }

        public int ActiveSwordCount => _slots.Count;

        public void Initialize(Character owner)
        {
            _owner = owner;
            _phase = 0f;
            _targetPhase = 0f;

            _slots.Clear();
            SyncCount();
            SnapAll();
        }

        public void Update(float deltaTime)
        {
            float brush = _owner.Definition.SwordScratchBrushSize;

            if (brush <= 0f)
                return;

            for (int i = 0; i < _slots.Count; i++)
                _scratch.Paint(_slots[i].Sword.View.transform.position, brush);
        }

        public void FixedUpdate(float deltaTime)
        {
            SweepDetached(deltaTime);
            SyncCount();

            if (_slots.Count == 0)
                return;

            _phase = Mathf.Repeat(_phase + _owner.Stats.OrbitAngularSpeed * deltaTime, 360f);

            float step = 360f / _slots.Count;
            float radius = _owner.Stats.OrbitRadius;
            float settleTime = _owner.Definition.RingSettleTime;
            float entryTime = _owner.Definition.RingEntryTime;

            Vector2 center = _owner.Movement.PredictedPosition(deltaTime);

            for (int i = 0; i < _slots.Count; i++)
            {
                SwordSlot slot = _slots[i];

                float targetOffset = _targetPhase + slot.SlotIndex * step;

                slot.Offset = Mathf.SmoothDamp(slot.Offset, targetOffset, ref slot.OffsetVel, settleTime, Mathf.Infinity, deltaTime);
                slot.Radius = Mathf.SmoothDamp(slot.Radius, radius, ref slot.RadiusVel, entryTime, Mathf.Infinity, deltaTime);
                slot.Scale = Mathf.SmoothDamp(slot.Scale, 1f, ref slot.ScaleVel, entryTime, Mathf.Infinity, deltaTime);

                _slots[i] = slot;

                PlaceSlot(slot, center, snap: false);
            }
        }

        public void Detach(Sword sword, float verticalSign)
        {
            if (_owner == null)
                return;

            int index = IndexOf(sword);

            if (index < 0)
                return;

            SwordSlot slot = _slots[index];
            _slots.RemoveAt(index);

            ReassignSlots();

            _owner.Stats.SwordCount = Mathf.Max(0, _owner.Stats.SwordCount - 1);

            float rad = (_phase + slot.Offset) * Mathf.Deg2Rad;
            Vector2 outward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            Vector2 direction = (outward + Vector2.up * verticalSign * _config.SwordThrowVerticalBias).normalized;

            sword.BeginDetach(direction, _config);

            _detached.Add(sword);
        }

        private void SweepDetached(float deltaTime)
        {
            for (int i = _detached.Count - 1; i >= 0; i--)
            {
                _detached[i].TickDetach(deltaTime);

                if (!_detached[i].IsDetachFinished)
                    continue;

                ReturnSword(_detached[i]);
                _detached.RemoveAt(i);
            }
        }

        private void SyncCount()
        {
            int desired = Mathf.Clamp(_owner.Stats.SwordCount, 0, _owner.Stats.MaxSwordCount);
            if (desired == _slots.Count) return;

            while (_slots.Count < desired) AddSlot();
            while (_slots.Count > desired) RemoveLastSlot();

            ReassignSlots();
        }

        private void AddSlot()
        {
            Sword sword = _pool.Rent();
            sword.Initialize(_owner, this);

            SwordSlot slot = new SwordSlot
            {
                Sword = sword,
                SlotIndex = _slots.Count,
                Offset = FindEntryOffset(),
                OffsetVel = 0f,
                Radius = _owner.Stats.OrbitRadius,
                RadiusVel = 0f,
                Scale = 0f,
                ScaleVel = 0f
            };

            _slots.Add(slot);

            PlaceSlot(slot, _owner.Position, snap: true);
        }

        private void RemoveLastSlot()
        {
            int last = _slots.Count - 1;
            ReturnSword(_slots[last].Sword);
            _slots.RemoveAt(last);
        }

        private float FindEntryOffset()
        {
            if (_slots.Count == 0)
                return 0f;

            if (_slots.Count == 1)
                return Mathf.Repeat(_slots[0].Offset + 180f, 360f);

            float bestMid = 0f;
            float bestGap = -1f;

            for (int i = 0; i < _slots.Count; i++)
            {
                float a = Mathf.Repeat(_slots[i].Offset, 360f);
                float b = Mathf.Repeat(_slots[(i + 1) % _slots.Count].Offset, 360f);
                float gap = Mathf.Repeat(b - a, 360f);

                if (gap <= bestGap) continue;

                bestGap = gap;
                bestMid = Mathf.Repeat(a + gap * 0.5f, 360f);
            }

            return bestMid;
        }

        private void ReassignSlots()
        {
            if (_slots.Count == 0)
            {
                _targetPhase = 0f;
                return;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                SwordSlot s = _slots[i];
                s.Offset = Mathf.Repeat(s.Offset, 360f);
                _slots[i] = s;
            }


            _slots.Sort(ByOffset);

            float step = 360f / _slots.Count;

            float phi = float.NegativeInfinity;
            for (int i = 0; i < _slots.Count; i++)
            {
                SwordSlot slot = _slots[i];
                slot.SlotIndex = i;
                _slots[i] = slot;

                float e = slot.Offset - i * step;
                if (e > phi) phi = e;
            }

            _targetPhase = phi;
        }

        private void SnapAll()
        {
            _targetPhase = 0f;
            if (_slots.Count == 0) return;

            float step = 360f / _slots.Count;
            float radius = _owner.Stats.OrbitRadius;
            Vector2 center = _owner.Position;

            for (int i = 0; i < _slots.Count; i++)
            {
                SwordSlot slot = _slots[i];
                slot.Offset = slot.SlotIndex * step;
                slot.OffsetVel = 0f;
                slot.Radius = radius;
                slot.RadiusVel = 0f;
                slot.Scale = 1f;
                slot.ScaleVel = 0f;
                _slots[i] = slot;

                PlaceSlot(slot, center, snap: true);
            }
        }

        private void PlaceSlot(in SwordSlot slot, Vector2 center, bool snap)
        {
            float rad = (_phase + slot.Offset) * Mathf.Deg2Rad;
            Vector2 pos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * slot.Radius;

            slot.Sword.SetScale(slot.Scale);

            if (snap)
            {
                slot.Sword.SnapTo(pos, rad);
                return;
            }

            slot.Sword.MoveTo(pos, rad);
        }

        private int IndexOf(Sword sword)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Sword == sword)
                    return i;
            }

            return -1;
        }

        private void ReturnSword(Sword sword)
        {
            sword.Deinitialize();
            _pool.Return(sword);
        }

        public void Deinitialize()
        {
            for (int i = 0; i < _detached.Count; i++)
                ReturnSword(_detached[i]);
            _detached.Clear();

            for (int i = 0; i < _slots.Count; i++)
                ReturnSword(_slots[i].Sword);
            _slots.Clear();

            _owner = null;
            _phase = 0f;
            _targetPhase = 0f;
        }
    }
}
