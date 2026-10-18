using System;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a2c: each enabled axis takes the target's
    // component, the others keep the current one.
    [Serializable]
    public struct SnapToAxis
    {
        public bool SnapToX;
        public bool SnapToY;
        public bool SnapToZ;

        public Vector3 Snap(Vector3 current, Vector3 target)
        {
            // The native body initialises Vector3's statics, so it starts from Vector3.zero.
            Vector3 snapped = Vector3.zero;
            snapped.x = SnapToX ? target.x : current.x;
            snapped.y = SnapToY ? target.y : current.y;
            snapped.z = SnapToZ ? target.z : current.z;
            return snapped;
        }
    }
}
