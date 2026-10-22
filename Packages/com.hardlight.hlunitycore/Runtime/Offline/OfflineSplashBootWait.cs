using System;
using System.Collections;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Offline
{
    // The one wait behind both desktop splash skips (H-056 in SplashScreenPlayer.Start, H-064 in
    // ApplicationStateBoot.DoOnEnter): the splash completion runs only once the boot graph has left
    // its Boot state.
    //
    // Why: the shipped graph (Application_Splash.json) leaves Boot only through AppleLoaded
    // (s_apple loaded, checked through SceneManager) and WaitForApple only through AppleUnloaded.
    // The completion's listener, Action_Unload(true) on s_apple's UnloadSceneAfterFirstBoot, starts
    // the scene's unload at once, because Boot loads the default save synchronously and the store
    // calls a late load handler immediately. Run before the graph has seen s_apple loaded, it would
    // leave the graph in Boot. In the original the completion is the video's loop point, seconds
    // after the scene loaded, so Boot has long since advanced; waiting for that is the original's own
    // order without the video.
    //
    // Lives in HLUnityCore.Runtime beside FiniteStateMachine because both hooked assemblies reach it:
    // Apple.Arcade.SplashScreen references only HLUnityCore.Runtime, and Game.Runtime does not
    // reference the splash assembly.
    public static class OfflineSplashBootWait
    {
        private static FiniteStateMachine s_fsm;
        private static IFSMState s_bootState;
        private static IGraphUser s_user;

        public static bool IsArmed => s_fsm != null;

        // The Boot state records itself before its original body loads s_apple (H-064). A later Boot
        // replaces the record.
        public static void Arm(FiniteStateMachine fsm, IFSMState bootState, IGraphUser user)
        {
            s_fsm = fsm;
            s_bootState = bootState;
            s_user = user;
        }

        public static void Disarm()
        {
            s_fsm = null;
            s_bootState = null;
            s_user = null;
        }

        // H-056: runs the bound splash player's completion once the armed boot graph has left Boot, and
        // consumes the record. With no armed Boot, or Boot already left, the completion runs now and no
        // coroutine is started; otherwise the rest of the wait runs as a coroutine on the player.
        public static void CompleteAfterBoot(MonoBehaviour host, Action completion)
        {
            FiniteStateMachine fsm = s_fsm;
            IFSMState bootState = s_bootState;
            IGraphUser user = s_user;
            Disarm();
            if (fsm == null)
            {
                completion();
                return;
            }
            IEnumerator wait = WhileInBoot(fsm, bootState, user, completion);
            if (wait.MoveNext())
                host.StartCoroutine(wait);
        }

        // Yields once per frame while the user's active state is the Boot state, then runs the completion once.
        public static IEnumerator WhileInBoot(FiniteStateMachine fsm, IFSMState bootState, IGraphUser user, Action completion)
        {
            while (ReferenceEquals(fsm.GetActiveState(user), bootState))
                yield return null;
            completion();
        }
    }
}
