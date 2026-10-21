using System;
using System.Collections;
using System.Diagnostics;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace HardlightProject
{
    // Original Game.Runtime 020006e4: shows one ornament prefab under the scene's OrnamentRoot, lit by the additively
    // loaded ornament lighting scene. The member order fixes the compiler-generated names (<>c__DisplayClass11_0 and
    // <OnOrnamentLoaded>d__12 are ordinals 11 and 12, counting fields, the constant and methods in declaration order).
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OrnamentViewer : MonoBehaviour
    {
        [SerializeField]
        private float m_yRotationSpeed;
        public const string MessageIdentifier = "OrnamentViewer";
        private AsyncOperationHandle<GameObject> m_ornamentHandle;
        private OrnamentRoot m_ornamentRoot;
        private Ornament m_currentOrnament;
        private Scene m_previouslyActiveScene;
        private AsyncOperationHandle<SceneInstance> m_lightingScene;
        private MessageExchange<string> m_messageExchange;

        // 06002538 (0x5A01B0): the active scene is remembered, the viewer listens for its root, and the lighting scene
        // (additive, activated on load, priority 100) becomes the active scene once loaded.
        private void OnEnable()
        {
            m_previouslyActiveScene = SceneManager.GetActiveScene();
            m_messageExchange = ProcessManager.GetSystemAutoCreate<MessageExchange<string>>();
            m_messageExchange.SubscribeToMessage<OrnamentRoot>(MessageIdentifier, OnOrnamentRootLoaded);
            Addressables.LoadSceneAsync("s_ornament_lighting", LoadSceneMode.Additive, true, 100).Completed += OnSceneLoaded;
        }

        // 06002539 (0x5A03C0)
        private void OnOrnamentRootLoaded(in OrnamentRoot ornamentRoot)
        {
            m_ornamentRoot = ornamentRoot;
        }

        // 0600253a (0x5A03E0)
        private void OnSceneLoaded(AsyncOperationHandle<SceneInstance> sceneInstance)
        {
            m_lightingScene = sceneInstance;
            SceneManager.SetActiveScene(m_lightingScene.Result.Scene);
        }

        // 0600253b (0x5A0480): the ornament is loaded immediately; its callback starts the instantiating coroutine.
        public void ShowOrnament(AssetReferenceT<GameObject> ornament, Action onOrnamentInstantiated)
        {
            m_ornamentHandle = ProcessManager.GetSystem<AddressableManager>().LoadAssetImmediate<GameObject>(
                ornament, ornamentObject => CoroutineUtils.RunCoroutine(OnOrnamentLoaded(ornamentObject, onOrnamentInstantiated)));
        }

        // 0600253c (0x5A05F0), MoveNext 0x5A0A60: waits until the root has announced itself, instantiates the ornament
        // under it, scales it from zero to one around the rotator setup and reports through the callback, which is
        // invoked without a null test.
        private IEnumerator OnOrnamentLoaded(GameObject ornamentObject, Action onOrnamentInstantiated)
        {
            while (m_ornamentRoot == null)
                yield return null;
            GameObject instance = Instantiate(ornamentObject, m_ornamentRoot.transform);
            m_currentOrnament = instance.GetComponent<Ornament>();
            Transform ornamentTransform = m_currentOrnament.transform;
            // 0x5A0BB6..0x5A0BE6: the ping-pong rotator starts from no offset.
            ornamentTransform.localRotation = Quaternion.Euler(0f, m_currentOrnament.TransformRotator.PingPongWhenIdle ? 0f : m_currentOrnament.YRotationOffset, 0f);
            ornamentTransform.localScale = Vector3.zero;
            m_currentOrnament.TransformRotator.enabled = true;
            m_currentOrnament.TransformRotator.SetIdleRotationSpeed(new Vector3(0f, m_yRotationSpeed, 0f));
            m_currentOrnament.TransformRotator.ToggleAutomation(true);
            instance.transform.localScale = Vector3.one;
            onOrnamentInstantiated();
        }

        // 0600253d (0x5A06B0): the render textures are released first; then the listener, the ornament handle, the
        // previously active scene (if still loaded) and the lighting scene follow.
        private void OnDestroy()
        {
            ReleaseRenderTextures();
            m_messageExchange.UnsubscribeFromMessage<OrnamentRoot>(MessageIdentifier, OnOrnamentRootLoaded);
            ProcessManager.GetSystem<AddressableManager>().ReleaseHandle(m_ornamentHandle);
            if (m_previouslyActiveScene.isLoaded)
                SceneManager.SetActiveScene(m_previouslyActiveScene);
            Addressables.UnloadSceneAsync(m_lightingScene, true);
        }

        // 0600253e (0x5A0890): a camera with a target texture has it released and cleared. The development-only log of
        // each release (LogReleasingRenderTexture) is compiled out of retail, so no call to it is restored here.
        private static void ReleaseRenderTextures()
        {
            foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (camera.targetTexture == null)
                    continue;
                camera.targetTexture.Release();
                camera.targetTexture = null;
            }
        }

        // 0600253f (0x5A0970): native computes the scene hierarchy and discards it; whatever consumed the string is
        // compiled out and not recoverable.
        [Conditional("BUILD_DEVELOPMENT")]
        private static void LogReleasingRenderTexture(Camera camera)
        {
            camera.gameObject.transform.GetSceneHierarchy();
        }

    }
}
