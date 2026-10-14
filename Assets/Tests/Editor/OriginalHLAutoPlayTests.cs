using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.AutoPlay;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // The shipped 1.10.1 HLAutoPlay.Runtime carries AutoPlay as stubs. Edit mode never runs
    // Awake, so the singleton is not registered here.
    public sealed class OriginalHLAutoPlayTests
    {
        private GameObject m_gameObject;

        [TearDown]
        public void TearDown()
        {
            if (m_gameObject != null)
                Object.DestroyImmediate(m_gameObject);
        }

        private HLAutoPlay Create()
        {
            m_gameObject = new GameObject("Lucid HLAutoPlay test");
            return m_gameObject.AddComponent<HLAutoPlay>();
        }

        [Test]
        public void DeclarationsMatchTheShippedAssembly()
        {
            Assert.That(typeof(HLAutoPlay).Assembly.GetName().Name, Is.EqualTo("HLAutoPlay.Runtime"));
            Assert.That(typeof(HLAutoPlay).BaseType, Is.EqualTo(typeof(MonoSingleton<HLAutoPlay>)));
            Assert.That((int)typeof(HLAutoPlay).Attributes, Is.EqualTo(0x00100001));
            Assert.That((int)typeof(AutoPlayScript).Attributes, Is.EqualTo(0x00100081));
            Assert.That((int)typeof(ProfilingConfiguration).Attributes, Is.EqualTo(0x00102001));
            Assert.That((int)typeof(IPerformanceTool).Attributes, Is.EqualTo(0xa1));
            Assert.That(System.Enum.GetNames(typeof(ScriptResult)), Is.EqualTo(new[] { "Pass", "Fail" }));
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(HLAutoPlay).GetFields(Fields).Select(field => field.Name),
                Is.EqualTo(new[] { "m_sendToServer", "m_gameCode", "m_profilingConfiguration", "RepositoryUpdated", "m_scriptElements" }));
            Assert.That(typeof(HLAutoPlay).GetNestedType("<>c", BindingFlags.NonPublic)
                .GetMethod("<.ctor>b__18_0", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null, "RepositoryUpdated starts as an empty anonymous method");
        }

        [Test]
        public void StubbedRunnerReportsFailureAndHoldsNoScripts()
        {
            HLAutoPlay autoPlay = Create();
            var results = new List<string>();
            Assert.That(autoPlay.RunTest("Lucid", (result, message) => results.Add(result + ":" + (message ?? "null"))), Is.False);
            autoPlay.RunAll((result, message) => results.Add(result + ":" + (message ?? "null")), true);
            Assert.That(results, Is.EqualTo(new[] { "Fail:null", "Fail:null" }));
            Assert.That(autoPlay.RunTest("Lucid"), Is.False, "a null callback is skipped");
            autoPlay.RunAll();
            autoPlay.Initialize("build", "1.10.1");
            autoPlay.AddScript(null);
            Assert.That(autoPlay.ContainsScript("Lucid"), Is.False);
            Assert.That(autoPlay.GetScript("Lucid"), Is.Null);
            Assert.That(autoPlay.GetScriptNames(), Is.Empty);
            Assert.That(autoPlay.GetRunningTestName(), Is.Null);
            autoPlay.RemoveScript("Lucid");
            autoPlay.RunScript(null, null);
            autoPlay.Terminate();
        }
    }
}
