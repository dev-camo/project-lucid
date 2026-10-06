using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Video;

namespace ProjectLucid
{
    public static class SplashScreenVerification
    {
        private static int checks;
        private static void Check(bool condition, string message) { if (!condition) throw new Exception("SplashScreen verification: " + message); checks++; }
        private static FieldInfo Field(string name) => typeof(SplashScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static void Set(SplashScreen owner, string name, object value) => Field(name).SetValue(owner, value);
        private static void Invoke(SplashScreen owner, string name, params object[] arguments)
        {
            try { typeof(SplashScreen).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, arguments); }
            catch (TargetInvocationException exception) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }
        private static TError Throws<TError>(Action action, string message) where TError : Exception
        {
            try { action(); } catch (TError error) { checks++; return error; }
            throw new Exception("SplashScreen verification expected " + typeof(TError).Name + ": " + message);
        }
        public static int RunManaged()
        {
            checks = 0; CheckDeclarations();
            var data = new SplashScreen.AspectRatioSplashScreenData();
            Check(data.AspectRatio.x == 0f && data.AspectRatio.y == 0f && data.Width == 0f, "original nested constructor leaves numeric defaults");
            Check(data.Videos == null && data.Sprites == null, "original nested constructor leaves both lists null");
            // Genuine uninitialized component isolates managed field/accessor and
            // pre-engine guards; it does not simulate MonoBehaviour construction.
            var owner = (SplashScreen)FormatterServices.GetUninitializedObject(typeof(SplashScreen));
            Check(owner.Progress == 0f && (float)Field("m_timer").GetValue(owner) == 0f, "managed zero fields without engine construction claim");
            Set(owner, "<Progress>k__BackingField", 0.375f); Check(owner.Progress == 0.375f, "original progress getter uses sole backing field");
            typeof(SplashScreen).GetProperty("Progress").GetSetMethod(true).Invoke(owner, new object[] { -2f });
            Check(owner.Progress == -2f && (float)Field("m_timer").GetValue(owner) == 0f, "private setter neither clamps nor updates timer");
            Set(owner, "m_aspectRatioSplashScreenData", Array.Empty<SplashScreen.AspectRatioSplashScreenData>());
            owner.BeginSplashScreen(); Check(owner.Progress == -2f, "empty array exits before Screen/video/image accesses or progress mutation");
            Set(owner, "m_aspectRatioSplashScreenData", null); Throws<NullReferenceException>(() => owner.BeginSplashScreen(), "original null array failure before engine");
            Invoke(owner, "VideoPlayerOnErrorReceived", null, null); Check(owner.Progress == -2f, "native RET error callback ignores null inputs without changing progress");
            Invoke(owner, "OnDirectorStopped", new object[] { null }); Check(owner.Progress == -2f, "null UnityEvent is skipped with no progress reset");
            return checks;
        }
        public static void Run()
        {
            RunManaged(); CheckEngine();
            Debug.Log("PASS original SplashScreen bounded checks=" + checks + "; generated fixture references only, no original splash asset/video/boot approval.");
        }
        private static void CheckDeclarations()
        {
            Type type = typeof(SplashScreen); Check((int)type.Attributes == 1048833 && type.BaseType == typeof(MonoBehaviour) && type.GetInterfaces().Contains(typeof(ISystem)), "original sealed public component and ISystem base contract");
            var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Check(options.Length == 2 && options.Select(o => o.Option).SequenceEqual(new[] { Option.NullChecks, Option.ArrayBoundsChecks }) && options.All(o => Equals(o.Value, false)), "original ordered two disabled IL2CPP options");
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Check(fields.Select(f => f.Name).SequenceEqual(new[] { "m_aspectRatioSplashScreenData", "m_tolerance", "m_targetVideoPlayers", "m_targetSplashScreens", "m_playableDirector", "m_onDirectorStopped", "<Progress>k__BackingField", "m_timer" }), "all eight original ordered fields, including progress before timer");
            Check(fields.All(f => f.IsPrivate && !f.IsStatic && !f.IsInitOnly), "original field visibility and mutability");
            Check(fields.Take(6).All(f => f.IsDefined(typeof(SerializeField), false)) && fields.Skip(6).All(f => !f.IsDefined(typeof(SerializeField), false)), "exact six serialized fields only");
            Check(fields[6].IsDefined(typeof(CompilerGeneratedAttribute), false) && fields.Where((f, i) => i != 6).All(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false)), "sole generated progress backing-field attribute");
            Check(fields.Select(f => f.FieldType).SequenceEqual(new[] { typeof(SplashScreen.AspectRatioSplashScreenData[]), typeof(float), typeof(List<VideoPlayer>), typeof(List<UnityEngine.UI.Image>), typeof(PlayableDirector), typeof(UnityEvent), typeof(float), typeof(float) }), "complete genuine original field type graph");
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(m => m.MetadataToken).ToArray();
            Check(methods.Select(m => m.Name).SequenceEqual(new[] { "get_Progress", "set_Progress", "BeginSplashScreen", "Awake", "Update", "OnDestroy", "VideoPlayerOnErrorReceived", "OnDirectorStopped" }), "original eight non-constructor method order");
            Check(methods.Select(m => (int)m.Attributes).SequenceEqual(new[] { 2182, 2177, 134, 129, 129, 129, 129, 129 }) && type.GetConstructors().Length == 1, "original full nine own method flags/count");
            Check(methods.Take(2).All(m => m.IsDefined(typeof(CompilerGeneratedAttribute), false)) && methods.Skip(2).All(m => m.GetCustomAttributes(false).Length == 0), "original generated accessor attributes only");
            Check(methods[6].GetParameters().Select(p => p.Name).SequenceEqual(new[] { "videoPlayer", "message" }) && methods[7].GetParameters().Single().Name == "director", "original callback parameter names");
            Type nested = typeof(SplashScreen.AspectRatioSplashScreenData);
            Check((int)nested.Attributes == 1056770 && nested.BaseType == typeof(object), "original serializable nested public before-field-init type");
            var nestedFields = nested.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Check(nestedFields.Select(f => f.Name).SequenceEqual(new[] { "AspectRatio", "Width", "Videos", "Sprites" }), "original nested four-field order");
            Check(nestedFields.Select(f => f.FieldType).SequenceEqual(new[] { typeof(Vector2), typeof(float), typeof(List<VideoClip>), typeof(List<Sprite>) }), "genuine nested field types");
            Check(nestedFields.All(f => f.GetCustomAttributes(false).Length == 1 && f.IsDefined(typeof(TooltipAttribute), false)), "exact original nested Tooltip attributes");
            Check(nestedFields.Select(f => f.GetCustomAttribute<TooltipAttribute>().tooltip).SequenceEqual(new[] { "Screen aspect ratio within tolerance will be included.", "Minimum screen width required.  Leave as 0 to apply to all dimensions.", "List of splash screen videos.", "List of splash screen images." }), "original tooltip values including minimum-width wording");
            Check(nested.GetConstructors().Length == 1 && nested.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0, "original nested ctor is only own member");
        }
        private static SplashScreen.AspectRatioSplashScreenData Data(float aspect, float width, Sprite sprite) => new SplashScreen.AspectRatioSplashScreenData { AspectRatio = new Vector2(aspect, 1f), Width = width, Videos = new List<VideoClip>(), Sprites = sprite == null ? new List<Sprite>() : new List<Sprite> { sprite } };
        private static void CheckEngine()
        {
            var registryField = typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic);
            object previousRegistry = registryField.GetValue(null);
            GameObject root = null, imageObject = null;
            SplashScreen owner = null;
            TimelineAsset timeline = null;
            Texture2D texture = null;
            Sprite spriteA = null, spriteB = null;
            RenderTexture renderTexture = null;
            try
            {
                // Lease a separate registry; existing SystemRef rows and callbacks
                // must not be mutated by this fixture's original Awake.
                registryField.SetValue(null, Activator.CreateInstance(registryField.FieldType));
                root = new GameObject("Lucid original SplashScreen fixture"); root.SetActive(false);
                owner = root.AddComponent<SplashScreen>(); var director = root.AddComponent<PlayableDirector>(); var player = root.AddComponent<VideoPlayer>();
                imageObject = new GameObject("Splash fixture image", typeof(RectTransform), typeof(UnityEngine.UI.Image)); imageObject.SetActive(false);
                var image = imageObject.GetComponent<UnityEngine.UI.Image>();
                timeline = ScriptableObject.CreateInstance<TimelineAsset>(); timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = 10d;
                texture = new Texture2D(2, 2); spriteA = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero); spriteB = Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero);
                renderTexture = new RenderTexture(16, 16, 0); renderTexture.Create(); player.targetTexture = renderTexture;
                Set(owner, "m_playableDirector", director); Set(owner, "m_targetVideoPlayers", new List<VideoPlayer> { player }); Set(owner, "m_targetSplashScreens", new List<UnityEngine.UI.Image> { image });
                Check((float)Field("m_tolerance").GetValue(owner) == 0.05f, "genuine engine component constructor original float constant");
                float aspect = (float)Screen.width / Screen.height; float width = Screen.width;
                Set(owner, "m_aspectRatioSplashScreenData", new[] { Data(aspect + 10f, width, spriteA), Data(aspect + 11f, width, spriteB) }); owner.BeginSplashScreen();
                Check(ReferenceEquals(image.sprite, spriteA), "no matching aspect retains original first-entry fallback");
                Set(owner, "m_aspectRatioSplashScreenData", new[] { Data(aspect, width + 2f, spriteA), Data(aspect, width - 100f, spriteB) }); owner.BeginSplashScreen();
                Check(ReferenceEquals(image.sprite, spriteB), "signed greedy width crosses zero and selects farther absolute difference");
                Set(owner, "m_tolerance", 1f); Set(owner, "m_aspectRatioSplashScreenData", new[] { Data(aspect, width - 1f, spriteA), Data(aspect, width, spriteB) }); owner.BeginSplashScreen();
                Check(ReferenceEquals(image.sprite, spriteA), "difference exactly tolerance stops original strict signed replacement");
                Set(owner, "m_tolerance", 0.05f); Set(owner, "m_aspectRatioSplashScreenData", new[] { Data(aspect, width, spriteA), Data(aspect, width - 1f, spriteB) }); owner.BeginSplashScreen();
                Check(ReferenceEquals(image.sprite, spriteA), "zero width difference retains first matched entry");
                Set(owner, "m_aspectRatioSplashScreenData", Array.Empty<SplashScreen.AspectRatioSplashScreenData>()); owner.BeginSplashScreen(); Check(ReferenceEquals(image.sprite, spriteA), "empty array does not clear existing target image");
                var empty = Data(aspect, width, null); Set(owner, "m_aspectRatioSplashScreenData", new[] { empty }); Set(owner, "m_targetSplashScreens", null); Set(owner, "m_targetVideoPlayers", null); owner.BeginSplashScreen();
                Check(empty.Videos.Count == 0 && empty.Sprites.Count == 0, "empty selected lists bypass null target-list accesses");
                Set(owner, "m_targetSplashScreens", new List<UnityEngine.UI.Image> { image }); Set(owner, "m_targetVideoPlayers", new List<VideoPlayer> { player });
                empty.Sprites = null; Throws<NullReferenceException>(() => owner.BeginSplashScreen(), "null selected sprite list");
                var data = Data(aspect, width, spriteA); data.Videos.Add(null); Set(owner, "m_aspectRatioSplashScreenData", new[] { data }); owner.BeginSplashScreen(); Check(player.clip == null && ReferenceEquals(image.sprite, spriteA), "actual video/image setters use selected null clip and sprite");
                int stopped = 0; var stoppedEvent = new UnityEvent(); stoppedEvent.AddListener(() => stopped++); Set(owner, "m_onDirectorStopped", stoppedEvent);
                Invoke(owner, "Awake"); Check(ReferenceEquals(ProcessManager.GetSystemRef(typeof(SplashScreen).ToString()).GetSafe(), owner), "original Awake registers real engine component");
                var directorCallbacks = typeof(PlayableDirector).GetField("stopped", BindingFlags.Instance | BindingFlags.NonPublic);
                var playerCallbacks = typeof(VideoPlayer).GetField("errorReceived", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(((Delegate)directorCallbacks.GetValue(director)).GetInvocationList().Any(d => ReferenceEquals(d.Target, owner) && d.Method.Name == "OnDirectorStopped"), "genuine director stopped subscription");
                Check(((Delegate)playerCallbacks.GetValue(player)).GetInvocationList().Any(d => ReferenceEquals(d.Target, owner) && d.Method.Name == "VideoPlayerOnErrorReceived"), "genuine video error subscription after registration");
                director.playableAsset = timeline; director.RebuildGraph(); Check(director.duration == 10d, "actual installed Timeline fixed-duration fixture");
                director.time = 3.25d; Set(owner, "m_timer", 2f); Invoke(owner, "Update"); Check(owner.Progress == 0.325f && (float)Field("m_timer").GetValue(owner) == 3.25f, "original timeline float arithmetic and double progress division");
                director.time = 0d; Invoke(owner, "Update"); Check(owner.Progress == 0.325f && (float)Field("m_timer").GetValue(owner) == 3.25f, "zero timeline time retains previous timer rather than restarting");
                director.time = 1d; Invoke(owner, "Update"); Check(owner.Progress == 0.1f && (float)Field("m_timer").GetValue(owner) == 1f, "positive backward timeline updates to original lower time");
                director.time = 0d; Set(owner, "m_timer", -1f); Invoke(owner, "Update"); Check(owner.Progress == 0f && (float)Field("m_timer").GetValue(owner) == -1f, "negative retained timer clamps only progress");
                Set(owner, "m_timer", 20f); Invoke(owner, "Update"); Check(owner.Progress == 1f && (float)Field("m_timer").GetValue(owner) == 20f, "upper clamp leaves underlying retained timer unchanged");
                Invoke(owner, "OnDirectorStopped", director); Check(stopped == 1 && owner.Progress == 1f && (float)Field("m_timer").GetValue(owner) == 20f, "director callback invokes UnityEvent without timer/progress reset");
                Invoke(owner, "VideoPlayerOnErrorReceived", player, "fixture error"); Check(stopped == 1 && owner.Progress == 1f, "native empty error callback changes no progress/events");
                Invoke(owner, "OnDestroy"); Check(!renderTexture.IsCreated(), "original teardown releases actual target RenderTexture");
                Check(playerCallbacks.GetValue(player) == null || !((Delegate)playerCallbacks.GetValue(player)).GetInvocationList().Any(d => ReferenceEquals(d.Target, owner)), "original teardown removes video callback");
                Check(((Delegate)directorCallbacks.GetValue(director)).GetInvocationList().Any(d => ReferenceEquals(d.Target, owner)), "original teardown intentionally retains director stopped callback");
                Check(ReferenceEquals(ProcessManager.GetSystemRef(typeof(SplashScreen).ToString()).GetSafe(), owner), "original teardown intentionally retains registered system reference");
            }
            finally
            {
                try { if (owner != null) Set(owner, "m_targetVideoPlayers", new List<VideoPlayer>()); }
                finally
                {
                    try { registryField.SetValue(null, previousRegistry); }
                    finally
                    {
                        try { UnityEngine.Object.DestroyImmediate(root); }
                        finally
                        {
                            try { UnityEngine.Object.DestroyImmediate(imageObject); }
                            finally
                            {
                                try { UnityEngine.Object.DestroyImmediate(timeline); }
                                finally
                                {
                                    try { UnityEngine.Object.DestroyImmediate(spriteA); }
                                    finally { try { UnityEngine.Object.DestroyImmediate(spriteB); } finally { try { UnityEngine.Object.DestroyImmediate(texture); } finally { try { if (renderTexture != null) renderTexture.Release(); } finally { UnityEngine.Object.DestroyImmediate(renderTexture); } } } }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
