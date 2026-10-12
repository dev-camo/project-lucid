using Unity.IL2CPP.CompilerServices;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using Hardlight;

namespace Hardlight.Generics
{
	// Every member forwards straight to List; the native code has no null checks.
	// Original HLUnityCore.Runtime 020002a0; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ReadOnlyWrapper<T>
	{
		public readonly ReadOnlyCollection<T> ReadOnlyCollection;

		private readonly List<T> List;

		public int Count
		{
			// 060010f2 (1.10.1 RVA 0xBC73F0)
			get
			{
				return List.Count;
			}
		}

		public int Capacity
		{
			// 060010f3 (1.10.1 RVA 0xBC7410)
			get
			{
				return List.Capacity;
			}
			// 060010f4 (1.10.1 RVA 0xBC7430)
			set
			{
				List.Capacity = value;
			}
		}

		public T this[int index]
		{
			// 060010f5 (1.10.1 RVA 0xBC7450)
			get
			{
				return List[index];
			}
			// 060010f6 (1.10.1 RVA 0xBC7540)
			set
			{
				List[index] = value;
			}
		}

		// 060010f7 (1.10.1 RVA 0xBC7630)
		public ReadOnlyWrapper()
		{
			List = new List<T>();
			ReadOnlyCollection = new ReadOnlyCollection<T>(List);
		}

		// 060010f8 (1.10.1 RVA 0xBC7700)
		public ReadOnlyWrapper(int capacity)
		{
			List = new List<T>(capacity);
			ReadOnlyCollection = new ReadOnlyCollection<T>(List);
		}

		// 060010f9 (1.10.1 RVA 0xBC77D0)
		public void Clear()
		{
			List.Clear();
		}

		// 060010fa (1.10.1 RVA 0xBC77F0)
		public bool Contains(T obj)
		{
			return List.Contains(obj);
		}

		// 060010fb (1.10.1 RVA 0xBC78A0)
		public void Add(T obj)
		{
			List.Add(obj);
		}

		// 060010fc (1.10.1 RVA 0xBC7950)
		public void AddUnique(T obj)
		{
			List.AddUnique(obj);
		}

		// 060010fd (1.10.1 RVA 0xBC7A00)
		public void Sort(IComparer<T> comparer)
		{
			List.Sort(comparer);
		}

		// 060010fe (1.10.1 RVA 0xBC7A20)
		public void AddRange(IEnumerable<T> list)
		{
			List.AddRange(list);
		}

		// 060010ff (1.10.1 RVA 0xBC7A40)
		public bool Remove(T obj)
		{
			return List.Remove(obj);
		}

		// 06001100 (1.10.1 RVA 0xBC7AF0)
		public void RemoveAt(int index)
		{
			if (List.IsIndexValid(index))
			{
				List.RemoveAt(index);
			}
		}
	}
}
