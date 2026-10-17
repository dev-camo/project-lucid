using System;
using System.Collections.Generic;
using Hardlight.JSON;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9246 (0x020001ef), methods 0x06000c92..0x06000c94;
	// bodies from the 1.10.1 x86-64 binary (0x1b08090..0x1b08160). Reads hashtable
	// entries as identifier and value pairs for PayloadJSON properties.
	public class PayloadReaderJSON : IPayloadReader<KeyValuePair<string, IJsonObject>, IJsonObject>
	{
		// 0x06000c92.
		public Type GetPayloadType()
		{
			return typeof(PayloadJSON);
		}

		// 0x06000c93: every entry is accepted, a null value included.
		public bool GetKeyValueFromData(KeyValuePair<string, IJsonObject> input, out string identifier, out IJsonObject output)
		{
			identifier = input.Key;
			output = input.Value;
			return true;
		}
	}
}
