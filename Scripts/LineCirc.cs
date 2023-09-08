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
public class LineCirc : MonoBehaviour
{
	private LineRenderer LineRend;
	public Vector2 Size = new Vector2(1.0f, 1.0f);
	public float StartDegree = -30.0f;
	public float EndDegree = 30.0f;
	public float DegreeIncrement = 5.0f;
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
		// Find nearest usable increment
		int incrementCount = (int)Mathf.Max(Mathf.Round((EndDegree - StartDegree) / DegreeIncrement), 1.0f);
		float incrementDegree = ((EndDegree - StartDegree) / (float)incrementCount);
		float currentRadian = 0.0f;

		List<Vector3> points = new List<Vector3>();
		for (var i = 0; i <= incrementCount; i++) {
			// points.Add(new Vector3(Mathf.Sin((float)i * increment) * CornerRadius, Mathf.Cos((float)i * increment) * CornerRadius, 0.0f));
			currentRadian = (StartDegree + (incrementDegree * (float)i)) * Mathf.Deg2Rad;
			points.Add(new Vector3(Mathf.Sin(currentRadian) * Size.x, Mathf.Cos(currentRadian) * Size.y, 0.0f));
		}

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
