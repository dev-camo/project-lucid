using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Shared setup for the challenge reward and meta-game unlock fixtures: real definition assets
    // built through their constructors, cached asset wrappers, and the reward base's service references.
    internal static class ChallengeRewardTestSupport
    {
        internal static T Create<T>(Owned owned, string json = null) where T : ScriptableObject
        {
            var asset = owned.Track(ScriptableObject.CreateInstance<T>());
            // Unity's own serializer sets authored fields without reflection.
            if (json != null) JsonUtility.FromJsonOverwrite(json, asset);
            return asset;
        }

        internal static Sprite NewSprite(Owned owned)
        {
            Texture2D texture = owned.Track(new Texture2D(2, 2));
            return owned.Track(Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.zero));
        }

        // The wrapper's uncached path starts an Addressables load; its cached path is driven by storing the
        // asset the way a finished load does (the wrapper has no public setter for it).
        internal static void Cache<T>(ManagedAddressableAsset<T> wrapper, T asset) where T : Object =>
            Set(wrapper, "m_loadedAsset", asset);

        internal static int Count<T>(ManagedAddressableAsset<T> wrapper) where T : Object =>
            Get<int>(wrapper, "m_refCount");

        internal static bool IsCached<T>(ManagedAddressableAsset<T> wrapper) where T : Object =>
            Get<T>(wrapper, "m_loadedAsset") != null;

        // ChallengeReward's static initialiser creates the DataManager and SaveManager rows when they are
        // absent; a case that does not take OwnedChallengeRewardServices leases them first.
        internal static void LeaseRewardRows(Owned owned)
        {
            owned.LeaseRow<DataManager>();
            owned.LeaseRow<SaveManager>();
        }

        // A reward built by its real constructor. Its static initialiser reads the two service rows.
        internal static T NewReward<T>(Owned owned, string json = null) where T : ChallengeReward => Create<T>(owned, json);

        internal static readonly RewardTrackType[] TrackTypes =
            { RewardTrackType.Adventure, RewardTrackType.Outfits, RewardTrackType.Shadow };
    }

    // The genuine SaveManager (with an open save), DataManager and App of OwnedCharacterManagers, and the
    // two static references ChallengeReward reads them through. The static fields are readonly, so they are
    // never replaced: an empty reference is filled and revoked again by StaticSystemReference.
    internal sealed class OwnedChallengeRewardServices : IDisposable
    {
        private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly OwnedCharacterManagers m_characters = new OwnedCharacterManagers();
        private readonly List<Action> m_restore = new List<Action>();
        private bool m_disposed;

        public SaveManager Save { get; private set; }
        public DataManager Data { get; private set; }

        public OwnedChallengeRewardServices()
        {
            bool built = false;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                Save = ProcessManager.GetSystem<SaveManager>();
                Data = ProcessManager.GetSystem<DataManager>();
                // Reading a static runs ChallengeReward's initialiser while both rows hold live systems.
                Bind("s_dataManagerRef", Data);
                Bind("s_saveManagerRef", Save);
                built = true;
            }, () => { if (!built) Dispose(); });
        }

        private void Bind<T>(string field, T system) where T : class, ISystem
        {
            Action restore = StaticSystemReference.Bind(typeof(ChallengeReward), field, system);
            if (restore != null) m_restore.Add(restore);
        }

        // A reward record the open save can look up by the track GUID the reward carries.
        public SaveDataChallengeReward AddRecord(string trackGuid, string rewardGuid)
        {
            Save.CurrentSave.CreateRewardTrackData(RewardTrackType.Adventure, trackGuid, rewardGuid);
            return Save.CurrentSave.GetRewardDataByTrackGUIDUnsafe(trackGuid);
        }

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            var steps = new List<Action>(m_restore);
            steps.Reverse();
            steps.Add(m_characters.Dispose);
            VerificationFixtureOwnership.RunThenCleanUp(() => { }, steps.ToArray());
        }
    }

    // The typed registry references that fixture owners read through private static readonly fields. A type
    // initialiser creates each once per process, and an earlier case's row release may leave it empty. An empty
    // one is filled through its own private InternalReplaceSystem; the returned action revokes it again and
    // restores the observers it had, so nothing outlives the case. A reference already holding the system needs
    // no restore (null); one holding another system is refused.
    internal static class StaticSystemReference
    {
        private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        internal static Action Bind<T>(Type owner, string field, T system) where T : class, ISystem
        {
            var reference = (SystemRef<T>)owner.GetField(field, Statics).GetValue(null);
            if (reference.IsValid())
            {
                if (!ReferenceEquals(reference.GetSafe(), system))
                    throw new InvalidOperationException("Fixture requires an empty reference: " + owner.Name + "." + field);
                return null;
            }
            // Reflection: the system reference exposes no public route that fills an empty reference without the App.
            MethodInfo replace = typeof(SystemRef<T>).GetMethod("Hardlight.ISystemRef.InternalReplaceSystem", Instance);
            MethodInfo revoke = typeof(SystemRef<T>).GetMethod("Hardlight.ISystemRef.InternalRevokeSystem", Instance);
            FieldInfo startup = typeof(SystemRef<T>).GetField("OnSystemStartup", Instance);
            object priorObservers = startup.GetValue(reference);
            replace.Invoke(reference, new object[] { system });
            return () =>
            {
                revoke.Invoke(reference, null);
                startup.SetValue(reference, priorObservers);
            };
        }
    }
}
