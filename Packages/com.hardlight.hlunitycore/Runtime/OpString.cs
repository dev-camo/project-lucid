using System;
using System.Text;
using System.Threading;

// Original HLUnityCore.Runtime type0x02000006, complete31 /3fields. The original
// maintained cloud-key subset is reused byte-for-byte below;21 missing bodies added.
public class OpString
{
    private static OpString instance;
    private static Thread singletonThread;
    private StringBuilder sb;

    // 0x06000007/08/09: cached capacity1024; explicit creation retains capacity.
    static OpString() { instance = new OpString(1024); }
    private OpString(int capacity) { sb = new StringBuilder(capacity); }
    public static OpString Create(int capacity) { return new OpString(capacity); }
    public static OpString small => Create(64); // 0x0600000a

    // 0x0600000d: the first accessing thread owns the reusable instance. Each
    // owner access clears it; other threads receive independent small builders.
    public static OpString i
    {
        get
        {
            if (singletonThread == null) singletonThread = Thread.CurrentThread;
            if (singletonThread != Thread.CurrentThread) return small;
            instance.sb.Length = 0;
            return instance;
        }
    }

    public override string ToString() { return sb.ToString(); } // 0x06000014
    public static OpString operator +(OpString t, string str) // 0x06000024
    {
        t.sb.Append(str);
        return t;
    }

    // 0x0600001e: retain log10/floor/power digit extraction rather than using
    // current-culture integer formatting. Zero is a separate ASCII append.
    public static OpString operator +(OpString t, uint v)
    {
        if (v == 0) { t.sb.Append('0'); return t; }
        for (int exponent = (int)Math.Floor(Math.Log10(v)); exponent >= 0; --exponent)
        {
            uint divisor = (uint)Math.Pow(10, exponent);
            uint digit = v / divisor;
            v -= digit * divisor;
            t.sb.Append((char)(digit + '0'));
        }
        return t;
    }

    // 0x0600001d: retain signed log10/floor/power digit extraction and its
    // unchecked Int32.MinValue / NaN-to-int backend boundary.
    public static OpString operator +(OpString t, int v)
    {
        if (v == 0) { t.sb.Append('0'); return t; }
        int remaining = v < 0 ? unchecked(-v) : v;
        int exponent = (int)Math.Floor(Math.Log10(remaining));
        if (v < 0) t.sb.Append('-');
        for (; exponent >= 0; --exponent)
        {
            int divisor = (int)Math.Pow(10, exponent);
            int digit = remaining / divisor;
            remaining -= digit * divisor;
            t.sb.Append((char)(digit + '0'));
        }
        return t;
    }
    // 0x0600001b: call virtual ToString, retaining original null failure and
    // virtual dispatch rather than reading the builder directly.
    public static implicit operator string(OpString t) { return t.ToString(); }
    // 0600000b / 0c: distinct original capacity256/1024 providers.
    public static OpString medium => Create(256);
    public static OpString large => Create(1024);

    // 0600000e / 0f / 10 / 11: direct StringBuilder accessors.
    public int Capacity { set => sb.Capacity = value; get => sb.Capacity; }
    public int Length { set => sb.Length = value; get => sb.Length; }

    // 06000012 / 13 / 14: mutations preserve this identity; virtual ToString.
    public OpString Remove(int startIndex, int length)
    {
        sb.Remove(startIndex, length);
        return this;
    }

    public OpString Replace(string oldValue, string newValue)
    {
        sb.Replace(oldValue, newValue);
        return this;
    }

    // 06000015: allocates a capacity0 builder, replacing the old builder.
    // This differs from the i accessor, which only clears the old length.
    public void Clear()
    {
        sb = new StringBuilder(0);
    }

    // 06000016 / 17: captured length, live builder and character rereads,
    // current-culture Char conversion, one-position replacement.
    public OpString ToLower()
    {
        int length = sb.Length;
        for (int index = 0; index < length; ++index)
            if (char.IsUpper(sb[index]))
                sb.Replace(sb[index], char.ToLower(sb[index]), index, 1);
        return this;
    }

    public OpString ToUpper()
    {
        int length = sb.Length;
        for (int index = 0; index < length; ++index)
            if (char.IsLower(sb[index]))
                sb.Replace(sb[index], char.ToUpper(sb[index]), index, 1);
        return this;
    }

    // 06000018: original call order is TrimEnd before TrimStart.
    public OpString Trim() => TrimEnd().TrimStart();

    // 06000019: reaching the end of an all-whitespace builder returns unchanged.
    public OpString TrimStart()
    {
        int length = sb.Length;
        for (int index = 0; index < length; ++index)
        {
            if (char.IsWhiteSpace(sb[index])) continue;
            if (index != 0) sb.Remove(0, index);
            break;
        }
        return this;
    }

    // 0600001a: removes the non-whitespace character at the stopping index as
    // well as the trailing whitespace. All-whitespace input stays unchanged.
    public OpString TrimEnd()
    {
        int lastIndex = sb.Length - 1;
        for (int index = lastIndex; index >= 0; --index)
        {
            if (char.IsWhiteSpace(sb[index])) continue;
            if (index < lastIndex) sb.Remove(index, lastIndex - index + 1);
            break;
        }
        return this;
    }

    // 0600001c: original StringBuilder bool append overload.
    public static OpString operator +(OpString t, bool v)
    {
        t.sb.Append(v);
        return t;
    }

    // 0600001f / 20 / 21 / 22 / 23 / 24 / 25: each exact original overload
    // appends to the same live builder and returns the same OpString instance.
    public static OpString operator +(OpString t, short v) { t.sb.Append(v); return t; }
    public static OpString operator +(OpString t, byte v) { t.sb.Append(v); return t; }
    public static OpString operator +(OpString t, float v) { t.sb.Append(v); return t; }
    public static OpString operator +(OpString t, char c) { t.sb.Append(c); return t; }
    public static OpString operator +(OpString t, char[] c) { t.sb.Append(c); return t; }
    public static OpString operator +(OpString t, StringBuilder sb) { t.sb.Append(sb); return t; }
}
