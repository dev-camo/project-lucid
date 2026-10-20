using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // The original source compiles editor-only branches under UNITY_EDITOR, and EditMode tests run that editor compile with
    // Application.isPlaying false. SplineUser, SplineComputer and TS_Transform cache Application.isPlaying in a private
    // editor-only flag (SplineUser._isPlaying and SplineComputer._editorIsPlaying in Awake, TS_Transform.isPlaying in Update)
    // and branch on that flag alone. With the flag true each of those branches is the same text as the player build, which
    // compiles no UNITY_EDITOR code (the player-define compile matches the DummyDll declarations). Tests of the shipped
    // queue, cache and transform paths set the flag to the value play mode caches; branches that read Application.isPlaying
    // directly cannot be reached this way and keep their edit-mode behaviour.
    internal static class DreamteckPlayState
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        public static T Cache<T>(T user) where T : SplineUser
        {
            Set(typeof(SplineUser), user, "_isPlaying");
            return user;
        }

        public static SplineComputer Cache(SplineComputer computer)
        {
            Set(typeof(SplineComputer), computer, "_editorIsPlaying");
            return computer;
        }

        public static TS_Transform Cache(TS_Transform snapshot)
        {
            Set(typeof(TS_Transform), snapshot, "isPlaying");
            return snapshot;
        }

        private static void Set(System.Type declaring, object target, string name)
        {
            FieldInfo field = declaring.GetField(name, Instance | BindingFlags.DeclaredOnly);
            Assert.That(field, Is.Not.Null, declaring.Name + "." + name + " is the editor-only play-state cache");
            Assert.That(field.FieldType, Is.EqualTo(typeof(bool)));
            field.SetValue(target, true);
        }
    }
}
