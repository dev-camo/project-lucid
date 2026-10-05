using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.UI.Binding;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    public static class UIWidgetProgressionVerification
    {
        private static readonly BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private static FieldInfo Field(string name) => typeof(UIWidgetProgression).GetField(name, Declared);
        private static int checks;
        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks++;
        }
        public static int RunManaged()
        {
            checks = 0;
            Type type = typeof(UIWidgetProgression), basis = typeof(UIWidget);
            Check(type.Assembly.GetName().Name == "Game.Runtime", "original progression assembly");
            Check(basis.Assembly.GetName().Name == "HLModernUI.Runtime", "original real base assembly");
            Check(typeof(Bindable<Texture>).Assembly.GetName().Name == "HLBinders.Runtime", "original real binder assembly");
            Check(type.IsPublic && type.IsSealed && type.BaseType == basis, "original sealed progression type");
            Check(basis.IsPublic && basis.IsAbstract && basis.BaseType == typeof(MonoBehaviour), "original complete abstract MonoBehaviour base");
            Check(basis.GetFields(Declared).Length == 0, "original base has no own fields");
            Check(basis.GetConstructors(Declared).Single().IsFamily, "original protected base constructor");
            Check(typeof(IUIWidgetParameters).IsInterface && typeof(IUIWidgetParameters).GetMethods().Length == 0
                && typeof(IUIWidgetParameters).GetFields().Length == 0, "complete original marker interface");
            MethodInfo setup = basis.GetMethod("Setup");
            Check(setup.IsPublic && setup.IsVirtual && !setup.IsAbstract && setup.GetParameters().Single().ParameterType == typeof(IUIWidgetParameters), "genuine RET virtual Setup contract");
            Check(type.GetMethods(Declared).Length == 4 && type.GetConstructors(Declared).Length == 1, "all original five progression methods");
            string[] names = type.GetFields(Declared).OrderBy(f => f.MetadataToken).Select(f => f.Name).ToArray();
            Check(names.SequenceEqual(new[] { "m_animation", "m_animateInClip", "m_animateOutClip", "AnimateOutComplete", "MaskedTexture" }), "exact original field declaration order");
            foreach (string name in new[] { "m_animation", "m_animateInClip", "m_animateOutClip" })
                Check(Field(name).IsPrivate && Field(name).IsDefined(typeof(SerializeField), false), name + " original private serialized field");
            Check(Field("m_animation").FieldType == typeof(Animation), "real Animation field");
            Check(Field("m_animateInClip").FieldType == typeof(AnimationClip) && Field("m_animateOutClip").FieldType == typeof(AnimationClip), "real AnimationClip fields");
            Check(Field("AnimateOutComplete").IsPublic && !Field("AnimateOutComplete").IsInitOnly && Field("AnimateOutComplete").FieldType == typeof(Action<UIWidgetProgression>), "original mutable callback field");
            Check(Field("MaskedTexture").IsPublic && Field("MaskedTexture").IsInitOnly && Field("MaskedTexture").FieldType == typeof(Bindable<Texture>), "original readonly binder field");
            Check(!Field("AnimateOutComplete").IsDefined(typeof(NonSerializedAttribute), false), "no invented nonserialized callback attribute");
            Check(!Field("MaskedTexture").IsDefined(typeof(SerializeField), false), "no invented serialized runtime binder");
            Check(basis.IsDefined(typeof(DisallowMultipleComponent), false), "original base component option");
            foreach (Type original in new[] { basis, type })
            {
                var options = original.GetCustomAttributes(typeof(Il2CppSetOptionAttribute), false).Cast<Il2CppSetOptionAttribute>().ToArray();
                Check(options.Length == 2 && options.Any(a => a.Option == Option.ArrayBoundsChecks && Equals(a.Value, false))
                    && options.Any(a => a.Option == Option.NullChecks && Equals(a.Value, false)), original.Name + " exact compiler options");
            }
            return checks;
        }

        // Actual Unity only. Real component/Texture2D instances exercise the
        // original binder/callback routes without gameplay or provider stand-ins.
        public static void Run()
        {
            RunManaged();
            GameObject go = new GameObject("Project Lucid original progression proof");
            Texture2D texture = null;
            try
            {
                UIWidgetProgression widget = go.AddComponent<UIWidgetProgression>();
                Check(widget.MaskedTexture != null && widget.MaskedTexture.Value == null, "genuine constructor creates default texture binder");
                Check(Field("m_animation").GetValue(widget) == null && Field("m_animateInClip").GetValue(widget) == null
                    && Field("m_animateOutClip").GetValue(widget) == null && widget.AnimateOutComplete == null, "other constructor fields retain native defaults");
                widget.Action_AnimateOutComplete();
                Check(widget.AnimateOutComplete == null, "null callback has no side effects");
                int called = 0;
                widget.AnimateOutComplete = value => { Check(ReferenceEquals(value, widget), "callback receives this exact widget"); called++; };
                widget.Action_AnimateOutComplete();
                Check(called == 1, "callback invoked once");
                int tail = 0;
                widget.AnimateOutComplete = value => widget.AnimateOutComplete = null;
                widget.AnimateOutComplete += value => tail++;
                widget.Action_AnimateOutComplete();
                Check(tail == 1 && widget.AnimateOutComplete == null, "captured multicast callback survives field replacement");
                var error = new InvalidOperationException("original progression callback proof");
                widget.AnimateOutComplete = value => throw error;
                widget.AnimateOutComplete += value => tail++;
                Exception caught = null;
                try { widget.Action_AnimateOutComplete(); } catch (Exception e) { caught = e; }
                Check(ReferenceEquals(caught, error) && tail == 1, "callback error propagates and stops later delegate");
                texture = new Texture2D(2, 2);
                int changed = 0;
                Action listener = () => changed++;
                widget.MaskedTexture.AddListener(listener);
                widget.SetMaskedImage(texture);
                Check(ReferenceEquals(widget.MaskedTexture.Value, texture) && changed == 1, "SetMaskedImage invokes genuine virtual Value setter");
                widget.Setup(null);
                Check(ReferenceEquals(widget.MaskedTexture.Value, texture) && changed == 1, "genuine base RET Setup ignores null input");
                widget.MaskedTexture.RemoveListener(listener);
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("Project Lucid original widget/progression checks=" + checks + "; animation route requires bounded live proof");
        }

        // Actual PlayMode only: genuine Animation/legacy clips exercise both
        // direct Play(string) calls and StopSameLayer semantics of that overload.
        // This uses no authored artwork, asset loading or manager replacement.
        public static IEnumerator RunLive()
        {
            GameObject go = new GameObject("Project Lucid original progression animation proof");
            AnimationClip incoming = null, outgoing = null;
            try
            {
                UIWidgetProgression widget = go.AddComponent<UIWidgetProgression>();
                Animation animation = go.AddComponent<Animation>();
                incoming = new AnimationClip { name = "ProjectLucidIncoming", legacy = true };
                outgoing = new AnimationClip { name = "ProjectLucidOutgoing", legacy = true };
                incoming.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
                outgoing.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 1, 1, 0));
                animation.AddClip(incoming, incoming.name);
                animation.AddClip(outgoing, outgoing.name);
                Field("m_animation").SetValue(widget, animation);
                Field("m_animateInClip").SetValue(widget, incoming);
                Field("m_animateOutClip").SetValue(widget, outgoing);
                yield return null;
                widget.AnimateIn();
                if (!animation.IsPlaying(incoming.name) || animation.IsPlaying(outgoing.name))
                    throw new InvalidOperationException("original AnimateIn configured clip name");
                yield return null;
                widget.AnimateOut();
                if (!animation.IsPlaying(outgoing.name) || animation.IsPlaying(incoming.name))
                    throw new InvalidOperationException("original AnimateOut configured clip and Play(string) same-layer stop");
                yield return null;
                Debug.Log("Project Lucid original progression live animation checks=2; bounded engine routes, no authored UI parity");
            }
            finally
            {
                UnityEngine.Object.Destroy(go);
                if (incoming != null) UnityEngine.Object.Destroy(incoming);
                if (outgoing != null) UnityEngine.Object.Destroy(outgoing);
            }
        }
    }
}
