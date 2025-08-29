using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways] // Ensures the script works in both play mode and edit mode.
[RequireComponent(typeof(Volume))] // Ensures the object has a Volume component
public class AutofocusDistance : MonoBehaviour
{
	public GameObject camera; // First object
	public GameObject target; // Second object
	
	private Volume globalVolume; // Reference to the global volume
	private DepthOfField depthOfField;
	
	void Start()
	{
		// Automatically get the Volume component on the current object
		globalVolume = GetComponent<Volume>();
		
		// Check if the volume has a Depth of Field override
		if (globalVolume.profile.TryGet(out depthOfField))
		{
			// Set the focus distance to the initial distance between the two objects
			UpdateFocusDistance();
		}
	}
	
	void Update()
	{
		// Continuously update the focus distance
		UpdateFocusDistance();
	}
	
	void UpdateFocusDistance()
	{
		if (depthOfField != null && camera != null && target != null)
		{
			// Calculate the distance between the two objects
			float distance = Vector3.Distance(camera.transform.position, target.transform.position);
			
			// Set the focus distance to the calculated distance
			depthOfField.focusDistance.value = distance;
		}
	}
}
