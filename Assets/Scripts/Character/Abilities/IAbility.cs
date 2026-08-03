namespace LoopGamesCaseStudy.AppleGrappleClone
{
    public interface IAbility
    {
        void Initialize(Character owner);

        void Update(float deltaTime);
        void FixedUpdate(float deltaTime);
        void Deinitialize();
    }
}
