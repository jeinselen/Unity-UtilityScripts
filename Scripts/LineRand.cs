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
public class LineRand : MonoBehaviour
{
	private LineRenderer LineRend;
	public Vector3 RangeMin = new Vector3(-1.0f, -1.0f, -1.0f);
	public Vector3 RangeMax = new Vector3(1.0f, 1.0f, 1.0f);
	public Vector3 StartMin = new Vector3(-1.0f, 0.0f, -1.0f);
	public Vector3 StartMax = new Vector3(-1.0f, 1.0f, 1.0f);
	public Vector3 DirectionMin = new Vector3(0.05f, -0.25f, -0.1f);
	public Vector3 DirectionMax = new Vector3(0.25f, -0.05f, 0.1f);
	public Vector3 DirectionProbability = new Vector3(4.0f, 4.0f, 1.0f);
	public float MaximumIterations = 100.0f;
	[InspectorButton("OnButtonClicked")]
	public bool Generate;

	private void OnButtonClicked()
	{
		// LineRend = GetComponent<LineRenderer>();
		LineRend = (GetComponent<LineRenderer>())? GetComponent<LineRenderer>(): gameObject.AddComponent<LineRenderer>();
		CalculateLine();
	}

	private void CalculateLine()
	{
		// Calculate probability total
		float probabilityRange = DirectionProbability.x + DirectionProbability.y + DirectionProbability.z;
		float probability = 0.0f;
		Vector3 direction = new Vector3(0.0f, 0.0f, 0.0f);
		Vector3 start = new Vector3(Random.Range(StartMin.x, StartMax.x), Random.Range(StartMin.y, StartMax.y), Random.Range(StartMin.z, StartMax.z));

		List<Vector3> points = new List<Vector3>();
		for (var i = 0; i < MaximumIterations; i++) {
			probability = Random.Range(0.0f, probabilityRange);
			if (probability <= DirectionProbability.x) {
				direction = new Vector3(Random.Range(DirectionMin.x, DirectionMax.x), 0.0f, 0.0f);
			} else if (probability <= DirectionProbability.x + DirectionProbability.y) {
				direction = new Vector3(0.0f, Random.Range(DirectionMin.y, DirectionMax.y), 0.0f);
			} else {
				direction = new Vector3(0.0f, 0.0f, Random.Range(DirectionMin.z, DirectionMax.z));
			}
			start += direction;

			// Stop propagating when the range is hit
			if (start.x < RangeMin.x || start.x > RangeMax.x || start.y < RangeMin.y || start.y > RangeMax.y ||start.z < RangeMin.z || start.z > RangeMax.z) {
				start.x = Mathf.Clamp(start.x, RangeMin.x, RangeMax.x);
				start.y = Mathf.Clamp(start.y, RangeMin.y, RangeMax.y);
				start.z = Mathf.Clamp(start.z, RangeMin.z, RangeMax.z);
				i = (int)MaximumIterations;
			}

			points.Add(start);
//			points.Add(new Vector3(Mathf.Sin(currentRadian) * Size.x, Mathf.Cos(currentRadian) * Size.y, 0.0f));
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
}
