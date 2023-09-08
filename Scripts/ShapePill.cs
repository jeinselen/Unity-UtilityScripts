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
public class ShapePill : MonoBehaviour
{
	public Vector2 Size = new Vector2(1.0f, 0.25f);
	public float CornerRadius = 0.1f;
	public int CornerResolution = 8;
	[InspectorButton("OnButtonClicked")]
	public bool Create;

	void Start()
	{
		// LoadCSV();
	}

	private void OnButtonClicked()
	{
		CalculateShape();
	}

	private void CalculateShape()
	{
		float W = Size.x * 0.5f;
		float H = Size.y * 0.5f;
		float R = 3.1415926535898f / (float)CornerResolution;


		List<Vector3> verticesList = new List<Vector3>();
		// Top row
		verticesList.Add(new Vector3(-W+H, +H, 0.0f));
		verticesList.Add(new Vector3(+W-H, +H, 0.0f));
		// Middle row
		verticesList.Add(new Vector3(-W+H, 0.0f, 0.0f));
		verticesList.Add(new Vector3(+W-H, 0.0f, 0.0f));
		// Bottom row
		verticesList.Add(new Vector3(-W+H, -H, 0.0f));
		verticesList.Add(new Vector3(+W-H, -H, 0.0f));
		// Left Radius
		for (int i = 1; i < CornerResolution; i++) {
			verticesList.Add(new Vector3(-W-Mathf.Sin(R*(float)i)*H+H, Mathf.Cos(R*(float)i)*H, 0.0f));
		}
		// Right Radius
		for (int i = 1; i < CornerResolution; i++) {
			verticesList.Add(new Vector3(+W+Mathf.Sin(R*(float)i)*H-H, Mathf.Cos(R*(float)i)*H, 0.0f));
		}
		// Convert list into array
		Vector3[] vertices = verticesList.ToArray();
		Debug.Log("Shape vertices updated with "+vertices.Length+" elements");


		List<int> trianglesList = new List<int>();
		// Top and Middle
		trianglesList.Add(0);
		trianglesList.Add(1);
		trianglesList.Add(3);
		trianglesList.Add(0);
		trianglesList.Add(3);
		trianglesList.Add(2);
		// Middle and Bottom
		trianglesList.Add(2);
		trianglesList.Add(3);
		trianglesList.Add(5);
		trianglesList.Add(2);
		trianglesList.Add(5);
		trianglesList.Add(4);
		// Left Radius
		for (int i = 1; i <= CornerResolution; i++) {
			if (i == 1) {
				trianglesList.Add(2);
				trianglesList.Add(i+5);
				trianglesList.Add(0);
			} else if (i == CornerResolution) {
				trianglesList.Add(2);
				trianglesList.Add(4);
				trianglesList.Add(i+4);
			} else {
				trianglesList.Add(2);
				trianglesList.Add(i+5);
				trianglesList.Add(i+4);
			}
		}
		// Right Radius
		for (int i = CornerResolution; i < CornerResolution+CornerResolution; i++) {
			if (i == CornerResolution) {
				trianglesList.Add(3);
				trianglesList.Add(1);
				trianglesList.Add(i+5);
			} else if (i == CornerResolution+CornerResolution-1) {
				trianglesList.Add(3);
				trianglesList.Add(i+4);
				trianglesList.Add(5);
			} else {
				trianglesList.Add(3);
				trianglesList.Add(i+4);
				trianglesList.Add(i+5);
			}
		}
		int[] triangles = trianglesList.ToArray();



		List<Vector2> uvsList = new List<Vector2>();
		List<Vector2> uvs2List = new List<Vector2>();
		for (int i = 0; i < vertices.Length; i++) {
			uvsList.Add(new Vector2(vertices[i].x/Size.x+0.5f, vertices[i].y/Size.y+0.5f));
			uvs2List.Add(new Vector2(vertices[i].x, vertices[i].y));
		}
		// Convert lists into arrays
		Vector2[] uvs = uvsList.ToArray();
		Vector2[] uvs2 = uvs2List.ToArray();



		// Mesh mesh = GetComponent<MeshFilter> ().mesh;
		Mesh mesh = new Mesh();
			mesh.Clear ();
			mesh.vertices = vertices;
			mesh.triangles = triangles;
			mesh.uv = uvs;
			mesh.uv2 = uvs2;
			mesh.name = "PillShape";
			mesh.Optimize ();
			mesh.RecalculateNormals ();
		GetComponent<MeshFilter>().sharedMesh = mesh;
		GetComponent<MeshFilter>().mesh = mesh;
		EditorUtility.SetDirty(gameObject);
	}
}

// Resources:
// https://ilkinulas.github.io/development/unity/2016/04/30/cube-mesh-in-unity3d.html
