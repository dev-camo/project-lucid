using System;
using System.Text;

// Original HLNetworking.Runtime NetworkBuffer, type 0x02000004. This is the
// original cursor-based codec, including unchecked arithmetic and partial writes.
public struct NetworkBuffer
{
    private byte[] m_data;
    private int m_start;
    private int m_capacity;
    private int m_offset;
    private int m_limit;

    // 0x06000001; ARM64 0x1a51734. No argument validation in the original ctor.
    public NetworkBuffer(byte[] data, int offset, int length)
    {
        m_data = data;
        m_start = offset;
        m_capacity = length;
        m_offset = 0;
        m_limit = length;
    }

    // 0x06000002; ARM64 0x1a51770. Offset is relative to start, not cursor.
    public NetworkBuffer SubRange(int offset, int length)
    {
        if (unchecked(offset + length) > m_limit) throw new IndexOutOfRangeException();
        return new NetworkBuffer(m_data, unchecked(m_start + offset), length);
    }

    // 0x06000003/04; ARM64 0x1a51804/0x1a5180c.
    public int Offset => m_offset;
    public int Remaining => Math.Max(unchecked(m_limit - m_offset), 0);

    // 0x06000005; ARM64 0x1a5181c. Negative skip is permitted by this bound.
    public void skip(int length)
    {
        if (unchecked(m_offset + length) > m_limit) throw new IndexOutOfRangeException();
        m_offset = unchecked(m_offset + length);
    }

    // 0x06000006; ARM64 0x1a5187c. Signed negatives use five bytes, no zigzag.
    public static int varInt32Length(int value)
    {
        uint bits = unchecked((uint)value);
        if (bits < 0x80) return 1;
        if (bits < 0x4000) return 2;
        if (bits < 0x200000) return 3;
        if (bits < 0x10000000) return 4;
        return 5;
    }

    // 0x06000007; ARM64 0x1a518c0. Length prefix is UTF8-byte-count plus one.
    public static int stringLength(string value)
    {
        if (string.IsNullOrEmpty(value)) return 1;
        byte[] data = Encoding.UTF8.GetBytes(value);
        return unchecked(varInt32Length(unchecked(data.Length + 1)) + data.Length);
    }

    // 0x06000008; ARM64 0x1a51978. Array.Copy exceptions do not advance cursor.
    public void readBytes(byte[] destData, int destOffset, int length)
    {
        if (unchecked(m_offset + length) > m_limit) throw new IndexOutOfRangeException();
        Array.Copy(m_data, unchecked(m_start + m_offset), destData, destOffset, length);
        m_offset = unchecked(m_offset + length);
    }

    // 0x06000009; ARM64 0x1a51a08. No five-byte cap or canonical-form check;
    // C# Int32 shifts wrap at 32 bits. Cursor advances before each array access.
    public int readVarInt32()
    {
        int result = 0;
        int shift = 0;
        while (m_offset < m_limit)
        {
            byte part = m_data[unchecked(m_start + m_offset++)];
            result |= (part & 0x7f) << shift;
            if ((part & 0x80) == 0) return result;
            shift = unchecked(shift + 7);
        }
        throw new IndexOutOfRangeException();
    }

    // 0x0600000a; ARM64 0x1a51ad4 tailcalls varint decoder.
    public int readInt32() => readVarInt32();

    // 0x0600000b; ARM64 0x1a51ad8. Prefix zero decodes to String.Empty.
    // Allocation happens before the readBytes range check; no null result sentinel.
    public string readString()
    {
        int length = readVarInt32();
        if (length == 0) return string.Empty;
        byte[] data = new byte[unchecked(length - 1)];
        readBytes(data, 0, data.Length);
        return Encoding.UTF8.GetString(data);
    }

    // 0x0600000c; ARM64 0x1a51c04. Copy completes before cursor update.
    public void writeBytes(byte[] srcData, int srcOffset, int length)
    {
        if (unchecked(m_offset + length) > m_limit) throw new IndexOutOfRangeException();
        Array.Copy(srcData, srcOffset, m_data, unchecked(m_start + m_offset), length);
        m_offset = unchecked(m_offset + length);
    }

    // 0x0600000d; ARM64 0x1a51c98. Bounds are checked one byte at a time;
    // failures retain preceding bytes and an increment before a failed array access.
    public void writeVarInt32(int value)
    {
        uint bits = unchecked((uint)value);
        while (bits >= 0x80)
        {
            if (m_offset >= m_limit) throw new IndexOutOfRangeException();
            m_data[unchecked(m_start + m_offset++)] = unchecked((byte)(bits | 0x80));
            bits >>= 7;
        }
        if (m_offset >= m_limit) throw new IndexOutOfRangeException();
        m_data[unchecked(m_start + m_offset++)] = unchecked((byte)bits);
    }

    // 0x0600000e; ARM64 0x1a51d80 tailcalls varint encoder.
    public void writeInt32(int value) => writeVarInt32(value);

    // 0x0600000f; ARM64 0x1a51d84. Null writes prefix0, empty writes prefix1.
    // Header is committed before byte-copy capacity/error checks.
    public void writeString(string value)
    {
        if (value == null) { writeVarInt32(0); return; }
        byte[] data = Encoding.UTF8.GetBytes(value);
        writeVarInt32(unchecked(data.Length + 1));
        writeBytes(data, 0, data.Length);
    }
}
