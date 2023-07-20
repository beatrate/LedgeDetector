using UnityEngine;

namespace Beatrate.Core
{
	public static class MathUtility
	{
		public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis, bool clockwise = false)
		{
			Vector3 right;
			if(clockwise)
			{
				right = Vector3.Cross(from, axis);
				from = Vector3.Cross(axis, right);
			}
			else
			{
				right = Vector3.Cross(axis, from);
				from = Vector3.Cross(right, axis);
			}

			return Mathf.Atan2(Vector3.Dot(to, right), Vector3.Dot(to, from)) * Mathf.Rad2Deg;
		}

		public static bool LineLineIntersection(out Vector3 intersection, out int sign, out float signedDistance, Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2)
		{
			Vector3 lineVec3 = linePoint2 - linePoint1;
			Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
			Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

			float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

			// Is coplanar, and not parrallel.
			if(Mathf.Abs(planarFactor) < 0.0001f && crossVec1and2.sqrMagnitude > 0.0001f)
			{
				float s = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
				sign = s >= 0.0f ? 1 : -1;
				intersection = linePoint1 + (lineVec1 * s);
				signedDistance = s;
				return true;
			}
			else
			{
				intersection = Vector3.zero;
				sign = 1;
				signedDistance = 0.0f;
				return false;
			}
		}

		public static Vector3 OnlyAxis(Vector3 vector, int axis)
		{
			for(int i = 0; i < 3; ++i)
			{
				if(i != axis)
				{
					vector[i] = 0.0f;
				}
			}

			return vector;
		}
	}
}
