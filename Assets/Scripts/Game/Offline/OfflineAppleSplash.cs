using System.Collections;
using Hardlight;
using HardlightProject;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace ProjectLucid.Offline
{
    // Offline Apple Arcade splash skip at the boot graph (hook H-064, ApplicationStateBoot.DoOnEnter),
    // for prepared projects where the restored splash player is not bound to the s_apple scene.
    //
    // Shipped graph (StreamingAssets/FSM/Application/Application_Splash.json): Boot loads s_apple
    // (CtorArgs SceneName "s_apple"), AppleLoaded leads to WaitForApple, and WaitForApple leaves only
    // through AppleUnloaded (s_apple no longer loaded) to LoadSplash. Nothing in the graph unloads
    // s_apple: the scene unloads itself. Its splash player (Apple.Arcade.SplashScreen.SplashScreenPlayer,
    // on the AppleArcadeSplashScreenPlayer object with its VideoPlayer) has one persistent listener on
    // m_onPlayerLoopPointReached: Action_Unload(true) on the UnloadSceneAfterFirstBoot of that same
    // object. The other UnloadSceneAfterFirstBoot in the scene (the EventSystem object) belongs to the
    // controller-input skip, Action_Unload(false), and is not touched.
    //
    // The prepared scene keeps UnloadSceneAfterFirstBoot (its script GUID is the exported one) but not
    // the splash player (restored with a fresh GUID, no scriptbindings rule yet), so H-056 never runs
    // there. This adapter runs the same listener call once the graph has left Boot: when s_apple has
    // loaded and the boot graph's active state is no longer the Boot state, it calls Action_Unload(true)
    // on the listener component. Waiting for the graph to leave Boot matters because the default save
    // is loaded synchronously by Boot, so Action_Unload(true) starts the unload at once; unloading
    // before Boot has seen AppleLoaded would leave the graph in Boot. The wait is
    // OfflineSplashBootWait (HLUnityCore.Runtime), shared with H-056: Arm records the Boot state there
    // too, and when the splash player is bound (a component of that type is in the scene) H-056 runs
    // its own completion path through that record once Boot is left, and this adapter does nothing.
    public static class OfflineAppleSplash
    {
        private const string SplashPlayerTypeName = "Apple.Arcade.SplashScreen.SplashScreenPlayer";

        // Called from the Boot state before its original body loads the scene.
        public static void Arm(FSMState bootState, IGraphUser user, string sceneName)
        {
            OfflineSplashBootWait.Arm(bootState.FSM, bootState, user);
            SceneManager.sceneLoaded += new PendingSplash(bootState, user, sceneName).OnSceneLoaded;
        }

        // The listener the splash player would call: the UnloadSceneAfterFirstBoot on the object that
        // holds the splash VideoPlayer. Null when a splash player component is present (H-056 handles
        // the scene) or when no such listener exists.
        public static UnloadSceneAfterFirstBoot FindSplashListener(GameObject[] roots)
        {
            UnloadSceneAfterFirstBoot listener = null;
            foreach (GameObject root in roots)
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && behaviour.GetType().FullName == SplashPlayerTypeName)
                        return null;
                }
                if (listener != null) continue;
                foreach (VideoPlayer video in root.GetComponentsInChildren<VideoPlayer>(true))
                {
                    listener = video.GetComponent<UnloadSceneAfterFirstBoot>();
                    if (listener != null) break;
                }
            }
            return listener;
        }

        // Waits while the boot graph is still in the Boot state, then makes the splash's listener call.
        public static IEnumerator CompleteAfterBoot(FiniteStateMachine fsm, IFSMState bootState, IGraphUser user,
            UnloadSceneAfterFirstBoot listener)
        {
            return OfflineSplashBootWait.WhileInBoot(fsm, bootState, user, () => listener.Action_Unload(true));
        }

        private sealed class PendingSplash
        {
            private readonly FSMState m_bootState;
            private readonly IGraphUser m_user;
            private readonly string m_sceneName;

            internal PendingSplash(FSMState bootState, IGraphUser user, string sceneName)
            {
                m_bootState = bootState;
                m_user = user;
                m_sceneName = sceneName;
            }

            internal void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != m_sceneName) return;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                UnloadSceneAfterFirstBoot listener = FindSplashListener(scene.GetRootGameObjects());
                if (listener == null) return;
                // This adapter makes the call, so the bound-player record is not needed.
                OfflineSplashBootWait.Disarm();
                // The coroutine lives on the listener, so it ends with the scene.
                listener.StartCoroutine(CompleteAfterBoot(m_bootState.FSM, m_bootState, m_user, listener));
            }
        }
    }
}
