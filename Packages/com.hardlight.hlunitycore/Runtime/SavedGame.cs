using System;
using System.Globalization;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200013b. A save-game identity parsed from the
    // native "Name|DeviceName|ModificationDate" join; equality uses only that join.
    public readonly struct SavedGame : ISavedGameIdentifier, IEquatable<SavedGame>
    {
        public readonly string Name;
        public readonly string DeviceName;
        public readonly DateTime ModificationDate;
        private const int SavedGameToParsePropertiesCount = 3;
        private readonly string m_propertiesJoinToIdentifySavedGame;
        // 0x060008ac: a one-element array holding the "|" separator literal.
        private static readonly string[] SeparatorArray = new string[] { GameCenterSaveGame.Separator };

        // 0x060008a3
        string ISavedGameIdentifier.PropertiesJoinToIdentifySavedGame => m_propertiesJoinToIdentifySavedGame;

        // 0x060008a4
        private SavedGame(string name, string deviceName, DateTime modificationDate, string propertiesJoinToIdentifySavedGame)
        {
            Name = name;
            DeviceName = deviceName;
            ModificationDate = modificationDate;
            m_propertiesJoinToIdentifySavedGame = propertiesJoinToIdentifySavedGame;
        }

        // 0x060008a5: empty segments are kept, the field count must be exactly three,
        // and the date is parsed invariantly with AdjustToUniversal. The whole input
        // string, not a re-join, becomes the identifying properties.
        public static bool TryParseProperties(string propertiesJoinToIdentifySavedGame, out SavedGame savedGame)
        {
            savedGame = default;
            if (string.IsNullOrEmpty(propertiesJoinToIdentifySavedGame)) return false;
            string[] properties = propertiesJoinToIdentifySavedGame.Split(SeparatorArray, StringSplitOptions.None);
            if (properties.Length != SavedGameToParsePropertiesCount) return false;
            if (!DateTime.TryParse(properties[2], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime modificationDate))
                return false;
            savedGame = new SavedGame(properties[0], properties[1], modificationDate, propertiesJoinToIdentifySavedGame);
            return true;
        }

        // 0x060008a6/0x060008a7: inequality negates the equality operator.
        public static bool operator ==(SavedGame lhs, SavedGame rhs) => lhs.Equals(rhs);
        public static bool operator !=(SavedGame lhs, SavedGame rhs) => !(lhs == rhs);

        // 0x060008a8: compares hash codes, not strings; a default value throws.
        public bool Equals(SavedGame other) => GetHashCode() == other.GetHashCode();

        // 0x060008a9: exact SavedGame boxes only.
        public override bool Equals(object obj) => obj is SavedGame other && Equals(other);

        // 0x060008aa
        public override int GetHashCode() => m_propertiesJoinToIdentifySavedGame.GetHashCode();

        // 0x060008ab: current-culture date text.
        public override string ToString() =>
            "Name:" + Name + " DeviceName:" + DeviceName + " ModificationDate:" + ModificationDate.ToString(DateTimeFormatInfo.CurrentInfo);
    }
}
