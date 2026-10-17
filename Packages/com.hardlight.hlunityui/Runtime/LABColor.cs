// Original HLUnityUI.Runtime LABColor 02000007 (MethodDefs 0x0600000c..1e), translated
// from the x86-64 bodies (0x1B393B0..0x1B39F90). A CIE L*a*b* colour (D65 white point)
// with sRGB conversion; the constants are the float literals the bodies load.
// Local names are inferred.
using UnityEngine;

[System.Serializable]
public class LABColor
{
    private float L;
    private float A;
    private float B;

    public float l
    {
        get { return L; }
        set { L = value; }
    }

    public float a
    {
        get { return A; }
        set { A = value; }
    }

    public float b
    {
        get { return B; }
        set { B = value; }
    }

    public LABColor(float l, float a, float b)
    {
        this.l = l;
        this.a = a;
        this.b = b;
    }

    public LABColor(Color col)
    {
        LABColor temp = FromColor(col);
        l = temp.l;
        a = temp.a;
        b = temp.b;
    }

    // Mathf.Lerp per channel, so t is clamped to [0, 1].
    public static LABColor Lerp(LABColor a, LABColor b, float t)
    {
        return new LABColor(Mathf.Lerp(a.l, b.l, t), Mathf.Lerp(a.a, b.a, t), Mathf.Lerp(a.b, b.b, t));
    }

    public static Color Lerp(Color a, Color b, float t)
    {
        return Lerp(FromColor(a), FromColor(b), t).ToColor();
    }

    // CIE76 delta E.
    public static float Distance(LABColor a, LABColor b)
    {
        return Mathf.Sqrt(Mathf.Pow(a.l - b.l, 2f) + Mathf.Pow(a.a - b.a, 2f) + Mathf.Pow(a.b - b.b, 2f));
    }

    // 0x06000017: sRGB linearised with gamma 2.2 (not the 2.4 sRGB curve), XYZ clamped
    // to the D65 white, alpha dropped.
    public static LABColor FromColor(Color c)
    {
        float D65x = 0.9505f;
        float D65y = 1.0f;
        float D65z = 1.0890f;
        float rLinear = c.r;
        float gLinear = c.g;
        float bLinear = c.b;
        float r = (rLinear > 0.04045f) ? Mathf.Pow((rLinear + 0.055f) / (1f + 0.055f), 2.2f) : (rLinear / 12.92f);
        float g = (gLinear > 0.04045f) ? Mathf.Pow((gLinear + 0.055f) / (1f + 0.055f), 2.2f) : (gLinear / 12.92f);
        float b = (bLinear > 0.04045f) ? Mathf.Pow((bLinear + 0.055f) / (1f + 0.055f), 2.2f) : (bLinear / 12.92f);
        float x = r * 0.4124f + g * 0.3576f + b * 0.1805f;
        float y = r * 0.2126f + g * 0.7152f + b * 0.0722f;
        float z = r * 0.0193f + g * 0.1192f + b * 0.9505f;
        x = (x > 0.9505f) ? 0.9505f : ((x < 0f) ? 0f : x);
        y = (y > 1.0f) ? 1.0f : ((y < 0f) ? 0f : y);
        z = (z > 1.089f) ? 1.089f : ((z < 0f) ? 0f : z);
        LABColor lab = new LABColor(0f, 0f, 0f);
        float fx = x / D65x;
        float fy = y / D65y;
        float fz = z / D65z;
        fx = (fx > 0.008856f) ? Mathf.Pow(fx, 1.0f / 3.0f) : (7.787f * fx + 16.0f / 116.0f);
        fy = (fy > 0.008856f) ? Mathf.Pow(fy, 1.0f / 3.0f) : (7.787f * fy + 16.0f / 116.0f);
        fz = (fz > 0.008856f) ? Mathf.Pow(fz, 1.0f / 3.0f) : (7.787f * fz + 16.0f / 116.0f);
        lab.l = 116.0f * fy - 16f;
        lab.a = 500.0f * (fx - fy);
        lab.b = 200.0f * (fy - fz);
        return lab;
    }

    // 0x06000018: the inverse, with the 2.4 sRGB curve and negative channels clamped
    // to zero (no upper clamp); alpha is always 1.
    public static Color ToColor(LABColor lab)
    {
        float D65x = 0.9505f;
        float D65y = 1.0f;
        float D65z = 1.0890f;
        float delta = 6.0f / 29.0f;
        float fy = (lab.l + 16f) / 116.0f;
        float fx = fy + (lab.a / 500.0f);
        float fz = fy - (lab.b / 200.0f);
        float x = (fx > delta) ? D65x * (fx * fx * fx) : (fx - 16.0f / 116.0f) * 3f * (delta * delta) * D65x;
        float y = (fy > delta) ? D65y * (fy * fy * fy) : (fy - 16.0f / 116.0f) * 3f * (delta * delta) * D65y;
        float z = (fz > delta) ? D65z * (fz * fz * fz) : (fz - 16.0f / 116.0f) * 3f * (delta * delta) * D65z;
        float r = x * 3.2410f - y * 1.5374f - z * 0.4986f;
        float g = -x * 0.9692f + y * 1.8760f - z * 0.0416f;
        float b = x * 0.0556f - y * 0.2040f + z * 1.0570f;
        r = (r <= 0.0031308f) ? 12.92f * r : (1f + 0.055f) * Mathf.Pow(r, 1.0f / 2.4f) - 0.055f;
        g = (g <= 0.0031308f) ? 12.92f * g : (1f + 0.055f) * Mathf.Pow(g, 1.0f / 2.4f) - 0.055f;
        b = (b <= 0.0031308f) ? 12.92f * b : (1f + 0.055f) * Mathf.Pow(b, 1.0f / 2.4f) - 0.055f;
        r = (r < 0) ? 0 : r;
        g = (g < 0) ? 0 : g;
        b = (b < 0) ? 0 : b;
        return new Color(r, g, b);
    }

    public Color ToColor()
    {
        return ToColor(this);
    }

    public override string ToString()
    {
        return "L:" + l + " A:" + a + " B:" + b;
    }

    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType()) return false;
        return this == (LABColor)obj;
    }

    public override int GetHashCode()
    {
        return l.GetHashCode() ^ a.GetHashCode() ^ b.GetHashCode();
    }

    // 0x0600001d/1e dereference both operands before comparing, so a null operand
    // throws instead of comparing by reference.
    public static bool operator ==(LABColor item1, LABColor item2)
    {
        return item1.l == item2.l && item1.a == item2.a && item1.b == item2.b;
    }

    public static bool operator !=(LABColor item1, LABColor item2)
    {
        return item1.l != item2.l || item1.a != item2.a || item1.b != item2.b;
    }
}
