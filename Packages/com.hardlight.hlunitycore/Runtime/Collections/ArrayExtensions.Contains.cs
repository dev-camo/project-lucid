using System;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0200005b, all eleven declarations, no fields.
    // Contains06000247 reuses its maintained body. The two conversion lambdas
    // preserve the real <>c__4<TFrom,TTarget> context (06000251..254); their
    // emitted native/layout binding remains separate from this source recovery.
    public static partial class ArrayExtensions
    {
        // 06000246: null array=false; empty array=true. The ordinary T null
        // comparison is CLR reference equality. Only the opt-in branch checks
        // a UnityEngine.Object with the original overloaded equality operation.
        public static bool IsArrayValid<T>(this T[] array, bool checkUnityObjects = false) where T : class
        {
            if (array == null) return false;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == null) return false;
                if (checkUnityObjects && array[i] is UnityEngine.Object unityObject && unityObject == null)
                    return false;
            }
            return true;
        }

        // 06000247: maintained original body, zero additional recovered credit.
        public static bool Contains<T>(this T[] array, T item)
        {
            return Array.IndexOf(array, item) >= 0;
        }

        // 06000248: allocation reads array.Length before invoking the delegate.
        // An empty source therefore succeeds even with a null delegate.
        public static TTarget[] Convert<TFrom, TTarget>(this TFrom[] array, Func<TFrom, TTarget> convertMethod)
        {
            TTarget[] convertedArray = new TTarget[array.Length];
            for (int i = 0; i < array.Length; i++)
                convertedArray[i] = convertMethod(array[i]);
            return convertedArray;
        }

        // 06000249: capture the actual target Type once before the allocation,
        // then pass that same object to each delegate invocation in array order.
        public static TTarget[] Convert<TFrom, TTarget>(this TFrom[] array, Func<TFrom, Type, TTarget> convertMethod)
        {
            Type type = typeof(TTarget);
            TTarget[] convertedArray = new TTarget[array.Length];
            for (int i = 0; i < array.Length; i++)
                convertedArray[i] = convertMethod(array[i], type);
            return convertedArray;
        }

        // 0600024a, natural callbacks06000253/254: enum conversion uses the
        // original Enum.ToObject(Type, object), retaining numeric unnamed values
        // and its string rejection. Other targets use Convert.ChangeType(object,
        // Type) with its genuine current-culture behavior. No parsing fallback.
        public static TTarget[] Convert<TFrom, TTarget>(this TFrom[] array) where TTarget : IConvertible
        {
            if (typeof(TTarget).IsEnum)
                return array.Convert<TFrom, TTarget>((fromValue, type) => (TTarget)Enum.ToObject(type, fromValue));
            return array.Convert<TFrom, TTarget>((fromValue, type) => (TTarget)System.Convert.ChangeType(fromValue, type));
        }

        // 0600024b..24d: genuine bare catch, including callback failure. Assign
        // the completed result only after Convert returns; failures set out=null.
        public static bool TryConvert<TFrom, TTarget>(this TFrom[] array, out TTarget[] convertedArray, Func<TFrom, TTarget> convertMethod)
        {
            try
            {
                convertedArray = array.Convert(convertMethod);
                return true;
            }
            catch
            {
                convertedArray = null;
                return false;
            }
        }

        public static bool TryConvert<TFrom, TTarget>(this TFrom[] array, out TTarget[] convertedArray, Func<TFrom, Type, TTarget> convertMethod)
        {
            try
            {
                convertedArray = array.Convert(convertMethod);
                return true;
            }
            catch
            {
                convertedArray = null;
                return false;
            }
        }

        public static bool TryConvert<TFrom, TTarget>(this TFrom[] array, out TTarget[] convertedArray) where TTarget : IConvertible
        {
            try
            {
                convertedArray = array.Convert<TFrom, TTarget>();
                return true;
            }
            catch
            {
                convertedArray = null;
                return false;
            }
        }

        // 0600024e: first endpoint is literal zero, before the requested index.
        public static void SwapFirst<T>(this T[] array, int elementToSwapIndex)
        {
            array.SwapElements(0, elementToSwapIndex);
        }

        // 0600024f: array.Length is read before the actual swap call, even when
        // the requested index would make the final pair equal.
        public static void SwapLast<T>(this T[] array, int elementToSwapIndex)
        {
            array.SwapElements(elementToSwapIndex, array.Length - 1);
        }

        // 06000250: equal indices return before accessing the array. Otherwise
        // read A, then B, write A, then B. Invalid access preserves prior state.
        public static void SwapElements<T>(this T[] array, int indexA, int indexB)
        {
            if (indexA == indexB) return;
            T temporary = array[indexA];
            array[indexA] = array[indexB];
            array[indexB] = temporary;
        }
    }
}
