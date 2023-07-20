using System.Collections.Generic;

namespace Beatrate.Core
{
	public interface IPredicate<T>
	{
		bool Invoke(T element);
	}

	public static class CollectionUtility
	{
		private static class IntrospectiveSortUtilities
		{
			// This is the threshold where Introspective sort switches to Insertion sort.
			// Imperically, 16 seems to speed up most cases without slowing down others, at least for integers.
			// Large value types may benefit from a smaller number.
			public const int IntrosortSizeThreshold = 16;
			public const int QuickSortDepthThreshold = 32;

			public static int FloorLog2(int n)
			{
				int result = 0;
				while(n >= 1)
				{
					result++;
					n = n / 2;
				}
				return result;
			}
		}

		public static void Sort<TElement, TComparer>(IList<TElement> collection, in TComparer comparer, int index, int length)
			where TComparer : struct, IComparer<TElement>
		{
			IntrospectiveSort(collection, index, length, comparer);
		}

		public static void Sort<TElement, TComparer>(IList<TElement> collection, in TComparer comparer)
			where TComparer : struct, IComparer<TElement>
		{
			IntrospectiveSort(collection, 0, collection.Count, comparer);
		}


		public static int FindIndex<TElement, TPredicate>(IList<TElement> collection, int startIndex, int count, in TPredicate predicate) where TPredicate : struct, IPredicate<TElement>
		{
			for(int i = 0; i < count; ++i)
			{
				if(predicate.Invoke(collection[startIndex + i]))
				{
					return startIndex + i;
				}
			}

			return -1;
		}

		public static int FindIndex<TElement, TPredicate>(IList<TElement> collection, int startIndex, in TPredicate predicate) where TPredicate : struct, IPredicate<TElement>
		{
			return FindIndex(collection, startIndex, collection.Count - startIndex, predicate);
		}

		public static int FindIndex<TElement, TPredicate>(IList<TElement> collection, in TPredicate predicate) where TPredicate : struct, IPredicate<TElement>
		{
			return FindIndex(collection, 0, collection.Count, predicate);
		}

		public static bool TryFind<TElement, TPredicate>(IList<TElement> collection, int startIndex, int count, in TPredicate predicate, out TElement element) where TPredicate : struct, IPredicate<TElement>
		{
			int i = FindIndex(collection, startIndex, count, predicate);
			if(i == -1)
			{
				element = default;
				return false;
			}

			element = collection[i];
			return true;
		}

		public static bool TryFind<TElement, TPredicate>(IList<TElement> collection, int startIndex, in TPredicate predicate, out TElement element) where TPredicate : struct, IPredicate<TElement>
		{
			return TryFind(collection, startIndex, collection.Count - startIndex, predicate, out element);
		}

		public static bool TryFind<TElement, TPredicate>(IList<TElement> collection, in TPredicate predicate, out TElement element) where TPredicate : struct, IPredicate<TElement>
		{
			return TryFind(collection, 0, collection.Count, predicate, out element);
		}

		private static void IntrospectiveSort<TElement, TComparer>(IList<TElement> collection, int left, int length, in TComparer comparer)
			where TComparer : struct, IComparer<TElement>
		{
			if(length < 2)
				return;

			IntroSort(collection, left, length + left - 1, 2 * IntrospectiveSortUtilities.FloorLog2(collection.Count), comparer);
		}

		private static void IntroSort<TElement, TComparer>(IList<TElement> collection, int lo, int hi, int depthLimit, in TComparer comparer)
			where TComparer : struct, IComparer<TElement>
		{
			while(hi > lo)
			{
				int partitionSize = hi - lo + 1;
				if(partitionSize <= IntrospectiveSortUtilities.IntrosortSizeThreshold)
				{
					if(partitionSize == 1)
					{
						return;
					}
					if(partitionSize == 2)
					{
						SwapIfGreater(collection, comparer, lo, hi);
						return;
					}
					if(partitionSize == 3)
					{
						SwapIfGreater(collection, comparer, lo, hi - 1);
						SwapIfGreater(collection, comparer, lo, hi);
						SwapIfGreater(collection, comparer, hi - 1, hi);
						return;
					}

					InsertionSort(collection, lo, hi, comparer);
					return;
				}

				if(depthLimit == 0)
				{
					HeapSort(collection, lo, hi, comparer);
					return;
				}
				depthLimit--;

				int p = PickPivotAndPartition(collection, lo, hi, comparer);
				// Note we've already partitioned around the pivot and do not have to move the pivot again.
				IntroSort(collection, p + 1, hi, depthLimit, comparer);
				hi = p - 1;
			}
		}

		private static void SwapIfGreater<TElement, TComparer>(IList<TElement> collection, in TComparer comparer, int a, int b)
			where TComparer : struct, IComparer<TElement>
		{
			if(a != b)
			{
				if(comparer.Compare(collection[a], collection[b]) > 0)
				{
					TElement element = collection[a];
					collection[a] = collection[b];
					collection[b] = element;
				}
			}
		}

		private static void InsertionSort<TElement, TComparer>(IList<TElement> collection, int lo, int hi, in TComparer comparer)
			where TComparer : struct, IComparer<TElement>
		{
			int i, j;
			TElement t;
			for(i = lo; i < hi; i++)
			{
				j = i;
				t = collection[i + 1];
				while(j >= lo && comparer.Compare(t, collection[j]) < 0)
				{
					collection[j + 1] = collection[j];
					j--;
				}
				collection[j + 1] = t;
			}
		}

		private static void HeapSort<TElement, TComparer>(IList<TElement> collection, int lo, int hi, in TComparer comparer)
			where TComparer : struct, IComparer<TElement>
		{
			int n = hi - lo + 1;
			for(int i = n / 2; i >= 1; i = i - 1)
			{
				DownHeap(collection, i, n, lo, comparer);
			}
			for(int i = n; i > 1; i = i - 1)
			{
				Swap(collection, lo, lo + i - 1);
				DownHeap(collection, 1, i - 1, lo, comparer);
			}
		}

		private static void DownHeap<TElement, TComparer>(IList<TElement> collection, int i, int n, int lo, in TComparer comparer)
			where TComparer : struct, IComparer<TElement>
		{
			TElement d = collection[lo + i - 1];
			int child;
			while(i <= n / 2)
			{
				child = 2 * i;
				if(child < n && comparer.Compare(collection[lo + child - 1], collection[lo + child]) < 0)
				{
					child++;
				}
				if(!(comparer.Compare(d, collection[lo + child - 1]) < 0))
					break;
				collection[lo + i - 1] = collection[lo + child - 1];
				i = child;
			}
			collection[lo + i - 1] = d;
		}

		private static int PickPivotAndPartition<TElement, TComparer>(IList<TElement> collection, int lo, int hi, in TComparer comparer)
			where TComparer : struct, IComparer<TElement>
		{
			// Compute median-of-three.  But also partition them, since we've done the comparison.
			int middle = lo + ((hi - lo) / 2);

			// Sort lo, mid and hi appropriately, then pick mid as the pivot.
			SwapIfGreater(collection, comparer, lo, middle);  // swap the low with the mid point
			SwapIfGreater(collection, comparer, lo, hi);   // swap the low with the high
			SwapIfGreater(collection, comparer, middle, hi); // swap the middle with the high

			TElement pivot = collection[middle];
			Swap(collection, middle, hi - 1);
			int left = lo, right = hi - 1;  // We already partitioned lo and hi and put the pivot in hi - 1.  And we pre-increment & decrement below.

			while(left < right)
			{
				while(comparer.Compare(collection[++left], pivot) < 0) ;
				while(comparer.Compare(pivot, collection[--right]) < 0) ;

				if(left >= right)
					break;

				Swap(collection, left, right);
			}

			// Put pivot in the right location.
			Swap(collection, left, (hi - 1));
			return left;
		}

		private static void Swap<TElement>(IList<TElement> collection, int i, int j)
		{
			if(i != j)
			{
				TElement t = collection[i];
				collection[i] = collection[j];
				collection[j] = t;
			}
		}
	}
}
