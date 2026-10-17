using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.Events;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000d8 (TypeDefIndex 12040), constructor 0x060005d1.
	// A serialized event with a lookup name; the constructor only calls Object.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Serializable]
	public class NamedUnityEvent
	{
		public string m_eventName;
		public UnityEvent m_event;
	}
}
