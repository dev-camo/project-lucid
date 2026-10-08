using System.Collections;
using System.Collections.Generic;

namespace Hardlight
{
    // Reconstructed original TypeDef 0x02000061; constructor 0x0600026c.
    // The compiler creates the original iterator family (0x0600026f through 0x06000276).
    public readonly struct EnumerableZip<T1, T2> : IEnumerable<(T1, T2)>
    {
        private readonly IEnumerable<T1> m_collection1;
        private readonly IEnumerable<T2> m_collection2;

        public EnumerableZip(IEnumerable<T1> collection1, IEnumerable<T2> collection2)
        {
            m_collection1 = collection1;
            m_collection2 = collection2;
        }

        // Original 0x0600026d: the left MoveNext short-circuits the right.
        // Using declarations preserve the original iterator cleanup without adding
        // field-null writes; the right enumerator disposes before the left.
        public IEnumerator<(T1, T2)> GetEnumerator()
        {
            using IEnumerator<T1> enumerator1 = m_collection1.GetEnumerator();
            using IEnumerator<T2> enumerator2 = m_collection2.GetEnumerator();
            while (enumerator1.MoveNext() && enumerator2.MoveNext())
                yield return (enumerator1.Current, enumerator2.Current);
        }

        // Original 0x0600026e: both enumeration interfaces use the same iterator.
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
