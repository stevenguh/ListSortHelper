// The MIT License (MIT)
//
// Copyright (c) .NET Foundation and Contributors
//
// All rights reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

/*============================================================
**
**
** 
**
**
** Purpose: class to sort lists
**
** 
===========================================================*/

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace System.Collections.Generic
{
  #region ListSortHelper for single lists

  public interface IListSortHelper<TKey>
  {
    void Sort(IList<TKey> keys, int index, int length, IComparer<TKey> comparer);
    int BinarySearch(IList<TKey> keys, int index, int length, TKey value, IComparer<TKey> comparer);
  }

  public static class IntrospectiveSortUtilities
  {
    // This is the threshold where Introspective sort switches to Insertion sort.
    // Empirically, 16 seems to speed up most cases without slowing down others, at least for integers.
    // Large value types may benefit from a smaller number.
    internal const int IntrosortSizeThreshold = 16;

    internal static int FloorLog2PlusOne(int n)
    {
      int result = 0;
      while (n >= 1)
      {
        result++;
        n = n / 2;
      }
      return result;
    }

    internal static void ThrowOrIgnoreBadComparer(Object comparer)
    {
      throw new ArgumentException();//SR.Format(SR.Arg_BogusIComparer, comparer));
    }
  }

  public class ListSortHelper<T> : IListSortHelper<T>
  {
    private static volatile IListSortHelper<T> defaultListSortHelper = null;

    public static IListSortHelper<T> Default => defaultListSortHelper ?? (defaultListSortHelper = new ListSortHelper<T>());

    #region IListSortHelper<T> Members

    public void Sort(IList<T> keys, int index, int length, IComparer<T> comparer)
    {
      Debug.Assert(keys != null, "Check the arguments in the caller!");
      Debug.Assert(index >= 0 && length >= 0 && (keys.Count - index >= length), "Check the arguments in the caller!");

      // Add a try block here to detect IComparers (or their
      // underlying IComparables, etc) that are bogus.
      try
      {
        comparer = comparer ?? Comparer<T>.Default;
        IntrospectiveSort(keys, index, length, comparer.Compare);
      }
      catch (IndexOutOfRangeException)
      {
        IntrospectiveSortUtilities.ThrowOrIgnoreBadComparer(comparer);
      }
      catch (Exception e)
      {
        throw new InvalidOperationException("", e);//SR.InvalidOperation_IComparerFailed, e);
      }
    }

    public int BinarySearch(IList<T> list, int index, int length, T value, IComparer<T> comparer)
    {
      try
      {
        comparer = comparer ?? Comparer<T>.Default;
        return InternalBinarySearch(list, index, length, value, comparer);
      }
      catch (Exception e)
      {
        throw new InvalidOperationException("", e);//SR.InvalidOperation_IComparerFailed, e);
      }
    }

    #endregion

    internal static void Sort(IList<T> keys, int index, int length, Comparison<T> comparer)
    {
      Debug.Assert(keys != null, "Check the arguments in the caller!");
      Debug.Assert(index >= 0 && length >= 0 && (keys.Count - index >= length), "Check the arguments in the caller!");
      Debug.Assert(comparer != null, "Check the arguments in the caller!");

      // Add a try block here to detect bogus comparisons
      try
      {
        IntrospectiveSort(keys, index, length, comparer);
      }
      catch (IndexOutOfRangeException)
      {
        IntrospectiveSortUtilities.ThrowOrIgnoreBadComparer(comparer);
      }
      catch (Exception e)
      {
        throw new InvalidOperationException("", e);//SR.InvalidOperation_IComparerFailed, e);
      }
    }

    internal static int InternalBinarySearch(IList<T> list, int index, int length, T value, IComparer<T> comparer)
    {
      Debug.Assert(list != null, "Check the arguments in the caller!");
      Debug.Assert(index >= 0 && length >= 0 && (list.Count - index >= length), "Check the arguments in the caller!");

      int lo = index;
      int hi = index + length - 1;
      while (lo <= hi)
      {
        int i = lo + ((hi - lo) >> 1);
        int order = comparer.Compare(list[i], value);

        if (order == 0) return i;
        if (order < 0)
        {
          lo = i + 1;
        }
        else
        {
          hi = i - 1;
        }
      }

      return ~lo;
    }

    private static void SwapIfGreater(IList<T> keys, Comparison<T> comparer, int a, int b)
    {
      if (a != b)
      {
        if (comparer(keys[a], keys[b]) > 0)
        {
          T key = keys[a];
          keys[a] = keys[b];
          keys[b] = key;
        }
      }
    }

    private static void Swap(IList<T> a, int i, int j)
    {
      if (i != j)
      {
        T t = a[i];
        a[i] = a[j];
        a[j] = t;
      }
    }

    internal static void IntrospectiveSort(IList<T> keys, int left, int length, Comparison<T> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(comparer != null);
      Debug.Assert(left >= 0);
      Debug.Assert(length >= 0);
      Debug.Assert(length <= keys.Count);
      Debug.Assert(length + left <= keys.Count);

      if (length < 2)
        return;

      IntroSort(keys, left, length + left - 1, 2 * IntrospectiveSortUtilities.FloorLog2PlusOne(length), comparer);
    }

    private static void IntroSort(IList<T> keys, int lo, int hi, int depthLimit, Comparison<T> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(comparer != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi < keys.Count);

      while (hi > lo)
      {
        int partitionSize = hi - lo + 1;
        if (partitionSize <= IntrospectiveSortUtilities.IntrosortSizeThreshold)
        {
          if (partitionSize == 1)
          {
            return;
          }
          if (partitionSize == 2)
          {
            SwapIfGreater(keys, comparer, lo, hi);
            return;
          }
          if (partitionSize == 3)
          {
            SwapIfGreater(keys, comparer, lo, hi - 1);
            SwapIfGreater(keys, comparer, lo, hi);
            SwapIfGreater(keys, comparer, hi - 1, hi);
            return;
          }

          InsertionSort(keys, lo, hi, comparer);
          return;
        }

        if (depthLimit == 0)
        {
          Heapsort(keys, lo, hi, comparer);
          return;
        }
        depthLimit--;

        int p = PickPivotAndPartition(keys, lo, hi, comparer);
        // Note we've already partitioned around the pivot and do not have to move the pivot again.
        IntroSort(keys, p + 1, hi, depthLimit, comparer);
        hi = p - 1;
      }
    }

    private static int PickPivotAndPartition(IList<T> keys, int lo, int hi, Comparison<T> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(comparer != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi > lo);
      Debug.Assert(hi < keys.Count);

      // Compute median-of-three.  But also partition them, since we've done the comparison.
      int middle = lo + ((hi - lo) / 2);

      // Sort lo, mid and hi appropriately, then pick mid as the pivot.
      SwapIfGreater(keys, comparer, lo, middle);  // swap the low with the mid point
      SwapIfGreater(keys, comparer, lo, hi);   // swap the low with the high
      SwapIfGreater(keys, comparer, middle, hi); // swap the middle with the high

      T pivot = keys[middle];
      Swap(keys, middle, hi - 1);
      int left = lo, right = hi - 1;  // We already partitioned lo and hi and put the pivot in hi - 1.  And we pre-increment & decrement below.

      while (left < right)
      {
        while (comparer(keys[++left], pivot) < 0) ;
        while (comparer(pivot, keys[--right]) < 0) ;

        if (left >= right)
          break;

        Swap(keys, left, right);
      }

      // Put pivot in the right location.
      Swap(keys, left, (hi - 1));
      return left;
    }

    private static void Heapsort(IList<T> keys, int lo, int hi, Comparison<T> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(comparer != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi > lo);
      Debug.Assert(hi < keys.Count);

      int n = hi - lo + 1;
      for (int i = n / 2; i >= 1; i = i - 1)
      {
        DownHeap(keys, i, n, lo, comparer);
      }
      for (int i = n; i > 1; i = i - 1)
      {
        Swap(keys, lo, lo + i - 1);
        DownHeap(keys, 1, i - 1, lo, comparer);
      }
    }

    private static void DownHeap(IList<T> keys, int i, int n, int lo, Comparison<T> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(comparer != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(lo < keys.Count);

      T d = keys[lo + i - 1];
      int child;
      while (i <= n / 2)
      {
        child = 2 * i;
        if (child < n && comparer(keys[lo + child - 1], keys[lo + child]) < 0)
        {
          child++;
        }
        if (!(comparer(d, keys[lo + child - 1]) < 0))
          break;
        keys[lo + i - 1] = keys[lo + child - 1];
        i = child;
      }
      keys[lo + i - 1] = d;
    }

    private static void InsertionSort(IList<T> keys, int lo, int hi, Comparison<T> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi >= lo);
      Debug.Assert(hi <= keys.Count);

      int i, j;
      T t;
      for (i = lo; i < hi; i++)
      {
        j = i;
        t = keys[i + 1];
        while (j >= lo && comparer(t, keys[j]) < 0)
        {
          keys[j + 1] = keys[j];
          j--;
        }
        keys[j + 1] = t;
      }
    }
  }

  public class GenericListSortHelper<T> : IListSortHelper<T>
      where T : IComparable<T>
  {
    // Do not add a constructor to this class because ListSortHelper<T>.CreateSortHelper will not execute it

    #region IListSortHelper<T> Members

    public void Sort(IList<T> keys, int index, int length, IComparer<T> comparer)
    {
      Debug.Assert(keys != null, "Check the arguments in the caller!");
      Debug.Assert(index >= 0 && length >= 0 && (keys.Count - index >= length), "Check the arguments in the caller!");

      try
      {
        if (comparer == null || comparer == Comparer<T>.Default)
        {
          IntrospectiveSort(keys, index, length);
        }
        else
        {
          ListSortHelper<T>.IntrospectiveSort(keys, index, length, comparer.Compare);
        }
      }
      catch (IndexOutOfRangeException)
      {
        IntrospectiveSortUtilities.ThrowOrIgnoreBadComparer(comparer);
      }
      catch (Exception e)
      {
        throw new InvalidOperationException("", e);//SR.InvalidOperation_IComparerFailed, e);
      }
    }

    public int BinarySearch(IList<T> list, int index, int length, T value, IComparer<T> comparer)
    {
      Debug.Assert(list != null, "Check the arguments in the caller!");
      Debug.Assert(index >= 0 && length >= 0 && (list.Count - index >= length), "Check the arguments in the caller!");

      try
      {
        if (comparer == null || comparer == Comparer<T>.Default)
        {
          return BinarySearch(list, index, length, value);
        }
        else
        {
          return ListSortHelper<T>.InternalBinarySearch(list, index, length, value, comparer);
        }
      }
      catch (Exception e)
      {
        throw new InvalidOperationException("", e);//SR.InvalidOperation_IComparerFailed, e);
      }
    }

    #endregion

    // This function is called when the user doesn't specify any comparer.
    // Since T is constrained here, we can call IComparable<T>.CompareTo here.
    // We can avoid boxing for value type and casting for reference types.
    private static int BinarySearch(IList<T> list, int index, int length, T value)
    {
      int lo = index;
      int hi = index + length - 1;
      while (lo <= hi)
      {
        int i = lo + ((hi - lo) >> 1);
        int order;
        if (list[i] == null)
        {
          order = (value == null) ? 0 : -1;
        }
        else
        {
          order = list[i].CompareTo(value);
        }

        if (order == 0)
        {
          return i;
        }

        if (order < 0)
        {
          lo = i + 1;
        }
        else
        {
          hi = i - 1;
        }
      }

      return ~lo;
    }

    private static void SwapIfGreaterWithItems(IList<T> keys, int a, int b)
    {
      Debug.Assert(keys != null);
      Debug.Assert(0 <= a && a < keys.Count);
      Debug.Assert(0 <= b && b < keys.Count);

      if (a != b)
      {
        if (keys[a] != null && keys[a].CompareTo(keys[b]) > 0)
        {
          T key = keys[a];
          keys[a] = keys[b];
          keys[b] = key;
        }
      }
    }

    private static void Swap(IList<T> a, int i, int j)
    {
      if (i != j)
      {
        T t = a[i];
        a[i] = a[j];
        a[j] = t;
      }
    }

    internal static void IntrospectiveSort(IList<T> keys, int left, int length)
    {
      Debug.Assert(keys != null);
      Debug.Assert(left >= 0);
      Debug.Assert(length >= 0);
      Debug.Assert(length <= keys.Count);
      Debug.Assert(length + left <= keys.Count);

      if (length < 2)
        return;

      IntroSort(keys, left, length + left - 1, 2 * IntrospectiveSortUtilities.FloorLog2PlusOne(length));
    }

    private static void IntroSort(IList<T> keys, int lo, int hi, int depthLimit)
    {
      Debug.Assert(keys != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi < keys.Count);

      while (hi > lo)
      {
        int partitionSize = hi - lo + 1;
        if (partitionSize <= IntrospectiveSortUtilities.IntrosortSizeThreshold)
        {
          if (partitionSize == 1)
          {
            return;
          }
          if (partitionSize == 2)
          {
            SwapIfGreaterWithItems(keys, lo, hi);
            return;
          }
          if (partitionSize == 3)
          {
            SwapIfGreaterWithItems(keys, lo, hi - 1);
            SwapIfGreaterWithItems(keys, lo, hi);
            SwapIfGreaterWithItems(keys, hi - 1, hi);
            return;
          }

          InsertionSort(keys, lo, hi);
          return;
        }

        if (depthLimit == 0)
        {
          Heapsort(keys, lo, hi);
          return;
        }
        depthLimit--;

        int p = PickPivotAndPartition(keys, lo, hi);
        // Note we've already partitioned around the pivot and do not have to move the pivot again.
        IntroSort(keys, p + 1, hi, depthLimit);
        hi = p - 1;
      }
    }

    private static int PickPivotAndPartition(IList<T> keys, int lo, int hi)
    {
      Debug.Assert(keys != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi > lo);
      Debug.Assert(hi < keys.Count);

      // Compute median-of-three.  But also partition them, since we've done the comparison.
      int middle = lo + ((hi - lo) / 2);

      // Sort lo, mid and hi appropriately, then pick mid as the pivot.
      SwapIfGreaterWithItems(keys, lo, middle);  // swap the low with the mid point
      SwapIfGreaterWithItems(keys, lo, hi);   // swap the low with the high
      SwapIfGreaterWithItems(keys, middle, hi); // swap the middle with the high

      T pivot = keys[middle];
      Swap(keys, middle, hi - 1);
      int left = lo, right = hi - 1;  // We already partitioned lo and hi and put the pivot in hi - 1.  And we pre-increment & decrement below.

      while (left < right)
      {
        if (pivot == null)
        {
          while (left < (hi - 1) && keys[++left] == null) ;
          while (right > lo && keys[--right] != null) ;
        }
        else
        {
          while (pivot.CompareTo(keys[++left]) > 0) ;
          while (pivot.CompareTo(keys[--right]) < 0) ;
        }

        if (left >= right)
          break;

        Swap(keys, left, right);
      }

      // Put pivot in the right location.
      Swap(keys, left, (hi - 1));
      return left;
    }

    private static void Heapsort(IList<T> keys, int lo, int hi)
    {
      Debug.Assert(keys != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi > lo);
      Debug.Assert(hi < keys.Count);

      int n = hi - lo + 1;
      for (int i = n / 2; i >= 1; i = i - 1)
      {
        DownHeap(keys, i, n, lo);
      }
      for (int i = n; i > 1; i = i - 1)
      {
        Swap(keys, lo, lo + i - 1);
        DownHeap(keys, 1, i - 1, lo);
      }
    }

    private static void DownHeap(IList<T> keys, int i, int n, int lo)
    {
      Debug.Assert(keys != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(lo < keys.Count);

      T d = keys[lo + i - 1];
      int child;
      while (i <= n / 2)
      {
        child = 2 * i;
        if (child < n && (keys[lo + child - 1] == null || keys[lo + child - 1].CompareTo(keys[lo + child]) < 0))
        {
          child++;
        }
        if (keys[lo + child - 1] == null || keys[lo + child - 1].CompareTo(d) < 0)
          break;
        keys[lo + i - 1] = keys[lo + child - 1];
        i = child;
      }
      keys[lo + i - 1] = d;
    }

    private static void InsertionSort(IList<T> keys, int lo, int hi)
    {
      Debug.Assert(keys != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi >= lo);
      Debug.Assert(hi <= keys.Count);

      int i, j;
      T t;
      for (i = lo; i < hi; i++)
      {
        j = i;
        t = keys[i + 1];
        while (j >= lo && (t == null || t.CompareTo(keys[j]) < 0))
        {
          keys[j + 1] = keys[j];
          j--;
        }
        keys[j + 1] = t;
      }
    }
  }

  #endregion

  #region ListSortHelper for paired key and value lists

  public interface IListSortHelper<TKey, TValue>
  {
    void Sort(IList<TKey> keys, IList<TValue> values, int index, int length, IComparer<TKey> comparer);
  }

  public class ListSortHelper<TKey, TValue> : IListSortHelper<TKey, TValue>
  {
    private static volatile IListSortHelper<TKey, TValue> defaultListSortHelper = null;

    public static IListSortHelper<TKey, TValue> Default => defaultListSortHelper ?? (defaultListSortHelper = new ListSortHelper<TKey, TValue>());

    public void Sort(IList<TKey> keys, IList<TValue> values, int index, int length, IComparer<TKey> comparer)
    {
      Debug.Assert(keys != null, "Check the arguments in the caller!");  // Precondition on interface method
      Debug.Assert(values != null, "Check the arguments in the caller!");
      Debug.Assert(index >= 0 && length >= 0 && (keys.Count - index >= length), "Check the arguments in the caller!");

      // Add a try block here to detect IComparers (or their
      // underlying IComparables, etc) that are bogus.
      try
      {
        if (comparer == null || comparer == Comparer<TKey>.Default)
        {
          comparer = Comparer<TKey>.Default;
        }

        IntrospectiveSort(keys, values, index, length, comparer);
      }
      catch (IndexOutOfRangeException)
      {
        IntrospectiveSortUtilities.ThrowOrIgnoreBadComparer(comparer);
      }
      catch (Exception e)
      {
        throw new InvalidOperationException("", e);//SR.InvalidOperation_IComparerFailed, e);
      }
    }

    private static void SwapIfGreaterWithItems(IList<TKey> keys, IList<TValue> values, IComparer<TKey> comparer, int a, int b)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null && values.Count >= keys.Count);
      Debug.Assert(comparer != null);
      Debug.Assert(0 <= a && a < keys.Count);
      Debug.Assert(0 <= b && b < keys.Count);

      if (a != b)
      {
        if (comparer.Compare(keys[a], keys[b]) > 0)
        {
          TKey key = keys[a];
          keys[a] = keys[b];
          keys[b] = key;

          TValue value = values[a];
          values[a] = values[b];
          values[b] = value;
        }
      }
    }

    private static void Swap(IList<TKey> keys, IList<TValue> values, int i, int j)
    {
      if (i != j)
      {
        TKey k = keys[i];
        keys[i] = keys[j];
        keys[j] = k;

        TValue v = values[i];
        values[i] = values[j];
        values[j] = v;
      }
    }

    internal static void IntrospectiveSort(IList<TKey> keys, IList<TValue> values, int left, int length, IComparer<TKey> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null);
      Debug.Assert(comparer != null);
      Debug.Assert(left >= 0);
      Debug.Assert(length >= 0);
      Debug.Assert(length <= keys.Count);
      Debug.Assert(length + left <= keys.Count);
      Debug.Assert(length + left <= values.Count);

      if (length < 2)
        return;

      IntroSort(keys, values, left, length + left - 1, 2 * IntrospectiveSortUtilities.FloorLog2PlusOne(length), comparer);
    }

    private static void IntroSort(IList<TKey> keys, IList<TValue> values, int lo, int hi, int depthLimit, IComparer<TKey> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null);
      Debug.Assert(comparer != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi < keys.Count);

      while (hi > lo)
      {
        int partitionSize = hi - lo + 1;
        if (partitionSize <= IntrospectiveSortUtilities.IntrosortSizeThreshold)
        {
          if (partitionSize == 1)
          {
            return;
          }
          if (partitionSize == 2)
          {
            SwapIfGreaterWithItems(keys, values, comparer, lo, hi);
            return;
          }
          if (partitionSize == 3)
          {
            SwapIfGreaterWithItems(keys, values, comparer, lo, hi - 1);
            SwapIfGreaterWithItems(keys, values, comparer, lo, hi);
            SwapIfGreaterWithItems(keys, values, comparer, hi - 1, hi);
            return;
          }

          InsertionSort(keys, values, lo, hi, comparer);
          return;
        }

        if (depthLimit == 0)
        {
          Heapsort(keys, values, lo, hi, comparer);
          return;
        }
        depthLimit--;

        int p = PickPivotAndPartition(keys, values, lo, hi, comparer);
        // Note we've already partitioned around the pivot and do not have to move the pivot again.
        IntroSort(keys, values, p + 1, hi, depthLimit, comparer);
        hi = p - 1;
      }
    }

    private static int PickPivotAndPartition(IList<TKey> keys, IList<TValue> values, int lo, int hi, IComparer<TKey> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null);
      Debug.Assert(comparer != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi > lo);
      Debug.Assert(hi < keys.Count);

      // Compute median-of-three.  But also partition them, since we've done the comparison.
      int middle = lo + ((hi - lo) / 2);

      // Sort lo, mid and hi appropriately, then pick mid as the pivot.
      SwapIfGreaterWithItems(keys, values, comparer, lo, middle);  // swap the low with the mid point
      SwapIfGreaterWithItems(keys, values, comparer, lo, hi);   // swap the low with the high
      SwapIfGreaterWithItems(keys, values, comparer, middle, hi); // swap the middle with the high

      TKey pivot = keys[middle];
      Swap(keys, values, middle, hi - 1);
      int left = lo, right = hi - 1;  // We already partitioned lo and hi and put the pivot in hi - 1.  And we pre-increment & decrement below.

      while (left < right)
      {
        while (comparer.Compare(keys[++left], pivot) < 0) ;
        while (comparer.Compare(pivot, keys[--right]) < 0) ;

        if (left >= right)
          break;

        Swap(keys, values, left, right);
      }

      // Put pivot in the right location.
      Swap(keys, values, left, (hi - 1));
      return left;
    }

    private static void Heapsort(IList<TKey> keys, IList<TValue> values, int lo, int hi, IComparer<TKey> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null);
      Debug.Assert(comparer != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi > lo);
      Debug.Assert(hi < keys.Count);

      int n = hi - lo + 1;
      for (int i = n / 2; i >= 1; i = i - 1)
      {
        DownHeap(keys, values, i, n, lo, comparer);
      }
      for (int i = n; i > 1; i = i - 1)
      {
        Swap(keys, values, lo, lo + i - 1);
        DownHeap(keys, values, 1, i - 1, lo, comparer);
      }
    }

    private static void DownHeap(IList<TKey> keys, IList<TValue> values, int i, int n, int lo, IComparer<TKey> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null);
      Debug.Assert(comparer != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(lo < keys.Count);

      TKey d = keys[lo + i - 1];
      TValue dValue = values[lo + i - 1];
      int child;
      while (i <= n / 2)
      {
        child = 2 * i;
        if (child < n && comparer.Compare(keys[lo + child - 1], keys[lo + child]) < 0)
        {
          child++;
        }
        if (!(comparer.Compare(d, keys[lo + child - 1]) < 0))
          break;
        keys[lo + i - 1] = keys[lo + child - 1];
        values[lo + i - 1] = values[lo + child - 1];
        i = child;
      }
      keys[lo + i - 1] = d;
      values[lo + i - 1] = dValue;
    }

    private static void InsertionSort(IList<TKey> keys, IList<TValue> values, int lo, int hi, IComparer<TKey> comparer)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null);
      Debug.Assert(comparer != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi >= lo);
      Debug.Assert(hi <= keys.Count);

      int i, j;
      TKey t;
      TValue tValue;
      for (i = lo; i < hi; i++)
      {
        j = i;
        t = keys[i + 1];
        tValue = values[i + 1];
        while (j >= lo && comparer.Compare(t, keys[j]) < 0)
        {
          keys[j + 1] = keys[j];
          values[j + 1] = values[j];
          j--;
        }
        keys[j + 1] = t;
        values[j + 1] = tValue;
      }
    }
  }

  public class GenericListSortHelper<TKey, TValue> : IListSortHelper<TKey, TValue>
      where TKey : IComparable<TKey>
  {
    public void Sort(IList<TKey> keys, IList<TValue> values, int index, int length, IComparer<TKey> comparer)
    {
      Debug.Assert(keys != null, "Check the arguments in the caller!");
      Debug.Assert(index >= 0 && length >= 0 && (keys.Count - index >= length), "Check the arguments in the caller!");

      // Add a try block here to detect IComparers (or their
      // underlying IComparables, etc) that are bogus.
      try
      {
        if (comparer == null || comparer == Comparer<TKey>.Default)
        {
          IntrospectiveSort(keys, values, index, length);
        }
        else
        {
          ListSortHelper<TKey, TValue>.IntrospectiveSort(keys, values, index, length, comparer);
        }
      }
      catch (IndexOutOfRangeException)
      {
        IntrospectiveSortUtilities.ThrowOrIgnoreBadComparer(comparer);
      }
      catch (Exception e)
      {
        throw new InvalidOperationException("", e);//SR.InvalidOperation_IComparerFailed, e);
      }
    }

    private static void SwapIfGreaterWithItems(IList<TKey> keys, IList<TValue> values, int a, int b)
    {
      if (a != b)
      {
        if (keys[a] != null && keys[a].CompareTo(keys[b]) > 0)
        {
          TKey key = keys[a];
          keys[a] = keys[b];
          keys[b] = key;

          TValue value = values[a];
          values[a] = values[b];
          values[b] = value;
        }
      }
    }

    private static void Swap(IList<TKey> keys, IList<TValue> values, int i, int j)
    {
      if (i != j)
      {
        TKey k = keys[i];
        keys[i] = keys[j];
        keys[j] = k;

        TValue v = values[i];
        values[i] = values[j];
        values[j] = v;
      }
    }

    internal static void IntrospectiveSort(IList<TKey> keys, IList<TValue> values, int left, int length)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null);
      Debug.Assert(left >= 0);
      Debug.Assert(length >= 0);
      Debug.Assert(length <= keys.Count);
      Debug.Assert(length + left <= keys.Count);
      Debug.Assert(length + left <= values.Count);

      if (length < 2)
        return;

      IntroSort(keys, values, left, length + left - 1, 2 * IntrospectiveSortUtilities.FloorLog2PlusOne(length));
    }

    private static void IntroSort(IList<TKey> keys, IList<TValue> values, int lo, int hi, int depthLimit)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi < keys.Count);

      while (hi > lo)
      {
        int partitionSize = hi - lo + 1;
        if (partitionSize <= IntrospectiveSortUtilities.IntrosortSizeThreshold)
        {
          if (partitionSize == 1)
          {
            return;
          }
          if (partitionSize == 2)
          {
            SwapIfGreaterWithItems(keys, values, lo, hi);
            return;
          }
          if (partitionSize == 3)
          {
            SwapIfGreaterWithItems(keys, values, lo, hi - 1);
            SwapIfGreaterWithItems(keys, values, lo, hi);
            SwapIfGreaterWithItems(keys, values, hi - 1, hi);
            return;
          }

          InsertionSort(keys, values, lo, hi);
          return;
        }

        if (depthLimit == 0)
        {
          Heapsort(keys, values, lo, hi);
          return;
        }
        depthLimit--;

        int p = PickPivotAndPartition(keys, values, lo, hi);
        // Note we've already partitioned around the pivot and do not have to move the pivot again.
        IntroSort(keys, values, p + 1, hi, depthLimit);
        hi = p - 1;
      }
    }

    private static int PickPivotAndPartition(IList<TKey> keys, IList<TValue> values, int lo, int hi)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi > lo);
      Debug.Assert(hi < keys.Count);

      // Compute median-of-three.  But also partition them, since we've done the comparison.
      int middle = lo + ((hi - lo) / 2);

      // Sort lo, mid and hi appropriately, then pick mid as the pivot.
      SwapIfGreaterWithItems(keys, values, lo, middle);  // swap the low with the mid point
      SwapIfGreaterWithItems(keys, values, lo, hi);   // swap the low with the high
      SwapIfGreaterWithItems(keys, values, middle, hi); // swap the middle with the high

      TKey pivot = keys[middle];
      Swap(keys, values, middle, hi - 1);
      int left = lo, right = hi - 1;  // We already partitioned lo and hi and put the pivot in hi - 1.  And we pre-increment & decrement below.

      while (left < right)
      {
        if (pivot == null)
        {
          while (left < (hi - 1) && keys[++left] == null) ;
          while (right > lo && keys[--right] != null) ;
        }
        else
        {
          while (pivot.CompareTo(keys[++left]) > 0) ;
          while (pivot.CompareTo(keys[--right]) < 0) ;
        }

        if (left >= right)
          break;

        Swap(keys, values, left, right);
      }

      // Put pivot in the right location.
      Swap(keys, values, left, (hi - 1));
      return left;
    }

    private static void Heapsort(IList<TKey> keys, IList<TValue> values, int lo, int hi)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi > lo);
      Debug.Assert(hi < keys.Count);

      int n = hi - lo + 1;
      for (int i = n / 2; i >= 1; i = i - 1)
      {
        DownHeap(keys, values, i, n, lo);
      }
      for (int i = n; i > 1; i = i - 1)
      {
        Swap(keys, values, lo, lo + i - 1);
        DownHeap(keys, values, 1, i - 1, lo);
      }
    }

    private static void DownHeap(IList<TKey> keys, IList<TValue> values, int i, int n, int lo)
    {
      Debug.Assert(keys != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(lo < keys.Count);

      TKey d = keys[lo + i - 1];
      TValue dValue = values[lo + i - 1];
      int child;
      while (i <= n / 2)
      {
        child = 2 * i;
        if (child < n && (keys[lo + child - 1] == null || keys[lo + child - 1].CompareTo(keys[lo + child]) < 0))
        {
          child++;
        }
        if (keys[lo + child - 1] == null || keys[lo + child - 1].CompareTo(d) < 0)
          break;
        keys[lo + i - 1] = keys[lo + child - 1];
        values[lo + i - 1] = values[lo + child - 1];
        i = child;
      }
      keys[lo + i - 1] = d;
      values[lo + i - 1] = dValue;
    }

    private static void InsertionSort(IList<TKey> keys, IList<TValue> values, int lo, int hi)
    {
      Debug.Assert(keys != null);
      Debug.Assert(values != null);
      Debug.Assert(lo >= 0);
      Debug.Assert(hi >= lo);
      Debug.Assert(hi <= keys.Count);

      int i, j;
      TKey t;
      TValue tValue;
      for (i = lo; i < hi; i++)
      {
        j = i;
        t = keys[i + 1];
        tValue = values[i + 1];
        while (j >= lo && (t == null || t.CompareTo(keys[j]) < 0))
        {
          keys[j + 1] = keys[j];
          values[j + 1] = values[j];
          j--;
        }
        keys[j + 1] = t;
        values[j + 1] = tValue;
      }
    }
  }

  #endregion
}