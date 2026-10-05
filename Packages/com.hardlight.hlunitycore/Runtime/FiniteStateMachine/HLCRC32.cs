namespace Hardlight
{
    // Original assembly: HLUnityCore.Runtime.dll. Native-derived implementation;
    // method tokens refer to the supplied 1.10.1 release, not this rebuild.
    public class HLCRC32
    {
        public enum Case { Upper = 0, Lower = 1, AsIs = 2 }

        private const uint Polynomial = 3988292384u;
        private const uint CRCSeed = uint.MaxValue;
        private const uint TableSize = 256u;
        private static uint[] s_table;

        // 0x06000f75; arm64 0x1b23980: tail call to InitialiseTable.
        static HLCRC32() { InitialiseTable(); }

        // 0x06000f76; arm64 0x1b23ae0.
        public static int GenerateInt(string crcSource) => unchecked((int)Generate(crcSource, Case.Upper));

        // 0x06000f77; arm64 0x1b23bb0.
        public static int GenerateInt(string crcSource, Case stringCase) => unchecked((int)Generate(crcSource, stringCase));

        // 0x06000f78; arm64 0x1b23d78.
        public static uint Generate(string crcSource) => Generate(crcSource, Case.Upper);

        // 0x06000f79; arm64 0x1b23c3c. Each UTF-16 code unit contributes
        // only its low byte. The original returns the running CRC without XOR-out.
        public static uint Generate(string crcSource, Case stringCase)
        {
            if (string.IsNullOrEmpty(crcSource)) return 0u;
            crcSource = GetStringForCase(crcSource, stringCase);
            uint crc = CRCSeed;
            for (int i = 0; i < crcSource.Length; ++i)
                crc = s_table[(crc ^ crcSource[i]) & 0xffu] ^ (crc >> 8);
            return crc;
        }

        // 0x06000f7a; arm64 0x1b23e20. Null and empty are both zero.
        public static uint Generate(byte[] crcSource)
        {
            if (crcSource == null || crcSource.Length == 0) return 0u;
            uint crc = CRCSeed;
            for (int i = 0; i < crcSource.Length; ++i)
                crc = s_table[(crc ^ crcSource[i]) & 0xffu] ^ (crc >> 8);
            return crc;
        }

        // 0x06000f7b; arm64 0x1b23984: reflected polynomial, eight shifts.
        private static void InitialiseTable()
        {
            s_table = new uint[TableSize];
            for (uint i = 0; i < TableSize; ++i)
            {
                uint value = i;
                for (int bit = 0; bit < 8; ++bit)
                    value = (value & 1u) != 0u ? (value >> 1) ^ Polynomial : value >> 1;
                s_table[i] = value;
            }
        }

        // 0x06000f7c; arm64 0x1b23e00: other enum values pass through.
        private static string GetStringForCase(string crcSource, Case stringCase)
        {
            if (stringCase == Case.Upper) return crcSource.ToUpperInvariant();
            if (stringCase == Case.Lower) return crcSource.ToLowerInvariant();
            return crcSource;
        }

        // 0x06000f7d; arm64 0x1b23f58: Object constructor only.
        public HLCRC32() { }
    }
}
