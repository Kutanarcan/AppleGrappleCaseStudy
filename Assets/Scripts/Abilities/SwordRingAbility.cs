using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Its only contract: "give me as many swords as the SwordCount stat says."
    ///
    /// Positioning happens in POLAR-LOCAL space:
    ///     pos = center + polar(_phase + offset) * radius
    /// The center is read live every step, so a sword never loses the owner; the
    /// only things that smooth are the per-sword offset and radius, both independent
    /// of how the character moves.
    /// </summary>
    public sealed class SwordRingAbility : IAbility
    {
        /// <summary>
        /// struct — lives inside the List, no allocation.
        /// Mutation pattern: copy out, edit, write back.
        /// </summary>
        private struct SwordSlot
        {
            public Sword Sword;
            public int   SlotIndex;
            public float Offset;      // degrees, relative to _phase
            public float OffsetVel;
            public float Radius;
            public float RadiusVel;
            public float Scale;       // visual entry pop-in: 0 grows to 1
            public float ScaleVel;
        }

        // static comparison so Sort() does not allocate a delegate on every call
        private static readonly Comparison<SwordSlot> ByOffset =
            (a, b) => Mathf.Repeat(a.Offset, 360f).CompareTo(Mathf.Repeat(b.Offset, 360f));

        private readonly List<SwordSlot> _slots    = new(12);
        private readonly List<Sword>     _detached = new(4);
        private readonly Pool<Sword>     _pool;
        private readonly FeedbackConfig  _config;

        private Character _owner;
        private float _phase;
        private float _targetPhase;      // global rotation that keeps every settle forward

        public SwordRingAbility(Pool<Sword> pool, FeedbackConfig config)
        {
            _pool   = pool;
            _config = config;
        }

        public int ActiveSwordCount => _slots.Count;

        // ================= INITIALIZE =================
        public void Initialize(Character owner)
        {
            _owner = owner;
            _phase = 0f;
            _targetPhase = 0f;

            _slots.Clear();
            SyncCount();
            SnapAll();          // no spiral at round start — a retry must look identical
        }

        public void Update(float deltaTime) { }

        // ================= TICK =================
        public void FixedUpdate(float deltaTime)
        {
            SweepDetached(deltaTime);
            SyncCount();

            if (_slots.Count == 0) return;

            _phase = Mathf.Repeat(_phase + _owner.Stats.OrbitAngularSpeed * deltaTime, 360f);

            float step       = 360f / _slots.Count;
            float radius     = _owner.Stats.OrbitRadius;
            float settleTime = _owner.Definition.RingSettleTime;
            float entryTime  = _owner.Definition.RingEntryTime;

            // CRITICAL: the center AFTER this physics step.
            Vector2 center = _owner.Movement.PredictedPosition(deltaTime);

            for (int i = 0; i < _slots.Count; i++)
            {
                SwordSlot slot = _slots[i];

                // _targetPhase is chosen at reassignment so this is ALWAYS >= slot.Offset:
                // the sword only ever moves forward (with the spin) to reach its slot.
                float targetOffset = _targetPhase + slot.SlotIndex * step;

                // Plain SmoothDamp (not SmoothDampAngle): the target is a monotonic value
                // ahead of the current offset, critically damped → no overshoot, no reversal.
                // SmoothDampAngle would take the shortest signed path and could go backward.
                slot.Offset = Mathf.SmoothDamp(
                    slot.Offset, targetOffset, ref slot.OffsetVel, settleTime, Mathf.Infinity, deltaTime);

                slot.Radius = Mathf.SmoothDamp(
                    slot.Radius, radius, ref slot.RadiusVel, entryTime, Mathf.Infinity, deltaTime);

                // Entry pop-in: a freshly added sword grows from 0 to full size in place.
                slot.Scale = Mathf.SmoothDamp(
                    slot.Scale, 1f, ref slot.ScaleVel, entryTime, Mathf.Infinity, deltaTime);

                _slots[i] = slot;                       // struct → write back

                PlaceSlot(slot, center, snap: false);
            }
        }

        // ================= DETACH =================
        /// <summary>
        /// The rule must call HERE, not the SwordCount stat: dropping the counter
        /// directly makes SyncCount discard the LAST slot, while the sword that
        /// actually collided stays in the ring.
        /// </summary>
        public void Detach(Sword sword, float verticalSign)
        {
            if (_owner == null) return;

            int index = IndexOf(sword);
            if (index < 0) return;

            SwordSlot slot = _slots[index];
            _slots.RemoveAt(index);
            ReassignSlots();

            _owner.Stats.SwordCount = Mathf.Max(0, _owner.Stats.SwordCount - 1);

            // Outward direction from the slot angle — the body position may lag behind
            // because of interpolation, so derive it from the angle instead.
            float rad = (_phase + slot.Offset) * Mathf.Deg2Rad;
            Vector2 outward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            Vector2 direction = (outward + Vector2.up * verticalSign * _config.SwordThrowVerticalBias)
                                .normalized;

            sword.BeginDetach(direction, _config);
            _detached.Add(sword);
        }

        private void SweepDetached(float deltaTime)
        {
            for (int i = _detached.Count - 1; i >= 0; i--)
            {
                _detached[i].TickDetach(deltaTime);
                if (!_detached[i].IsDetachFinished) continue;

                ReturnSword(_detached[i]);
                _detached.RemoveAt(i);
            }
        }

        // ================= SLOT MANAGEMENT =================
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
            Sword sword = _pool.Rent();               // prewarm means no Instantiate
            sword.Initialize(_owner, this);

            SwordSlot slot = new SwordSlot
            {
                Sword     = sword,
                SlotIndex = _slots.Count,
                Offset    = FindEntryOffset(),        // enter the widest gap
                OffsetVel = 0f,
                Radius    = _owner.Stats.OrbitRadius, // no outside-in spiral — enter at the ring
                RadiusVel = 0f,
                Scale     = 0f,                       // grow into view from nothing
                ScaleVel  = 0f
            };
            _slots.Add(slot);

            // Teleport onto the entry position immediately so the sword never spends a
            // frame at the pool origin, where Continuous detection would sweep it across
            // the map and clash it against every other freshly-spawned sword.
            PlaceSlot(slot, _owner.Position, snap: true);
        }

        private void RemoveLastSlot()
        {
            int last = _slots.Count - 1;
            ReturnSword(_slots[last].Sword);
            _slots.RemoveAt(last);
        }

        /// <summary>
        /// A new sword enters the middle of the widest angular gap, so it lands in a
        /// visually correct spot without disturbing the existing order.
        /// </summary>
        private float FindEntryOffset()
        {
            if (_slots.Count == 0) return 0f;
            if (_slots.Count == 1) return Mathf.Repeat(_slots[0].Offset + 180f, 360f);

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

        /// <summary>
        /// Called ONLY when the count changes. Calling it every frame would swap the
        /// targets of two swords as they pass each other and start an oscillation.
        ///
        /// Sort by current offset and renumber so no sword crosses through another,
        /// then choose a global target rotation (_targetPhase) that lets EVERY sword
        /// reach its slot by moving FORWARD, in the spin direction — never backward,
        /// which reads as a stutter against the orbit.
        /// </summary>
        private void ReassignSlots()
        {
            if (_slots.Count == 0)
            {
                _targetPhase = 0f;
                return;
            }

            // Wrap offsets into [0,360) first. This changes the stored number but NOT
            // the rendered position (cos/sin are 360-periodic), so there is no jump —
            // and it keeps the values bounded across many reassignments.
            for (int i = 0; i < _slots.Count; i++)
            {
                SwordSlot s = _slots[i];
                s.Offset = Mathf.Repeat(s.Offset, 360f);
                _slots[i] = s;
            }

            _slots.Sort(ByOffset);

            float step = 360f / _slots.Count;

            // Forward-only rotation: for slot i the forward delta is (phi - e_i) where
            // e_i = offset_i - i*step is how far the sword already sits ahead of a
            // phi=0 pattern. phi = max(e_i) makes the most advanced sword hold still
            // (delta 0) while the rest catch up forward — all deltas are >= 0.
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

        /// <summary>Round start — place everything directly, no interpolation.</summary>
        private void SnapAll()
        {
            _targetPhase = 0f;              // offsets settle to exactly SlotIndex * step
            if (_slots.Count == 0) return;

            float step     = 360f / _slots.Count;
            float radius   = _owner.Stats.OrbitRadius;
            Vector2 center = _owner.Position;

            for (int i = 0; i < _slots.Count; i++)
            {
                SwordSlot slot = _slots[i];
                slot.Offset    = slot.SlotIndex * step;
                slot.OffsetVel = 0f;
                slot.Radius    = radius;
                slot.RadiusVel = 0f;
                slot.Scale     = 1f;                    // round start is instant — full size
                slot.ScaleVel  = 0f;
                _slots[i] = slot;

                PlaceSlot(slot, center, snap: true);    // teleport to final ring position
            }
        }

        // Computes the world position from the slot's polar state and applies it.
        // snap = hard teleport (spawn / mid-game rent); otherwise MovePosition so the
        // kinematic body interpolates smoothly between physics steps.
        private void PlaceSlot(in SwordSlot slot, Vector2 center, bool snap)
        {
            float rad = (_phase + slot.Offset) * Mathf.Deg2Rad;
            Vector2 pos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * slot.Radius;

            slot.Sword.SetScale(slot.Scale);

            if (snap)
                slot.Sword.SnapTo(pos, rad);
            else
                slot.Sword.MoveTo(pos, rad);
        }

        private int IndexOf(Sword sword)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Sword == sword) return i;

            return -1;
        }

        private void ReturnSword(Sword sword)
        {
            sword.Deinitialize();
            _pool.Return(sword);
        }

        // ================= DEINITIALIZE =================
        public void Deinitialize()
        {
            // In-flight swords return to the pool without waiting for the animation
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
