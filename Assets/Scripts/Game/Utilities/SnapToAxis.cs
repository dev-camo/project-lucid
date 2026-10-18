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
            return new Vector3(SnapToX ? target.x : current.x, SnapToY ? target.y : current.y,
                SnapToZ ? target.z : current.z);
        }
    }
}
