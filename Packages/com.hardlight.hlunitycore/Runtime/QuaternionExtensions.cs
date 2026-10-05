using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000077; complete six-method type.
    public static class QuaternionExtensions
    {
        // 0x060002d4: Unity vector equality supplies the original near-zero
        // threshold; the matrix's forward/up columns retain their exact order.
        public static Quaternion ExtractRotationFromMatrix(ref Matrix4x4 matrix)
        {
            Vector3 forward = new Vector3(matrix.m02, matrix.m12, matrix.m22);
            Vector3 up = new Vector3(matrix.m01, matrix.m11, matrix.m21);
            if (forward == Vector3.zero) return Quaternion.identity;
            return Quaternion.LookRotation(forward, up);
        }

        // 0x060002d5: relative rotation is B * Inverse(A). The axis-angle sign
        // test precedes wrapping, so a zero comparison axis keeps the angle.
        public static float SignedAngle(Quaternion A, Quaternion B, Vector3 axis)
        {
            Quaternion relative = B * Quaternion.Inverse(A);
            relative.ToAngleAxis(out float angle, out Vector3 relativeAxis);
            if (Vector3.Angle(axis, relativeAxis) > 90f) angle = -angle;
            angle = Mathf.Repeat(angle, 360f);
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        // 0x060002d6: do not normalize the result. The original independently
        // smooths all four components, then removes the velocity component
        // parallel to that unnormalized quaternion, including zero/NaN cases.
        public static Quaternion SmoothDamp(Quaternion current, Quaternion target,
            ref Quaternion currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            if (Quaternion.Dot(current, target) < 0f)
                target = new Quaternion(-target.x, -target.y, -target.z, -target.w);
            Quaternion result = new Quaternion(
                Mathf.SmoothDamp(current.x, target.x, ref currentVelocity.x, smoothTime, maxSpeed, deltaTime),
                Mathf.SmoothDamp(current.y, target.y, ref currentVelocity.y, smoothTime, maxSpeed, deltaTime),
                Mathf.SmoothDamp(current.z, target.z, ref currentVelocity.z, smoothTime, maxSpeed, deltaTime),
                Mathf.SmoothDamp(current.w, target.w, ref currentVelocity.w, smoothTime, maxSpeed, deltaTime));
            float parallel = Quaternion.Dot(currentVelocity, result) / Quaternion.Dot(result, result);
            currentVelocity.x -= result.x * parallel;
            currentVelocity.y -= result.y * parallel;
            currentVelocity.z -= result.z * parallel;
            currentVelocity.w -= result.w * parallel;
            return result;
        }

        // 0x060002d7: current-culture Single parsing, exact parentheses and four
        // comma-delimited fields. Commit to the ref only after all four parse;
        // a null input retains the original NullReferenceException boundary.
        public static bool TryParse(string sQuaternion, ref Quaternion result)
        {
            if (!sQuaternion.StartsWith("(") || !sQuaternion.EndsWith(")")) return false;
            string[] parts = sQuaternion.Substring(1, sQuaternion.Length - 2).Split(',');
            if (parts.Length != 4) return false;
            if (!float.TryParse(parts[0], out float x) || !float.TryParse(parts[1], out float y) ||
                !float.TryParse(parts[2], out float z) || !float.TryParse(parts[3], out float w)) return false;
            result = new Quaternion(x, y, z, w);
            return true;
        }

        // 0x060002d8: retain the real reusable OpString path and its signed
        // decimal formatter, then String.Format's original current culture.
        public static string ToDebugString(this Quaternion q, int numDecimalPlaces = 2)
        {
            OpString format = OpString.i + "({0:F" + numDecimalPlaces + "}, {1:F" + numDecimalPlaces
                + "}, {2:F" + numDecimalPlaces + "}, {3:F" + numDecimalPlaces + "})";
            return string.Format(format.ToString(), new object[] { q.x, q.y, q.z, q.w });
        }
        public static bool IsValid(this Quaternion q) // 0x060002d9
        {
            return q.w.IsValid() && q.x.IsValid() && q.y.IsValid() && q.z.IsValid();
        }
    }
}
