using System;
using System.Security.Cryptography;
using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000022; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class HashUtils
	{
		// 06000109 (1.10.1 RVA 0x1A15DD0). Base64 of the SHA-1 of the UTF-8 bytes with every '/'
		// removed; '+' and '=' padding are kept. The SHA1 instance is not disposed.
		public static string GetFileSafeSha1(string value)
		{
			SHA1 sha1 = SHA1.Create();
			byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
			return Convert.ToBase64String(hash).Replace("/", string.Empty);
		}

		// 0600010a (1.10.1 RVA 0x1A189D0)
		public HashUtils()
		{
		}
	}
}
