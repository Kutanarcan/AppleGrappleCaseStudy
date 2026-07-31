using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class InteractionBody : MonoBehaviour
    {
        private InteractionResolver _resolver;

        public IInteractionEntity Entity { get; private set; }

        public void Bind(IInteractionEntity entity, InteractionResolver resolver)
        {
            Entity    = entity;
            _resolver = resolver;
        }

        public void Unbind()
        {
            Entity    = null;
            _resolver = null;
        }

        /// <summary>Derived types forward their trigger messages here.</summary>
        protected void ReportContact(Collider2D other)
        {
            if (Entity == null || _resolver == null) return;

            Rigidbody2D otherRigidbody = other.attachedRigidbody;
            if (otherRigidbody == null) return;
            if (!otherRigidbody.TryGetComponent(out InteractionBody otherBody)) return;
            if (otherBody.Entity == null) return;

            _resolver.Resolve(Entity, otherBody.Entity, other.ClosestPoint(transform.position));
        }
    }
}
