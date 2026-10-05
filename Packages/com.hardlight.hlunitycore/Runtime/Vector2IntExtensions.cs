using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000087; no fields or hidden constructor.
    public static class Vector2IntExtensions
    {
        public static int MaxComponent(this Vector2Int vector) // 0x0600033e
        {
            return Mathf.Max(vector.x, vector.y);
        }
    }
}
