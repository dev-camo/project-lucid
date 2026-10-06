using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class WaypointSettings : System.Object
    {
        [UnityEngine.SerializeField]
        private UnityEngine.Sprite m_icon;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Height difference between waypoint target and character when the height indicator will display. Used for both above/below.")]
        private System.Single m_showHeightIndicatorThreshold;

        [UnityEngine.SerializeField]
        [UnityEngine.Tooltip("Waypoint marker will be disabled when this close to target.")]
        private System.Single m_minDrawDistance;

        [UnityEngine.Tooltip("Waypoint marker will be disabled until within this distance of the target.")]
        [UnityEngine.SerializeField]
        private System.Single m_maxDrawDistance;

        [UnityEngine.Tooltip("If un-ticked, displays only the icon without the distance text.")]
        [UnityEngine.SerializeField]
        private System.Boolean m_showDistanceIndicator = true;

        // Original Game.Runtime 0x06001fbd, ARM 0x532fe8.
        public UnityEngine.Sprite Icon => m_icon;

        // Original Game.Runtime 0x06001fbe, ARM 0x532ff0.
        public System.Single ShowHeightIndicatorThreshold => m_showHeightIndicatorThreshold;

        // Original Game.Runtime 0x06001fbf, ARM 0x532ff8.
        public System.Single MinDrawDistance => m_minDrawDistance;

        // Original Game.Runtime 0x06001fc0, ARM 0x533000.
        public System.Single MaxDrawDistance => m_maxDrawDistance;

        // Original Game.Runtime 0x06001fc1, ARM 0x533008.
        public System.Boolean ShowDistanceIndicator => m_showDistanceIndicator;

        // Original Game.Runtime 0x06001fc2, ARM 0x533010.
        // Natural original constructor; documented field initializers precede the genuine base call.
    }
}
