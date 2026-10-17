using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9237 (0x020001e6), methods 0x06000c65..0x06000c68;
	// bodies from the 1.10.1 x86-64 binary (0x1b06fb0, 0x1b07260, shared generics
	// 0x985890 and 0x9858f0). A base for objects whose IPayload<> properties are filled
	// by a PayloadPropertyBuilder, gated by IsSafePointToUpdateProperties.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class PayloadPopulator
	{
		private readonly PayloadPropertyBuilder m_payloadPropertyBuilder;

		// 0x06000c65: the builder is assigned in the constructor body after
		// Object..ctor, receiving this and a delegate bound virtually (vtable slot 4)
		// to IsSafePointToUpdateProperties, so overrides decide when updates may run.
		protected PayloadPopulator()
		{
			m_payloadPropertyBuilder = new PayloadPropertyBuilder(this, IsSafePointToUpdateProperties);
		}

		// 0x06000c66.
		protected virtual bool IsSafePointToUpdateProperties()
		{
			return true;
		}

		// 0x06000c67, rgctx [2] PayloadPropertyBuilder.UpdateProperties<TInput, TOutput> 0x06000c76.
		protected void UpdateProperties<TInput, TOutput>(IEnumerable<TInput> payloadData, IPayloadReader<TInput, TOutput> payloadReader, Action onCompleted, bool clearCollectionTypes = true)
		{
			m_payloadPropertyBuilder.UpdateProperties(payloadData, payloadReader, onCompleted, clearCollectionTypes);
		}

		// 0x06000c68, rgctx [2] PayloadPropertyBuilder.UpdateProperty<TInput, TOutput> 0x06000c77.
		protected void UpdateProperty<TInput, TOutput>(TInput payloadData, IPayloadReader<TInput, TOutput> payloadReader, Action onCompleted, bool clearIfPropertyIsCollection = true)
		{
			m_payloadPropertyBuilder.UpdateProperty(payloadData, payloadReader, onCompleted, clearIfPropertyIsCollection);
		}
	}
}
