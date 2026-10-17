using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000254, methods 0x06000ef5..0x06000eff.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class UnityObjectWithGuid : ISerializationCallbackReceiver, IButtonReceiver
    {
        [SerializeField]
        [Button(false, "Regenerate Guid")]
        private string m_serialisedGuid;
        [SerializeField]
        private Object m_objectReference;
        private Guid m_guid;

        public Guid Guid => m_guid;
        public Object Object => m_objectReference;

        // 0x06000ef7: Guid.NewGuid runs before Object's constructor, as a chained argument.
        public UnityObjectWithGuid(Object objectReference) : this(objectReference, Guid.NewGuid()) { }

        // 0x06000ef8.
        public UnityObjectWithGuid(Object objectReference, Guid guid)
        {
            m_objectReference = objectReference;
            m_guid = guid;
        }

        // 0x06000ef9.
        public void Deconstruct(out Guid guid, out Object objectReference)
        {
            guid = m_guid;
            objectReference = m_objectReference;
        }

        // 0x06000efa: HashCode.Combine<Object, Guid>.
        public override int GetHashCode() => HashCode.Combine(m_objectReference, m_guid);

        // 0x06000efb: type test, then the typed comparison below.
        public override bool Equals(object obj) => obj is UnityObjectWithGuid other && Equals(other);

        // 0x06000efc: the reference is compared through its virtual Object.Equals,
        // so a null own reference faults instead of comparing false.
        public bool Equals(UnityObjectWithGuid other)
        {
            if (other == null) return false;
            return m_objectReference.Equals(other.m_objectReference) && m_guid.Equals(other.m_guid);
        }

        // 0x06000efd: Guid.ToString() default "D" format.
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            m_serialisedGuid = m_guid.ToString();
        }

        // 0x06000efe: an unparsable string logs and is replaced in memory only;
        // m_serialisedGuid keeps the rejected text until the next serialization.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (Guid.TryParse(m_serialisedGuid, out m_guid)) return;
            Guid newGuid = Guid.NewGuid();
            HLOutput.LogError(string.Format("Unable to parse Guid: {0} A new Guid has been assigned: {1}", m_serialisedGuid, newGuid));
            m_guid = newGuid;
        }

        // 0x06000eff: any button regenerates the Guid; the field name is not checked.
        void IButtonReceiver.OnButtonClick(string fieldName)
        {
            m_guid = Guid.NewGuid();
        }
    }
}
