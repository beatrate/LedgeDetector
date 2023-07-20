using System.Collections.Generic;
using UnityEngine;

namespace LedgeDetection
{
	public readonly struct ColliderDistanceComparer : IComparer<Collider>
	{
		public Vector3 ReferencePoint { get; }

		public ColliderDistanceComparer(Vector3 referencePoint)
		{
			ReferencePoint = referencePoint;
		}

		public int Compare(Collider x, Collider y)
		{
			float distanceX = Vector3.Distance(ReferencePoint, x.ClosestPoint(ReferencePoint));
			float distanceY = Vector3.Distance(ReferencePoint, y.ClosestPoint(ReferencePoint));
			return distanceX.CompareTo(distanceY);
		}
	}
}
