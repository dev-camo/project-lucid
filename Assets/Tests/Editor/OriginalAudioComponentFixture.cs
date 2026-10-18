using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
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
    // runs, with a real HLAudioSourceHandler and HLAudioClipHandler. Clips the
    // case declares are completed handles from an owned ResourceManager, so
    // GetClip answers synchronously; any other identifier misses an empty
    // DataManager table and answers with a null clip. HLOutput errors are read
    // through OwnedCoreErrorLog, the audio enum-string tables are leased, and
    // UnityEngine.Random.state is put back.
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
