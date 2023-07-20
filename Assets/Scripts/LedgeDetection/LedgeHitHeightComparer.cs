using System.Collections.Generic;

namespace LedgeDetection
{
	public readonly struct LedgeHitHeightComparer : IComparer<LedgeHit>
	{
		public int Compare(LedgeHit x, LedgeHit y)
		{
			return y.PositionOnLedge.y.CompareTo(x.PositionOnLedge.y);
		}
	}
}
