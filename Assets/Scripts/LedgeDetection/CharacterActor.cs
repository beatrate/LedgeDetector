using UnityEngine;

namespace LedgeDetection
{
	public abstract class CharacterActor : MonoBehaviour
	{
		public abstract Vector3 Position { get; set; }
		public abstract Quaternion Rotation { get; set; }
		public abstract Vector2 BodySize { get; }

		public abstract Vector3 GetBottomCenter(Vector3 position);
		public abstract Vector3 GetTopCenter(Vector3 position);
		public abstract Vector3 GetCenter(Vector3 position);
	}
}
