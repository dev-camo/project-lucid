using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight.AutoPlay
{
	// Original HLAutoPlay.Runtime 02000004; bodies translated from the 1.10.1 x86-64 binary.
	// The shipped build carries AutoPlay as stubs: scripts are never added or run, and
	// every requested test reports a failure.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class HLAutoPlay : MonoSingleton<HLAutoPlay>
	{
		[SerializeField]
		private bool m_sendToServer;
		[UsedImplicitly]
		[SerializeField]
		[ShowIf("m_sendToServer", null)]
		private string m_gameCode;
		[UsedImplicitly]
		[SerializeField]
		private ProfilingConfiguration m_profilingConfiguration;

		// 06000003 / 06000004
		public event Action<string> RepositoryUpdated = delegate { };

		private readonly Dictionary<string, AutoPlayScript> m_scriptElements = new Dictionary<string, AutoPlayScript>();

		// 06000005
		public void Initialize(string buildCode, string versionNumber)
		{
		}

		// 06000006
		public void Terminate()
		{
		}

		// 06000007
		public void AddScript(AutoPlayScript script)
		{
		}

		// 06000008
		public void RemoveScript(string scriptName)
		{
		}

		// 06000009
		public bool ContainsScript(string scriptName)
		{
			return GetScript(scriptName) != null;
		}

		// 0600000a
		public IEnumerable<string> GetScriptNames()
		{
			return m_scriptElements.Keys;
		}

		// 0600000b
		public bool RunTest(string testName, TestFinishedCallback callback = null)
		{
			callback?.Invoke(ScriptResult.Fail);
			return false;
		}

		// 0600000c
		public void RunAll(TestFinishedCallback callback = null, bool closeOnEnd = false)
		{
			callback?.Invoke(ScriptResult.Fail);
		}

		// 0600000d
		public string GetRunningTestName()
		{
			return null;
		}

		// 0600000e
		public AutoPlayScript GetScript(string scriptName)
		{
			m_scriptElements.TryGetValue(scriptName, out AutoPlayScript script);
			return script;
		}

		// 0600000f
		public void RunScript(AutoPlayScript autoPlayScript, OnScriptComplete onScriptComplete)
		{
		}
	}
}
