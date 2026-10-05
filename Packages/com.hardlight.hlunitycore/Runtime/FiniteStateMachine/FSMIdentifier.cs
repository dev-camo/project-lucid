namespace Hardlight
{
    // Original assembly: HLUnityCore.Runtime.dll. The lazy fields are mutable:
    // native getters write resolved values back into this struct instance.
    public struct FSMIdentifier
    {
        private const int DefaultId = 0;
        private string m_name;
        private int m_id;

        // 0x060003e2; arm64 0x1abbd78.
        public string Name
        {
            get
            {
                if (m_name == null) m_name = GraphNameLookup.LookupNameUsingId(m_id);
                return m_name;
            }
        }

        // 0x060003e3; arm64 0x1abbcf4.
        public int Id
        {
            get
            {
                if (m_id == DefaultId) m_id = GraphNameLookup.ConvertNameToId(m_name);
                return m_id;
            }
        }

        // 0x060003e4; arm64 0x1ac5b20.
        public static implicit operator int(FSMIdentifier identifier) => identifier.Id;
        // 0x060003e5; arm64 0x1ac5bb8.
        public static implicit operator FSMIdentifier(int id) => new FSMIdentifier(id);
        // 0x060003e6; arm64 0x1ac5c24.
        public static implicit operator string(FSMIdentifier identifier) => identifier.Name;
        // 0x060003e7; arm64 0x1ac0dd8.
        public static implicit operator FSMIdentifier(string name) => new FSMIdentifier(name);

        // 0x060003e8; arm64 0x1ac5cd4.
        public FSMIdentifier(string name) { m_name = name; m_id = DefaultId; }
        // 0x060003e9; arm64 0x1ac5bf4.
        public FSMIdentifier(int id) { m_name = null; m_id = id; }
    }
}
