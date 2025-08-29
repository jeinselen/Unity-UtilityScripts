// File: Assets/Editor/CsvToEmptiesWindow.cs
// Unity 2021+ compatible (tested patterns). Designed for Unity 6 as well.

using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class CsvToEmptiesWindow : EditorWindow
{
	[SerializeField] private TextAsset csvAsset;
	[SerializeField] private string namePrefix = "Empty";
	[SerializeField] private bool appendIndex = true;
	[SerializeField] private bool groupUnderContainer = true;
	[SerializeField] private string containerName = "CSV Empties";
	[SerializeField] private float scale = 1f;
	[SerializeField] private bool autoSkipHeader = true;
	
	private readonly List<Vector3> parsedBlenderCoords = new List<Vector3>();
	private readonly List<string> parseWarnings = new List<string>();
	private Bounds parsedBounds;
	private string csvFileName = "";
	
	// --- Menu entries ---
	[MenuItem("Tools/CSV → Empties (Blender → Unity)")]
	public static void ShowWindow()
	{
		var w = GetWindow<CsvToEmptiesWindow>("CSV → Empties");
		w.minSize = new Vector2(420, 320);
		w.Focus();
	}
	
	// Context menu on CSV assets
	[MenuItem("Assets/Create Empties From CSV…", true)]
	private static bool ValidateCreateFromCsvAsset() => Selection.activeObject is TextAsset;
	
	[MenuItem("Assets/Create Empties From CSV…", false, 19)]
	private static void CreateFromCsvAsset()
	{
		var asset = Selection.activeObject as TextAsset;
		var w = GetWindow<CsvToEmptiesWindow>("CSV → Empties");
		w.csvAsset = asset;
		w.ParseCsv(); // preload parse
		w.Focus();
	}
	
	private void OnGUI()
	{
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("CSV Source", EditorStyles.boldLabel);
		
		using (new EditorGUI.ChangeCheckScope())
		{
			var newCsv = (TextAsset)EditorGUILayout.ObjectField(new GUIContent("CSV (TextAsset)"),
				csvAsset, typeof(TextAsset), false);
			if (newCsv != csvAsset)
			{
				csvAsset = newCsv;
				ParseCsv();
			}
		}
		
		if (csvAsset == null)
		{
			EditorGUILayout.HelpBox(
				"Drop a CSV TextAsset from your Project. Format: x,y,z per line.\n" +
				"Coordinates are assumed to be in Blender space (X right, Y forward, Z up).",
				MessageType.Info);
		}
		else
		{
			if (parsedBlenderCoords.Count > 0)
			{
				EditorGUILayout.HelpBox(
					$"{parsedBlenderCoords.Count:n0} rows parsed from '{csvAsset.name}'.\n" +
					$"Bounds (Blender order X,Y,Z): min {V(parsedBounds.min)} • max {V(parsedBounds.max)}",
					MessageType.None);
			}
			
			if (parseWarnings.Count > 0)
			{
				if (GUILayout.Button($"Show {parseWarnings.Count} parse warnings in Console…"))
				{
					foreach (var w in parseWarnings)
					Debug.LogWarning(w, csvAsset);
				}
			}
		}
		
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Import Options", EditorStyles.boldLabel);
		
		namePrefix = EditorGUILayout.TextField(new GUIContent("Name Prefix"), namePrefix);
		appendIndex = EditorGUILayout.Toggle(new GUIContent("Append Index"), appendIndex);
		groupUnderContainer = EditorGUILayout.Toggle(new GUIContent("Group Under Container"), groupUnderContainer);
		using (new EditorGUI.DisabledScope(!groupUnderContainer))
		{
			containerName = EditorGUILayout.TextField(new GUIContent("Container Name"),
				string.IsNullOrEmpty(containerName) ? "CSV Empties" : containerName);
		}
		
		scale = EditorGUILayout.FloatField(new GUIContent("Scale (units multiplier)"), Mathf.Max(0f, scale));
		autoSkipHeader = EditorGUILayout.Toggle(new GUIContent("Auto-skip header if detected"), autoSkipHeader);
		
		EditorGUILayout.Space();
		
		// Target parent info
		var parent = Selection.activeTransform;
		if (parent == null)
		{
			EditorGUILayout.HelpBox(
				"Select a parent GameObject in the Hierarchy.\n" +
				"New empties will be created as its children with LOCAL positions.",
				MessageType.Warning);
		}
		else
		{
			EditorGUILayout.HelpBox($"Parent: {parent.name}", MessageType.Info);
		}
		
		EditorGUILayout.Space();
		
		// Create button (enabled when everything is ready)
		using (new EditorGUI.DisabledScope(csvAsset == null || parsedBlenderCoords.Count == 0 || parent == null))
		{
			if (GUILayout.Button($"Create {parsedBlenderCoords.Count:n0} Empty GameObjects…", GUILayout.Height(32)))
			{
				var proceed = EditorUtility.DisplayDialog(
					"Confirm Import",
					$"This will create {parsedBlenderCoords.Count:n0} empty GameObjects as children of '{parent.name}'.\n\n" +
					"Coordinate mapping:\n" +
					"  Blender (X, Y, Z) → Unity (X, Y, Z) = (x, z, y)\n\n" +
					"Proceed?",
					"Create", "Cancel");
				
				if (proceed)
				{
					CreateEmpties(parent);
				}
			}
		}
		
		GUILayout.FlexibleSpace();
		
		EditorGUILayout.HelpBox(
			"Tip: For large CSVs, creation is undoable and shows a cancelable progress bar. " +
			"If you cancel midway, use Edit → Undo to revert.",
			MessageType.None);
	}
	
	// --- Parsing ---
	
	private void ParseCsv()
	{
		parsedBlenderCoords.Clear();
		parseWarnings.Clear();
		parsedBounds = default;
		csvFileName = csvAsset ? csvAsset.name : "";
		
		if (csvAsset == null) return;
		
		var text = csvAsset.text;
		if (string.IsNullOrEmpty(text))
		{
			parseWarnings.Add("CSV is empty.");
			return;
		}
		
		var lines = text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
		if (lines.Length == 0)
		{
			parseWarnings.Add("CSV contains no lines.");
			return;
		}
		
		int lineStart = 0;
		if (autoSkipHeader && LooksLikeHeader(lines[0])) lineStart = 1;
		
		var inv = CultureInfo.InvariantCulture;
		var splitter = new Regex("[,;\\t ]+"); // commas, semicolons, tabs, or spaces
		
		for (int i = lineStart; i < lines.Length; i++)
		{
			var raw = lines[i];
			if (string.IsNullOrWhiteSpace(raw)) continue;
			
			var line = raw.Trim();
			if (line.StartsWith("#")) continue; // comment
			
			// strip surrounding quotes if present
			line = line.Trim('\"', '\'');
			
			var tokens = splitter.Split(line);
			if (tokens.Length < 3)
			{
				parseWarnings.Add($"Line {i + 1}: expected 3 numbers, got '{raw}'.");
				continue;
			}
			
			if (!float.TryParse(tokens[0], NumberStyles.Float, inv, out float bx) ||
				!float.TryParse(tokens[1], NumberStyles.Float, inv, out float by) ||
				!float.TryParse(tokens[2], NumberStyles.Float, inv, out float bz))
			{
				parseWarnings.Add($"Line {i + 1}: could not parse as floats: '{raw}'.");
				continue;
			}
			
			var v = new Vector3(bx, by, bz); // Blender order (x,y,z)
			if (parsedBlenderCoords.Count == 0) parsedBounds = new Bounds(v, Vector3.zero);
			else parsedBounds.Encapsulate(v);
			
			parsedBlenderCoords.Add(v);
		}
	}
	
	private static bool LooksLikeHeader(string line)
	{
		// Basic heuristic: mentions x,y,z and doesn't start with a digit
		var trimmed = line.TrimStart();
		if (trimmed.Length == 0) return false;
		char c = trimmed[0];
		if (char.IsDigit(c) || c == '-' || c == '+') return false;
		
		var lower = line.ToLowerInvariant();
		return lower.Contains("x") && lower.Contains("y") && lower.Contains("z");
	}
	
	// --- Creation ---
	
	private void CreateEmpties(Transform parent)
	{
		if (parsedBlenderCoords.Count == 0) return;
		
		// Group undo
		Undo.IncrementCurrentGroup();
		int group = Undo.GetCurrentGroup();
		Undo.SetCurrentGroupName($"Import CSV '{csvFileName}'");
		
		var created = new List<GameObject>();
		Transform actualParent = parent;
		
		if (groupUnderContainer)
		{
			string defaultName = string.IsNullOrEmpty(containerName) ? $"CSV Empties - {csvFileName}" : containerName;
			var containerGO = new GameObject(defaultName);
			Undo.RegisterCreatedObjectUndo(containerGO, "Create Container");
			Undo.SetTransformParent(containerGO.transform, parent, "Parent Container");
			actualParent = containerGO.transform;
			containerGO.transform.localPosition = Vector3.zero;
			containerGO.transform.localRotation = Quaternion.identity;
			containerGO.transform.localScale = Vector3.one;
		}
		
		try
		{
			int total = parsedBlenderCoords.Count;
			for (int i = 0; i < total; i++)
			{
				if (EditorUtility.DisplayCancelableProgressBar("Creating empties",
					$"Creating {i + 1}/{total}", (float)(i + 1) / total))
				{
					Debug.LogWarning($"Creation canceled after {created.Count} objects. " +
						"Use Edit → Undo to revert if needed.");
					break;
				}
				
				// Scale and convert coordinates:
				// Blender (x,y,z) → Unity (x,z,y)
				var b = parsedBlenderCoords[i] * scale;
				var u = new Vector3(b.x, b.z, b.y);
				
				string goName = appendIndex ? $"{namePrefix} {i:00000}" : namePrefix;
				
				var go = new GameObject(goName);
				Undo.RegisterCreatedObjectUndo(go, "Create Empty");
				Undo.SetTransformParent(go.transform, actualParent, "Parent Empty");
				go.transform.localPosition = u;               // local placement
				go.transform.localRotation = Quaternion.identity;
				go.transform.localScale = Vector3.one;
				
				created.Add(go);
			}
		}
		finally
		{
			EditorUtility.ClearProgressBar();
			Undo.CollapseUndoOperations(group);
			
			// Select the container or first created object to make the result obvious
			if (groupUnderContainer && actualParent != parent)
			Selection.activeTransform = actualParent;
			else if (created.Count > 0)
			Selection.activeGameObject = created[0];
		}
	}
	
	// Pretty vector for help box
	private static string V(Vector3 v) => $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###})";
}