using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// REQUIRES InspectorButton.cs
// https://www.reddit.com/r/Unity3D/comments/1s6czv/inspectorbutton_add_a_custom_button_to_your/

[RequireComponent(typeof (MeshFilter))]
[RequireComponent(typeof (MeshRenderer))]

[ExecuteInEditMode]
public class ShapeRect : MonoBehaviour
{
	private LineRenderer LineRend;
	public Vector2 Size = new Vector2(1.0f, 1.0f);
	public float CornerRadius = 0.1f;
	[InspectorButton("OnButtonClicked")]
	public bool Create;

	void Start()
	{
		// LineRend = GetComponent<LineRenderer>();
		// LoadCSV();
	}

	private void OnButtonClicked()
	{
		// LineRend = GetComponent<LineRenderer>();
		CalculateShape();
	}

	private void CalculateShape()
	{
		float W = Size.x * 0.5f;
		float H = Size.y * 0.5f;
		float R = CornerRadius;

		Vector3[] vertices = {
			// Top row 1
			new Vector3 (-W-R, +H+R, 0),
			new Vector3 (-W+R, +H+R, 0),
			new Vector3 (+W-R, +H+R, 0),
			new Vector3 (+W+R, +H+R, 0),
			// Top row 2
			new Vector3 (-W-R, +H-R, 0),
			new Vector3 (-W+R, +H-R, 0),
			new Vector3 (+W-R, +H-R, 0),
			new Vector3 (+W+R, +H-R, 0),
			// Bottom row 1
			new Vector3 (-W-R, -H+R, 0),
			new Vector3 (-W+R, -H+R, 0),
			new Vector3 (+W-R, -H+R, 0),
			new Vector3 (+W+R, -H+R, 0),
			// Bottom row 2
			new Vector3 (-W-R, -H-R, 0),
			new Vector3 (-W+R, -H-R, 0),
			new Vector3 (+W-R, -H-R, 0),
			new Vector3 (+W+R, -H-R, 0)
		};

		int[] triangles = {
			0, 1, 5, // top left corner
			0, 5, 4,
			1, 2, 6, // top edge
			1, 6, 5,
			2, 3, 7, // top right corner
			2, 7, 6,
			4, 5, 9, // left edge
			4, 9, 8,
			5, 6, 10, // middle
			5, 10, 9,
			6, 7, 11, // right edge
			6, 11, 10,
			8, 9, 13, // bottom left corner
			8, 13, 12,
			9, 10, 14, // bottom edge
			9, 14, 13,
			10, 11, 15, // bottom right corner
			10, 15, 14
		};

		Vector2[] uvs = {
			// Top row 1
			new Vector2 (0.00f, 1.0f),
			new Vector2 (0.25f, 1.0f),
			new Vector2 (0.75f, 1.0f),
			new Vector2 (1.00f, 1.0f),
			// Top row 2
			new Vector2 (0.00f, 0.75f),
			new Vector2 (0.25f, 0.75f),
			new Vector2 (0.75f, 0.75f),
			new Vector2 (1.00f, 0.75f),
			// Bottom row 1
			new Vector2 (0.00f, 0.25f),
			new Vector2 (0.25f, 0.25f),
			new Vector2 (0.75f, 0.25f),
			new Vector2 (1.00f, 0.25f),
			// Bottom row 2
			new Vector2 (0.00f, 0.00f),
			new Vector2 (0.25f, 0.00f),
			new Vector2 (0.75f, 0.00f),
			new Vector2 (1.00f, 0.00f)
		};

		// Create normalised UV map for the entire shape
		// Start by creating a normalising value to scale everything, and an offset parameter to center the results
		Vector2 S = Size + new Vector2(CornerRadius * 2.0f, CornerRadius * 2.0f);
		Vector2 O = new Vector2 (0.5f, 0.5f);

		Vector2[] uvs2 = {
			// Top row 1
			new Vector2 (-W-R, +H+R) / S + O,
			new Vector2 (-W+R, +H+R) / S + O,
			new Vector2 (+W-R, +H+R) / S + O,
			new Vector2 (+W+R, +H+R) / S + O,
			// Top row 2
			new Vector2 (-W-R, +H-R) / S + O,
			new Vector2 (-W+R, +H-R) / S + O,
			new Vector2 (+W-R, +H-R) / S + O,
			new Vector2 (+W+R, +H-R) / S + O,
			// Bottom row 1
			new Vector2 (-W-R, -H+R) / S + O,
			new Vector2 (-W+R, -H+R) / S + O,
			new Vector2 (+W-R, -H+R) / S + O,
			new Vector2 (+W+R, -H+R) / S + O,
			// Bottom row 2
			new Vector2 (-W-R, -H-R) / S + O,
			new Vector2 (-W+R, -H-R) / S + O,
			new Vector2 (+W-R, -H-R) / S + O,
			new Vector2 (+W+R, -H-R) / S + O
		};

		// Mesh mesh = GetComponent<MeshFilter> ().mesh;
		Mesh mesh = new Mesh();
			mesh.Clear ();
			mesh.vertices = vertices;
			mesh.triangles = triangles;
			mesh.uv = uvs;
			mesh.uv2 = uvs2;
			mesh.name = "RectShape";
			mesh.Optimize ();
			mesh.RecalculateNormals ();
		GetComponent<MeshFilter>().sharedMesh = mesh;
		GetComponent<MeshFilter>().mesh = mesh;
		EditorUtility.SetDirty(gameObject);
	}
}

// Resources:
// https://ilkinulas.github.io/development/unity/2016/04/30/cube-mesh-in-unity3d.html
