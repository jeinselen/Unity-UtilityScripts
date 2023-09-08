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
public class LinePill : MonoBehaviour
{
	private LineRenderer LineRend;
	public Vector2 Size = new Vector2(1.0f, 0.25f);
	public float LineSafety = 0.1f; // Line safety (helps prevent jumbling at glancing angles)
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
		float CornerRadius = Size.y*0.5f;
		// Create corner divisions and Sine-Cosine vectors
		float increment = (90.0f / (float)CornerResolution) * Mathf.Deg2Rad;
		// 90° in radians: 1.5707963267949
		increment = 1.5707963267949f / (float)CornerResolution;
		// increment = 3.1415926535898f / (float)CornerResolution;
		List<Vector3> corner = new List<Vector3>();
		for (var i = 0; i <= CornerResolution; i++) {
			if (i == 0) {
				// corner.Add(new Vector3(0.0f, CornerRadius, 0.0f));
			} else if (i < CornerResolution) {
				corner.Add(new Vector3(Mathf.Sin((float)i * increment) * CornerRadius, Mathf.Cos((float)i * increment) * CornerRadius, 0.0f));
			} else {
				// corner.Add(new Vector3(CornerRadius, 0.0f, 0.0f));
			}
		}

		// Start compiling the list of points
		List<Vector3> points = new List<Vector3>();

		// Starting point
		points.Add(new Vector3(-Size.x*0.5f, 0.0f, 0.0f));

		// Top-left corner
		foreach (var segment in corner) {
			points.Add(new Vector3(-segment.y - (Size.x*0.5f-CornerRadius), segment.x, 0.0f));
		}
		points.Add(new Vector3(-Size.x*0.5f + CornerRadius, CornerRadius, 0.0f));
		if (LineSafety > 0.0f) { points.Add(new Vector3(-Size.x*0.5f + CornerRadius + LineSafety, CornerRadius, 0.0f)); }

		// Top-right corner
		if (LineSafety > 0.0f) { points.Add(new Vector3(Size.x*0.5f - CornerRadius - LineSafety, CornerRadius, 0.0f)); }
		points.Add(new Vector3(Size.x*0.5f - CornerRadius, CornerRadius, 0.0f));
		foreach (var segment in corner) {
			points.Add(new Vector3(segment.x + (Size.x*0.5f-CornerRadius), segment.y, 0.0f));
		}

		// Midpoint
		points.Add(new Vector3(Size.x*0.5f, 0.0f, 0.0f));

		// Bottom-right corner
		foreach (var segment in corner) {
			points.Add(new Vector3(segment.y + (Size.x*0.5f-CornerRadius), -segment.x, 0.0f));
		}
		points.Add(new Vector3(Size.x*0.5f - CornerRadius, -CornerRadius, 0.0f));
		if (LineSafety > 0.0f) { points.Add(new Vector3(Size.x*0.5f - CornerRadius - LineSafety, -CornerRadius)); }

		// Bottom-left corner
		if (LineSafety > 0.0f) { points.Add(new Vector3(-Size.x*0.5f + CornerRadius + LineSafety, -CornerRadius)); }
		foreach (var segment in corner) {
			points.Add(new Vector3(-segment.x - (Size.x*0.5f-CornerRadius), -segment.y, 0.0f));
		}

		// Ending point
		points.Add(new Vector3(-Size.x*0.5f, 0.0f, 0.0f));

		// Fix looping failure through overlap
		points.Add(new Vector3(-corner[0].y - (Size.x*0.5f-CornerRadius), corner[0].x, 0.0f));

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
