using UnityEngine;
namespace HardlightProject
{
    // Original02000300, full8 abstract methods, no inherited contracts or runtime bodies.
    public interface IHomingAttack
    {
        Vector3 TargetZoneSize { get; }
        Vector3 TargetZoneOffset { get; }
        void AddTarget(HomingTarget target);
        void RemoveTarget(HomingTarget target);
        void OnHomingTargetHit(HomingTarget target);
        bool IsHomingTargetValid(HomingTarget target);
        void OnTriggerEnter(Collider otherCollider);
        void OnTriggerExit(Collider otherCollider);
    }
    // Original020006d4, genuine value struct with one public field and zero local methods.
    public struct HomingTargetRequestMessage { public HomingTarget Target; }
    // Original02000ac8, full2 abstract methods; original generic GetComponent has no constraint.
    public interface ITriggerCollider
    {
        void ManualTriggerEnter(Collider other);
        T GetComponent<T>();
    }
}
