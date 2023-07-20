using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace LedgeDetection
{
	public struct Ledge
	{
		public List<Vector3> Points;
		public List<Vector3> ForwardNormals;
		public List<Vector3> VerticalNormals;
		private bool disposed;

		public static Ledge Create()
		{
			var ledge = new Ledge
			{
				Points = ListPool<Vector3>.Get(),
				ForwardNormals = ListPool<Vector3>.Get(),
				VerticalNormals = ListPool<Vector3>.Get()
			};

			return ledge;
		}

		public Ledge Copy()
		{
			var ledge = Create();
			ledge.Points.AddRange(Points);
			ledge.ForwardNormals.AddRange(ForwardNormals);
			ledge.VerticalNormals.AddRange(VerticalNormals);
			return ledge;
		}

		public float GetLength()
		{
			float length = 0.0f;
			for(int pointIndex = 0; pointIndex < Points.Count - 1; ++pointIndex)
			{
				length += Vector3.Distance(Points[pointIndex], Points[pointIndex + 1]);
			}

			return length;
		}

		public float PointToDistance(Vector3 point)
		{
			if(Points.Count < 2)
			{
				return 0.0f;
			}

			float totalDistance = 0.0f;
			float pointDistance = 0.0f;
			float? minDistanceToSegment = null;

			for(int i = 0; i < Points.Count - 1; ++i)
			{
				Vector3 start = Points[i];
				Vector3 end = Points[i + 1];
				Vector3 tangent = (end - start).normalized;
				float length = (end - start).magnitude;

				float distanceToSegment = DistanceToSegment(point, start, end, tangent, length, out float projection);
				if(minDistanceToSegment == null || distanceToSegment < minDistanceToSegment.Value)
				{
					minDistanceToSegment = distanceToSegment;
					pointDistance = totalDistance + projection;
				}

				totalDistance += length;
			}

			return pointDistance;
		}

		public Vector3 DistanceToPoint(float distance, out Vector3 tangent, out Vector3 forwardNormal, out Vector3 verticalNormal)
		{
			Vector3 point = Vector3.zero;

			if(Points.Count == 0)
			{
				tangent = Vector3.forward;
				point = Vector3.zero;
				forwardNormal = Vector3.zero;
				verticalNormal = Vector3.zero;
			}
			else if(Points.Count == 1)
			{
				tangent = Vector3.forward;
				point = Points[0];
				forwardNormal = ForwardNormals[0];
				verticalNormal = VerticalNormals[0];
			}
			else
			{
				if(distance < 0.0f)
				{
					tangent = (Points[1] - Points[0]).normalized;
					point = Points[0];
					forwardNormal = ForwardNormals[0];
					verticalNormal = VerticalNormals[0];
				}
				else
				{
					float remainingDistance = distance;
					Vector3 pointTangent = Vector3.zero;
					Vector3 pointForwardNormal = Vector3.zero;
					Vector3 pointVerticalNormal = Vector3.zero;

					for(int i = 0; i < Points.Count - 1; ++i)
					{
						Vector3 start = Points[i];
						Vector3 end = Points[i + 1];
						float length = (end - start).magnitude;

						if(length >= remainingDistance)
						{
							float t = remainingDistance / length;
							point = Vector3.Lerp(start, end, t);
							pointTangent = (end - start).normalized;
							pointForwardNormal = ForwardNormals[i];
							pointVerticalNormal = VerticalNormals[i];
							break;
						}
						else if(length < remainingDistance && i == Points.Count - 2)
						{
							point = end;
							pointTangent = (end - start).normalized;
							pointForwardNormal = ForwardNormals[i];
							pointVerticalNormal = VerticalNormals[i];
						}

						remainingDistance -= length;
					}

					tangent = pointTangent;
					forwardNormal = pointForwardNormal;
					verticalNormal = pointVerticalNormal;
				}
			}

			return point;
		}

		private float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end, Vector3 tangent, float length, out float projection)
		{
			Vector3 local = point - start;
			float dot = Vector3.Dot(tangent, local);

			if(dot < 0.0f)
			{
				projection = 0.0f;
				return Vector3.Distance(start, point);
			}

			if(dot > length)
			{
				projection = length;
				return Vector3.Distance(end, point);
			}

			Vector3 projectedPoint = start + tangent * dot;
			projection = dot;
			return Vector3.Distance(projectedPoint, point);
		}

		public void Dispose()
		{
			if(!disposed)
			{
				ListPool<Vector3>.Release(Points);
				ListPool<Vector3>.Release(ForwardNormals);
				ListPool<Vector3>.Release(VerticalNormals);
				Points = null;
				ForwardNormals = null;
				VerticalNormals = null;
				disposed = true;
			}
		}
	}
}
