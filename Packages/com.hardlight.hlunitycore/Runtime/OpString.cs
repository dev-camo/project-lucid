using System;
using System.Text;
using System.Threading;

// Original HLUnityCore.Runtime type0x02000006. This recovered subset supplies
// the original cloud-key path. Other formatting operations remain unrecovered.
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
}
