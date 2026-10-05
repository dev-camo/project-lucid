using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hardlight
{
    // HLUnityCore.Runtime.dll, original type 0x020001f8. Properties are identified
    // by a lowercase CRC, not by the display name.
    public partial class HLPropertyList
    {
        public class Property
        {
            public string m_value;
            public uint m_nameCRC;
            public string m_name;

            // 0x06000ced; arm64 0x1b0e194.
            public Property(string name, uint nameCRC, string val)
            {
                m_value = val;
                m_nameCRC = nameCRC;
                m_name = name;
            }
        }

        // 0x06000cd6 / 0x06000cd7; arm64 0x1b0cfc0 / 0x1b0cfc8.
        public List<Property> Properties { get; private set; }
        private static readonly string[] m_invalidDecimalSeparators;
        private static readonly char[] m_decimalAdditionalCharacters;

        // 0x06000cec; arm64 0x1b0e04c. The six-byte compiler initializer at
        // metadata offset 0x716778 is 2e002b002d00 (SHA256 f9f2b1ee...47aa636).
        static HLPropertyList()
        {
            m_invalidDecimalSeparators = new[] { ",", "/" };
            m_decimalAdditionalCharacters = new[] { '.', '+', '-' };
        }

        // 0x06000cd8; arm64 0x1b0cfd0.
        public HLPropertyList() { Properties = new List<Property>(); }

        // 0x06000cd9; arm64 0x1b0d05c. A present null value stays null.
        public string AsString(string propertyName, string defaultValue = null)
        {
            return TryGetProperty(propertyName, out Property property) ? property.m_value : defaultValue;
        }

        // 0x06000cda; arm64 0x1b0d0e0. Failed conversion propagates; the default
        // is used only when the property is absent. This applies to all readers.
        public int AsInt(string propertyName, int defaultValue = 0)
        {
            return TryGetProperty(propertyName, out Property property)
                ? Convert.ToInt32(property.m_value, CultureInfo.InvariantCulture) : defaultValue;
        }

        // 0x06000cdb; arm64 0x1b0d1d8.
        public uint AsUInt(string propertyName, uint defaultValue = 0u)
        {
            return TryGetProperty(propertyName, out Property property)
                ? Convert.ToUInt32(property.m_value, CultureInfo.InvariantCulture) : defaultValue;
        }

        // 0x06000cdc; arm64 0x1b0d2d0.
        public float AsFloat(string propertyName, float defaultValue = 0f)
        {
            return TryGetProperty(propertyName, out Property property)
                ? Convert.ToSingle(SafeParseDecimal(propertyName, property.m_value), CultureInfo.InvariantCulture) : defaultValue;
        }

        // 0x06000cdd; arm64 0x1b0d6d0.
        public double AsDouble(string propertyName, double defaultValue = 0.0)
        {
            return TryGetProperty(propertyName, out Property property)
                ? Convert.ToDouble(SafeParseDecimal(propertyName, property.m_value), CultureInfo.InvariantCulture) : defaultValue;
        }

        // 0x06000cde; arm64 0x1b0d404. Only the first matching separator is
        // replaced, then non-alphanumeric characters other than .+- are removed.
        // The diagnostic still uses the original HLOutput integration boundary.
        private static string SafeParseDecimal(string propertyName, string propertyValue)
        {
            foreach (string separator in m_invalidDecimalSeparators)
            {
                if (!propertyValue.Contains(separator)) continue;
                HLOutput.LogError("Property Name: " + propertyName + " Value: " + propertyValue +
                    " was not saved using the InvariantCulture, replacing ',' with '.'");
                propertyValue = propertyValue.Replace(separator[0], '.');
                break;
            }
            return propertyValue.MakeAlphanumeric(m_decimalAdditionalCharacters);
        }

        // 0x06000cdf; arm64 0x1b0d804.
        public long AsLong(string propertyName, long defaultValue = 0L)
        {
            return TryGetProperty(propertyName, out Property property)
                ? Convert.ToInt64(property.m_value, CultureInfo.InvariantCulture) : defaultValue;
        }

        // 0x06000ce0; arm64 0x1b0d8fc.
        public ulong AsULong(string propertyName, ulong defaultValue = 0uL)
        {
            return TryGetProperty(propertyName, out Property property)
                ? Convert.ToUInt64(property.m_value, CultureInfo.InvariantCulture) : defaultValue;
        }

        // 0x06000ce1; arm64 0x1b0d9f4.
        public bool AsBool(string propertyName, bool defaultValue = false)
        {
            return TryGetProperty(propertyName, out Property property)
                ? Convert.ToBoolean(property.m_value, CultureInfo.InvariantCulture) : defaultValue;
        }

        // 0x06000ce2; shared arm64 0x911dc8. RGCTX slot 1 resolves method
        // 0x06000f60 (EnumUtilities.SafeParse<T>), retaining the caller's fallback.
        public T AsEnum<T>(string propertyName, T defaultValue) where T : struct, IConvertible
        {
            return TryGetProperty(propertyName, out Property property)
                ? EnumUtilities.SafeParse(property.m_value, defaultValue) : defaultValue;
        }

        // 0x06000ce3 / 0x06000ce4; arm64 0x1b0daec / 0x1b0d0a4.
        public bool PropertyValid(string propertyName) { return GetProperty(propertyName) != null; }
        private bool TryGetProperty(string propertyName, out Property property)
        {
            property = GetProperty(propertyName);
            return property != null;
        }

        // 0x06000ce5; generic arm64 bodies at 0x910d3c..0x911124.
        public void AddProperty<T>(string propertyName, T property)
        {
            if (propertyName != null) AddProperty(propertyName, HLPropertyStore.GetCRC(propertyName), property);
        }

        // 0x06000ce6; arm64 0x1b0dbf4. This explicitly replaces an existing name.
        public bool AssociateNameWithProperty(string propertyName)
        {
            Property property = GetProperty(HLPropertyStore.GetCRC(propertyName));
            if (property != null) property.m_name = propertyName;
            return property != null;
        }

        // 0x06000ce7; reference/fully-shared bodies at arm64 0x9117cc / 0x911a1c.
        // IFormattable uses invariant formatting; arbitrary objects use ToString.
        // Updating a CRC collision changes the value, retaining its first name.
        public void AddProperty<T>(string propertyName, uint propertyNameCRC, T property)
        {
            if (propertyNameCRC == 0u || (object)property == null) return;
            string value = property is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture) : property.ToString();
            Property existing = GetProperty(propertyNameCRC);
            if (existing == null) Properties.Add(new Property(propertyName, propertyNameCRC, value));
            else
            {
                if (existing.m_name == null) existing.m_name = propertyName;
                existing.m_value = value;
            }
        }

        // 0x06000ce8; arm64 0x1b0db08.
        public Property GetProperty(string propertyName)
        {
            return propertyName == null ? null : GetProperty(HLPropertyStore.GetCRC(propertyName));
        }

        // 0x06000ce9; arm64 0x1b0dd60. List order selects the first CRC match.
        public Property GetProperty(uint propertyNameCRC)
        {
            if (propertyNameCRC == 0u || Properties == null || Properties.Count == 0) return null;
            foreach (Property property in Properties)
                if (property.m_nameCRC == propertyNameCRC) return property;
            return null;
        }

        // 0x06000cea; arm64 0x1b0de4c.
        public bool RemoveProperty(string propertyName)
        {
            return propertyName != null && RemoveProperty(HLPropertyStore.GetCRC(propertyName));
        }

        // 0x06000ceb; arm64 0x1b0df38. Remove the first match and return at once.
        public bool RemoveProperty(uint propertyNameCRC)
        {
            if (propertyNameCRC == 0u || Properties == null || Properties.Count == 0) return false;
            foreach (Property property in Properties)
                if (property.m_nameCRC == propertyNameCRC)
                {
                    Properties.Remove(property);
                    return true;
                }
            return false;
        }
    }
}
