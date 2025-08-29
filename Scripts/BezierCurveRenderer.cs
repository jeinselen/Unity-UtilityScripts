using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// One-stop Bézier renderer:
/// - Inputs: pointA, pointB, pointC
/// - Sliders: handleLengthNormalized (0..1), curvatureNormalized (0..1)
/// - Render Mode: LineRenderer OR Tube Mesh
/// - Works in Play Mode and the Editor (ExecuteAlways)
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BezierCurveRenderer : MonoBehaviour
{
	// ----------- Scene Inputs -----------
	public Transform pointA;
	public Transform pointB;
	public Transform pointC;
	
	// ----------- Curve Controls ---------
	[Tooltip("0 = straight line; 1 = both handles extend to the midpoint.")]
	[Range(0f, 1f)] public float handleLengthNormalized = 0.5f;
	
	[Tooltip("0 = handles stay on A↔B line; 1 = handles pushed away from C by |A-B|.")]
	[Range(0f, 1f)] public float curvatureNormalized = 0.5f;
	
	// Exposed (computed) Bezier handles in world space
	public Vector3 Control1WS { get; private set; }
	public Vector3 Control2WS { get; private set; }
	
	// ----------- Rendering --------------
	public enum RenderMode { TubeMesh, LineRenderer }
	
	[SerializeField] private RenderMode _renderMode = RenderMode.TubeMesh;  // default: Tube Mesh
	public RenderMode renderMode
	{
		get => _renderMode;
		set
		{
			if (_renderMode == value) return;
			_renderMode = value;
			MarkNeedsRebuild();
		}
	}
	
	// --- Line Renderer Settings (only when selected) ---
	[Min(2)] public int lineSamples = 32;
	
	// --- Tube Mesh Settings (only when selected) ---
	[Min(2)] public int pathSamples = 32;
	[Min(3)] public int radialSegments = 12;
	public float radius = 0.05f;
	public bool capEnds = true;
	public bool smoothShading = true;
	public bool meshInvert = false;
	public bool useRadiusCurve = false;
	public AnimationCurve radiusOverT = AnimationCurve.Linear(0, 1, 1, 1);
	
	// Internals
	Mesh _tubeMesh;
	MeshFilter _meshFilter;
	MeshRenderer _meshRenderer;
	LineRenderer _lineRenderer;
	
	#if UNITY_EDITOR
	bool _editorRebuildQueued;
	#endif
	
	// ----------- Lifecycle --------------
	void OnEnable()
	{
		RebuildAll();  // safe to add/assign components/meshes here
	}
	
	void Update()
	{
		RebuildAll();  // keep curve live in Edit & Play modes
	}
	
	#if UNITY_EDITOR
	void OnValidate()
	{
		// Don't mutate components/meshes here. Debounce & rebuild after validation.
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
			RebuildAll();
		};
	}
	#else
	void MarkNeedsRebuild() { /* noop at runtime */ }
	#endif
	
	// ----------- Public API -------------
	public Vector3 Evaluate(float t)
	{
		if (pointA == null || pointB == null) return transform.position;
		Vector3 A = pointA.position, B = pointB.position, C1 = Control1WS, C2 = Control2WS;
		float u = 1f - t, uu = u * u, uuu = uu * u, tt = t * t, ttt = tt * t;
		return uuu * A + 3f * uu * t * C1 + 3f * u * tt * C2 + ttt * B;
	}
	
	public Vector3 EvaluateTangent(float t)
	{
		Vector3 A = pointA.position, Bp = pointB.position, C1 = Control1WS, C2 = Control2WS;
		float u = 1f - t;
		Vector3 d = 3f * (u * u) * (C1 - A) + 6f * (u * t) * (C2 - C1) + 3f * (t * t) * (Bp - C2);
		return d.sqrMagnitude < 1e-12f ? Vector3.forward : d.normalized;
	}
	
	// ----------- Core -------------------
	void RebuildAll()
	{
		if (!isActiveAndEnabled) return;
		
		RecomputeHandles();
		
		switch (_renderMode)
		{
			case RenderMode.LineRenderer:
			EnsureLineRenderer();
			BuildLineRenderer();
			ReleaseTubeMesh();
			break;
			
			case RenderMode.TubeMesh:
			EnsureTubeMesh();
			BuildTubeMesh();
			DisableLineRenderer();
			break;
		}
	}
	
	void RecomputeHandles()
	{
		if (pointA == null || pointB == null || pointC == null)
		{
			Control1WS = Control2WS = transform.position;
			return;
		}
		
		Vector3 A = pointA.position, B = pointB.position;
		Vector3 AB = B - A;
		float lenAB = AB.magnitude;
		if (lenAB < 1e-6f) { Control1WS = Control2WS = A; return; }
		
		Vector3 dirAB = AB / lenAB;
		float along = 0.5f * lenAB * Mathf.Clamp01(handleLengthNormalized);
		Vector3 h1 = A + dirAB * along;
		Vector3 h2 = B - dirAB * along;
		
		float tProj = Vector3.Dot(pointC.position - A, dirAB);
		Vector3 closestOnLine = A + dirAB * tProj;
		Vector3 pushVec = closestOnLine - pointC.position;
		
		if (pushVec.sqrMagnitude < 1e-8f)
		{
			Vector3 candidate = Vector3.Cross(dirAB, Vector3.up);
			if (candidate.sqrMagnitude < 1e-8f) candidate = Vector3.Cross(dirAB, Vector3.right);
			pushVec = candidate.normalized;
		}
		else pushVec.Normalize();
		
		float pushMag = lenAB * Mathf.Clamp01(curvatureNormalized);
		Vector3 push = pushVec * pushMag;
		
		Control1WS = h1 + push;
		Control2WS = h2 + push;
	}
	
	// ----------- LineRenderer -----------
	void EnsureLineRenderer()
	{
		_lineRenderer = GetComponent<LineRenderer>();
		if (_lineRenderer == null) _lineRenderer = gameObject.AddComponent<LineRenderer>();
		_lineRenderer.useWorldSpace = true;
		_lineRenderer.enabled = true;
	}
	
	void BuildLineRenderer()
	{
		int n = Mathf.Max(2, lineSamples);
		_lineRenderer.positionCount = n;
		for (int i = 0; i < n; i++)
		{
			float t = (float)i / (n - 1);
			_lineRenderer.SetPosition(i, Evaluate(t));
		}
	}
	
	void DisableLineRenderer()
	{
		_lineRenderer = GetComponent<LineRenderer>();
		if (_lineRenderer != null) _lineRenderer.enabled = false;
	}
	
	// ----------- Tube Mesh --------------
	void EnsureTubeMesh()
	{
		_meshFilter   ??= GetComponent<MeshFilter>()   ?? gameObject.AddComponent<MeshFilter>();
		_meshRenderer ??= GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
		
		if (_tubeMesh == null)
		{
			_tubeMesh = new Mesh { name = "BezierTubeMesh" };
			_meshFilter.sharedMesh = _tubeMesh;   // safe (not in OnValidate)
		}
		else if (_meshFilter.sharedMesh != _tubeMesh)
		{
			_meshFilter.sharedMesh = _tubeMesh;
		}
	}
	
	void ReleaseTubeMesh()
	{
		if (_meshFilter != null) _meshFilter.sharedMesh = null;
	}
	
	void BuildTubeMesh()
	{
		if (_tubeMesh == null || pointA == null || pointB == null) return;
		
		int rings = Mathf.Max(2, pathSamples);
		int ringVerts = Mathf.Max(3, radialSegments);
		int bodyVertCount = rings * ringVerts;
		int capExtra = capEnds ? 2 : 0;
		
		var verts = new Vector3[bodyVertCount + capExtra];
		var norms = new Vector3[bodyVertCount + capExtra];
		var uvs   = new Vector2[bodyVertCount + capExtra];
		
		Vector3 prevT = Vector3.forward, N = Vector3.up, Bn = Vector3.right;
		
		for (int i = 0; i < rings; i++)
		{
			float t = (float)i / (rings - 1);
			Vector3 posWS = Evaluate(t);
			Vector3 T = EvaluateTangent(t);
			
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
			
			for (int j = 0; j < ringVerts; j++)
			{
				float ang = (j / (float)ringVerts) * Mathf.PI * 2f;
				Vector3 radialWS = Mathf.Cos(ang) * N + Mathf.Sin(ang) * Bn;
				Vector3 vWS = posWS + radialWS * r;
				
				int idx = i * ringVerts + j;
				verts[idx] = transform.InverseTransformPoint(vWS);
				norms[idx] = transform.InverseTransformDirection(smoothShading ? radialWS : T).normalized;
				uvs[idx]   = new Vector2(j / (float)ringVerts, t);
			}
		}
		
		// BODY TRIANGLES (fixed winding to face outward)
		// Previous order (a,c,b / b,c,d) produced inside-out normals for the body.
		// Use outward clockwise in Unity: (a, b, c) and (b, d, c)
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
					tris[ti++] = a; tris[ti++] = b; tris[ti++] = c; // quad tri 1
					tris[ti++] = b; tris[ti++] = d; tris[ti++] = c; // quad tri 2
				} else {
					tris[ti++] = a; tris[ti++] = c; tris[ti++] = b; // quad tri 1
					tris[ti++] = b; tris[ti++] = c; tris[ti++] = d; // quad tri 2
				}
			}
		}
		
		// CAPS (unchanged — were already correct)
		if (capEnds)
		{
			int startCenter = bodyVertCount;
			int endCenter   = bodyVertCount + 1;
			
			Vector3 startPosWS = Evaluate(0f), endPosWS = Evaluate(1f);
			Vector3 startT = EvaluateTangent(0f), endT = EvaluateTangent(1f);
			
			verts[startCenter] = transform.InverseTransformPoint(startPosWS);
			verts[endCenter]   = transform.InverseTransformPoint(endPosWS);
			norms[startCenter] = transform.InverseTransformDirection(-startT).normalized;
			norms[endCenter]   = transform.InverseTransformDirection( endT).normalized;
			uvs[startCenter]   = new Vector2(0.5f, 0f);
			uvs[endCenter]     = new Vector2(0.5f, 1f);
			
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
		
		_tubeMesh.Clear();
		_tubeMesh.vertices  = verts;
		_tubeMesh.normals   = norms;
		_tubeMesh.uv        = uvs;
		_tubeMesh.triangles = tris;
		_tubeMesh.RecalculateBounds();
		if (!smoothShading) _tubeMesh.RecalculateNormals();
	}
}

#if UNITY_EDITOR
// ----------- Custom Inspector (shows only relevant settings) --------------
[CustomEditor(typeof(BezierCurveRenderer))]
public class BezierCurveRendererEditor : Editor
{
	SerializedProperty pointA, pointB, pointC;
	SerializedProperty handleLen, curvature;
	SerializedProperty renderMode;
	SerializedProperty lineSamples;
	SerializedProperty pathSamples, radialSegments, radius, capEnds, smoothShading, meshInvert, useRadiusCurve, radiusOverT;
	
	void OnEnable()
	{
		pointA = serializedObject.FindProperty("pointA");
		pointB = serializedObject.FindProperty("pointB");
		pointC = serializedObject.FindProperty("pointC");
		
		handleLen = serializedObject.FindProperty("handleLengthNormalized");
		curvature = serializedObject.FindProperty("curvatureNormalized");
		
		renderMode   = serializedObject.FindProperty("_renderMode");
		
		lineSamples  = serializedObject.FindProperty("lineSamples");
		
		pathSamples     = serializedObject.FindProperty("pathSamples");
		radialSegments  = serializedObject.FindProperty("radialSegments");
		radius          = serializedObject.FindProperty("radius");
		capEnds         = serializedObject.FindProperty("capEnds");
		smoothShading   = serializedObject.FindProperty("smoothShading");
		meshInvert  = serializedObject.FindProperty("meshInvert");
		useRadiusCurve  = serializedObject.FindProperty("useRadiusCurve");
		radiusOverT     = serializedObject.FindProperty("radiusOverT");
	}
	
	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		
		EditorGUILayout.LabelField("Scene Inputs (world space)", EditorStyles.boldLabel);
		EditorGUILayout.PropertyField(pointA);
		EditorGUILayout.PropertyField(pointB);
		EditorGUILayout.PropertyField(pointC);
		
		EditorGUILayout.Space(6);
		EditorGUILayout.LabelField("Curve Controls", EditorStyles.boldLabel);
		EditorGUILayout.PropertyField(handleLen);
		EditorGUILayout.PropertyField(curvature);
		
		EditorGUILayout.Space(6);
		EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
		EditorGUILayout.PropertyField(renderMode);
		
		var mode = (BezierCurveRenderer.RenderMode)renderMode.enumValueIndex;
		
//		EditorGUILayout.Space(6);
		if (mode == BezierCurveRenderer.RenderMode.LineRenderer)
		{
//			EditorGUILayout.LabelField("Line Renderer Settings", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(lineSamples);
			
			// Validate/ensure a LineRenderer exists on this object
			var comp = ((BezierCurveRenderer)target).GetComponent<LineRenderer>();
//			using (new EditorGUI.DisabledScope(true))
//			{
//				EditorGUILayout.ObjectField("LineRenderer (auto)", comp, typeof(LineRenderer), true);
//			}
			if (comp == null)
			{
				if (GUILayout.Button("Add LineRenderer"))
				{
					Undo.AddComponent<LineRenderer>(((BezierCurveRenderer)target).gameObject);
					EditorUtility.SetDirty(target);
				}
			}
		}
		else // TubeMesh
		{
//			EditorGUILayout.LabelField("Tube Mesh Settings", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(pathSamples);
			EditorGUILayout.PropertyField(radialSegments);
			EditorGUILayout.PropertyField(radius);
			EditorGUILayout.PropertyField(capEnds);
			EditorGUILayout.PropertyField(smoothShading);
			EditorGUILayout.PropertyField(meshInvert);
			EditorGUILayout.PropertyField(useRadiusCurve);
			if (useRadiusCurve.boolValue)
			EditorGUILayout.PropertyField(radiusOverT);
			
			// Validate/ensure MeshFilter & MeshRenderer exist
			var go = ((BezierCurveRenderer)target).gameObject;
			var mf = go.GetComponent<MeshFilter>();
			var mr = go.GetComponent<MeshRenderer>();
//			using (new EditorGUI.DisabledScope(true))
//			{
//				EditorGUILayout.ObjectField("MeshFilter (auto)", mf, typeof(MeshFilter), true);
//				EditorGUILayout.ObjectField("MeshRenderer (auto)", mr, typeof(MeshRenderer), true);
//			}
			if (mf == null || mr == null)
			{
				if (GUILayout.Button("Add Missing Mesh Components"))
				{
					if (mf == null) Undo.AddComponent<MeshFilter>(go);
					if (mr == null) Undo.AddComponent<MeshRenderer>(go);
					EditorUtility.SetDirty(target);
				}
			}
		}
		
		serializedObject.ApplyModifiedProperties();
	}
}
#endif