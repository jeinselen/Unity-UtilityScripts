using UnityEngine;
using UnityEngine.Splines;
//using Unity.Mathematics;

[ExecuteInEditMode]
public class SplinePlacement : MonoBehaviour
{
	[SerializeField] private SplineContainer splineContainer;
	[SerializeField, Range( 0.0f, 1.0f)] private float relativeDistance = 0.0f; // Value between 0 and 1
	[SerializeField, Range(-0.1f, 0.1f)] private float offsetX = 0.0f; // Value between 0 and 1
	[SerializeField, Range(-0.1f, 0.1f)] private float offsetY = 0.0f; // Value between 0 and 1
	
	void Update()
	{
		if (splineContainer == null) return;
		
		// Get the position on the spline at the specified relative distance
		Vector3 splinePosition = splineContainer.EvaluatePosition(relativeDistance);
		
		// Get the direction of the spline at the specified relative distance
		// The min/max clamping is required to prevent NaN errors from the 0,0,0 vector Unity retuns at 0.0f and 1.0f
		Vector3 splineDirection = splineContainer.EvaluateTangent(Mathf.Min(Mathf.Max(relativeDistance, 0.0000001f), 0.9999999f));
//		Vector3 splineDirection = Vector3.Normalize(splineContainer.EvaluateTangent(Mathf.Min(Mathf.Max(relativeDistance, 0.0000001f), 0.9999999f)));
		
		// Calculate the offset perpendicular to the spline's direction
//		Vector3 directionX = Vector3.Cross(splineDirection, Vector3.down) * offsetX;
		Vector3 directionX = Vector3.Normalize(Vector3.Cross(splineDirection, Vector3.down)) * offsetX;
//		Vector3 directionY = Vector3.Cross(splineDirection, Vector3.right) * offsetY;
		Vector3 directionY = Vector3.Normalize(Vector3.Cross(splineDirection, Vector3.left)) * offsetY;
		Vector3 offsetPosition = splinePosition + directionX + directionY;
		
		// Set the position of this object
		transform.position = offsetPosition;
		
		// Align the object's forward direction with the spline's direction
		transform.rotation = Quaternion.LookRotation(splineDirection);
	}
}
