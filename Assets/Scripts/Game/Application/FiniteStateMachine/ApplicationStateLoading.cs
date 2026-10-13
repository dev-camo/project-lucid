using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [GraphNodeMenuFormat("Application/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplicationStateLoading : FSMState
    {
        // Game.Runtime 060006ac: the original public constructor takes a String.
        // Keep its implicit String-to-FSMIdentifier conversion at the base call.
        public ApplicationStateLoading(FiniteStateMachine fsm, string stateName)
            : base(fsm, stateName) { }

        // 060006ad: the factory performs the inverse identifier-to-String
        // conversion before construction. The shipped factory ignores JSON.
        public static IFSMState ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new ApplicationStateLoading(fsm, transitionId);
        }

        // 060006ae: every registration is ordered and can retain earlier effects
        // if a genuine provider fails. This original path contains no rollback.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            App app = user.GetAs<App>();
            app.InitialiseProcessManagerSystems();
            app.RegisterAndInitialiseSystem<CameraStackManager>();
            app.RegisterAndInitialiseSystem<SaveManager>();
            app.RegisterAndInitialiseSystem<ProgressionManager>();
            app.RegisterAndInitialiseSystem<CutsceneManager>();
            app.RegisterAndInitialiseSystem<OrnamentManager>();
            app.RegisterAndInitialiseSystem<GameCenterAccessPointManager>();
            app.RegisterAndInitialiseSystem<UINavigationManager>();
            app.RegisterAndInitialiseSystem<WaypointManager>();
            app.RegisterAndInitialiseSystem<MissionManager>();
            app.RegisterAndInitialiseSystem<MusicTrackManager>();
            app.RegisterAndInitialiseSystem<CharacterCustomisationManager>();
            if (SystemConfiguration.GetConfig<CoreGameConfiguration>().GameCenter)
                app.RegisterAndInitialiseSystem<GameCenter>();

            // Original natural <>c owner: cctor060006af, ctor060006b0,
            // callback060006b1. Both CPUs retain the callback cache and its null
            // failure. The public setter is the inferred pre-inlining call form;
            // the shipping callback writes true to the original field at 0x29.
            ProcessManager.GetSystemRef<VibrationManager>(null, true)
                .InvokeOnValid(vibrationManager => vibrationManager.SetVibrationFromSettings(true));
        }
    }
}
