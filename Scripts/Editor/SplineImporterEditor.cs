using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SplineImporter))]
public class SplineImporterEditor : Editor
{
	public override void OnInspectorGUI()
	{
		// Draw the default inspector
		DrawDefaultInspector();
		
		// Add a button to trigger the import function
		SplineImporter splineImporter = (SplineImporter)target;
		if (GUILayout.Button("Import Spline From CSV"))
		{
			splineImporter.ImportSplineFromCSV();
		}
	}
}
