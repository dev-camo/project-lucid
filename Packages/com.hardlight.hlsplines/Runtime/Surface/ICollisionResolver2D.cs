using UnityEngine;

namespace Hardlight
{
    // Original0200007a has these four actual contracts; no default bodies.
    public interface ICollisionResolver2D
    {
        Collision2DOutput GetNearestLocationFromWorld(Collision2DInput input, Vector3 worldPosition);
        RayHit2DOutput GetNearestLocationHitFromWorld(RayHit2DInput input);
        Collision2DOutput MoveTowardsWorldLocation(Collision2DInput input, Vector3 worldOffset);
        Collision2DOutput MoveTowardsLocalLocation(Collision2DInput input, Vector3 localOffset);
    }
}
