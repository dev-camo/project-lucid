// Original Game.Runtime.dll:HardlightProject.ApplicationStateBoot.
// Complete methods: 0x0600063b, 0x0600063c, 0x0600063d, 0x0600063e.
// arm64 ranges: 0x0600063e 0x512d18..0x512d20, 0x0600063b 0x5126fc..0x512758, 0x0600063c 0x512758..0x512840, 0x0600063d 0x512840..0x512d18.
// x86_64 ranges: 0x0600063e 0x53c680..0x53c690, 0x0600063b 0x53c0a0..0x53c0f0, 0x0600063c 0x53c0f0..0x53c1b0, 0x0600063d 0x53c1b0..0x53c680.
// Original macOS refresh fraction is numerator=1, denominator=60 in both native builds; preserve it.
// Base enter, display setup, store construction/registration/load and GameLevels caching precede scene loading.
// The original dictionary read follows the load request; present-null storage retains its fault.
// The scene name and user.Storage are read again at their later original use sites.
// Hook H-064 (DoOnEnter): see the comment in DoOnEnter. The original code after it is unchanged.
using System;
using System.Collections.Generic;
using Hardlight;
using ProjectLucid.Offline;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplicationStateBoot : FSMState
    {
        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class JSONCtorArgs
        {
            [GraphUnityScene] public string SceneName;
            public JSONCtorArgs() { }
        }

        private readonly string m_sceneName;

        private ApplicationStateBoot(FiniteStateMachine fsm, string stateName, string sceneName)
            : base(fsm, stateName)
        {
            m_sceneName = sceneName;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            return new ApplicationStateBoot(fsm, transitionId, args.SceneName);
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            // Offline hook H-064: on every desktop target (owner decision D7) the Apple Arcade splash
            // is skipped even where the prepared s_apple scene has no bound splash player (so H-056
            // cannot run): once the scene loaded below is in and the graph has left Boot, the adapter
            // makes the splash player's own listener call, Action_Unload(true) on the scene's
            // UnloadSceneAfterFirstBoot, and WaitForApple leaves through AppleUnloaded. Then the
            // original body runs unchanged.
            if (OfflineAdapters.Active && OfflineDesktopPlatform.IsCurrent)
                OfflineAppleSplash.Arm(this, user, m_sceneName);
            base.DoOnEnter(user, action);
            if (Application.platform == RuntimePlatform.OSXPlayer)
            {
                DisplayInfo displayInfo = Screen.mainWindowDisplayInfo;
                Screen.SetResolution(displayInfo.width, displayInfo.height,
                    Screen.fullScreenMode, new RefreshRate { numerator = 1, denominator = 60 });
            }
            else
            {
                Application.targetFrameRate = 60;
            }

            var propertyStore = new HLPropertyStore("H4RDl1ght_S4VE", 0, "Primary");
            ProcessManager.RegisterSystem(propertyStore, null, false, false);
            propertyStore.LoadFromIdentifier("HL_Default_Save_ID");
            SystemConfiguration.GetConfig<GameLevels>().CacheLevels();

            var handle = Addressables.LoadSceneAsync(m_sceneName, LoadSceneMode.Additive, true, 100);
            if (!user.Storage.TryGetValue(AppFSMKeys.LoadedLevels,
                out Dictionary<string, AsyncOperationHandle> loadedLevels))
                loadedLevels = new Dictionary<string, AsyncOperationHandle>();
            loadedLevels[m_sceneName] = handle;
            user.Storage.SetValue(AppFSMKeys.LoadedLevels, loadedLevels);
        }
    }
}
