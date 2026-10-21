using Dreamteck;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Player branch of TS_Transform. The editor compile caches isPlaying in Update (Application.isPlaying, true in play mode)
    // and starts each point/direction method with `if (!isPlaying) return transform.X(...)`; the shipped build has no such line.
    // The EditMode class OriginalDreamteckMeshTests covers the editor branch (PointAndDirectionTransformsForwardToTheWrappedTransformInEditMode).
    public sealed class OriginalDreamteckTransformSnapshotPlayModeTests
    {
        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-5f), message + " (actual " + actual.ToString("F6") + ")");
        }

        // Original TransformPoint 0x504e40 = rotation * Scale(point, lossyScale) + position (scale, then rotate,
        // then translate); TransformDirection 0x504f50 = TransformPoint(direction) - position;
        // InverseTransformPoint 0x5050a0 = inverse(rotation) * (point - position) divided component-wise by the
        // lossy scale; InverseTransformDirection 0x5051b0 = inverse(rotation) * direction divided likewise.
        // A quarter turn about Y maps (x, y, z) to (z, y, -x), so scaling (1, 1, 1) by (2, 3, 4) first gives
        // (4, 3, -2); rotating first and scaling second would give (2, 3, -4).
        // The constructor runs Update on a real transform, which caches isPlaying = Application.isPlaying (true here), so the
        // shipped arithmetic runs. The wrapped transform keeps different values, so a result that followed it would differ.
        [Test]
        public void PointAndDirectionTransformsScaleThenRotateThenTranslate()
        {
            GameObject target = new GameObject("snapshot target");
            try
            {
                target.transform.position = new Vector3(-7f, 8f, 9f);
                target.transform.rotation = Quaternion.Euler(30f, 40f, 50f);
                target.transform.localScale = new Vector3(5f, 6f, 7f);
                TS_Transform snapshot = new TS_Transform(target.transform);
                snapshot.position = new Vector3(1f, 0f, 0f);
                snapshot.rotation = Quaternion.Euler(0f, 90f, 0f);
                snapshot.lossyScale = new Vector3(2f, 3f, 4f);

                Near(snapshot.TransformPoint(Vector3.one), new Vector3(5f, 3f, -2f), "TransformPoint");
                Near(snapshot.TransformDirection(Vector3.one), new Vector3(4f, 3f, -2f), "TransformDirection");
                Near(snapshot.InverseTransformPoint(new Vector3(5f, 3f, -2f)), Vector3.one, "InverseTransformPoint");
                Near(snapshot.InverseTransformDirection(new Vector3(4f, 3f, -2f)), Vector3.one, "InverseTransformDirection");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
