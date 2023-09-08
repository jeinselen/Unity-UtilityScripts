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

[RequireComponent(typeof (MeshFilter))]
[RequireComponent(typeof (MeshRenderer))]

[ExecuteInEditMode]
public class ShapeCSV : MonoBehaviour
{
	private LineRenderer LineRend;
	private string fileName;
	// public string FileName = "DFI_SP_1.csv";
	public Vector3 Scale = new Vector3(0.01f, -0.01f, 0.01f);
	public Vector3 Offset = new Vector3(0.0f, 1.0f, 0.0f);
	public Vector3 Extrude = new Vector3(0.0f, 0.0f, 1.0f);
	public Color Begin;
	public Color End;
	public bool ReverseOrder;
	[InspectorButton("OnButtonClicked")]
	public bool LoadFile;

	void Start()
	{
		// LineRend = GetComponent<LineRenderer>();
		// LoadCSV();
	}

	private void OnButtonClicked()
	{
		LineRend = GetComponent<LineRenderer>();
		LoadCSV();
	}

	private void LoadCSV()
	{
		// string CSVPath = Application.streamingAssetsPath + "/" + FileName;
		string CSVPath = EditorUtility.OpenFilePanel("choose CSV file", "", "csv");
		string fileContent = "";
		fileName = Path.GetFileName(CSVPath);
		// Debug.Log("CSVPath:"+CSVPath);
		// Debug.Log("CSVPath File Name:"+fileName);
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
			points.Add(new Vector3(x, y, z)+Extrude);
		}

		Vector3[] pointsArray = points.ToArray();

		Debug.Log("Mesh creation started with "+pointsArray.Length+" points loaded from CSV file");

		CalculateShape(pointsArray);
	}

private void CalculateShape(Vector3[] vertices)
	{
		// Vertices were already calculated, so we just have to generate the polygon and UV data now

		// Generate triangles
		List<int> trianglesList = new List<int>();
		// Create UV map
		List<Vector2> uvList = new List<Vector2>();
		// Also calculate total distance so we can normalise the UV map after creating it
		float totalDistance = 0.0f;
		for (int i = 0; i < vertices.Length; i++) {
			// Polygons (skip the last set of vertices because we're working with four vertices at a time)
			if (i < vertices.Length-2) {
				trianglesList.Add(i);
				trianglesList.Add(i+1);
				trianglesList.Add(i+3);
				trianglesList.Add(i);
				trianglesList.Add(i+3);
				trianglesList.Add(i+2);
			}

			// UVs (skip calculating distance with the first set of vertices because we're working with the previous value to determine distance)
			if (i >= 2) {
				totalDistance += Vector3.Distance(vertices[i], vertices[i-2]);;
			}
			uvList.Add(new Vector2(totalDistance, 0.0f));
			uvList.Add(new Vector2(totalDistance, 1.0f));

			i++; // Only operate with even numbered vertices by incrementing twice each loop
		}
		int[] triangles = trianglesList.ToArray();

		Debug.Log("Line Shape total edge length: "+totalDistance+"");

		Vector2[] uv = uvList.ToArray();
		// Normalise the U value using the total distance traced
		for (int i = 0; i < uv.Length; i++) {
			uv[i] /= new Vector2(totalDistance, 1.0f);
		}

		// Create colour gradient
		List<Color> colorsList = new List<Color>();
		for (int i = 0; i < uv.Length; i++) {
			colorsList.Add(Color.Lerp(Begin, End, uv[i].x));
		}
		Color[] colors = colorsList.ToArray();

		Debug.Log("vertices array length: "+vertices.Length+"");
		Debug.Log("triangles array length: "+triangles.Length+"");
		Debug.Log("uv array length: "+uv.Length+"");

		// Mesh mesh = GetComponent<MeshFilter> ().mesh;
		Mesh mesh = new Mesh();
			mesh.Clear ();
			mesh.vertices = vertices;
			mesh.triangles = triangles;
			mesh.uv = uv;
			mesh.colors = colors;
			mesh.name = fileName;
			mesh.Optimize ();
			mesh.RecalculateNormals ();
		GetComponent<MeshFilter>().sharedMesh = mesh;
		GetComponent<MeshFilter>().mesh = mesh;
		EditorUtility.SetDirty(gameObject);
	}

	private static string[] SplitCsvLine(string line)
	{
		return (from System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(line,
			@"(((?<x>(?=[,\r\n]+))|""(?<x>([^""]|"""")+)""|(?<x>[^,\r\n]+)),?)",
			System.Text.RegularExpressions.RegexOptions.ExplicitCapture)
				select m.Groups[1].Value).ToArray();
	}
}

// Resources:
// https://ilkinulas.github.io/development/unity/2016/04/30/cube-mesh-in-unity3d.html
