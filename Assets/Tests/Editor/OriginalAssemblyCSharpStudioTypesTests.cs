using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // The six original HardlightProject types shipped in Assembly-CSharp (1.10.1). Test
    // assemblies cannot reference the predefined Assembly-CSharp, so types are reached by name.
    public sealed class OriginalAssemblyCSharpStudioTypesTests
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static Type Studio(string name) => Type.GetType("HardlightProject." + name + ", Assembly-CSharp", true);

        private static Option[] Options(Type type) =>
            type.GetCustomAttributesData().Where(data => data.AttributeType == typeof(Il2CppSetOptionAttribute))
                .Select(data => (Option)data.ConstructorArguments[0].Value).ToArray();

        [Test]
        public void DeclarationsMatchTheShippedAssembly()
        {
            Assert.That(Studio("DreamDissolveManagerDebug").IsDefined(typeof(ExecuteInEditMode)), Is.True);
            Assert.That(Studio("DreamGetPlayerPositionDebug").IsDefined(typeof(ExecuteInEditMode)), Is.True);
            Assert.That(Options(Studio("prototype_timer")), Is.EqualTo(new[] { Option.ArrayBoundsChecks, Option.NullChecks }));
            Assert.That(Options(Studio("laserTest")), Is.EqualTo(new[] { Option.NullChecks, Option.ArrayBoundsChecks }));
            Assert.That(Studio("DreamDissolveManagerDebug").GetFields(Declared).Select(field => field.Name + ":" + ((int)field.Attributes).ToString("x")),
                Is.EqualTo(new[] { "m_mainCamera:1", "m_dreamDissolve:1", "m_maskPosition:1", "m_opacity:1", "camToWorld:8051", "maskPosition:8051", "opacity:8051" }));
            Assert.That(Studio("prototype_power_line").GetFields(Declared).Select(field => field.Name),
                Is.EqualTo(new[] { "target", "lineR", "powerOn", "powerSource", "mainPower", "powerGlowOn", "powerGlowOff", "poweredTime", "p" }));
            Assert.That((int)Studio("prototype_power_line").GetMethod("Update", Declared).Attributes, Is.EqualTo(0x81));
        }

        [Test]
        public void TimerShowsAndHidesItsSpawnedObjects()
        {
            var host = new GameObject("Lucid prototype timer");
            var spawned = new GameObject("Lucid spawned");
            try
            {
                Type type = Studio("prototype_timer");
                Component timer = host.AddComponent(type);
                type.GetField("timerDuration").SetValue(timer, 2f);
                type.GetField("spawnedObjects").SetValue(timer, spawned);
                spawned.SetActive(false);
                type.GetMethod("resetTimer").Invoke(timer, null);
                Assert.That(spawned.activeSelf, Is.True);
                Assert.That(type.GetField("spawnedTime", Declared).GetValue(timer), Is.EqualTo(2f));
                type.GetMethod("TimerEnd").Invoke(timer, null);
                Assert.That(spawned.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(spawned);
            }
        }
    }
}
