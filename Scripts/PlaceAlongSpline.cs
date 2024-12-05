using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[ExecuteInEditMode]
public class PlaceAlongSpline : MonoBehaviour
{
	[SerializeField] private SplineContainer splineContainer;
	[SerializeField, Range(0f, 1f)] private float relativeDistance = 0f; // Value between 0 and 1
	[SerializeField, Range(-0.1f, 0.1f)] private float sideOffset = 0f; // Value between 0 and 1
	
	void Update()
	{
		if (splineContainer == null) return;
		
		// Get the spline from the container
		Spline spline = splineContainer.Spline;
		
		// Get the position on the spline at the specified relative distance
		Vector3 splinePosition = spline.EvaluatePosition(relativeDistance);
		
		// Get the direction of the spline at the specified relative distance
//		Vector3 splineDirection = spline.EvaluateTangent(relativeDistance);//.normalized;
		Vector3 splineDirection = math.normalize(spline.EvaluateTangent(relativeDistance));
		
		// Calculate the offset perpendicular to the spline's direction
//		Vector3 sideDirection = Vector3.Cross(splineDirection, Vector3.up);//.normalized;
		Vector3 sideDirection = math.normalize(Vector3.Cross(splineDirection, Vector3.up));
		Vector3 offsetPosition = splinePosition + sideDirection * -sideOffset;
		
		// Set the position of this object
		transform.position = offsetPosition;
		
		// Align the object's forward direction with the spline's direction
		transform.rotation = Quaternion.LookRotation(splineDirection);
	}
}
