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
	
	// Optimized data structures - reused to avoid allocations
	private readonly List<float> xListCache = new List<float>(128);
	private readonly List<Vector3> vertsCache = new List<Vector3>(256);
	private readonly List<Vector2> uvsCache = new List<Vector2>(256);
	private readonly List<int> trisCache = new List<int>(384);
	
	// Pre-computed trigonometry cache
	private float[] cosCache;
	private float[] sinCache;
	private int cachedCornerSegments = -1;
	
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
	
	private void PrecomputeTrigonometry()
	{
		if (cachedCornerSegments == cornerSegments && cosCache != null)
		return;
		
		cachedCornerSegments = cornerSegments;
		int arraySize = cornerSegments + 1;
		
		if (cosCache == null || cosCache.Length != arraySize)
		{
			cosCache = new float[arraySize];
			sinCache = new float[arraySize];
		}
		
		const float halfPi = math.PI * 0.5f;
		float invSegments = 1f / cornerSegments;
		
		for (int i = 0; i <= cornerSegments; i++)
		{
			float angle = halfPi + (i * invSegments) * halfPi;
			cosCache[i] = math.cos(angle);
			sinCache[i] = math.sin(angle);
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
			mesh.bounds = new Bounds(Vector3.zero, Vector3.zero);
		}
		else
		{
			float halfW = width * 0.5f;
			float halfH = height * 0.5f;
			//float r = math.min(math.max(0f, cornerRadius), math.min(halfW, halfH));
			float r = Mathf.Min(cornerRadius, halfW, halfH);
			
			float innerMinX = -halfW + r;
			float innerMaxX = halfW - r;
			
			// Precompute trigonometry
			PrecomputeTrigonometry();
			
			// Clear and reuse cached collections
			xListCache.Clear();
			
			// Generate corner arc X positions using cached trigonometry
			for (int i = 0; i <= cornerSegments; i++)
			{
				float offset = cosCache[i] * r;
				float xL = innerMinX + offset;
				float xR = -xL;
				xListCache.Add(xL);
				xListCache.Add(xR);
			}
			
			// Add straight-wall subdivisions with equal spacing based on input distance
			if (subdivisionSize > 0f && innerMaxX > innerMinX)
			{
				int subdivCount = math.max(1, (int)math.ceil((innerMaxX - innerMinX) / subdivisionSize));
				float actualSubdivSize = (innerMaxX - innerMinX) / subdivCount;
				
				for (int i = 0; i < subdivCount; i++)
				{
					xListCache.Add(innerMinX + i * actualSubdivSize);
				}
				xListCache.Add(innerMaxX);
			}
			
			// Sort and remove duplicates
			xListCache.Sort();
			
			int cols = xListCache.Count;
			
			// Clear and prepare vertex/UV caches
			vertsCache.Clear();
			vertsCache.Capacity = math.max(vertsCache.Capacity, cols * 2);
			uvsCache.Clear();
			uvsCache.Capacity = math.max(uvsCache.Capacity, cols * 2);
			
			// Build vertices and UVs
			float invWidth = 1f / math.max(width, 1e-6f);
			float invHeight = 1f / math.max(height, 1e-6f);
			float rSquared = r * r;
			
			for (int i = 0; i < cols; i++)
			{
				float x = xListCache[i];
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
				
				vertsCache.Add(new Vector3(x, yBot, 0f));
				vertsCache.Add(new Vector3(x, yTop, 0f));
				
				if (calculateUV)
				{
					uvsCache.Add(new Vector2((x + halfW) * invWidth, (yBot + halfH) * invHeight));
					uvsCache.Add(new Vector2((x + halfW) * invWidth, (yTop + halfH) * invHeight));
				}
			}
			
			// Spline deformation
			if (splineContainer != null && splineContainer.Splines != null && splineContainer.Splines.Count > 0 && vertsCache.Count > 0)
			{
				using (var nativeSpline = new NativeSpline(splineContainer.Splines[0], splineContainer.transform.localToWorldMatrix, Allocator.Temp))
				{
					float splineLength = nativeSpline.GetLength();
					
					// Deform each vertex along the spline
					if (splineLength > 0f)
					{
						float baseOffset = splinePosition * (splineLength - width) + halfW;
						float invSplineLength = 1f / splineLength;
						
						for (int i = 0; i < vertsCache.Count; i++)
						{
							Vector3 v = vertsCache[i];
							float t = math.min(math.max((v.x + baseOffset) * invSplineLength, 0f), 0.99999994f);
							//float t = (v.x + baseOffset) * invSplineLength;
							Vector3 samplePos = nativeSpline.EvaluatePosition(t);
							vertsCache[i] = samplePos + new Vector3(0f, v.y, 0f);
						}
					}
				}
			}
			
			// Build triangles
			trisCache.Clear();
			trisCache.Capacity = math.max(trisCache.Capacity, (cols - 1) * 6);
			
			for (int i = 0; i < cols - 1; i++)
			{
				int bl = 2 * i;
				int tl = bl + 1;
				int br = 2 * (i + 1);
				int tr = br + 1;
				
				// First triangle
				trisCache.Add(bl);
				trisCache.Add(tl);
				trisCache.Add(tr);
				
				// Second triangle
				trisCache.Add(bl);
				trisCache.Add(tr);
				trisCache.Add(br);
			}
			
			// Set blank UV0 if disabled or null
			if (!calculateUV || uvsCache == null || uvsCache.Count != vertsCache.Count)
			{
				uvsCache.Clear();
				for (int i = 0; i < vertsCache.Count; i++) uvsCache.Add(Vector2.zero);
			}
			
			// Assign mesh and UV0
			mesh.SetVertices(vertsCache);
			mesh.SetTriangles(trisCache, 0);
			mesh.SetUVs(0, uvsCache);
			
			// Rebind normals
			if (calculateNormals && vertsCache.Count > 0 && trisCache.Count > 0)
			{
				mesh.RecalculateNormals();
			}
			else
			{
				var normalsFallback = new System.Collections.Generic.List<Vector3>(vertsCache.Count);
				for (int i = 0; i < vertsCache.Count; i++) normalsFallback.Add(Vector3.forward);
				mesh.SetNormals(normalsFallback);
			}
			
			mesh.RecalculateBounds();
		}
	}
}
	