using System;
using System.Reflection;
using Cinemachine;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Shared setup for the Cinemachine camera extension fixtures. A CinemachineVirtualCamera or a
    // CinemachineFreeLook builds its pipeline child or its three rigs in the Reset that AddComponent sends in
    // Edit Mode. The Editor assembly may install an Undo-recording hook in the static creation overrides, so
    // the component is added with both hooks null (a plain owned GameObject results) and the hooks put back.
    internal static class CinemachineCameraTestRig
    {
        private static readonly FieldInfo SoloCameraField =
            typeof(CinemachineBrain).GetField("mSoloCamera", BindingFlags.Static | BindingFlags.NonPublic);

        internal static T AddWithoutPipelineHooks<T>(GameObject host) where T : Component
        {
            CinemachineVirtualCamera.CreatePipelineDelegate priorPipeline = CinemachineVirtualCamera.CreatePipelineOverride;
            CinemachineFreeLook.CreateRigDelegate priorRig = CinemachineFreeLook.CreateRigOverride;
            try
            {
                CinemachineVirtualCamera.CreatePipelineOverride = null;
                CinemachineFreeLook.CreateRigOverride = null;
                return host.AddComponent<T>();
            }
            finally
            {
                CinemachineVirtualCamera.CreatePipelineOverride = priorPipeline;
                CinemachineFreeLook.CreateRigOverride = priorRig;
            }
        }

        // Makes the camera the brain's solo camera, which CinemachineBrain.IsLive answers true for. The public
        // SoloCamera setter would also run the camera's transition (it builds and updates the child cameras),
        // which a case about the liveness test alone does not want, so the static field is written directly
        // and put back by the returned release.
        internal static IDisposable MakeLive(ICinemachineCamera camera)
        {
            object prior = SoloCameraField.GetValue(null);
            SoloCameraField.SetValue(null, camera);
            return new Release(() => SoloCameraField.SetValue(null, prior));
        }

        internal sealed class Release : IDisposable
        {
            private Action m_release;
            internal Release(Action release) => m_release = release;

            public void Dispose()
            {
                Action release = m_release;
                m_release = null;
                release?.Invoke();
            }
        }
    }
}
