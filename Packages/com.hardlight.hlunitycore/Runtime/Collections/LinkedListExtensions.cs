using System.Collections.Generic;

namespace Hardlight
{
    public static class LinkedListExtensions
    {
        // Original060002a7, natural <Reverse>d__0<T>060002ba..c1. Iteration
        // reads links after each yield and intentionally performs no version check.
        public static IEnumerable<T> Reverse<T>(this LinkedList<T> linkedList)
        {
            LinkedListNode<T> element = linkedList.Last;
            while (element != null)
            {
                yield return element.Value;
                element = element.Previous;
            }
        }
        // Original060002a8, natural <Nodes>d__1<T>060002aa..b1.
        public static IEnumerable<LinkedListNode<T>> Nodes<T>(this LinkedList<T> linkedList)
        {
            LinkedListNode<T> element = linkedList.First;
            while (element != null)
            {
                yield return element;
                element = element.Next;
            }
        }
        // Original060002a9, natural <NodesReverse>d__2<T>060002b2..b9.
        public static IEnumerable<LinkedListNode<T>> NodesReverse<T>(this LinkedList<T> linkedList)
        {
            LinkedListNode<T> element = linkedList.Last;
            while (element != null)
            {
                yield return element;
                element = element.Previous;
            }
        }
    }
}
