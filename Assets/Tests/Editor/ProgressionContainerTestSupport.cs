using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Shared setup for the progression screens: the real App, DataManager and SaveManager of
    // OwnedCharacterManagers, an empty UI event list in the App storage and containers built by their real
    // constructors on inactive owned hosts (Awake and Start never run by themselves in Edit Mode, so a case
    // calls them).
    internal static class ProgressionContainerTestSupport
    {
        internal static void WithApp(Action<Owned, App> body) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: it owns the App, DataManager and SaveManager rows.
            owned.Lease(new OwnedCharacterManagers());
            App app = ProcessManager.GetSystem<App>();
            app.Storage.SetValue(AppFSMKeys.UIModernEvents, new List<UIModernEvent>());
            app.Storage.SetValue(AppFSMKeys.MetaGameUnlocks, new List<IMetaGameUnlock>());
            body(owned, app);
        });

        // The pending unlocks the screens read from the App storage, head first.
        internal static void SetUnlocks(App app, params IMetaGameUnlock[] unlocks) =>
            app.Storage.SetValue(AppFSMKeys.MetaGameUnlocks, new List<IMetaGameUnlock>(unlocks));

        internal static T NewContainer<T>(Owned owned) where T : Component =>
            owned.Host("Lucid progression container", false).AddComponent<T>();

        internal static UIModernEvent NewEvent(Owned owned) => owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());

        internal static AnimationClip NewClip(Owned owned, string name)
        {
            var clip = owned.Track(new AnimationClip { name = name, legacy = true });
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return clip;
        }
    }
}
