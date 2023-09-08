using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using System; // this was added indiscriminately because it adds the array reverse feature and I couldn't easily find a list of system elements so I could do this more efficiently

// REQUIRES InspectorButton.cs
// https://www.reddit.com/r/Unity3D/comments/1s6czv/inspectorbutton_add_a_custom_button_to_your/

[ExecuteInEditMode]
public class LineCSV : MonoBehaviour
{
	private LineRenderer LineRend;
	// public string FileName = "DFI_SP_1.csv";
	public Vector3 Scale = new Vector3(0.01f, -0.01f, 0.01f);
	public Vector3 Offset = new Vector3(0.0f, 1.0f, 0.0f);
	public bool ReverseOrder;
	[InspectorButton("OnButtonClicked")]
	public bool LoadFile;

	private void OnButtonClicked()
	{
		// LineRend = GetComponent<LineRenderer>();
		LineRend = (GetComponent<LineRenderer>())? GetComponent<LineRenderer>(): gameObject.AddComponent<LineRenderer>();
		LoadCSV();
	}

	private void LoadCSV()
	{
		// string CSVPath = Application.streamingAssetsPath + "/" + FileName;
		string CSVPath = EditorUtility.OpenFilePanel("choose CSV file", "", "csv");
		string fileContent = "";
		StreamReader reader = new StreamReader(CSVPath);
		fileContent = reader.ReadToEnd();
		ParseCSV(fileContent);
	}

	private void ParseCSV(string fileCont)
	{
		string[] lines = fileCont.Split('\n');
		lines = lines.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
		if (ReverseOrder) { Array.Reverse( lines ); }

		List<Vector3> points = new List<Vector3>();
		foreach (var line in lines)
		{
			var parts = SplitCsvLine(line);

			// Debug.Log(parts+" — "+parts[0]);
			// Debug.Log(parts.Length);

			float x = float.TryParse(parts[0], out x) ? x*Scale.x+Offset.x : 0;
			float y = float.TryParse(parts[1], out y) ? y*Scale.y+Offset.y : 0;
			float z = 0;
			if (parts.Length >= 3) {
				z = float.TryParse(parts[2], out z) ? z*Scale.z+Offset.z : 0;
			}

			points.Add(new Vector3(x, y, z));
		}

		Vector3[] pointsArray = points.ToArray();
		SetLineValues(pointsArray);
	}

	private void SetLineValues(Vector3[] linePoints)
	{
		Debug.Log("Line Renderer updated with "+linePoints.Length+" points loaded from CSV file");
		LineRend.positionCount = linePoints.Length;
		LineRend.SetPositions(linePoints);
	}

	private static string[] SplitCsvLine(string line)
	{
		return (from System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(line,
			@"(((?<x>(?=[,\r\n]+))|""(?<x>([^""]|"""")+)""|(?<x>[^,\r\n]+)),?)",
			System.Text.RegularExpressions.RegexOptions.ExplicitCapture)
				select m.Groups[1].Value).ToArray();
	}
}
