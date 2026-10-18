using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ProjectLucid.Tests
{
    // Owned services for the Game.Runtime audio components. Every registry row
    // a component constructor or body reads is admitted before the component
    // exists: AudioManager (and any row a case registers into) is acquired
    // absent and removed, rows only read are borrowed idle or owned through
    // LifecycleRow. The AudioManager is the real class, inactive so Awake never
    // runs; its two handler fields are written by reflection and it is
    // registered directly. That skips what DataManagerValid does in the
    // original: no AudioManager_ManagedObjects parent, no HLAudioMixerHandler
    // (m_audioMixerHandler stays null, so GetMixer and ApplyAudioSettings would
    // fault on fixture state), no LoadAudioSource of the UI, UI background and
    // jukebox sources, and no Shutdown subscription. The components under test
    // reach none of those. Clips the case declares are completed handles from
    // an owned ResourceManager, so GetClip answers synchronously; any other
    // identifier misses an empty DataManager table and answers with a null
    // clip. HLOutput errors are read through OwnedCoreErrorLog, the audio
    // enum-string tables are leased, and UnityEngine.Random.state is put back
    // to what it was when the services were constructed, so a case seeds the
    // generator only after constructing them.
    internal sealed class OwnedAudioServices : IDisposable
    {
        internal const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<IDisposable> m_releases = new List<IDisposable>();
        private readonly Dictionary<string, VerificationFixtureOwnership.RegistryRow> m_rows =
            new Dictionary<string, VerificationFixtureOwnership.RegistryRow>();
        private readonly List<Object> m_objects = new List<Object>();
        private readonly List<AsyncOperationHandle> m_handles = new List<AsyncOperationHandle>();
        private readonly Random.State m_randomState;
        private ResourceManager m_resources;
        private HLAudioClipHandler m_clipHandler;
        private bool m_disposed;

        public readonly OwnedCoreErrorLog Log;
        public AudioManager Audio { get; private set; }

        public OwnedAudioServices()
        {
            m_randomState = Random.state;
            try
            {
                m_releases.Add(new VerificationFixtureOwnership.StaticDictionaryLease(typeof(HardlightProject.HardlightEnumExtensions),
                    "HLAudioClipIdentifierEnumToString", "HLAudioSourceIdentifierEnumToString"));
                Log = new OwnedCoreErrorLog();
                m_releases.Add(Log);
                Row<AudioManager>();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        // A row the case registers into: acquired absent, holding no system yet.
        public void Row<T>() where T : class, ISystem
        {
            string name = ProcessManager.GetDefaultName<T>();
            var row = new VerificationFixtureOwnership.RegistryRow(name);
            m_releases.Add(row);
            m_rows.Add(name, row);
            row.Register(null);
        }

        // A row only read through GetSystemRef or GetSystem.
        public void ReadRow<T>() where T : class, ISystem =>
            m_releases.Add(new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<T>()));

        public void Register<T>(T system) where T : class, ISystem => m_rows[ProcessManager.GetDefaultName<T>()].Register(system);

        public IDictionary TypedReferences<T>() where T : class, ISystem
        {
            var registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            object row = registry[ProcessManager.GetDefaultName<T>()];
            return (IDictionary)row.GetType().GetField("SystemRefDictionary").GetValue(row);
        }

        public GameObject Host(string name)
        {
            var host = new GameObject("Lucid audio " + name);
            host.SetActive(false);
            m_objects.Add(host);
            return host;
        }

        public T Add<T>(GameObject host = null) where T : Component => (host != null ? host : Host(typeof(T).Name)).AddComponent<T>();

        // Registers the AudioManager, which runs any InvokeOnValid work already deferred on its row.
        public AudioManager RegisterAudioManager()
        {
            AudioManager audio = Add<AudioManager>();
            HLAudioSourceHandler sources = audio.gameObject.AddComponent<HLAudioSourceHandler>();
            sources.Initialise(audio.transform);
            Set(audio, "m_audioSourceHandler", sources);
            Set(audio, "m_audioClipHandler", ClipHandler());
            Audio = audio;
            Register(audio);
            return audio;
        }

        private HLAudioClipHandler ClipHandler()
        {
            if (m_clipHandler != null) return m_clipHandler;
            ReadRow<AddressableManager>();
            m_clipHandler = new HLAudioClipHandler(Add<DataManager>());
            return m_clipHandler;
        }

        // A clip GetClip hands back at once for this identifier.
        public AudioClip Clip(HLAudioClipIdentifier identifier, string name)
        {
            AudioClip clip = AudioClip.Create(name, 441, 1, 44100, false);
            m_objects.Add(clip);
            if (m_resources == null)
            {
                m_resources = new ResourceManager();
                typeof(ResourceManager).GetField("CallbackHooksEnabled", Instance).SetValue(m_resources, false);
            }
            AsyncOperationHandle<AudioClip> handle = m_resources.CreateCompletedOperation(clip, null);
            m_handles.Add(handle);
            Type sfx = typeof(HLAudioClipHandler).GetNestedType("SfxData", BindingFlags.NonPublic);
            object data = Activator.CreateInstance(sfx, Instance, null, new object[] { handle, false, null }, null);
            ((IDictionary)Get(ClipHandler(), "m_loadedAudioClips")).Add(identifier, data);
            return clip;
        }

        public AudioSource AddSource(HLAudioSourceIdentifier identifier, HLAudioSourceTransition transition = HLAudioSourceTransition.None)
        {
            AudioSource source = Add<AudioSource>();
            Audio.AddSource(new HLAudioSourceData(identifier, source, false, transition, default));
            return source;
        }

        public HLAudioSourceData Source(HLAudioSourceIdentifier identifier)
        {
            Audio.TryGetAudioSourceData(identifier, out HLAudioSourceData data);
            return data;
        }

        public void Own(Object value) => m_objects.Add(value);

        // A TimeManager whose PlayerMovement timescale is 1, so GetDeltaTime is
        // Time.deltaTime. It is a real component on an inactive host (no Awake or
        // Reset, which need the shared TimeCategoryConfiguration) and never registered; the
        // TimeExtensions category lookup it is read through is leased.
        public TimeManager PlayerMovementTime()
        {
            FieldInfo lookupField = typeof(TimeExtensions).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);
            m_releases.Add(new StaticFieldLease(lookupField));
            var category = ScriptableObject.CreateInstance<TimeCategoryObject>();
            Own(category);
            Set(category, "m_guid", "lucid-audio-player-movement");
            var lookup = ScriptableObject.CreateInstance<TimeCategoryLookup>();
            Own(lookup);
            lookup.Dictionary.Add(TimeCategory.PlayerMovement, category);
            lookupField.SetValue(null, lookup);
            var setting = new TimeSetting();
            setting.SetCategory(category, 1f);
            TimeManager manager = Add<TimeManager>();
            var settings = (StackableData)Get(manager, "m_timeSettings");
            settings.SetBaseValue((int)typeof(TimeManager).GetField("SettingId", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue(), setting);
            return manager;
        }

        // Leases CoroutineUtils.s_instance to an inactive host, so a
        // RunCoroutine is refused with "Coroutine couldn't be started" instead
        // of running. The host never wakes, so it neither registers nor
        // unregisters a CoroutineUtils row.
        public void RefusingCoroutineHost()
        {
            FieldInfo instance = typeof(CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            m_releases.Add(new StaticFieldLease(instance));
            instance.SetValue(null, Add<CoroutineUtils>());
        }

        // Sets a static field for the case and puts back what it held. Reading
        // it first runs the declaring type's initializer, if it has not run.
        public void LeaseStatic(FieldInfo field, object value)
        {
            m_releases.Add(new StaticFieldLease(field));
            field.SetValue(null, value);
        }

        private sealed class StaticFieldLease : IDisposable
        {
            private readonly FieldInfo m_field;
            private readonly object m_value;

            public StaticFieldLease(FieldInfo field)
            {
                m_field = field;
                m_value = field.GetValue(null);
            }

            public void Dispose() => m_field.SetValue(null, m_value);
        }

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            var failures = new List<Exception>();
            for (int i = m_releases.Count - 1; i >= 0; i--)
            {
                try { m_releases[i].Dispose(); }
                catch (Exception failure) { failures.Add(failure); }
            }
            foreach (Object value in m_objects)
                if (value != null) Object.DestroyImmediate(value);
            foreach (AsyncOperationHandle handle in m_handles)
                if (handle.IsValid()) m_resources.Release(handle);
            m_resources?.Dispose();
            Random.state = m_randomState;
            if (failures.Count != 0) throw new AggregateException("Owned audio service cleanup failed", failures);
        }

        private static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(name);
        }

        public static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);

        public static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

        // Invokes the overload declared on owner, rethrowing what the body threw.
        public static object Call(Type owner, object target, string name, Type[] parameters, params object[] args)
        {
            MethodInfo method = owner.GetMethod(name, Instance | BindingFlags.DeclaredOnly, null, parameters, null)
                ?? throw new MissingMethodException(owner.Name, name);
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException failure)
            {
                ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
        }

        public static object Call(Type owner, object target, string name) => Call(owner, target, name, Type.EmptyTypes);
    }

    // Counts Unity log lines, which is how a PlayOneShot of a null clip is seen:
    // AudioSource.PlayOneShot(clip, volumeScale) warns and returns for one.
    internal sealed class UnityLogCounter : IDisposable
    {
        public const string NullOneShot = "PlayOneShot was called with a null AudioClip.";
        public readonly List<string> Lines = new List<string>();

        public UnityLogCounter() => Application.logMessageReceived += Record;

        private void Record(string condition, string stackTrace, LogType type) => Lines.Add(type + ": " + condition);

        public int Count(LogType type, string message) => Lines.FindAll(line => line == type + ": " + message).Count;

        public void Dispose() => Application.logMessageReceived -= Record;
    }
}
