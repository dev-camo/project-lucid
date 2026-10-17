using System.Collections.Generic;
using Hardlight.JSON;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9240 (0x020001e9), methods 0x06000c6d..0x06000c73;
	// bodies from the 1.10.1 x86-64 binary (0x1b07270..0x1b07530). A JSON payload base whose helpers read typed values out of a received JSONHashtable.
	public abstract class PayloadJSON : IPayload<IJsonObject>
	{
		public abstract void UpdateData(IJsonObject data);

		public abstract string GetIdentifier();

		// 0x06000c6f: a straight forward to JSONHashtable.TryGetBool.
		protected bool TryGetBool(JSONHashtable data, string identifier, out bool result)
		{
			return data.TryGetBool(identifier, out result);
		}

		// 0x06000c70.
		protected bool TryGetDouble(JSONHashtable data, string identifier, out double result)
		{
			return data.TryGetDouble(identifier, out result);
		}

		// 0x06000c71.
		protected bool TryGetString(JSONHashtable data, string identifier, out string result)
		{
			return data.TryGetString(identifier, out result);
		}

		// 0x06000c72, 0x1b072d0: an existing list is cleared before the lookup, so a
		// missing array still empties it. The list is created only when the first entry
		// converts; entries that do not convert are logged and skipped. The result is
		// whether the array was found, not whether any entry converted. The LogError
		// overload and whether a context is passed are unrecoverable from 0x1b073c0,
		// because LogError at 0x1af8470 never reads its context.
		protected bool TryGetStringArray(JSONHashtable data, string identifier, ref List<string> result)
		{
			result?.Clear();
			bool found = data.TryGetArray(identifier, out JSONArray array);
			if (found)
			{
				for (int i = 0; i < array.Count; i++)
				{
					if (array.TryGetStringFromIndex(i, out string entry))
					{
						if (result == null)
						{
							result = new List<string>();
						}
						result.Add(entry);
					}
					else
					{
						HLOutput.LogError(string.Format("Failed to convert {0} to a string entry from a received JSONArray.", array[i]));
					}
				}
			}
			return found;
		}
	}
}
