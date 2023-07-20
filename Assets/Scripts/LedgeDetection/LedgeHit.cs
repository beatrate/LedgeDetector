using UnityEngine;

namespace LedgeDetection
{
	public struct LedgeHit
	{
		public Ledge Ledge;
		public float DistanceOnLedge;
		public Vector3 PositionOnLedge;
		public Vector3 VerticalNormal;
		public Vector3 ForwardNormal;
		public Vector3 EndCharacterPosition;

		public LedgeHit Copy()
		{
			var hit = new LedgeHit
			{
				Ledge = this.Ledge.Copy(),
				DistanceOnLedge = this.DistanceOnLedge,
				PositionOnLedge = this.PositionOnLedge,
				VerticalNormal = this.VerticalNormal,
				ForwardNormal = this.ForwardNormal,
				EndCharacterPosition = this.EndCharacterPosition
			};
			return hit;
		}

		public void Dispose()
		{
			Ledge.Dispose();
		}
	}
}
