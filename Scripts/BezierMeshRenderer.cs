using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Multi-target Bézier renderer:
/// - Inputs: source, targets (list)
/// - Control point: local transform center (parent position)
/// - Sliders: handleLengthNormalized (0..1), curvatureNormalized (0..1)
/// - UV Mapping: X = curve length in units, Y = normalized circumference (0-1)
/// - Works in Play Mode and the Editor (ExecuteAlways)
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BezierMeshRenderer : MonoBehaviour
{
	// ---- Scene Inputs ----
	[Tooltip("Starting point for all curves")]
	public Transform source;
	
	[Tooltip("List of target points - one curve will be created for each target")]
	public List<Transform> targets = new List<Transform>();
	
	// ---- Curve Controls ----
	[Tooltip("0 = straight line; 1 = both handles extend to the midpoint.")]
	[Range(0f, 1f)] public float handleLengthNormalized = 0.5f;
	
	[Tooltip("0 = handles stay on Source↔Target line; 1 = handles pushed away from center by |Source-Target|.")]
	[Range(0f, 1f)] public float curvatureNormalized = 0.5f;
	
	// --- Tube Mesh Settings ---
	[Min(2)] public int pathSamples = 32;
	[Min(3)] public int radialSegments = 4;
	public float radius = 0.05f;
	public bool capEnds = true;
	public bool smoothShading = true;
	public bool meshInvert = false;
	public bool useRadiusCurve = false;
	public AnimationCurve radiusOverT = AnimationCurve.Linear(0, 1, 1, 1);
	
	// --- UV Settings ---
	[Header("UV Mapping")]
	[Tooltip("Number of samples used to calculate curve length for UV mapping")]
	[Min(8)] public int lengthSamples = 32;
	
	// Internals
	Mesh _combinedMesh;
	MeshFilter _meshFilter;
	MeshRenderer _meshRenderer;
	
	#if UNITY_EDITOR
	bool _editorRebuildQueued;
	#endif
	
	// ---- Lifecycle ----
	void Awake()
	{
		// Initialize components early to avoid null reference issues
		InitializeComponents();
	}
	
	void OnEnable()
	{
		InitializeComponents();
		RebuildAll();
	}
	
	void Update()
	{
		RebuildAll();
	}
	
	#if UNITY_EDITOR
	void OnValidate()
	{
		MarkNeedsRebuild();
	}
	
	void MarkNeedsRebuild()
	{
		if (!this) return;
		if (_editorRebuildQueued) return;
		_editorRebuildQueued = true;
		
		EditorApplication.delayCall += () =>
		{
			_editorRebuildQueued = false;
			if (!this || !isActiveAndEnabled) return;
			InitializeComponents();
			RebuildAll();
		};
	}
	#else
	void MarkNeedsRebuild() { /* noop at runtime */ }
	#endif
	
	// ---- Component Initialization ----
	void InitializeComponents()
	{
		if (_meshFilter == null)
			_meshFilter = GetComponent<MeshFilter>();
		
		if (_meshRenderer == null)
			_meshRenderer = GetComponent<MeshRenderer>();
		
		// Ensure components exist (they should due to RequireComponent)
		if (_meshFilter == null)
			_meshFilter = gameObject.AddComponent<MeshFilter>();
		
		if (_meshRenderer == null)
			_meshRenderer = gameObject.AddComponent<MeshRenderer>();
	}
	
	// ---- Public API ----
	public Vector3 EvaluateCurve(Transform target, float t)
	{
		if (source == null || target == null) return transform.position;
		
		var handles = ComputeHandles(source.position, target.position);
		Vector3 A = source.position, B = target.position, C1 = handles.control1, C2 = handles.control2;
		float u = 1f - t, uu = u * u, uuu = uu * u, tt = t * t, ttt = tt * t;
		return uuu * A + 3f * uu * t * C1 + 3f * u * tt * C2 + ttt * B;
	}
	
	public Vector3 EvaluateTangent(Transform target, float t)
	{
		if (source == null || target == null) return Vector3.forward;
		
		var handles = ComputeHandles(source.position, target.position);
		Vector3 A = source.position, B = target.position, C1 = handles.control1, C2 = handles.control2;
		float u = 1f - t;
		Vector3 d = 3f * (u * u) * (C1 - A) + 6f * (u * t) * (C2 - C1) + 3f * (t * t) * (B - C2);
		return d.sqrMagnitude < 1e-12f ? Vector3.forward : d.normalized;
	}
	
	// Calculate the total length of a curve
	public float CalculateCurveLength(Transform target)
	{
		if (source == null || target == null) return 0f;
		
		float totalLength = 0f;
		Vector3 prevPos = EvaluateCurve(target, 0f);
		
		for (int i = 1; i <= lengthSamples; i++)
		{
			float t = (float)i / lengthSamples;
			Vector3 currentPos = EvaluateCurve(target, t);
			totalLength += Vector3.Distance(prevPos, currentPos);
			prevPos = currentPos;
		}
		
		return totalLength;
	}
	
	// Calculate distance along curve from start to parameter t
	public float CalculateDistanceAlongCurve(Transform target, float t)
	{
		if (source == null || target == null || t <= 0f) return 0f;
		if (t >= 1f) return CalculateCurveLength(target);
		
		float distance = 0f;
		Vector3 prevPos = EvaluateCurve(target, 0f);
		int samples = Mathf.RoundToInt(lengthSamples * t);
		
		for (int i = 1; i <= samples; i++)
		{
			float currentT = (float)i / lengthSamples;
			if (currentT > t) currentT = t;
			
			Vector3 currentPos = EvaluateCurve(target, currentT);
			distance += Vector3.Distance(prevPos, currentPos);
			prevPos = currentPos;
			
			if (currentT >= t) break;
		}
		
		return distance;
	}
	
	// ---- Core ----
	void RebuildAll()
	{
		if (!isActiveAndEnabled) return;
		
		// Ensure components are initialized before proceeding
		InitializeComponents();
		
		EnsureCombinedMesh();
		BuildCombinedMesh();
	}
	
	(Vector3 control1, Vector3 control2) ComputeHandles(Vector3 sourcePos, Vector3 targetPos)
	{
		Vector3 AB = targetPos - sourcePos;
		float lenAB = AB.magnitude;
		if (lenAB < 1e-6f) return (sourcePos, sourcePos);
		
		Vector3 dirAB = AB / lenAB;
		float along = 0.5f * lenAB * Mathf.Clamp01(handleLengthNormalized);
		Vector3 h1 = sourcePos + dirAB * along;
		Vector3 h2 = targetPos - dirAB * along;
		
		// Use local transform center (parent position in world space) as control point
		Vector3 centerPos = transform.parent != null ? transform.parent.position : transform.position;
		
		float tProj = Vector3.Dot(centerPos - sourcePos, dirAB);
		Vector3 closestOnLine = sourcePos + dirAB * tProj;
		Vector3 pushVec = closestOnLine - centerPos;
		
		if (pushVec.sqrMagnitude < 1e-8f)
		{
			Vector3 candidate = Vector3.Cross(dirAB, Vector3.up);
			if (candidate.sqrMagnitude < 1e-8f) candidate = Vector3.Cross(dirAB, Vector3.right);
			pushVec = candidate.normalized;
		}
		else pushVec.Normalize();
		
		float pushMag = lenAB * Mathf.Clamp01(curvatureNormalized);
		Vector3 push = pushVec * pushMag;
		
		return (h1 + push, h2 + push);
	}
	
	// ---- Mesh Management ----
	void EnsureCombinedMesh()
	{
		// Double-check component references
		if (_meshFilter == null)
		InitializeComponents();
		
		// Additional safety check
		if (_meshFilter == null)
		{
			Debug.LogError("MeshFilter component is missing and could not be added to " + gameObject.name);
			return;
		}
		
		if (_combinedMesh == null)
		{
			_combinedMesh = new Mesh { name = "BezierCombinedMesh" };
			_meshFilter.sharedMesh = _combinedMesh;
		}
		else if (_meshFilter.sharedMesh != _combinedMesh)
		{
			_meshFilter.sharedMesh = _combinedMesh;
		}
	}
	
	void BuildCombinedMesh()
	{
		if (_combinedMesh == null || source == null || targets == null || targets.Count == 0) 
		{
			if (_combinedMesh != null) _combinedMesh.Clear();
			return;
		}
		
		// Filter out null targets
		var validTargets = new List<Transform>();
		foreach (var target in targets)
		{
			if (target != null) validTargets.Add(target);
		}
		
		if (validTargets.Count == 0)
		{
			_combinedMesh.Clear();
			return;
		}
		
		// Build individual tube meshes and combine them
		var meshesToCombine = new List<CombineInstance>();
		
		float splineIndex = 0.0f;
		foreach (var target in validTargets)
		{
			var tubeMesh = BuildSingleTubeMesh(target, splineIndex / ((float)validTargets.Count - 1.0f));
			if (tubeMesh != null)
			{
				var combine = new CombineInstance
				{
					mesh = tubeMesh,
					transform = Matrix4x4.identity
				};
				meshesToCombine.Add(combine);
				splineIndex += 1.0f;
			}
		}
		
		if (meshesToCombine.Count > 0)
		{
			_combinedMesh.Clear();
			_combinedMesh.CombineMeshes(meshesToCombine.ToArray(), true, false);
			_combinedMesh.RecalculateBounds();
		}
		
		// Clean up temporary meshes
		foreach (var combine in meshesToCombine)
		{
			if (combine.mesh != null)
			{
				DestroyImmediate(combine.mesh);
			}
		}
	}
	
	Mesh BuildSingleTubeMesh(Transform target, float splineFactor)
	{
		if (source == null || target == null) return null;
		
		// Calculate total curve length for UV mapping
		float totalCurveLength = CalculateCurveLength(target);
		
		int rings = Mathf.Max(2, pathSamples);
		int ringVerts = Mathf.Max(3, radialSegments);
		int bodyVertCount = rings * ringVerts;
		int capExtra = capEnds ? 2 : 0;
		
		var verts = new Vector3[bodyVertCount + capExtra];
		var norms = new Vector3[bodyVertCount + capExtra];
		var uv0   = new Vector2[bodyVertCount + capExtra];
		var uv1   = new Vector2[bodyVertCount + capExtra];
		
		Vector3 prevT = Vector3.forward, N = Vector3.up, Bn = Vector3.right;
		
		for (int i = 0; i < rings; i++)
		{
			float t = (float)i / (rings - 1);
			Vector3 posWS = EvaluateCurve(target, t);
			Vector3 T = EvaluateTangent(target, t);
			
			if (i == 0)
			{
				Vector3 refUp = Mathf.Abs(Vector3.Dot(T, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
				N  = (refUp - Vector3.Dot(refUp, T) * T).normalized;
				Bn = Vector3.Cross(T, N).normalized;
			}
			else
			{
				Vector3 axis = Vector3.Cross(prevT, T);
				float axisSq = axis.sqrMagnitude;
				if (axisSq > 1e-12f)
				{
					float angle = Mathf.Atan2(Mathf.Sqrt(axisSq), Mathf.Clamp(Vector3.Dot(prevT, T), -1f, 1f));
					Quaternion q = Quaternion.AngleAxis(Mathf.Rad2Deg * angle, axis.normalized);
					N  = q * N;
					Bn = Vector3.Cross(T, N).normalized;
				}
				else
				{
					Bn = Vector3.Cross(T, N).normalized;
					N  = Vector3.Cross(Bn, T).normalized;
				}
			}
			prevT = T;
			
			float r = radius * (useRadiusCurve ? Mathf.Max(0f, radiusOverT.Evaluate(t)) : 1f);
			
			// Calculate distance along curve for UV.x mapping
			float distanceAlongCurve = CalculateDistanceAlongCurve(target, t);
			float factorAlongCurve = distanceAlongCurve / totalCurveLength;
			
			for (int j = 0; j < ringVerts; j++)
			{
				float ang = (j / (float)ringVerts) * Mathf.PI * 2f;
				Vector3 radialWS = Mathf.Cos(ang) * N + Mathf.Sin(ang) * Bn;
				Vector3 vWS = posWS + radialWS * r;
				
				int idx = i * ringVerts + j;
				verts[idx] = transform.InverseTransformPoint(vWS);
				norms[idx] = transform.InverseTransformDirection(smoothShading ? radialWS : T).normalized;
				
				// UV Mapping 0: X = distance along curve in world units, Y = normalized circumference (0-1)
				uv0[idx] = new Vector2(distanceAlongCurve, j / (float)ringVerts);
				uv1[idx] = new Vector2(factorAlongCurve, splineFactor);
			}
		}
		
		// Body triangles
		int[] tris = new int[(rings - 1) * ringVerts * 6];
		int ti = 0;
		for (int i = 0; i < rings - 1; i++)
		{
			int iNext = i + 1;
			for (int j = 0; j < ringVerts; j++)
			{
				int jNext = (j + 1) % ringVerts;
				
				int a = i * ringVerts + j;
				int b = iNext * ringVerts + j;
				int c = i * ringVerts + jNext;
				int d = iNext * ringVerts + jNext;
				
				if (meshInvert)
				{
					tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
					tris[ti++] = b; tris[ti++] = d; tris[ti++] = c;
				} 
				else 
				{
					tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
					tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
				}
			}
		}
		
		// Caps
		if (capEnds)
		{
			int startCenter = bodyVertCount;
			int endCenter   = bodyVertCount + 1;
			
			Vector3 startPosWS = EvaluateCurve(target, 0f), endPosWS = EvaluateCurve(target, 1f);
			Vector3 startT = EvaluateTangent(target, 0f), endT = EvaluateTangent(target, 1f);
			
			verts[startCenter] = transform.InverseTransformPoint(startPosWS);
			verts[endCenter]   = transform.InverseTransformPoint(endPosWS);
			norms[startCenter] = transform.InverseTransformDirection(-startT).normalized;
			norms[endCenter]   = transform.InverseTransformDirection( endT).normalized;
			
			// Cap UVs: center at (distance, 0.5) for start and (totalLength, 0.5) for end
			uv0[startCenter] = new Vector2(0f, 0.5f);
			uv0[endCenter]   = new Vector2(totalCurveLength, 0.5f);
			uv1[startCenter] = new Vector2(0f, 0.0f);
			uv1[endCenter]   = new Vector2(1.0f, 1.0f);
			
			System.Array.Resize(ref tris, tris.Length + ringVerts * 6);
			for (int j = 0; j < ringVerts; j++)
			{
				int jNext = (j + 1) % ringVerts;
				// start fan (faces outward)
				int a = startCenter, b = jNext, c = j;
				tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
				// end fan (faces outward)
				int off = (rings - 1) * ringVerts;
				int d = endCenter, e = off + j, f = off + jNext;
				tris[ti++] = d; tris[ti++] = e; tris[ti++] = f;
			}
		}
		
		var mesh = new Mesh { name = "BezierTubeMesh_Single" };
		mesh.vertices = verts;
		mesh.normals = norms;
		mesh.uv = uv0;
		mesh.uv2 = uv1;
		mesh.triangles = tris;
		mesh.RecalculateBounds();
		if (!smoothShading) mesh.RecalculateNormals();
		
		return mesh;
	}
}