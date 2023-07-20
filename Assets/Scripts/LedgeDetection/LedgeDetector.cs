using Beatrate.Core;
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace LedgeDetection
{
	public class ClimbingMovement : MonoBehaviour
	{
		private const bool MergePoints = false;

		[SerializeField]
		private CharacterActor characterActor = null;

		[SerializeField]
		private LayerMask detectionLayerMask = default;

		[SerializeField]
		[Min(0.0f)]
		private float forwardCheckBoxWidth = 0.1f;

		[SerializeField]
		[Min(0)]
		private int horizontalForwardCheckBoxCount = 2;

		[SerializeField]
		[Min(0)]
		private int verticalForwardCheckBoxCount = 6;

		[SerializeField]
		[Min(0.0f)]
		private float maxForwardCheckDistance = 2.0f;

		[SerializeField]
		private float forwardCheckStartVerticalOffset = 1.0f;

		[SerializeField]
		private float forwardCheckStartForwardOffset = 0.5f;

		[SerializeField]
		[Min(0.0f)]
		private float maxVerticalCheckDistance = 0.5f;

		[SerializeField]
		[Min(0.0f)]
		private float maxVerticalCheckHeight = 2.0f;

		[SerializeField]
		[Min(1)]
		private int verticalCheckStepCount = 2;

		[SerializeField]
		[Min(0.0f)]
		private float verticalCheckInset = 0.001f;

		[SerializeField]
		[Min(0.0f)]
		private float minForwardNormalAngle = 0.0f;

		[SerializeField]
		[Min(0.0f)]
		private float maxForwardNormalAngle = 0.0f;

		[SerializeField]
		[Min(0.0f)]
		private float maxVerticalNormalAngle = 0.0f;

		[SerializeField]
		[Min(0.0f)]
		private float minAngleBetweenNormals = 60.0f;

		[SerializeField]
		[Min(0.0f)]
		private float minLedgeThickness = 0.1f;

		[SerializeField]
		[Min(0.0f)]
		private float minLedgeHeight = 0.1f;

		[SerializeField]
		[Min(0.0f)]
		private float minLedgeLength = 0.2f;

		[SerializeField]
		[Min(0.0f)]
		private float minLedgeAngle = 45.0f;

		[SerializeField]
		[Min(0.0f)]
		private float maxLedgeAngle = 135.0f;

		[SerializeField]
		[Min(0.0f)]
		private float maxAngleBetweenSegments = 10.0f;

		[SerializeField]
		[Range(0.0f, 90.0f)]
		private float maxAngleRelativeToView = 45.0f;

		[SerializeField]
		[Min(0.0f)]
		private float ledgePointMergeDistance = 0.01f;

		[SerializeField]
		private float ledgePointConnectDistance = 0.1f;

		[SerializeField]
		[Min(0.0f)]
		private float maxClimbHeightAboveLedge = 0.1f;

		[SerializeField]
		[Min(0.0f)]
		private float climbForwardDistance = 0.0f;

		[SerializeField]
		[Min(1)]
		private int climbHeightStepCount = 2;

		[SerializeField]
		[Min(0.0f)]
		private float minClimbHeight = 1.0f;

		[SerializeField]
		[Min(0.0f)]
		private float maxClimbHeight = 2.0f;

		[SerializeField]
		[Min(0.0f)]
		private float maxClimbDistance = 0.2f;

		[SerializeField]
		[Min(0.0f)]
		private float maxClimbAngleRelativeToView = 45.0f;

		[SerializeField]
		[Min(0.0f)]
		private float climbPrepareSpeed = 10.0f;

		private Collider[] colliderBuffer = new Collider[6];
		private List<Ledge> ledges = new List<Ledge>();
		private List<LedgeHit> ledgeHits = new List<LedgeHit>();
		private LedgeHit? ledgeHit = default;

		private void FixedUpdate()
		{
			FindLedges();
		}

		public bool TryGetLedgePoint(out Vector3 point, out Vector3 projectedNormal)
		{
			if(!ledgeHit.HasValue)
			{
				point = Vector3.zero;
				projectedNormal = Vector3.zero;
				return false;
			}

			LedgeHit hit = ledgeHit.Value;
			point = hit.PositionOnLedge;
			projectedNormal = Vector3.ProjectOnPlane(hit.ForwardNormal, Vector3.up).normalized;
			return true;
		}

		private void FindLedges()
		{
			DetectLedges();
			ProcessLedges();

			if(ledgeHit.HasValue)
			{
				ledgeHit.Value.Dispose();
			}
			ledgeHit = default;

			if(ledgeHits.Count != 0)
			{
				ledgeHit = ledgeHits[0].Copy();
			}
		}

		private void DetectLedges()
		{
			bool physicsHitTriggers = Physics.queriesHitTriggers;
			Physics.queriesHitTriggers = false;

			Vector3 characterPosition = transform.position;
			Quaternion characterRotation = transform.rotation;

			Vector3 boxSize = new Vector3(forwardCheckBoxWidth, forwardCheckBoxWidth, forwardCheckBoxWidth);

			var detectionSteps = ListPool<LedgeDetectionStep>.Get();
			var detectionStepIndicesToConsider = ListPool<int>.Get();

			// Fill up empty steps.
			for(int rowIndex = 0; rowIndex < verticalForwardCheckBoxCount; ++rowIndex)
			{
				for(int columnIndex = 0; columnIndex < horizontalForwardCheckBoxCount; ++columnIndex)
				{
					detectionStepIndicesToConsider.Add(detectionSteps.Count);
					detectionSteps.Add(new LedgeDetectionStep
					{
						RowIndex = rowIndex,
						ColumnIndex = columnIndex,
						ForwardRayIndex = -1,
						VerticalRayStartIndex = -1,
						SelectedVerticalRayStepIndex = -1,
						Corner = Vector3.zero,
						ForwardSpaceRayIndex = -1,
						VerticalSpaceRayIndex = -1,
						AllChecksPassed = false
					});
				}
			}


			// Find the forward normal.
			var forwardRayCommands = new NativeList<RaycastCommand>(detectionStepIndicesToConsider.Count, Allocator.TempJob);

			for(int detectionStepIndexIndex = detectionStepIndicesToConsider.Count - 1; detectionStepIndexIndex >= 0; --detectionStepIndexIndex)
			{
				int detectionStepIndex = detectionStepIndicesToConsider[detectionStepIndexIndex];
				LedgeDetectionStep detectionStep = detectionSteps[detectionStepIndex];

				detectionStep.ForwardRayIndex = forwardRayCommands.Length;
				detectionSteps[detectionStepIndex] = detectionStep;

				float rowY = forwardCheckStartVerticalOffset + (detectionStep.RowIndex + 0.5f) * boxSize.y;
				float horizontalPosition = -0.5f * (boxSize.x * horizontalForwardCheckBoxCount) + boxSize.x * (detectionStep.ColumnIndex + 0.5f);
				Vector3 boxCenter = characterPosition + characterRotation * (new Vector3(0.0f, rowY, forwardCheckStartForwardOffset) + Vector3.right * horizontalPosition);
				Vector3 forwardRayCenter = boxCenter;

				var command = new RaycastCommand(forwardRayCenter, characterRotation * Vector3.forward, maxForwardCheckDistance + 0.001f, detectionLayerMask, 1);
				forwardRayCommands.Add(command);
			}

			var forwardRayHits = new NativeArray<RaycastHit>(forwardRayCommands.Length, Allocator.TempJob);
			RaycastCommand.ScheduleBatch(forwardRayCommands, forwardRayHits, 32).Complete();
			forwardRayCommands.Dispose();


			// Find the upper edge.
			var verticalRayCommands = new NativeList<RaycastCommand>(detectionStepIndicesToConsider.Count * verticalCheckStepCount, Allocator.TempJob);

			for(int detectionStepIndexIndex = detectionStepIndicesToConsider.Count - 1; detectionStepIndexIndex >= 0; --detectionStepIndexIndex)
			{
				int detectionStepIndex = detectionStepIndicesToConsider[detectionStepIndexIndex];
				LedgeDetectionStep detectionStep = detectionSteps[detectionStepIndex];

				RaycastHit forwardRayHit = forwardRayHits[detectionStep.ForwardRayIndex];
				if(forwardRayHit.collider == null)
				{
					detectionStepIndicesToConsider.RemoveAt(detectionStepIndexIndex);
				}
				else
				{
					detectionStep.VerticalRayStartIndex = verticalRayCommands.Length;
					detectionSteps[detectionStepIndex] = detectionStep;

					Vector3 projectedNormal = Vector3.ProjectOnPlane(-forwardRayHit.normal, Vector3.up).normalized;

					float rowY = forwardCheckStartVerticalOffset + (detectionStep.RowIndex + 0.5f) * boxSize.y;
					float horizontalPosition = -0.5f * (boxSize.x * horizontalForwardCheckBoxCount) + boxSize.x * (detectionStep.ColumnIndex + 0.5f);
					Vector3 boxCenter = characterPosition + characterRotation * (new Vector3(0.0f, rowY, forwardCheckStartForwardOffset) + Vector3.right * horizontalPosition);
					Vector3 boxTop = boxCenter + Vector3.up * 0.5f * boxSize.y * 2;

					Vector3 baseRayCenter = new Vector3(forwardRayHit.point.x, boxTop.y + 0.001f, forwardRayHit.point.z);

					for(int verticalCheckStepIndex = 0; verticalCheckStepIndex < verticalCheckStepCount; ++verticalCheckStepIndex)
					{
						float t = (float)verticalCheckStepIndex / (verticalCheckStepCount > 1 ? (verticalCheckStepCount - 1) : 1);
						float distance = t * maxVerticalCheckDistance + verticalCheckInset;

						Vector3 rayCenter = baseRayCenter + projectedNormal * distance;
						var command = new RaycastCommand(rayCenter, Vector3.down, maxVerticalCheckHeight, detectionLayerMask, 1);
						verticalRayCommands.Add(command);
					}
				}
			}

			var verticalRayHits = new NativeArray<RaycastHit>(verticalRayCommands.Length, Allocator.TempJob);
			RaycastCommand.ScheduleBatch(verticalRayCommands, verticalRayHits, 32).Complete();
			verticalRayCommands.Dispose();

			// Find possible ledge corner.
			var spaceRayCommands = new NativeList<RaycastCommand>(detectionStepIndicesToConsider.Count * 2, Allocator.TempJob);

			for(int detectionStepIndexIndex = detectionStepIndicesToConsider.Count - 1; detectionStepIndexIndex >= 0; --detectionStepIndexIndex)
			{
				int detectionStepIndex = detectionStepIndicesToConsider[detectionStepIndexIndex];
				LedgeDetectionStep detectionStep = detectionSteps[detectionStepIndex];

				RaycastHit forwardRayHit = forwardRayHits[detectionStep.ForwardRayIndex];
				int successfulVerticalCheckStepIndex = -1;

				for(int verticalCheckStepIndex = 0; verticalCheckStepIndex < verticalCheckStepCount; ++verticalCheckStepIndex)
				{
					RaycastHit verticalRayHit = verticalRayHits[detectionStep.VerticalRayStartIndex + verticalCheckStepIndex];

					float rowY = forwardCheckStartVerticalOffset + (detectionStep.RowIndex + 0.5f) * boxSize.y;
					float horizontalPosition = -0.5f * (boxSize.x * horizontalForwardCheckBoxCount) + boxSize.x * (detectionStep.ColumnIndex + 0.5f);
					Vector3 boxCenter = characterPosition + characterRotation * (new Vector3(0.0f, rowY, forwardCheckStartForwardOffset) + Vector3.right * horizontalPosition);
					Vector3 boxTop = boxCenter + Vector3.up * 0.5f * boxSize.y * 2;

					Vector3 baseRayCenter = new Vector3(forwardRayHit.point.x, boxTop.y + 0.001f, forwardRayHit.point.z);
					float t = (float)verticalCheckStepIndex / (verticalCheckStepCount > 1 ? (verticalCheckStepCount - 1) : 1);
					float distance = t * maxVerticalCheckDistance + verticalCheckInset;

					Vector3 projectedNormal = Vector3.ProjectOnPlane(-forwardRayHit.normal, Vector3.up).normalized;
					Vector3 rayCenter = baseRayCenter + projectedNormal * distance;

					if(verticalRayHit.collider == null)
					{
						break;
					}

					Vector3 alongPlane = Vector3.ProjectOnPlane(forwardRayHit.normal, Vector3.up).normalized;
					Vector3 planeNormal = Vector3.Cross(alongPlane, Vector3.up);

					float angleBetweenNormals = Vector3.Angle(forwardRayHit.normal, verticalRayHit.normal);
					float forwardAngle = Vector3.Angle(Vector3.up, forwardRayHit.normal);
					float verticalAngle = Vector3.Angle(Vector3.up, verticalRayHit.normal);

					if(forwardAngle < minForwardNormalAngle || forwardAngle > maxForwardNormalAngle)
					{
						break;
					}

					if(verticalAngle <= maxVerticalNormalAngle && angleBetweenNormals >= minAngleBetweenNormals)
					{
						successfulVerticalCheckStepIndex = verticalCheckStepIndex;
						break;
					}
				}

				if(successfulVerticalCheckStepIndex == -1)
				{
					detectionStepIndicesToConsider.RemoveAt(detectionStepIndexIndex);
				}
				else
				{
					RaycastHit verticalRayHit = verticalRayHits[detectionStep.VerticalRayStartIndex + successfulVerticalCheckStepIndex];

					Vector3 planeNormal = Vector3.Cross(forwardRayHit.normal, verticalRayHit.normal);
					// Forward plane tangent goes up.
					Vector3 alongForwardPlane = Vector3.Cross(planeNormal, forwardRayHit.normal).normalized;
					// Vertical plane tangent goes forward away from us.
					Vector3 alongVerticalPlane = Vector3.Cross(planeNormal, verticalRayHit.normal).normalized;

					float signedDistanceToPlane = Vector3.Dot(planeNormal, verticalRayHit.point - forwardRayHit.point);
					Vector3 projectedVerticalRayHitPoint = verticalRayHit.point - planeNormal * signedDistanceToPlane;

					float angleBetweenNormals = Vector3.Angle(forwardRayHit.normal, verticalRayHit.normal);
					float forwardAngle = Vector3.Angle(Vector3.up, forwardRayHit.normal);
					float verticalAngle = Vector3.Angle(Vector3.up, verticalRayHit.normal);

					if(MathUtility.LineLineIntersection(out Vector3 intersection, out int sign, out _, forwardRayHit.point, alongForwardPlane, projectedVerticalRayHitPoint, -alongVerticalPlane) && sign == 1)
					{
						Vector3 corner = intersection;
						detectionStep.SelectedVerticalRayStepIndex = successfulVerticalCheckStepIndex;
						detectionStep.Corner = corner;
						detectionStep.ForwardSpaceRayIndex = spaceRayCommands.Length;
						detectionStep.VerticalSpaceRayIndex = spaceRayCommands.Length + 1;
						detectionSteps[detectionStepIndex] = detectionStep;

						Vector3 inflatedCorner = corner + (alongForwardPlane - alongVerticalPlane).normalized * 0.001f;

						var forwardSpaceCommand = new RaycastCommand(inflatedCorner, -alongForwardPlane, minLedgeHeight + 0.001f, detectionLayerMask, 1);
						var verticalSpaceCommand = new RaycastCommand(inflatedCorner, alongVerticalPlane, minLedgeHeight + 0.001f, detectionLayerMask, 1);
						spaceRayCommands.Add(forwardSpaceCommand);
						spaceRayCommands.Add(verticalSpaceCommand);
					}
					else
					{
						detectionStepIndicesToConsider.RemoveAt(detectionStepIndexIndex);
					}
				}
			}

			var spaceRayHits = new NativeArray<RaycastHit>(spaceRayCommands.Length, Allocator.TempJob);
			RaycastCommand.ScheduleBatch(spaceRayCommands, spaceRayHits, 32).Complete();
			spaceRayCommands.Dispose();


			// Check that there is enough space for a small ledge.
			for(int detectionStepIndexIndex = detectionStepIndicesToConsider.Count - 1; detectionStepIndexIndex >= 0; --detectionStepIndexIndex)
			{
				int detectionStepIndex = detectionStepIndicesToConsider[detectionStepIndexIndex];
				LedgeDetectionStep detectionStep = detectionSteps[detectionStepIndex];

				RaycastHit forwardSpaceRay = spaceRayHits[detectionStep.ForwardSpaceRayIndex];
				RaycastHit verticalSpaceRay = spaceRayHits[detectionStep.VerticalSpaceRayIndex];

				if(verticalSpaceRay.collider != null && verticalSpaceRay.distance < minLedgeHeight)
				{
					detectionStepIndicesToConsider.RemoveAt(detectionStepIndexIndex);
				}
				else if(forwardSpaceRay.collider != null && forwardSpaceRay.distance < minLedgeThickness)
				{
					detectionStepIndicesToConsider.RemoveAt(detectionStepIndexIndex);
				}
				else
				{
					detectionStep.AllChecksPassed = true;
					detectionSteps[detectionStepIndex] = detectionStep;
				}
			}


			if(MergePoints)
			{
				// Merge duplicate points.
#pragma warning disable CS0162 // Unreachable code detected
				for(int detectionStepIndexIndex = detectionStepIndicesToConsider.Count - 1; detectionStepIndexIndex >= 0; --detectionStepIndexIndex)
#pragma warning restore CS0162 // Unreachable code detected
				{
					int detectionStepIndex = detectionStepIndicesToConsider[detectionStepIndexIndex];
					LedgeDetectionStep detectionStep = detectionSteps[detectionStepIndex];

					if(detectionStep.AllChecksPassed)
					{
						int mergeDistanceInBoxes = Mathf.Max(1, Mathf.CeilToInt(ledgePointMergeDistance / boxSize.x));
						for(int verticalBoxOffset = -mergeDistanceInBoxes; verticalBoxOffset <= mergeDistanceInBoxes; ++verticalBoxOffset)
						{
							for(int horizontalBoxOffset = -mergeDistanceInBoxes; horizontalBoxOffset <= mergeDistanceInBoxes; ++horizontalBoxOffset)
							{
								if(horizontalBoxOffset == 0 && verticalBoxOffset == 0)
								{
									// Origin box.
									continue;
								}

								int row = detectionStep.RowIndex + verticalBoxOffset;
								int column = detectionStep.ColumnIndex + horizontalBoxOffset;

								if(row < 0 || row > verticalForwardCheckBoxCount - 1 || column < 0 || column > horizontalForwardCheckBoxCount - 1)
								{
									continue;
								}

								int neighbourDetectionStepIndex = row * horizontalForwardCheckBoxCount + column;
								LedgeDetectionStep neighbourDetectionStep = detectionSteps[neighbourDetectionStepIndex];
								if(neighbourDetectionStep.AllChecksPassed)
								{
									if(Vector3.Distance(detectionStep.Corner, neighbourDetectionStep.Corner) <= ledgePointMergeDistance)
									{
										neighbourDetectionStep.AllChecksPassed = false;
										detectionSteps[neighbourDetectionStepIndex] = neighbourDetectionStep;
									}
								}
							}
						}
					}
				}

				for(int detectionStepIndexIndex = detectionStepIndicesToConsider.Count - 1; detectionStepIndexIndex >= 0; --detectionStepIndexIndex)
				{
					int detectionStepIndex = detectionStepIndicesToConsider[detectionStepIndexIndex];
					LedgeDetectionStep detectionStep = detectionSteps[detectionStepIndex];

					if(!detectionStep.AllChecksPassed)
					{
						detectionStepIndicesToConsider.RemoveAt(detectionStepIndexIndex);
					}
				}
			}


			// Connect ledge points.
			var connectionToIndex = ListPool<int>.Get();
			var connectionFromIndex = ListPool<int>.Get();
			for(int i = 0; i < horizontalForwardCheckBoxCount * verticalForwardCheckBoxCount; ++i)
			{
				connectionToIndex.Add(-1);
				connectionFromIndex.Add(-1);
			}

			for(int detectionStepIndexIndex = 0; detectionStepIndexIndex < detectionStepIndicesToConsider.Count; ++detectionStepIndexIndex)
			{
				int detectionStepIndex = detectionStepIndicesToConsider[detectionStepIndexIndex];
				LedgeDetectionStep detectionStep = detectionSteps[detectionStepIndex];

				int connectDistanceInBoxes = Mathf.Max(1, Mathf.CeilToInt(ledgePointConnectDistance / boxSize.x));
				int optimalNeighbourIndex = -1;
				float optimalNeighbourAngle = 0.0f;
				float optimalNeighbourDistance = 0.0f;

				for(int verticalBoxOffset = -connectDistanceInBoxes; verticalBoxOffset <= connectDistanceInBoxes; ++verticalBoxOffset)
				{
					for(int horizontalBoxOffset = 1; horizontalBoxOffset <= connectDistanceInBoxes; ++horizontalBoxOffset)
					{
						if(horizontalBoxOffset == 0 && verticalBoxOffset == 0)
						{
							// Origin box.
							continue;
						}

						int row = detectionStep.RowIndex + verticalBoxOffset;
						int column = detectionStep.ColumnIndex + horizontalBoxOffset;

						if(row < 0 || row > verticalForwardCheckBoxCount - 1 || column < 0 || column > horizontalForwardCheckBoxCount - 1)
						{
							continue;
						}

						int neighbourDetectionStepIndex = row * horizontalForwardCheckBoxCount + column;
						LedgeDetectionStep neighbourDetectionStep = detectionSteps[neighbourDetectionStepIndex];

						if(neighbourDetectionStep.AllChecksPassed && connectionToIndex[neighbourDetectionStepIndex] == -1)
						{
							float neighbourDistance = Vector3.Distance(detectionStep.Corner, neighbourDetectionStep.Corner);

							if(neighbourDistance <= ledgePointConnectDistance)
							{
								Vector3 direction = neighbourDetectionStep.Corner - detectionStep.Corner;
								direction.Normalize();
								float angle = Vector3.Angle(Vector3.up, direction);
								Vector3 projectedDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
								Vector3 projectedNormal = Vector3.Cross(projectedDirection, Vector3.up);

								float angleRelativeToView = Vector3.Angle(characterRotation * Vector3.forward, projectedNormal);

								// Sometimes we get a dumb case where we errouneously connect ledge points right to left instead of left to right.
								// So we apply a hacky fix.
								if(Vector3.Dot(characterRotation * Vector3.right, direction) >= 0.0f && angleRelativeToView <= maxAngleRelativeToView && (angle >= minLedgeAngle && angle <= maxLedgeAngle))
								{
									bool fitsPreviousConnection = true;
									bool fitsNextConnection = true;
									int previousDetectionStepIndex = connectionToIndex[detectionStepIndex];
									int nextDetectionStepIndex = connectionFromIndex[neighbourDetectionStepIndex];

									if(previousDetectionStepIndex != -1)
									{
										Vector3 previousDirection = detectionStep.Corner - detectionSteps[previousDetectionStepIndex].Corner;
										previousDirection.Normalize();
										float angleBetweenSegments = Vector3.Angle(previousDirection, direction);
										fitsPreviousConnection = angleBetweenSegments <= maxAngleBetweenSegments;
									}

									if(nextDetectionStepIndex != -1)
									{
										Vector3 nextDirection = detectionSteps[nextDetectionStepIndex].Corner - neighbourDetectionStep.Corner;
										nextDirection.Normalize();
										float angleBetweenSegments = Vector3.Angle(direction, nextDirection);
										fitsNextConnection = angleBetweenSegments <= maxAngleBetweenSegments;
									}

									if(fitsPreviousConnection && fitsNextConnection)
									{
										if(optimalNeighbourIndex == -1)
										{
											optimalNeighbourIndex = neighbourDetectionStepIndex;
											optimalNeighbourAngle = angle;
											optimalNeighbourDistance = neighbourDistance;
										}
										else
										{
											float angleDifferenceWithHorizontal = Mathf.Abs(90.0f - angle);
											float optimalAngleDifferenceWithHorizontal = Mathf.Abs(90.0f - optimalNeighbourAngle);

											if(neighbourDistance < optimalNeighbourDistance)
											{
												optimalNeighbourIndex = neighbourDetectionStepIndex;
												optimalNeighbourAngle = angle;
												optimalNeighbourDistance = neighbourDistance;
											}
										}
									}
								}
							}
						}
					}
				}

				if(optimalNeighbourIndex != -1)
				{
					connectionFromIndex[detectionStepIndex] = optimalNeighbourIndex;
					connectionToIndex[optimalNeighbourIndex] = detectionStepIndex;
				}
			}


			// Rebuild ledges using the connection graph.
			var visitedDetectionStepIndices = HashSetPool<int>.Get();
			for(int i = 0; i < ledges.Count; ++i)
			{
				ledges[i].Dispose();
			}
			ledges.Clear();

			for(int detectionStepIndexIndex = 0; detectionStepIndexIndex < detectionStepIndicesToConsider.Count; ++detectionStepIndexIndex)
			{
				int detectionStepIndex = detectionStepIndicesToConsider[detectionStepIndexIndex];
				LedgeDetectionStep detectionStep = detectionSteps[detectionStepIndex];

				if(visitedDetectionStepIndices.Contains(detectionStepIndex))
				{
					continue;
				}

				if(connectionToIndex[detectionStepIndex] == -1 && connectionFromIndex[detectionStepIndex] != -1)
				{
					const int MaxIterationCount = 40;
					var ledge = Ledge.Create();

					int iterationDetectionStepIndex = detectionStepIndex;
					while(iterationDetectionStepIndex != -1 && !visitedDetectionStepIndices.Contains(iterationDetectionStepIndex))
					{
						var iterationDetectionStep = detectionSteps[iterationDetectionStepIndex];
						ledge.Points.Add(iterationDetectionStep.Corner);
						ledge.ForwardNormals.Add(forwardRayHits[iterationDetectionStep.ForwardRayIndex].normal);
						ledge.VerticalNormals.Add(verticalRayHits[iterationDetectionStep.VerticalRayStartIndex + iterationDetectionStep.SelectedVerticalRayStepIndex].normal);
						visitedDetectionStepIndices.Add(iterationDetectionStepIndex);
						iterationDetectionStepIndex = connectionFromIndex[iterationDetectionStepIndex];
						if(ledge.Points.Count > MaxIterationCount)
						{
							Debug.LogWarning("Spending too long in reconstructing ledges!");
							break;
						}
					}

					if(ledge.Points.Count < 2 || ledge.GetLength() < minLedgeLength)
					{
						ledge.Dispose();
					}
					else
					{
						for(int pointIndex = ledge.Points.Count - 1; pointIndex >= 0; --pointIndex)
						{
							Vector3 start = ledge.Points[pointIndex];
							if(pointIndex - 1 >= 0)
							{
								Vector3 end = ledge.Points[pointIndex - 1];
								Vector3 direction = end - start;
								direction.Normalize();

								if(pointIndex - 2 >= 0)
								{
									Vector3 next = ledge.Points[pointIndex - 2];
									Vector3 nextDirection = next - end;
									nextDirection.Normalize();

									float angle = Vector3.Angle(direction, nextDirection);
									if(angle < 0.001f)
									{
										ledge.Points[pointIndex - 1] = start;
										ledge.ForwardNormals[pointIndex - 1] = ledge.ForwardNormals[pointIndex];
										ledge.VerticalNormals[pointIndex - 1] = ledge.VerticalNormals[pointIndex];

										ledge.Points.RemoveAt(pointIndex);
										ledge.ForwardNormals.RemoveAt(pointIndex);
										ledge.VerticalNormals.RemoveAt(pointIndex);
									}
								}
							}
						}

						ledges.Add(ledge);
					}
				}
			}

			forwardRayHits.Dispose();
			verticalRayHits.Dispose();
			spaceRayHits.Dispose();

			ListPool<int>.Release(connectionToIndex);
			ListPool<int>.Release(connectionFromIndex);
			HashSetPool<int>.Release(visitedDetectionStepIndices);
			ListPool<LedgeDetectionStep>.Release(detectionSteps);
			ListPool<int>.Release(detectionStepIndicesToConsider);

			Physics.queriesHitTriggers = physicsHitTriggers;
		}

		private void ProcessLedges()
		{
			Vector3 characterPosition = characterActor.Position;
			Quaternion characterRotation = characterActor.Rotation;
			Vector3 referencePoint = characterPosition;
			for(int i = 0; i < ledgeHits.Count; ++i)
			{
				ledgeHits[i].Dispose();
			}
			ledgeHits.Clear();

			for(int ledgeIndex = ledges.Count - 1; ledgeIndex >= 0; --ledgeIndex)
			{
				Ledge ledge = ledges[ledgeIndex];
				float distanceOnLedge = ledge.PointToDistance(referencePoint);
				Vector3 mainLedgePoint = ledge.DistanceToPoint(distanceOnLedge, out Vector3 tangent, out Vector3 forwardNormal, out Vector3 verticalNormal);
				bool hasExactLedgePoint = false;

				for(int ledgePointIndex = 0; ledgePointIndex < ledge.Points.Count - 1; ++ledgePointIndex)
				{
					Vector3 start = ledge.Points[ledgePointIndex];
					Vector3 end = ledge.Points[ledgePointIndex + 1];
					Vector3 direction = (end - start).normalized;
					Vector3 projectedStart = new Vector3(start.x, 0.0f, start.z);
					Vector3 projectedEnd = new Vector3(end.x, 0.0f, end.z);
					Vector3 projectedDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
					Vector3 projectedPlayerPosition = new Vector3(characterPosition.x, 0.0f, characterPosition.z);

					if(MathUtility.LineLineIntersection(out Vector3 intersection, out int sign, out float signedDistance, projectedStart, projectedDirection, projectedPlayerPosition, characterRotation * Vector3.forward) && sign == 1 && signedDistance <= Vector3.Distance(projectedStart, projectedEnd))
					{
						hasExactLedgePoint = true;
						intersection.y = Vector3.Lerp(start, end, signedDistance / Vector3.Distance(start, end)).y;
						mainLedgePoint = intersection;
						tangent = (end - start).normalized;
						forwardNormal = ledge.ForwardNormals[ledgePointIndex];
						verticalNormal = ledge.VerticalNormals[ledgePointIndex];
						break;
					}
				}

				if(!hasExactLedgePoint)
				{
					ledges.RemoveAt(ledgeIndex);
				}
				else
				{
					float forwardDistance = Vector3.Distance(new Vector3(mainLedgePoint.x, 0.0f, mainLedgePoint.z), new Vector3(referencePoint.x, 0.0f, referencePoint.z));
					bool isInVerticalRange = mainLedgePoint.y <= (characterPosition.y + maxClimbHeight) && mainLedgePoint.y >= (characterPosition.y + minClimbHeight);

					if(forwardDistance > maxClimbDistance || !isInVerticalRange)
					{
						ledges.RemoveAt(ledgeIndex);
					}
					else
					{
						Vector3 projectedDirection = Vector3.ProjectOnPlane(tangent, Vector3.up).normalized;
						Vector3 projectedNormal = Vector3.Cross(projectedDirection, Vector3.up).normalized;
						float angleRelativeToView = Vector3.Angle(characterRotation * Vector3.forward, projectedNormal);

						if(angleRelativeToView > maxClimbAngleRelativeToView)
						{
							ledges.RemoveAt(ledgeIndex);
						}
						else
						{
							Vector3 planeNormal = Vector3.Cross(forwardNormal, verticalNormal);
							// Forward plane tangent goes up.
							Vector3 alongForwardPlane = Vector3.Cross(planeNormal, forwardNormal).normalized;
							// Vertical plane tangent goes forward away from us.
							Vector3 alongVerticalPlane = Vector3.Cross(planeNormal, verticalNormal).normalized;
							bool foundUnoccupiedSpace = false;
							Vector3 capsuleCenter = Vector3.zero;
							Vector3 positionRelativeToLedge = Vector3.zero;

							for(int climbHeightStepIndex = 0; climbHeightStepIndex < climbHeightStepCount; ++climbHeightStepIndex)
							{
								float t = (float)climbHeightStepIndex / (climbHeightStepCount > 1 ? (climbHeightStepCount - 1) : 1);
								float height = t * maxClimbHeightAboveLedge + 0.001f;
								positionRelativeToLedge = mainLedgePoint + Vector3.up * height + alongVerticalPlane * climbForwardDistance;
								Vector3 bottomCenter = characterActor.GetBottomCenter(positionRelativeToLedge);
								Vector3 topCenter = characterActor.GetTopCenter(positionRelativeToLedge);
								capsuleCenter = characterActor.GetCenter(positionRelativeToLedge);

								int collisionCount = Physics.OverlapCapsuleNonAlloc(bottomCenter, topCenter, 0.5f * characterActor.BodySize.x, colliderBuffer, detectionLayerMask, QueryTriggerInteraction.Ignore);
								if(collisionCount == 0)
								{
									foundUnoccupiedSpace = true;
									break;
								}
							}

							if(!foundUnoccupiedSpace)
							{
								ledges.RemoveAt(ledgeIndex);
							}
							else
							{
								Vector3 endCharacterPosition = positionRelativeToLedge;
								Vector3 narrowCheckStartPosition = positionRelativeToLedge;
								Vector3 bottomCenter = characterActor.GetBottomCenter(narrowCheckStartPosition);
								Vector3 topCenter = characterActor.GetTopCenter(narrowCheckStartPosition);
								float narrowCheckDistance = Mathf.Min(mainLedgePoint.y + maxClimbHeightAboveLedge, positionRelativeToLedge.y) - mainLedgePoint.y + 0.001f;
								if(Physics.CapsuleCast(bottomCenter, topCenter, 0.5f * characterActor.BodySize.x, Vector3.down, out RaycastHit narrowHit, narrowCheckDistance, detectionLayerMask, QueryTriggerInteraction.Ignore))
								{
									endCharacterPosition = narrowCheckStartPosition + Vector3.down * narrowHit.distance;
								}

								var hit = new LedgeHit
								{
									Ledge = ledge.Copy(),
									DistanceOnLedge = distanceOnLedge,
									PositionOnLedge = mainLedgePoint,
									VerticalNormal = verticalNormal,
									ForwardNormal = forwardNormal,
									EndCharacterPosition = endCharacterPosition
								};
								ledgeHits.Add(hit);
							}
						}
					}
				}
			}

			CollectionUtility.Sort(ledgeHits, new LedgeHitHeightComparer());
		}
	}
}
