using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

//[ExecuteInEditMode]
public class SplineAnimate : MonoBehaviour
{
	[SerializeField] private SplineContainer splineContainer;
	[SerializeField] private bool loop = false; // Toggles between looping and non-looping
	[SerializeField, Range( 0.0f, 1.0f)] private float speed = 0.0f; // Value between 0 and 1
	[SerializeField, Range(-0.1f, 0.1f)] private float offsetX = 0.0f; // Value between 0 and 1
	[SerializeField, Range(-0.1f, 0.1f)] private float offsetY = 0.0f; // Value between 0 and 1
	[SerializeField, Range( 0.0f, 1.0f)] private float offsetZ = 0.0f; // Value between 0 and 1
	[SerializeField, Range( 0.0f, 1.0f)] private float positionFollow = 0.1f; // Value between 0 and 1
	[SerializeField, Range( 0.0f, 1.0f)] private float rotationFollow = 0.01f; // Value between 0 and 1
	private float relativeDistance = 0.0f;
	
	void Start()
	{
		relativeDistance = offsetZ;
		
//		Debug.Log("relativeDistance: "+relativeDistance);
	}
	
	void Update()
	{
		if (splineContainer == null || speed == 0.0f) return;
		
		// Incrememnt the relative distance
		relativeDistance += Time.deltaTime * speed;
		if (loop && relativeDistance >= 1.0f)
		{
			relativeDistance = 0.0f;
		}
		
//		Debug.Log("relativeDistance over time: "+relativeDistance);
		
		// Get the position on the spline at the specified relative distance
		Vector3 splinePosition = splineContainer.EvaluatePosition(relativeDistance);
		
		// Get the direction of the spline at the specified relative distance
		// The min/max clamping is required to prevent NaN errors from the 0,0,0 vector Unity retuns at 0.0f and 1.0f
//		Vector3 splineDirection = splineContainer.EvaluateTangent(relativeDistance);
		Vector3 splineDirection = splineContainer.EvaluateTangent(Mathf.Min(Mathf.Max(relativeDistance, 0.0000001f), 0.9999999f));
//		Vector3 splineDirection = Vector3.Normalize(splineContainer.EvaluateTangent(Mathf.Min(Mathf.Max(relativeDistance, 0.0000001f), 0.9999999f)));
		
		// Calculate the offset perpendicular to the spline's direction
//		Vector3 directionX = Vector3.Cross(splineDirection, Vector3.down) * offsetX;
		Vector3 directionX = Vector3.Normalize(Vector3.Cross(splineDirection, Vector3.down)) * offsetX;
//		Vector3 directionY = Vector3.Cross(splineDirection, Vector3.right) * offsetY;
		Vector3 directionY = Vector3.Normalize(Vector3.Cross(splineDirection, Vector3.left)) * offsetY;
		Vector3 offsetPosition = splinePosition + directionX + directionY;
		
		// Set the position of this object
		transform.position = Vector3.Lerp(transform.position, offsetPosition, positionFollow);
		
		// Align the object's forward direction with the spline's direction
		transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(splineDirection), rotationFollow);
	}
}
