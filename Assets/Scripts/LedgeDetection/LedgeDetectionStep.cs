using System.Runtime.InteropServices;
using UnityEngine;

namespace LedgeDetection
{
	[StructLayout(LayoutKind.Auto)]
	public struct LedgeDetectionStep
	{
		public int RowIndex;
		public int ColumnIndex;
		public int ForwardRayIndex;
		public int VerticalRayStartIndex;
		public int SelectedVerticalRayStepIndex;
		public Vector3 Corner;
		public int ForwardSpaceRayIndex;
		public int VerticalSpaceRayIndex;
		public bool AllChecksPassed;
	}
}
