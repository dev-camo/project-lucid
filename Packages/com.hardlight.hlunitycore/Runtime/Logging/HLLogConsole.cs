using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 0x0200016e (TypeDefIndex 9117). Only the
	// static AllLogTypes field and its .cctor (0x06000a22) are restored so far,
	// because HLLogHandler.LogMessageReceiveThreaded reads the field. The
	// console window's fields, nested ConsoleLogMessage and methods
	// 0x060009ec..0x06000a21 are still to be recovered into this declaration.
	[RequireComponent(typeof(HLLogHandler))]
	public class HLLogConsole : MonoBehaviour
	{
		// 0x1b007a0 stores 0x3e, every SupportedLogTypes flag. The field is
		// public and mutable, so readers compare against its current value.
		public static SupportedLogTypes AllLogTypes = SupportedLogTypes.Log | SupportedLogTypes.Warning | SupportedLogTypes.Error | SupportedLogTypes.Assert | SupportedLogTypes.Exception;
	}
}
