// Complete original Game.Runtime 02000a4f and its original noncapturing callback family.
using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class DebugTrackRender
    {
        private GameObject m_debugRoot;
        private readonly TrackManager m_trackManager;
        private List<SurfaceMeshPipeGenerator> m_debugRailMeshes;

        public DebugTrackRender(TrackManager trackManager) // 06003b3e
        {
            m_trackManager = trackManager;
            m_debugRoot = new GameObject("TrackDebug");
            SceneManager.MoveGameObjectToScene(m_debugRoot, trackManager.gameObject.scene);
            m_debugRoot.transform.SetParent(trackManager.transform);
        }
        public void Close() // 06003b3f: the stored generator list remains unchanged.
        {
            if (m_debugRoot == null) return;
            UnityEngine.Object.Destroy(m_debugRoot);
            m_debugRoot = null;
        }
        [Conditional("BUILD_DEVELOPMENT")]
        public void Render(bool visible) // 06003b40 + original <>c callback06003b45.
        {
            if (visible)
            {
                if (m_debugRailMeshes != null) return;
                m_debugRailMeshes = new List<SurfaceMeshPipeGenerator>();
                m_trackManager.ForEachRailNeighbour(sections =>
                {
                    foreach (SurfaceSplineSection section in sections)
                    {
                        // Shipping retains enumeration/current/disposal after conditional drawing calls disappear.
                    }
                });
            }
            else
            {
                if (m_debugRailMeshes == null) return;
                foreach (SurfaceMeshPipeGenerator mesh in m_debugRailMeshes) UnityEngine.Object.Destroy(mesh.gameObject);
                m_debugRailMeshes = null;
            }
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void DebugAddSplineSectionMesh(SurfaceSplineSection splineSplineSection, float distanceStep = 1f) // 06003b41
        {
            ObjectSerializable<ISpline> wrapper = splineSplineSection.SourceSpline;
            MonoBehaviour component = wrapper.Value as MonoBehaviour;
            if (component == null) return;
            DebugRailConfiguration configuration = SystemConfiguration.GetConfig<DebugRailConfiguration>();
            Transform sourceTransform = component.transform;
            GameObject section = new GameObject(sourceTransform.parent.name + "_section");
            section.transform.SetParent(m_debugRoot.transform);
            section.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
            SurfaceMeshPipeGenerator mesh = section.AddComponent<SurfaceMeshPipeGenerator>();
            m_debugRailMeshes.Add(mesh);
            List<LightweightTransform> points = new List<LightweightTransform>();
            float distance = splineSplineSection.SourceDistanceStart;
            float end = splineSplineSection.SourceDistanceEnd;
            while (distance < end)
            {
                DebugAddSplinePoint(wrapper.Value, distance, points, splineSplineSection.TargetSide, configuration.SpaceOffset);
                distance += distanceStep;
                end = splineSplineSection.SourceDistanceEnd;
            }
            DebugAddSplinePoint(wrapper.Value, end, points, splineSplineSection.TargetSide, configuration.SpaceOffset);
        }
        private static void DebugAddSplinePoint(ISpline spline, float distance, List<LightweightTransform> points,
            SurfaceSplineSection.Side side, float spaceOffset) // 06003b42
        {
            LightweightTransform transform = spline.GetWorldTransformFromDistance(distance);
            Vector3 right = transform.Orientation * Vector3.right;
            if (side == SurfaceSplineSection.Side.Left) right = -right;
            transform.Location += right * spaceOffset;
            points.Add(transform);
        }
    }
}
