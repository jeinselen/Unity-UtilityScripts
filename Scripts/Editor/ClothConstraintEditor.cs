#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(ClothConstraint))]
public class ClothConstraintEditor : Editor
{
	public override void OnInspectorGUI()
	{
		// Cast the target to the specific type
		ClothConstraint initializer = (ClothConstraint)target;
		
		// Draw the default inspector
		DrawDefaultInspector();
		
		// Add a button to apply constraints
		if (GUILayout.Button("Apply Constraints"))
		{
			// Apply the constraints
			initializer.ApplyConstraints();
			
			// Mark the object and scene as dirty to ensure changes are saved
			EditorUtility.SetDirty(initializer);
			if (!Application.isPlaying)
			{
				EditorSceneManager.MarkSceneDirty(initializer.gameObject.scene);
			}
		}
	}
}
#endif