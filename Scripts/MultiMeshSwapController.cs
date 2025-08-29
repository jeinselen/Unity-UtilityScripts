using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Mesh Swapper/Multi Mesh Swap Controller")]
public class MultiMeshSwapController : MonoBehaviour
{
	[System.Serializable]
	public class MeshSwapTarget
	{
		[Tooltip("The GameObject whose MeshFilter you want to swap meshes on")]
		public GameObject targetObject;
		
		[Tooltip("List of Mesh assets to swap through for this target")]
		public List<Mesh> meshOptions = new List<Mesh>();
	}
	
	[Tooltip("List of targets and their associated mesh options")]
	public List<MeshSwapTarget> targets = new List<MeshSwapTarget>();
	
	/// <summary>
	/// Swap meshes on all target objects to the mesh at the given index in their meshOptions list.
	/// </summary>
	/// <param name="index">Index of the mesh option to swap to</param>
	public void SwapMeshes(int index)
	{
		for (int i = 0; i < targets.Count; i++)
		{
			MeshSwapTarget swapTarget = targets[i];
			
			if (swapTarget.targetObject == null)
			{
				Debug.LogWarning($"[MultiMeshSwapController] Target at index {i} has no GameObject assigned.");
				continue;
			}
			
			MeshFilter mf = swapTarget.targetObject.GetComponent<MeshFilter>();
			if (mf == null)
			{
				Debug.LogWarning($"[MultiMeshSwapController] {swapTarget.targetObject.name} does not have a MeshFilter component.");
				continue;
			}
			
			if (swapTarget.meshOptions == null || swapTarget.meshOptions.Count == 0)
			{
				Debug.LogWarning($"[MultiMeshSwapController] No mesh options set for target {swapTarget.targetObject.name}.");
				continue;
			}
			
			if (index < 0 || index >= swapTarget.meshOptions.Count)
			{
				Debug.LogWarning($"[MultiMeshSwapController] Index {index} out of range for mesh options of {swapTarget.targetObject.name} (0 to {swapTarget.meshOptions.Count - 1}).");
				continue;
			}
			
			mf.mesh = swapTarget.meshOptions[index];
		}
	}
}
