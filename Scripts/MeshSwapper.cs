using System.Collections.Generic;
using UnityEngine;

namespace MeshSwapping
{
	[System.Serializable]
	public class MeshSwapTarget
	{
		[Header("Target Configuration")]
		public GameObject targetObject;
		
		[Header("Mesh Assets")]
		public List<Mesh> meshes = new List<Mesh>();
		
		[Header("Debug Info (Read Only)")]
		[SerializeField, HideInInspector] 
		private string targetName;
		
		// Update target name for inspector display
		public void UpdateTargetName()
		{
			targetName = targetObject != null ? targetObject.name : "None";
		}
		
		// Validate this target configuration
		public bool IsValid(out string errorMessage)
		{
			errorMessage = "";
			
			if (targetObject == null)
			{
				errorMessage = "Target object is null";
				return false;
			}
			
			MeshFilter meshFilter = targetObject.GetComponent<MeshFilter>();
			if (meshFilter == null)
			{
				errorMessage = $"Target object '{targetObject.name}' does not have a MeshFilter component";
				return false;
			}
			
			if (meshes.Count == 0)
			{
				errorMessage = $"Target object '{targetObject.name}' has no mesh assets assigned";
				return false;
			}
			
			return true;
		}
	}
	
	public class MeshSwapper : MonoBehaviour
	{
		[Header("Mesh Swap Configuration")]
		[SerializeField] private List<MeshSwapTarget> targets = new List<MeshSwapTarget>();
		
		[Header("Current State")]
		[SerializeField, ReadOnly] private int currentMeshIndex = 0;
		
		[Header("Debug Options")]
		[SerializeField] private bool enableDebugLogging = true;
		[SerializeField] private bool validateOnAwake = true;
		
		#region Unity Lifecycle
		
		private void Awake()
		{
			if (validateOnAwake)
			{
				ValidateConfiguration();
			}
		}
		
		private void OnValidate()
		{
			// Update target names for better inspector display
			foreach (var target in targets)
			{
				target?.UpdateTargetName();
			}
		}
		
		#endregion
		
		#region Public Methods
		
		/// <summary>
		/// Swaps all target objects' mesh filters to use the mesh at the specified index
		/// </summary>
		/// <param name="meshIndex">Index of the mesh to swap to (0-based)</param>
		/// <returns>True if swap was successful, false otherwise</returns>
		public bool SwapMeshes(int meshIndex)
		{
			if (!IsValidMeshIndex(meshIndex))
			{
				LogError($"Invalid mesh index: {meshIndex}. Must be between 0 and {GetMinMeshCount() - 1}");
				return false;
			}
			
			bool allSuccessful = true;
			int successCount = 0;
			
			foreach (var target in targets)
			{
				if (SwapSingleTargetMesh(target, meshIndex))
				{
					successCount++;
				}
				else
				{
					allSuccessful = false;
				}
			}
			
			if (allSuccessful)
			{
				currentMeshIndex = meshIndex;
				Log($"Successfully swapped meshes for {successCount}/{targets.Count} targets to index {meshIndex}");
			}
			else
			{
				LogWarning($"Mesh swap partially successful: {successCount}/{targets.Count} targets updated to index {meshIndex}");
			}
			
			return allSuccessful;
		}
		
		/// <summary>
		/// Cycles to the next mesh index (wraps around to 0 when reaching the end)
		/// </summary>
		/// <returns>True if swap was successful, false otherwise</returns>
		public bool SwapToNextMesh()
		{
			int minMeshCount = GetMinMeshCount();
			if (minMeshCount <= 0) return false;
			
			int nextIndex = (currentMeshIndex + 1) % minMeshCount;
			return SwapMeshes(nextIndex);
		}
		
		/// <summary>
		/// Cycles to the previous mesh index (wraps around to max when reaching 0)
		/// </summary>
		/// <returns>True if swap was successful, false otherwise</returns>
		public bool SwapToPreviousMesh()
		{
			int minMeshCount = GetMinMeshCount();
			if (minMeshCount <= 0) return false;
			
			int prevIndex = currentMeshIndex - 1;
			if (prevIndex < 0) prevIndex = minMeshCount - 1;
			return SwapMeshes(prevIndex);
		}
		
		/// <summary>
		/// Gets the current mesh index being used
		/// </summary>
		/// <returns>Current mesh index</returns>
		public int GetCurrentMeshIndex()
		{
			return currentMeshIndex;
		}
		
		/// <summary>
		/// Gets the minimum number of meshes across all targets
		/// </summary>
		/// <returns>Minimum mesh count, or 0 if no valid targets</returns>
		public int GetMinMeshCount()
		{
			if (targets.Count == 0) return 0;
			
			int minCount = int.MaxValue;
			foreach (var target in targets)
			{
				if (target?.meshes != null && target.meshes.Count > 0)
				{
					minCount = Mathf.Min(minCount, target.meshes.Count);
				}
			}
			
			return minCount == int.MaxValue ? 0 : minCount;
		}
		
		/// <summary>
		/// Validates the current configuration and logs any issues
		/// </summary>
		/// <returns>True if configuration is valid, false otherwise</returns>
		public bool ValidateConfiguration()
		{
			bool isValid = true;
			
			if (targets.Count == 0)
			{
				LogWarning("No targets configured in MeshSwapper");
				return false;
			}
			
			for (int i = 0; i < targets.Count; i++)
			{
				var target = targets[i];
				if (target == null)
				{
					LogError($"Target at index {i} is null");
					isValid = false;
					continue;
				}
				
				if (!target.IsValid(out string errorMessage))
				{
					LogError($"Target at index {i}: {errorMessage}");
					isValid = false;
				}
			}
			
			if (isValid)
			{
				Log($"MeshSwapper configuration is valid. {targets.Count} targets with minimum {GetMinMeshCount()} meshes each.");
			}
			
			return isValid;
		}
		
		#endregion
		
		#region Private Methods
		
		private bool SwapSingleTargetMesh(MeshSwapTarget target, int meshIndex)
		{
			if (!target.IsValid(out string errorMessage))
			{
				LogError($"Cannot swap mesh for invalid target: {errorMessage}");
				return false;
			}
			
			if (meshIndex >= target.meshes.Count)
			{
				LogError($"Mesh index {meshIndex} is out of range for target '{target.targetObject.name}' " +
					$"(has {target.meshes.Count} meshes)");
				return false;
			}
			
			Mesh newMesh = target.meshes[meshIndex];
			if (newMesh == null)
			{
				LogError($"Mesh at index {meshIndex} is null for target '{target.targetObject.name}'");
				return false;
			}
			
			MeshFilter meshFilter = target.targetObject.GetComponent<MeshFilter>();
			meshFilter.mesh = newMesh;
			
			return true;
		}
		
		private bool IsValidMeshIndex(int index)
		{
			int minMeshCount = GetMinMeshCount();
			return index >= 0 && index < minMeshCount;
		}
		
		#endregion
		
		#region Logging
		
		private void Log(string message)
		{
			if (enableDebugLogging)
			{
				Debug.Log($"[MeshSwapper] {message}");
			}
		}
		
		private void LogWarning(string message)
		{
			if (enableDebugLogging)
			{
				Debug.LogWarning($"[MeshSwapper] {message}");
			}
		}
		
		private void LogError(string message)
		{
			Debug.LogError($"[MeshSwapper] {message}");
		}
		
		#endregion
		
		#region Editor Helpers
		
		[ContextMenu("Validate Configuration")]
		private void ValidateConfigurationContext()
		{
			ValidateConfiguration();
		}
		
		[ContextMenu("Test Swap to Index 0")]
		private void TestSwapToIndex0()
		{
			SwapMeshes(0);
		}
		
		[ContextMenu("Test Swap to Next Mesh")]
		private void TestSwapToNext()
		{
			SwapToNextMesh();
		}
		
		#endregion
	}
	
	#region Helper Attributes
	
	// Custom attribute for read-only fields in inspector
	public class ReadOnlyAttribute : PropertyAttribute { }
	
	#if UNITY_EDITOR
	[UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
	public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
	{
		public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
		{
			GUI.enabled = false;
			UnityEditor.EditorGUI.PropertyField(position, property, label, true);
			GUI.enabled = true;
		}
	}
	#endif
	
	#endregion
}
