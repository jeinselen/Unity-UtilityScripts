using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.Splines;
using Unity.Mathematics;

#if UNITY_EDITOR
[ExecuteAlways]
#endif
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SplinePanel : MonoBehaviour
{
	[Header("Dimensions")]
	[Tooltip("Total width of the panel")]
	[Range(0f, 10f)] public float width = 2f;
	[Tooltip("Total height of the panel")]
	[Range(0f, 10f)] public float height = 1f;
	
	[Header("Corners")]
	[Tooltip("Radius of each rounded corner (clamped to half of width/height)")]
	[Range(0f, 1f)] public float cornerRadius = 0.2f;
	[Tooltip("Number of segments per quarter-circle corner")]
	[Range(1, 64)] public int cornerSegments = 8;
	
	[Header("Subdivisions")]
	[Tooltip("Horizontal spacing (in world units) between vertical subdivisions in the straight middle section")]
	[Range(0.01f, 1f)]
	public float subdivisionSize = 0.1f;
	
	[Header("Performance")]
	[Tooltip("Calculate normals (disable if shader doesn't need them)")]
	public bool calculateNormals = true;
	[Tooltip("Calculate UV coordinates (disable if shader doesn't need them)")]
	public bool calculateUV = true;
	
	[Header("Safety Limits")]
	[Tooltip("Hard cap on total vertex count to avoid >65k meshes.")]
	[Range(1e-6f, 0.001f)] public float minDistance = 1e-6f;
	
	[Header("Spline Deformation")]
	[Tooltip("The spline along which to deform this panel")]
	public SplineContainer splineContainer;
	[Range(0f, 1f), Tooltip("Base offset along the spline (0..1)")]
	public float splinePosition = 0f;
	
	[SerializeField, HideInInspector]
	private Mesh mesh;
	
	// Cache of last-used parameters
	private float prevWidth, prevHeight, prevCornerRadius, prevSubdivisionSize, prevSplinePosition;
	private int prevCornerSegments;
	private SplineContainer prevSplineContainer;
	private bool prevCalculateNormals, prevcalculateUV;
	private bool needsRebuild = true;
	
	private void EnsureMesh()
	{
		var mf = GetComponent<MeshFilter>();
		
		// If mesh is null or destroyed, create a new one
		if (mesh == null)
		{
			mesh = new Mesh { name = $"SplinePanel{GetInstanceID()}" };
			// Remove HideAndDontSave to allow serialization
			mesh.hideFlags = HideFlags.DontSaveInBuild;
			needsRebuild = true;
		}
		
		// Pretty sure this is NOT good, but trying it anyway
		mesh.MarkDynamic();
		
		// Always ensure the MeshFilter has the correct reference
		if (mf.sharedMesh != mesh)
		{
			mf.sharedMesh = mesh;
		}
	}
	
	void Awake()
	{
		EnsureMesh();
		needsRebuild = true;
	}
	
	void OnEnable()
	{
		EnsureMesh();
		needsRebuild = true;
	}
	
	void Start()
	{
		EnsureMesh();
		needsRebuild = true;
		UpdateMesh();
	}
	
	void OnValidate()
	{
		// Don't assign mesh in OnValidate, just mark for rebuild
		needsRebuild = true;
		
		#if UNITY_EDITOR
		// Use EditorApplication.delayCall to defer mesh assignment
		UnityEditor.EditorApplication.delayCall += () =>
		{
			if (this != null)
			{
				EnsureMesh();
				UpdateMesh();
			}
		};
		#endif
	}
	
	void OnDestroy()
	{
		if (mesh != null)
		{
			#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				DestroyImmediate(mesh);
			}
			else
			#endif
			{
				Destroy(mesh);
			}
			mesh = null;
		}
	}
	
	void Update()
	{
		EnsureMesh();
		UpdateMesh();
	}
	
	void LateUpdate()
	{
		#if UNITY_EDITOR
		// Force update during animation preview
		if (!Application.isPlaying)
		{
			EnsureMesh();
			UpdateMesh();
		}
		#endif
	}
	
	private void UpdateMesh()
	{
		// Always check for changes, even if cache seems valid
		bool hasChanges = needsRebuild ||
		math.abs(width - prevWidth) > minDistance ||
		math.abs(height - prevHeight) > minDistance ||
		math.abs(cornerRadius - prevCornerRadius) > minDistance ||
		cornerSegments != prevCornerSegments ||
		math.abs(subdivisionSize - prevSubdivisionSize) > minDistance ||
		splineContainer != prevSplineContainer ||
		math.abs(splinePosition - prevSplinePosition) > minDistance ||
		calculateNormals != prevCalculateNormals ||
		calculateUV != prevcalculateUV;
		
		if (hasChanges)
		{
			BuildMesh();
			
			prevWidth = width;
			prevHeight = height;
			prevCornerRadius = cornerRadius;
			prevCornerSegments = cornerSegments;
			prevSubdivisionSize = subdivisionSize;
			prevSplineContainer = splineContainer;
			prevSplinePosition = splinePosition;
			prevCalculateNormals = calculateNormals;
			prevcalculateUV = calculateUV;
			needsRebuild = false;
		}
	}
	
	private void BuildMesh()
	{
		if (mesh == null) return;
		
		mesh.Clear();
		
		if (width <= minDistance || height <= minDistance)
		{
			mesh.vertices = new Vector3[] {new Vector3(0f, -10f, 0f), new Vector3(1e-4f, -10f, 0f), new Vector3(0f, -10f, 1e-4f)};
			//mesh.vertices = new Vector3[] {new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f)};
			mesh.uv = new Vector2[] {new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1)};
			mesh.triangles =  new int[] {0, 1, 2};
		}
		else
		{
			// Create new list instead of reusing
			List<float> xList = new List<float>();
			
			// Initialise variables for advanced mesh generation
			float halfW = width * 0.5f;
			float halfH = height * 0.5f;
			float r = Mathf.Min(cornerRadius, halfW, halfH);
			
			float innerMinX = -halfW + r;
			float innerMaxX = halfW - r;
			
			// Generate corner arc X positions
			for (int i = 0; i <= cornerSegments; i++)
			{
				float t = i / (float)cornerSegments;
				float ang = Mathf.PI * 0.5f + t * (Mathf.PI * 0.5f);
				float offset = Mathf.Cos(ang) * r;
				float xL = innerMinX + offset;
				float xR = -xL;
				xList.Add(xL);
				xList.Add(xR);
			}
			
			// Add straight-wall subdivisions
			
			//if (subdivisionSize > minDistance && innerMaxX > innerMinX)
			//{
			//	for (float x = innerMinX; x < innerMaxX; x += subdivisionSize)
			//	xList.Add(x);
			//	xList.Add(innerMaxX);
			//}
			
			if (subdivisionSize > 0f && innerMaxX > innerMinX)
			{
				int subdivCount = math.max(1, (int)math.ceil((innerMaxX - innerMinX) / subdivisionSize));
				float actualSubdivSize = (innerMaxX - innerMinX) / subdivCount;
				
				for (int i = 0; i < subdivCount; i++)
				{
					xList.Add(innerMinX + i * actualSubdivSize);
				}
				xList.Add(innerMaxX);
			}
			
			// Sort and remove duplicates
			xList.Sort();
			//for (int i = xList.Count - 1; i > 0; i--)
			//{
			//	if (math.abs(xList[i] - xList[i - 1]) < minDistance)
			//	xList.RemoveAt(i);
			//}
			
			int cols = xList.Count;
			
			// Create lists
			List<Vector3> verts = new List<Vector3>(cols * 2);
			List<Vector2> uvs = new List<Vector2>(cols * 2);
			List<int> tris = new List<int>((cols - 1) * 6);
			
			// Build vertices and UVs
			float invWidth = 1f / math.max(width, 1e-6f);
			float invHeight = 1f / math.max(height, 1e-6f);
			float rSquared = r * r;
			
			for (int i = 0; i < cols; i++)
			{
				float x = xList[i];
				float absX = math.abs(x);
				float dx = math.max(0f, absX - innerMaxX);
				
				float yTop, yBot;
				float dSquared = rSquared - dx * dx;
				
				if (absX <= innerMaxX)
				{
					// Rectangle middle
					yTop = halfH;
					yBot = -halfH;
				}
				else if (dSquared <= 0f)
				{
					// Rectangle ends
					yTop = halfH - r;
					yBot = -halfH + r;
				}
				else
				{
					// Rectangle rounded corners
					float d = math.sqrt(dSquared);
					yTop = halfH - r + d;
					yBot = -halfH + r - d;
				}
				
				verts.Add(new Vector3(x, yBot, 0f));
				verts.Add(new Vector3(x, yTop, 0f));
				
				if (calculateUV)
				{
					uvs.Add(new Vector2((x + halfW) * invWidth, (yBot + halfH) * invHeight));
					uvs.Add(new Vector2((x + halfW) * invWidth, (yTop + halfH) * invHeight));
				}
			}
			
			// Spline deformation
			if (splineContainer != null && splineContainer.Splines != null && splineContainer.Splines.Count > 0 && verts.Count > 0)
			{
				using (var nativeSpline = new NativeSpline(splineContainer.Splines[0], splineContainer.transform.localToWorldMatrix, Allocator.Temp))
				{
					float splineLength = nativeSpline.GetLength();
					
					// Deform each vertex along the spline
					if (splineLength > 0f)
					{
						float baseOffset = splinePosition * (splineLength - width) + halfW;
						float invSplineLength = 1f / splineLength;
						
						for (int i = 0; i < verts.Count; i++)
						{
							Vector3 v = verts[i];
							//float t = math.min(math.max((v.x + baseOffset) * invSplineLength, 0f), 0.99999994f);
							float t = (v.x + baseOffset) * invSplineLength;
							Vector3 samplePos = nativeSpline.EvaluatePosition(t);
							verts[i] = samplePos + new Vector3(0f, v.y, 0f);
						}
					}
				}
			}
			
			for (int i = 0; i < cols - 1; i++)
			{
				int bl = 2 * i;
				int tl = bl + 1;
				int br = 2 * (i + 1);
				int tr = br + 1;
				
				// First triangle
				tris.Add(bl);
				tris.Add(tl);
				tris.Add(tr);
				
				// Second triangle
				tris.Add(bl);
				tris.Add(tr);
				tris.Add(br);
			}
			
			// Assign to mesh
			mesh.SetVertices(verts);
			mesh.SetTriangles(tris, 0);
			mesh.SetUVs(0, uvs);
		}
		
		// Calculate normals and boundary
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
	}
}
