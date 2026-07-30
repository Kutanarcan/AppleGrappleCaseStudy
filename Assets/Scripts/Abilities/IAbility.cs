namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public interface IAbility
    {
        /// <summary>Round start. The ability is attached in CREATE, only set up here.</summary>
        void Initialize(Character owner);

        void Update(float deltaTime);
        void FixedUpdate(float deltaTime);

        /// <summary>Round end or death. Resources (swords) are returned to the pool.</summary>
        void Deinitialize();
    }
}
