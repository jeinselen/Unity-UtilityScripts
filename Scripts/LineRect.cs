using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// REQUIRES InspectorButton.cs
// https://www.reddit.com/r/Unity3D/comments/1s6czv/inspectorbutton_add_a_custom_button_to_your/

[ExecuteInEditMode]
public class LineRect: MonoBehaviour
{
	private LineRenderer LineRend;
	public Vector2 Size = new Vector2(1.0f, 1.0f);
	public float TopGap = 0.2f;
	public float LineSafety = 0.1f; // Helps prevent jumbling at glancing angles
	public float CornerRadius = 0.1f;
	public int CornerResolution = 8;
	[InspectorButton("OnButtonClicked")]
	public bool Create;

	private void OnButtonClicked()
	{
		// LineRend = GetComponent<LineRenderer>();
		LineRend = (GetComponent<LineRenderer>())? GetComponent<LineRenderer>(): gameObject.AddComponent<LineRenderer>();
		CalculateLine();
	}

	private void CalculateLine()
	{
		// Create corner divisions and Sine-Cosine vectors
		float increment = (90.0f / (float)CornerResolution) * Mathf.Deg2Rad;
		// increment = (1.0f / (float)CornerResolution);
		// 90° in radians: 1.5707963267949
		// increment = 0.5f * Mathf.PI / (float)CornerResolution;
		// increment = 0.5235988f;
		increment = 1.5707963267949f / (float)CornerResolution;
		List<Vector3> corner = new List<Vector3>();
		for (var i = 0; i <= CornerResolution; i++) {
			if (i == 0) {
				corner.Add(new Vector3(0.0f, CornerRadius, 0.0f));
			} else if (i < CornerResolution) {
				corner.Add(new Vector3(Mathf.Sin((float)i * increment) * CornerRadius, Mathf.Cos((float)i * increment) * CornerRadius, 0.0f));
			} else {
				corner.Add(new Vector3(CornerRadius, 0.0f, 0.0f));
			}
		}

		// Start compiling the list of points
		List<Vector3> points = new List<Vector3>();

		// Starting point
		points.Add(new Vector3(0.0f+TopGap*0.5f, Size.y*0.5f, 0.0f));

		// Top-right corner
		// points.Add(new Vector3(Size.x*0.5f, Size.y*0.5f, 0.0f));
		if (LineSafety > 0.0f) { points.Add(new Vector3(Size.x*0.5f - CornerRadius - LineSafety, Size.y*0.5f, 0.0f)); }
		foreach (var segment in corner) {
			points.Add(new Vector3(Size.x*0.5f - CornerRadius + segment.x, Size.y*0.5f - CornerRadius + segment.y, 0.0f));
		}
		if (LineSafety > 0.0f) { points.Add(new Vector3(Size.x*0.5f, Size.y*0.5f - CornerRadius - LineSafety, 0.0f)); }

		// Bottom-right corner
		// points.Add(new Vector3(Size.x*0.5f, -Size.y*0.5f, 0.0f));
		if (LineSafety > 0.0f) { points.Add(new Vector3(Size.x*0.5f, -Size.y*0.5f + CornerRadius + LineSafety, 0.0f)); }
		foreach (var segment in corner) {
			points.Add(new Vector3(Size.x*0.5f - CornerRadius + segment.y, Size.y*-0.5f + CornerRadius - segment.x, 0.0f));
		}
		if (LineSafety > 0.0f) { points.Add(new Vector3(Size.x*0.5f - CornerRadius - LineSafety, -Size.y*0.5f, 0.0f)); }

		// Bottom-left corner
		// points.Add(new Vector3(-Size.x*0.5f, -Size.y*0.5f, 0.0f));
		if (LineSafety > 0.0f) { points.Add(new Vector3(-Size.x*0.5f + CornerRadius + LineSafety, -Size.y*0.5f, 0.0f)); }
		foreach (var segment in corner) {
			points.Add(new Vector3(Size.x*-0.5f + CornerRadius - segment.x, Size.y*-0.5f + CornerRadius - segment.y, 0.0f));
		}
		if (LineSafety > 0.0f) { points.Add(new Vector3(-Size.x*0.5f, -Size.y*0.5f + CornerRadius + LineSafety, 0.0f)); }

		// Top-left corner
		// points.Add(new Vector3(-Size.x*0.5f, Size.y*0.5f, 0.0f));
		if (LineSafety > 0.0f) { points.Add(new Vector3(-Size.x*0.5f, Size.y*0.5f - CornerRadius - LineSafety, 0.0f)); }
		foreach (var segment in corner) {
			points.Add(new Vector3(Size.x*-0.5f + CornerRadius - segment.y, Size.y*0.5f - CornerRadius + segment.x, 0.0f));
		}
		if (LineSafety > 0.0f) { points.Add(new Vector3(-Size.x*0.5f + CornerRadius + LineSafety, Size.y*0.5f, 0.0f)); }

		// Ending point
		points.Add(new Vector3(0.0f-TopGap*0.5f, Size.y*0.5f, 0.0f));

		Vector3[] pointsArray = points.ToArray();
		SetLineValues(pointsArray);
	}

	private void SetLineValues(Vector3[] linePoints)
	{
		Debug.Log("Line Renderer updated with "+linePoints.Length+" points");
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
