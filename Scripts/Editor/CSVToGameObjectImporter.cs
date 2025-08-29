using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

public class CSVToGameObjectImporter : EditorWindow
{
	private string csvFilePath = "";
	private Object csvFile;
	private int coordinateCount = 0;
	private bool showPreview = false;
	private Vector2 scrollPosition;
	private List<Vector3> previewCoordinates = new List<Vector3>();
	
	[MenuItem("Tools/CSV to GameObject Importer")]
	public static void ShowWindow()
	{
		GetWindow<CSVToGameObjectImporter>("CSV Importer");
	}
	
	void OnGUI()
	{
		GUILayout.Label("CSV to GameObject Importer", EditorStyles.boldLabel);
		GUILayout.Space(10);
		
		// File selection
		GUILayout.Label("Select CSV File:", EditorStyles.label);
		csvFile = EditorGUILayout.ObjectField("CSV File", csvFile, typeof(TextAsset), false);
		
		if (csvFile != null)
		{
			csvFilePath = AssetDatabase.GetAssetPath(csvFile);
			
			// Preview button
			if (GUILayout.Button("Preview Coordinates"))
			{
				PreviewCSV();
			}
			
			if (showPreview && coordinateCount > 0)
			{
				GUILayout.Space(10);
				
				// Warning about number of objects
				EditorGUILayout.HelpBox($"This will create {coordinateCount} empty GameObjects as children of the selected object.", 
					coordinateCount > 1000 ? MessageType.Warning : MessageType.Info);
				
				// Show selected parent object
				Transform selectedTransform = Selection.activeTransform;
				string parentName = selectedTransform != null ? selectedTransform.name : "Scene Root";
				GUILayout.Label($"Parent Object: {parentName}", EditorStyles.helpBox);
				
				GUILayout.Space(5);
				
				// Preview coordinates (limited display)
				GUILayout.Label("Coordinate Preview (Unity coordinates):", EditorStyles.boldLabel);
				scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
				
				int displayLimit = Mathf.Min(previewCoordinates.Count, 20);
				for (int i = 0; i < displayLimit; i++)
				{
					Vector3 coord = previewCoordinates[i];
					GUILayout.Label($"{i + 1}: ({coord.x:F3}, {coord.y:F3}, {coord.z:F3})");
				}
				
				if (previewCoordinates.Count > displayLimit)
				{
					GUILayout.Label($"... and {previewCoordinates.Count - displayLimit} more coordinates");
				}
				
				EditorGUILayout.EndScrollView();
				
				GUILayout.Space(10);
				
				// Create button
				GUI.backgroundColor = coordinateCount > 1000 ? Color.yellow : Color.green;
				if (GUILayout.Button($"Create {coordinateCount} Empty GameObjects", GUILayout.Height(30)))
				{
					CreateGameObjects();
				}
				GUI.backgroundColor = Color.white;
			}
		}
		else
		{
			EditorGUILayout.HelpBox("Please select a CSV file from your project assets.", MessageType.Info);
		}
		
		GUILayout.Space(20);
		GUILayout.Label("Instructions:", EditorStyles.boldLabel);
		GUILayout.Label("1. Place your CSV file in the project", EditorStyles.wordWrappedLabel);
		GUILayout.Label("2. Select the CSV file above", EditorStyles.wordWrappedLabel);
		GUILayout.Label("3. Click 'Preview Coordinates' to see what will be created", EditorStyles.wordWrappedLabel);
		GUILayout.Label("4. Select a parent object in the hierarchy (optional)", EditorStyles.wordWrappedLabel);
		GUILayout.Label("5. Click 'Create Empty GameObjects' to import", EditorStyles.wordWrappedLabel);
		
		GUILayout.Space(10);
		EditorGUILayout.HelpBox("Coordinates are converted from Blender (X right, Y forward, Z up) to Unity (X right, Y up, Z forward)", MessageType.Info);
	}
	
	void PreviewCSV()
	{
		if (csvFile == null) return;
		
		previewCoordinates.Clear();
		coordinateCount = 0;
		
		try
		{
			string csvContent = ((TextAsset)csvFile).text;
			string[] lines = csvContent.Split('\n');
			
			foreach (string line in lines)
			{
				if (string.IsNullOrWhiteSpace(line)) continue;
				
				string[] values = line.Split(',');
				if (values.Length >= 3)
				{
					if (float.TryParse(values[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
						float.TryParse(values[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
						float.TryParse(values[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
					{
						// Convert from Blender coordinates (X right, Y forward, Z up) 
						// to Unity coordinates (X right, Y up, Z forward)
						Vector3 unityCoord = new Vector3(x, z, y);
						previewCoordinates.Add(unityCoord);
						coordinateCount++;
					}
				}
			}
			
			showPreview = true;
			
			if (coordinateCount == 0)
			{
				EditorUtility.DisplayDialog("Error", "No valid coordinates found in the CSV file. Make sure it contains numeric X, Y, Z values separated by commas.", "OK");
			}
		}
		catch (System.Exception e)
		{
			EditorUtility.DisplayDialog("Error", $"Failed to read CSV file: {e.Message}", "OK");
			showPreview = false;
		}
	}
	
	void CreateGameObjects()
	{
		if (previewCoordinates.Count == 0) return;
		
		// Confirm creation for large numbers
		if (coordinateCount > 500)
		{
			if (!EditorUtility.DisplayDialog("Confirm Creation", 
				$"You are about to create {coordinateCount} GameObjects. This might take a while and could impact performance. Continue?", 
				"Yes", "Cancel"))
			{
				return;
			}
		}
		
		Transform parentTransform = Selection.activeTransform;
		
		// Create a parent container
		GameObject container = new GameObject("CSV_Imported_Objects");
		if (parentTransform != null)
		{
			container.transform.SetParent(parentTransform);
		}
		
		// Register undo
		Undo.RegisterCreatedObjectUndo(container, "Import CSV GameObjects");
		
		// Create progress bar for large imports
		bool showProgress = coordinateCount > 100;
		
		try
		{
			for (int i = 0; i < previewCoordinates.Count; i++)
			{
				if (showProgress && i % 50 == 0)
				{
					EditorUtility.DisplayProgressBar("Creating GameObjects", 
						$"Creating object {i + 1} of {coordinateCount}", 
						(float)i / coordinateCount);
				}
				
				GameObject emptyObj = new GameObject($"Point_{i + 1:D4}");
				emptyObj.transform.SetParent(container.transform);
				emptyObj.transform.localPosition = previewCoordinates[i];
				
				// Register each object for undo
				Undo.RegisterCreatedObjectUndo(emptyObj, "Import CSV GameObjects");
			}
		}
		finally
		{
			if (showProgress)
			{
				EditorUtility.ClearProgressBar();
			}
		}
		
		// Select the container
		Selection.activeGameObject = container;
		
		Debug.Log($"Successfully created {coordinateCount} GameObjects from CSV file.");
		EditorUtility.DisplayDialog("Success", $"Created {coordinateCount} empty GameObjects!", "OK");
	}
}