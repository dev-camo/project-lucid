using UnityEngine;

namespace Hardlight
{
    public class SurfaceEditorDefinition : ScriptableObject
    {
        // HLSplines.Runtime 0x06000225: each access copies the current skin style.
        public GUIStyle ButtonStyle => new GUIStyle(GUI.skin.button);

        // Original 0x06000226 and 0x06000227 preserve copy, font, state and colour order.
        public GUIStyle LabelStyle
        {
            get
            {
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.grey;
                return style;
            }
        }

        public GUIStyle LabelWarningStyle
        {
            get
            {
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.yellow;
                return style;
            }
        }

        // Original 0x06000228: the eight corners are tested in authored order.
        protected static bool HasVisibleCorners(Bounds bounds, Vector3 cameraPosition, Vector3 cameraForward)
        {
            Vector3[] corners = GetBoundsCorners(bounds);
            for (int index = 0; index < corners.Length; ++index)
                if (IsVisible(cameraPosition, cameraForward, corners[index]))
                    return true;
            return false;
        }

        // Original 0x06000229: camera-minus-point and a strict negative dot product.
        protected static bool IsVisible(Vector3 camPosition, Vector3 camForward, Vector3 testedPosition)
        {
            return Vector3.Dot(camForward, (camPosition - testedPosition).normalized) < 0f;
        }

        // Original 0x0600022a: min/max are re-read for each corner, preserving order.
        private static Vector3[] GetBoundsCorners(Bounds bounds)
        {
            return new Vector3[]
            {
                bounds.min,
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                bounds.max
            };
        }

        // Original 0x0600022b: Camera.current is tested then reloaded; the distance is squared.
        protected static float GetDistanceFromCamera(Vector3 location)
        {
            if (Camera.current != null)
            {
                Transform transform = Camera.current.transform;
                Vector3 cameraForward = transform.forward;
                Vector3 cameraPosition = transform.position;
                Vector3 delta = cameraPosition - location;
                float distance = delta.sqrMagnitude;
                return Vector3.Dot(cameraForward, delta.normalized) < 0f ? distance : -1f;
            }
            return float.MaxValue;
        }

        // Original 0x0600022c: a rear-facing centre can still have visible corners.
        protected static float GetDistanceFromCamera(Bounds bounds)
        {
            if (Camera.current != null)
            {
                Transform transform = Camera.current.transform;
                Vector3 cameraForward = transform.forward;
                Vector3 cameraPosition = transform.position;
                bool cameraInside = bounds.Contains(cameraPosition);
                float dot = Vector3.Dot(cameraForward, (cameraPosition - bounds.center).normalized);
                float distance = (cameraPosition - bounds.center).sqrMagnitude;
                if (dot < 0f || cameraInside)
                    return distance;
                if (distance > bounds.extents.sqrMagnitude)
                    return -1f;
                return HasVisibleCorners(bounds, cameraPosition, cameraForward) ? distance : -1f;
            }
            return float.MaxValue;
        }
    }
}
