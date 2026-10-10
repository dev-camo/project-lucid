using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HardlightProject
{
    // Complete original Game.Runtime 0x02000299: five ordinary methods and
    // two genuine OnCameraUnloaded closure methods. All full native ranges
    // are retained; natural compiler emission/native binding is unverified.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CameraStackManager : ISystem
    {
        private Camera m_baseCamera;
        private readonly Dictionary<Camera, AudioListener> m_cameraAudioListeners = new Dictionary<Camera, AudioListener>();

        public void OnCameraLoaded(Camera camera, AudioListener audioListener)
        {
            if (m_baseCamera == camera)
                return;

            // Original set_Item permits replacing a retained listener entry.
            m_cameraAudioListeners[camera] = audioListener;
            audioListener.enabled = false;
            if (m_baseCamera == null)
            {
                SetBaseCamera(camera);
                return;
            }

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            UniversalAdditionalCameraData baseCameraData = m_baseCamera.GetUniversalAdditionalCameraData();
            if (cameraData.renderType != CameraRenderType.Base)
            {
                baseCameraData.cameraStack.AddUnique(camera);
                return;
            }

            Camera oldBaseCamera = m_baseCamera;
            SetBaseCamera(camera, baseCameraData.cameraStack);
            baseCameraData.renderType = CameraRenderType.Overlay;
            cameraData.cameraStack.AddUnique(oldBaseCamera);
        }

        private void SetBaseCamera(Camera camera, List<Camera> cameraStack = null)
        {
            if (m_baseCamera != null && m_cameraAudioListeners.TryGetValue(m_baseCamera, out AudioListener oldAudioListener))
                oldAudioListener.enabled = false;

            m_baseCamera = camera;
            UniversalAdditionalCameraData cameraData = m_baseCamera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Base;
            if (cameraStack != null)
            {
                foreach (Camera stackCamera in cameraStack)
                {
                    if (stackCamera == m_baseCamera)
                        continue;
                    cameraData.cameraStack.AddUnique(stackCamera);
                }
            }

            if (m_cameraAudioListeners.TryGetValue(camera, out AudioListener audioListener))
                audioListener.enabled = true;
        }

        public void OnCameraUnloaded(Camera camera)
        {
            if (m_cameraAudioListeners.TryGetValue(camera, out AudioListener audioListener))
                audioListener.enabled = false;

            if (camera == m_baseCamera)
            {
                UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
                if (cameraData.cameraStack.Count > 0)
                {
                    int cameraIndex = 0;
                    SystemRef<CinemachineCameraManager> cameraManager = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
                    if (cameraManager.IsValid())
                    {
                        // The original natural capture is named gameCamera;
                        // its predicate uses Unity equality, whereas AddUnique
                        // retains the original IList.Contains equality rules.
                        Camera gameCamera = cameraManager.Get().MainCamera;
                        if (gameCamera.enabled)
                        {
                            // Native code clamps a negative FindIndex to zero;
                            // the exact authored integer-max spelling is inferred.
                            cameraIndex = Mathf.Max(0, cameraData.cameraStack.FindIndex(cam => cam == gameCamera));
                        }
                    }

                    Camera nextBaseCamera = cameraData.cameraStack[cameraIndex];
                    cameraData.cameraStack.RemoveAt(cameraIndex);
                    SetBaseCamera(nextBaseCamera, cameraData.cameraStack);
                }
            }
            else if (m_baseCamera != null)
            {
                m_baseCamera.GetUniversalAdditionalCameraData().cameraStack.Remove(camera);
            }

            // Original unload retains dictionary entries and does not clear
            // the base-camera field when its camera stack is empty.
        }

        public bool TryGetAudioListener(Camera camera, out AudioListener audioListener)
        {
            return m_cameraAudioListeners.TryGetValue(camera, out audioListener);
        }

        public CameraStackManager()
        {
        }
    }
}
