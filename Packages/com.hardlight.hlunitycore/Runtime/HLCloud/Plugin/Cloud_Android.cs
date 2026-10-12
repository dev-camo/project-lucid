using System.Collections.Generic;
using System;

namespace HLCloud.Plugin
{
	// Original HLUnityCore.Runtime 02000015; bodies translated from the 1.10.1 x86-64 binary.
	public class Cloud_Android : Cloud
	{
		// 06000085 (1.10.1 RVA 0x1A9D180)
		public override void InitializeWithGameObjectName(string gameObjectName, bool useGamePlayerID)
		{
		}

		// 06000086 (1.10.1 RVA 0x1A9D190)
		public override bool Synchronize()
		{
			return true;
		}

		// 06000087 (1.10.1 RVA 0x1A9D1A0)
		public override HashSet<string> RetrieveAllCloudKeys(string allCloudKeysSeparator)
		{
			return new HashSet<string>();
		}

		// 06000088 (1.10.1 RVA 0x1A9D200)
		public override string StringForKey(string key)
		{
			return string.Empty;
		}

		// 06000089 (1.10.1 RVA 0x1A9D240)
		public override void SetStringForKey(string value, string key)
		{
		}

		// 0600008a (1.10.1 RVA 0x1A9D250)
		public override float FloatForKey(string key)
		{
			return 0f;
		}

		// 0600008b (1.10.1 RVA 0x1A9D260)
		public override void SetFloatForKey(float value, string key)
		{
		}

		// 0600008c (1.10.1 RVA 0x1A9D270)
		public override int IntForKey(string key)
		{
			return 0;
		}

		// 0600008d (1.10.1 RVA 0x1A9D280)
		public override void SetIntForKey(int value, string key)
		{
		}

		// 0600008e (1.10.1 RVA 0x1A9D290)
		public override bool BoolForKey(string key)
		{
			return true;
		}

		// 0600008f (1.10.1 RVA 0x1A9D2A0)
		public override void SetBoolForKey(bool value, string key)
		{
		}

		// 06000090 (1.10.1 RVA 0x1A9D2B0)
		public override void RemoveForKey(string key)
		{
		}

		// 06000091 (1.10.1 RVA 0x1A9D2C0)
		public override void CloudDidChange(string message)
		{
		}

		// 06000092 (1.10.1 RVA 0x1A9D2D0)
		public Cloud_Android()
		{
		}
	}
}
