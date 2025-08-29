// Place this script anywhere under an Editor folder, e.g. Assets/Editor/CSVEmptiesImporter.cs
// Unity 6 (2023/2024 LTS compatible)
// Creates an EditorWindow under Tools → CSV → Import Empties From Blender
// - Select a CSV TextAsset from your project
// - Optionally toggle header handling and delimiter
// - Click Analyze to preview how many empties will be created
// - Click Create Empties to instantiate children under the currently selected object
// Coordinates are interpreted as Blender (X right, Y forward, Z up) and converted to Unity (X right, Y up, Z forward): (x, y, z)_blender → (x, z, y)_unity.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class CSVEmptiesImporter : EditorWindow
{
	private TextAsset csvFile;
	
	private enum Delim { Auto, Comma, Semicolon, Tab, Space }
	private Delim delimiterMode = Delim.Auto;
	private char resolvedDelimiter = ','; // set during analysis
	
	private bool hasHeader = false;
	private bool trimWhitespace = true;
	private bool ignoreBlankLines = true;
	
	private string namePrefix = "Empty_";
	private float uniformScale = 1f; // optional scaler applied to all coordinates
	
	private int analyzedCount = 0;
	private List<Vector3> analyzedPoints = new List<Vector3>();
	private string analysisMessage = "";
	
	[MenuItem("Tools/CSV/Import Empties From Blender")] 
	public static void ShowWindow()
	{
		var win = GetWindow<CSVEmptiesImporter>(true, "CSV → Empties (Blender → Unity)");
		win.minSize = new Vector2(460, 360);
	}
	
	private void OnGUI()
	{
		EditorGUILayout.LabelField("Source CSV", EditorStyles.boldLabel);
		csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV TextAsset", csvFile, typeof(TextAsset), false);
		
		EditorGUILayout.Space(4);
		EditorGUILayout.LabelField("CSV Options", EditorStyles.boldLabel);
		delimiterMode = (Delim)EditorGUILayout.EnumPopup("Delimiter", delimiterMode);
		hasHeader = EditorGUILayout.Toggle(new GUIContent("Has Header Row", "Skip the first row if it contains column names (e.g., X,Y,Z)."), hasHeader);
		trimWhitespace = EditorGUILayout.Toggle(new GUIContent("Trim Whitespace", "Trim spaces around values."), trimWhitespace);
		ignoreBlankLines = EditorGUILayout.Toggle(new GUIContent("Ignore Blank Lines", "Skip any empty rows."), ignoreBlankLines);
		
		EditorGUILayout.Space(4);
		EditorGUILayout.LabelField("Import Settings", EditorStyles.boldLabel);
		using (new EditorGUI.DisabledScope(true))
		{
			var sel = Selection.activeTransform ? Selection.activeTransform.name : "<none>";
			EditorGUILayout.TextField("Parent (Selected)", sel);
		}
		namePrefix = EditorGUILayout.TextField(new GUIContent("Name Prefix", "Each created GameObject will be named Prefix + index"), namePrefix);
		uniformScale = EditorGUILayout.FloatField(new GUIContent("Uniform Scale", "Optional multiplier applied to all coordinates after axis conversion."), Mathf.Max(0f, uniformScale));
		
		EditorGUILayout.Space(6);
		using (new EditorGUI.DisabledScope(csvFile == null))
		{
			if (GUILayout.Button("Analyze CSV"))
			{
				AnalyzeCSV();
			}
		}
		
		using (new EditorGUI.DisabledScope(analyzedCount == 0))
		{
			EditorGUILayout.HelpBox(analysisMessage, analyzedCount > 0 ? MessageType.Warning : MessageType.Info);
		}
		
		using (new EditorGUI.DisabledScope(analyzedCount == 0))
		{
			if (GUILayout.Button($"Create {analyzedCount} Empties"))
			{
				CreateEmpties();
			}
		}
		
		EditorGUILayout.Space(8);
		EditorGUILayout.HelpBox("Coordinates are treated as LOCAL: each empty is created as a child of the currently selected object (or at scene root if none is selected). Blender (X,Y,Z) → Unity (X,Z,Y).", MessageType.Info);
	}
	
	private void AnalyzeCSV()
	{
		analyzedPoints.Clear();
		analyzedCount = 0;
		analysisMessage = "";
		
		if (csvFile == null)
		{
			analysisMessage = "Select a CSV TextAsset from the Project.";
			return;
		}
		
		try
		{
			string text = csvFile.text;
			var lines = new List<string>();
			using (var reader = new StringReader(text))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					if (trimWhitespace) line = line.Trim();
					if (ignoreBlankLines && string.IsNullOrEmpty(line)) continue;
					lines.Add(line);
				}
			}
			
			if (lines.Count == 0)
			{
				analysisMessage = "CSV appears empty after applying filters.";
				return;
			}
			
			int startIndex = hasHeader ? 1 : 0;
			if (startIndex >= lines.Count)
			{
				analysisMessage = "CSV only contains a header row; no data lines found.";
				return;
			}
			
			// Resolve delimiter based on mode / sample line
			resolvedDelimiter = ResolveDelimiter(lines[startIndex]);
			
			var ci = CultureInfo.InvariantCulture;
			int badRows = 0;
			for (int i = startIndex; i < lines.Count; i++)
			{
				var row = lines[i];
				var parts = SplitRow(row, resolvedDelimiter);
				if (parts.Length < 3)
				{
					badRows++;
					continue;
				}
				
				if (trimWhitespace)
				{
					for (int p = 0; p < parts.Length; p++) parts[p] = parts[p].Trim();
				}
				
				if (TryParse3(parts, out float bx, out float by, out float bz, ci))
				{
					// Blender (X right, Y fwd, Z up) → Unity (X right, Y up, Z fwd)
					Vector3 unityLocal = new Vector3(bx, bz, by) * uniformScale;
					analyzedPoints.Add(unityLocal);
				}
				else
				{
					badRows++;
				}
			}
			
			analyzedCount = analyzedPoints.Count;
			
			if (analyzedCount == 0)
			{
				analysisMessage = "No valid rows parsed. Check delimiter, header setting, and numeric formatting (expects dot decimal).";
			}
			else
			{
				string warn = analyzedCount >= 10000 ? " LARGE import (10k+)." : string.Empty;
				analysisMessage = $"Will create {analyzedCount} empty GameObjects.{warn}\nInvalid/Skipped rows: {badRows}. Delimiter: '{PrintableDelimiter(resolvedDelimiter)}'.";
			}
		}
		catch (Exception ex)
		{
			analysisMessage = "Error while analyzing: " + ex.Message;
		}
	}
	
	private void CreateEmpties()
	{
		if (analyzedCount == 0)
		{
			EditorUtility.DisplayDialog("CSV → Empties", "Nothing to create. Run Analyze first.", "OK");
			return;
		}
		
		string parentName = Selection.activeTransform ? Selection.activeTransform.name : "<scene root>";
		bool proceed = EditorUtility.DisplayDialog(
			"Confirm Creation",
			$"About to create {analyzedCount} empty GameObjects as children of '{parentName}'.\n\nThis may take a moment and cannot be undone as a single step (but each object is registered with Undo). Proceed?",
			"Create",
			"Cancel");
		if (!proceed) return;
		
		Transform parent = Selection.activeTransform;
		Undo.IncrementCurrentGroup();
		int undoGroup = Undo.GetCurrentGroup();
		
		for (int i = 0; i < analyzedPoints.Count; i++)
		{
			var go = new GameObject($"{namePrefix}{i:0000}");
			Undo.RegisterCreatedObjectUndo(go, "Create Empty From CSV");
			if (parent != null)
			{
				Undo.SetTransformParent(go.transform, parent, "Parent Empty From CSV");
				go.transform.localPosition = analyzedPoints[i];
			}
			else
			{
				go.transform.position = analyzedPoints[i]; // at root, local == world
			}
		}
		
		Undo.CollapseUndoOperations(undoGroup);
		
		// Ping the last created object for convenience
		if (Selection.activeTransform != null)
		{
			EditorGUIUtility.PingObject(Selection.activeTransform);
		}
		
		EditorUtility.DisplayDialog("CSV → Empties", $"Created {analyzedCount} empties.", "OK");
	}
	
	private char ResolveDelimiter(string sample)
	{
		if (delimiterMode != Delim.Auto)
		{
			return delimiterMode switch
			{
				Delim.Comma => ',',
				Delim.Semicolon => ';',
				Delim.Tab => '\t',
				Delim.Space => ' ',
				_ => ','
			};
		}
		
		// Auto-detect by counting candidates in sample line
		var candidates = new Dictionary<char, int>
		{
			{ ',', sample.Count(c => c == ',') },
			{ ';', sample.Count(c => c == ';') },
			{ '\t', sample.Count(c => c == '\t') },
			{ ' ', CountSpaces(sample) }
		};
		
		// Choose the delimiter with the highest count (must be at least 2 delimiters → 3 fields)
		var best = candidates.OrderByDescending(kv => kv.Value).First();
		return best.Value >= 2 ? best.Key : ',';
	}
	
	private static int CountSpaces(string s)
	{
		int count = 0;
		for (int i = 0; i < s.Length; i++) if (s[i] == ' ') count++;
		return count;
	}
	
	private static string PrintableDelimiter(char d)
	{
		return d switch
		{
			'\t' => "TAB",
			' ' => "SPACE",
			_ => d.ToString()
		};
	}
	
	private static string[] SplitRow(string row, char delim)
	{
		// Simple split; if you need quoted CSV with embedded delimiters, swap in a CSV parser
		return row.Split(new[] { delim }, StringSplitOptions.None);
	}
	
	private static bool TryParse3(string[] parts, out float x, out float y, out float z, IFormatProvider fmt)
	{
		x = y = z = 0f;
		// If header was present, AnalyzeCSV already skipped it. Here we just parse the first 3 numeric-ish fields.
		int found = 0;
		for (int i = 0; i < parts.Length && found < 3; i++)
		{
			if (float.TryParse(parts[i], NumberStyles.Float | NumberStyles.AllowThousands, fmt, out float v))
			{
				if (found == 0) x = v; else if (found == 1) y = v; else z = v;
				found++;
			}
		}
		return found == 3;
	}
}
