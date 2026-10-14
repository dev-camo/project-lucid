using System;
using System.Collections.Generic;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Localisation
{
    // Original HLLocalisation.Runtime; bodies translated from the 1.10.1 x86-64 binary.
    // Either literal content or a string-table ID, with optional nested format parameters.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OptionallyLocalisedString
    {
        private string content;
        private Strings stringID = Strings.NONE;
        private bool inStringTable;
        private List<OptionallyLocalisedString> parameters;

        // 06000036
        public bool HasContent()
        {
            return inStringTable ? stringID != Strings.NONE : !string.IsNullOrEmpty(content);
        }

        // 06000037. Table content is stored only as its CRC string ID.
        public OptionallyLocalisedString(string content, bool inStringTable)
        {
            this.inStringTable = inStringTable;
            if (inStringTable)
                stringID = (Strings)HLCRC32.GenerateInt(content);
            else
                this.content = content;
        }

        // 06000038
        public OptionallyLocalisedString(Strings stringID)
        {
            this.stringID = stringID;
            inStringTable = true;
        }

        // 06000039. Returns the added parameter, not this string.
        public OptionallyLocalisedString AddParameter(OptionallyLocalisedString parameter)
        {
            if (parameters == null)
                parameters = new List<OptionallyLocalisedString>();
            parameters.Add(parameter);
            return parameter;
        }

        // 0600003a. Returns the new parameter, not this string.
        public OptionallyLocalisedString AddParameter(string content, bool inStringTable)
        {
            if (parameters == null)
                parameters = new List<OptionallyLocalisedString>();
            var parameter = new OptionallyLocalisedString(content, inStringTable);
            parameters.Add(parameter);
            return parameter;
        }

        // 0600003b. A format mismatch leaves the unformatted text.
        public override string ToString()
        {
            string text = inStringTable ? StringTable.GetString(stringID) : content;
            if (parameters != null && parameters.Count > 0)
            {
                var formatParameters = new string[parameters.Count];
                for (int i = 0; i < parameters.Count; i++)
                    formatParameters[i] = parameters[i].ToString();
                try
                {
                    text = string.Format(text, formatParameters);
                }
                catch (FormatException)
                {
                }
            }
            return text;
        }
    }
}
