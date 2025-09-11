// AudioAmplitudeMapper.cs — Unity 6.2+
// Maps live audio amplitude to either a component member (via reflection) or a material shader property.
// Robust serialization (prefab-safe), unified target dropdown, modern Shader API, MaterialPropertyBlock runtime updates.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class AudioAmplitudeMapper : MonoBehaviour
{
	public enum UpdatePhase { Update, LateUpdate, FixedUpdate }
	public enum TargetMode { Reflection, Material }
	public enum MemberKind { Field, Property }
	public enum ValueKind { Float, Vector2, Vector3, Vector4, Color }
	public enum SubChannel { None, X, Y, Z, W, R, G, B, A }
	
//	[Header("Sampling Input")]
	[SerializeField] private AudioSource audioSource;
	[SerializeField, Range(64, 8192)] private int sampleSize = 1024;
	[SerializeField, Range(0f, 1f)] private float smoothing = 0.15f;
	[SerializeField] private Vector2 inputRange = new Vector2(0.00f, 0.30f);
//	[Header("Target Selection")]
	[SerializeField] private TargetMode targetMode = TargetMode.Reflection;
	
	[Serializable]
	public class ReflectionTarget
	{
		public Component component;
		public string memberName;
		public MemberKind memberKind = MemberKind.Property;
		public ValueKind valueKind = ValueKind.Float;
		public SubChannel subChannel = SubChannel.None;
	}
	
	[Serializable]
	public class MaterialTarget
	{
		// Kept serialized for persistence, but hidden in inspector (set via unified dropdown)
		[HideInInspector] public Renderer renderer;
		[HideInInspector] public int materialIndex = 0;
		
		public string propertyName;
		public ShaderPropertyType propertyType = ShaderPropertyType.Float; // auto-detected by editor
		public SubChannel channel = SubChannel.X; // X/Y/Z/W or R/G/B/A depending on type
	}
	
	[SerializeField] private ReflectionTarget reflection = new();
	[SerializeField] private MaterialTarget material = new();
	
//	[Header("Output Mapping")]
	[SerializeField] private Vector2 outputRange = new Vector2(0.0f, 1.0f);
	[SerializeField] private UpdatePhase updatePhase = UpdatePhase.Update;
	
	
	
	// --- runtime ---
	private float[] _samples;
	private float _smoothed;
	private FieldInfo _fieldInfo;
	private PropertyInfo _propertyInfo;
	private MaterialPropertyBlock _mpb;
	
	void Reset()
	{
		audioSource = GetComponent<AudioSource>();
		reflection.component = GetComponent<Transform>();
		reflection.memberName = "localScale";
		reflection.memberKind = MemberKind.Property;
		reflection.valueKind = ValueKind.Vector3;
		reflection.subChannel = SubChannel.X;
		outputRange.y = 2f;
		
		var rend = GetComponent<Renderer>();
		if (rend != null)
		{
			material.renderer = rend;
			material.materialIndex = 0;
		}
	}
	
	void OnValidate()
	{
		sampleSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(sampleSize, 64, 8192));
		if (inputRange.y < inputRange.x) inputRange.y = inputRange.x;
	}
	
	void Awake()
	{
		EnsureBuffers();
		CacheMemberInfo();
		if (_mpb == null) _mpb = new MaterialPropertyBlock();
	}
	
	void EnsureBuffers()
	{
		if (_samples == null || _samples.Length != sampleSize)
		_samples = new float[sampleSize];
	}
	
	public void CacheMemberInfo()
	{
		_fieldInfo = null;
		_propertyInfo = null;
		
		if (targetMode != TargetMode.Reflection) return;
		if (reflection.component == null || string.IsNullOrEmpty(reflection.memberName)) return;
		
		var t = reflection.component.GetType();
		if (reflection.memberKind == MemberKind.Field)
		_fieldInfo = t.GetField(reflection.memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		else
		_propertyInfo = t.GetProperty(reflection.memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	}
	
	void Update()
	{
		if (updatePhase == UpdatePhase.Update) SampleAndApply();
	}
	void LateUpdate()
	{
		if (updatePhase == UpdatePhase.LateUpdate) SampleAndApply();
	}
	void FixedUpdate()
	{
		if (updatePhase == UpdatePhase.FixedUpdate) SampleAndApply();
	}
	
	void SampleAndApply()
	{
		EnsureBuffers();
		if (audioSource == null) return;
		
		try
		{
			audioSource.GetOutputData(_samples, 0);
		}
		catch { return; }
		
		// RMS amplitude
		double sum = 0.0;
		for (int i = 0; i < _samples.Length; i++) { float s = _samples[i]; sum += (double)s * s; }
		float rms = Mathf.Sqrt((float)(sum / _samples.Length));
		
		// Remap + smooth
		float t = Mathf.InverseLerp(inputRange.x, inputRange.y, Mathf.Clamp(rms, inputRange.x, inputRange.y));
		float mapped = Mathf.Lerp(outputRange.x, outputRange.y, t);
		
		if (smoothing > 0f)
		{
			float a = 1f - Mathf.Pow(1f - Mathf.Clamp01(1f - smoothing), Time.deltaTime * 60f);
			_smoothed = Mathf.Lerp(_smoothed, mapped, a);
		}
		else
		{
			_smoothed = mapped;
		}
		
		if (targetMode == TargetMode.Material) ApplyToMaterial(_smoothed);
		else ApplyToMember(_smoothed);
	}
	
	void ApplyToMaterial(float v)
	{
		var rend = material.renderer;
		if (rend == null || string.IsNullOrEmpty(material.propertyName)) return;
		
		int idx = Mathf.Clamp(material.materialIndex, 0, Mathf.Max(0, (rend.sharedMaterials?.Length ?? 1) - 1));
		
		// Use MaterialPropertyBlock (doesn't instantiate/dirty the material)
		if (_mpb == null) _mpb = new MaterialPropertyBlock();
		rend.GetPropertyBlock(_mpb, idx);
		
		int id = Shader.PropertyToID(material.propertyName);
		
		switch (material.propertyType)
		{
			case ShaderPropertyType.Float:
			case ShaderPropertyType.Range:
			_mpb.SetFloat(id, v);
			break;
			
			case ShaderPropertyType.Vector:
			{
				Vector4 vec = _mpb.GetVector(id);
				vec = SetChannel(vec, v, material.channel);
				_mpb.SetVector(id, vec);
				break;
			}
			
			case ShaderPropertyType.Color:
			{
				Color col = _mpb.GetColor(id);
				col = SetChannel(col, v, material.channel);
				_mpb.SetColor(id, col);
				break;
			}
			
			default:
			break;
		}
		
		rend.SetPropertyBlock(_mpb, idx);
	}
	
	void ApplyToMember(float v)
	{
		var comp = reflection.component;
		if (comp == null) return;
		if (_fieldInfo == null && _propertyInfo == null) CacheMemberInfo();
		if (_fieldInfo == null && _propertyInfo == null) return;
		
		try
		{
			switch (reflection.valueKind)
			{
				case ValueKind.Float:
				SetFloatMember(comp, v);
				break;
				
				case ValueKind.Vector2:
				{
					Vector2 val = GetMember<Vector2>(comp);
					val = SetChannel(val, v, reflection.subChannel);
					SetMember(comp, val);
					break;
				}
				case ValueKind.Vector3:
				{
					Vector3 val = GetMember<Vector3>(comp);
					val = SetChannel(val, v, reflection.subChannel);
					SetMember(comp, val);
					break;
				}
				case ValueKind.Vector4:
				{
					Vector4 val = GetMember<Vector4>(comp);
					val = SetChannel(val, v, reflection.subChannel);
					SetMember(comp, val);
					break;
				}
				case ValueKind.Color:
				{
					Color val = GetMember<Color>(comp);
					val = SetChannel(val, v, reflection.subChannel);
					SetMember(comp, val);
					break;
				}
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning($"AudioAmplitudeMapper: Failed to set '{reflection.memberName}' on {comp?.GetType().Name}: {e.Message}", this);
		}
	}
	
	void SetFloatMember(Component comp, float v)
	{
		if (_fieldInfo != null)
		{
			var t = _fieldInfo.FieldType;
			if (t == typeof(float)) _fieldInfo.SetValue(comp, v);
			else if (t == typeof(int)) _fieldInfo.SetValue(comp, Mathf.RoundToInt(v));
			else if (t == typeof(double)) _fieldInfo.SetValue(comp, (double)v);
		}
		else if (_propertyInfo != null && _propertyInfo.CanWrite)
		{
			var t = _propertyInfo.PropertyType;
			if (t == typeof(float)) _propertyInfo.SetValue(comp, v, null);
			else if (t == typeof(int)) _propertyInfo.SetValue(comp, Mathf.RoundToInt(v), null);
			else if (t == typeof(double)) _propertyInfo.SetValue(comp, (double)v, null);
		}
	}
	
	T GetMember<T>(Component comp)
	{
		if (_fieldInfo != null) return (T)_fieldInfo.GetValue(comp);
		if (_propertyInfo != null && _propertyInfo.CanRead) return (T)_propertyInfo.GetValue(comp, null);
		return default;
	}
	
	static void SetMember<T>(Component comp, T value, FieldInfo fi, PropertyInfo pi)
	{
		if (fi != null) fi.SetValue(comp, value);
		else if (pi != null && pi.CanWrite) pi.SetValue(comp, value, null);
	}
	
	void SetMember<T>(Component comp, T value) => SetMember(comp, value, _fieldInfo, _propertyInfo);
	
	// --- channel helpers ---
	static Vector2 SetChannel(Vector2 v, float f, SubChannel ch)
	{
		if (ch == SubChannel.X) v.x = f; else if (ch == SubChannel.Y) v.y = f;
		return v;
	}
	static Vector3 SetChannel(Vector3 v, float f, SubChannel ch)
	{
		if (ch == SubChannel.X) v.x = f; else if (ch == SubChannel.Y) v.y = f; else if (ch == SubChannel.Z) v.z = f;
		return v;
	}
	static Vector4 SetChannel(Vector4 v, float f, SubChannel ch)
	{
		switch (ch) { case SubChannel.X: v.x = f; break; case SubChannel.Y: v.y = f; break; case SubChannel.Z: v.z = f; break; case SubChannel.W: v.w = f; break; }
		return v;
	}
	static Color SetChannel(Color c, float f, SubChannel ch)
	{
		switch (ch) { case SubChannel.R: c.r = f; break; case SubChannel.G: c.g = f; break; case SubChannel.B: c.b = f; break; case SubChannel.A: c.a = f; break; }
		return c;
	}
}

#if UNITY_EDITOR
[CustomEditor(typeof(AudioAmplitudeMapper))]
public class AudioAmplitudeMapperEditor : Editor
{
	// Sampling & Input
	private SerializedProperty _audioSource;
	private SerializedProperty _sampleSize;
	private SerializedProperty _smoothing;
	private SerializedProperty _inputRange;
	
	// Output
	private SerializedProperty _outputRange;
	private SerializedProperty _updatePhase;
	private SerializedProperty _targetMode;
	
	// Reflection
	private SerializedProperty _reflection;
	private SerializedProperty _re_component;
	private SerializedProperty _re_memberName;
	private SerializedProperty _re_memberKind;
	private SerializedProperty _re_valueKind;
	private SerializedProperty _re_subChannel;
	
	// Material
	private SerializedProperty _material;
	private SerializedProperty _ma_renderer;      // hidden field, not drawn
	private SerializedProperty _ma_materialIndex; // hidden field, not drawn
	private SerializedProperty _ma_propertyName;
	private SerializedProperty _ma_propertyType;
	private SerializedProperty _ma_channel;
	
	private struct UnifiedOption
	{
		public bool isMaterial;
		public string label;
		public Component component; // when !isMaterial
		public Renderer renderer;   // when isMaterial
		public int matIndex;
	}
	private List<UnifiedOption> _options = new();
	private int _selectedIndex = -1;
	
	// Component Members
	private struct MemberOption
	{
		public string display;
		public string name;
		public AudioAmplitudeMapper.MemberKind kind;
		public AudioAmplitudeMapper.ValueKind valueKind;
		public AudioAmplitudeMapper.SubChannel subChannel;
	}
	private List<MemberOption> _memberOptions = new();
	private int _memberIndex = -1;
	
	// Shader properties
	private string[] _shaderPropDisplay = Array.Empty<string>();
	private ShaderPropertyType[] _shaderPropTypes = Array.Empty<ShaderPropertyType>();
	private int _shaderPropIndex = -1;
	
	void OnEnable()
	{
		// Sampling & Input
		_audioSource = serializedObject.FindProperty("audioSource");
		_sampleSize  = serializedObject.FindProperty("sampleSize");
		_smoothing   = serializedObject.FindProperty("smoothing");
		_inputRange = serializedObject.FindProperty("inputRange");
		
		// Output
		_outputRange = serializedObject.FindProperty("outputRange");
		_updatePhase = serializedObject.FindProperty("updatePhase");
		_targetMode  = serializedObject.FindProperty("targetMode");
		
		_reflection     = serializedObject.FindProperty("reflection");
		_re_component   = _reflection.FindPropertyRelative("component");
		_re_memberName  = _reflection.FindPropertyRelative("memberName");
		_re_memberKind  = _reflection.FindPropertyRelative("memberKind");
		_re_valueKind   = _reflection.FindPropertyRelative("valueKind");
		_re_subChannel  = _reflection.FindPropertyRelative("subChannel");
		
		_material         = serializedObject.FindProperty("material");
		_ma_renderer      = _material.FindPropertyRelative("renderer");
		_ma_materialIndex = _material.FindPropertyRelative("materialIndex");
		_ma_propertyName  = _material.FindPropertyRelative("propertyName");
		_ma_propertyType  = _material.FindPropertyRelative("propertyType");
		_ma_channel       = _material.FindPropertyRelative("channel");
		
		RebuildUnifiedOptions();
		SyncSelectionFromSerialized();
		RebuildMembersOrShaderProps();
	}
	
	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		
		// --- Sample Input ---
//		EditorGUILayout.LabelField("Sample Input", EditorStyles.boldLabel);
		EditorGUILayout.PropertyField(_audioSource);
		EditorGUILayout.PropertyField(_sampleSize);
		EditorGUILayout.PropertyField(_smoothing);
		EditorGUILayout.PropertyField(_inputRange, new GUIContent("Input Range"));
		
		EditorGUILayout.Space(6);
		
		// --- Target Output ---
		EditorGUILayout.LabelField("Map Output", EditorStyles.boldLabel);
		DrawUnifiedDropdown();
		
		var mode = (AudioAmplitudeMapper.TargetMode)_targetMode.enumValueIndex;
		if (mode == AudioAmplitudeMapper.TargetMode.Material)
		DrawMaterialUI();
		else
		DrawReflectionUI();
		
//		EditorGUILayout.Space(6);
//		EditorGUILayout.LabelField("Output Mapping", EditorStyles.boldLabel);
		EditorGUILayout.PropertyField(_outputRange, new GUIContent("Output Range"));
		
		EditorGUILayout.PropertyField(_updatePhase);
		
		if (serializedObject.ApplyModifiedProperties())
		{
			RebuildUnifiedOptions();
			SyncSelectionFromSerialized();
			RebuildMembersOrShaderProps();
			
			foreach (var t in targets)
			(t as AudioAmplitudeMapper)?.CacheMemberInfo();
		}
	}
	
	// ---------- Unified dropdown ----------
	void DrawUnifiedDropdown()
	{
		if (_options.Count == 0)
		{
			EditorGUILayout.HelpBox("No components or materials found on this GameObject.", MessageType.Info);
			return;
		}
		
		string[] labels = _options.Select(o => o.label).ToArray();
		int newIndex = EditorGUILayout.Popup("Target", _selectedIndex, labels);
		if (newIndex == _selectedIndex || newIndex < 0) return;
		
		_selectedIndex = newIndex;
		var sel = _options[_selectedIndex];
		
		Undo.RecordObjects(targets, "Change AudioAmplitudeMapper Target");
		
		if (sel.isMaterial)
		{
			_targetMode.enumValueIndex = (int)AudioAmplitudeMapper.TargetMode.Material;
			_ma_renderer.objectReferenceValue = sel.renderer;
			_ma_materialIndex.intValue = sel.matIndex;
			_ma_propertyName.stringValue = string.Empty;
			_shaderPropIndex = -1;
		}
		else
		{
			_targetMode.enumValueIndex = (int)AudioAmplitudeMapper.TargetMode.Reflection;
			_re_component.objectReferenceValue = sel.component;
			_re_memberName.stringValue = string.Empty;
		}
	}
	
	void RebuildUnifiedOptions()
	{
		_options.Clear();
		var go = ((AudioAmplitudeMapper)target).gameObject;
		
		// Components
		foreach (var c in go.GetComponents<Component>())
		{
			if (c == null) continue;
			_options.Add(new UnifiedOption { isMaterial = false, label = $"Component: {c.GetType().Name}", component = c });
		}
		
		// Materials
		foreach (var rend in go.GetComponents<Renderer>())
		{
			if (rend == null) continue;
			var mats = rend.sharedMaterials;
			if (mats == null) continue;
			
			for (int i = 0; i < mats.Length; i++)
			{
				string matName = mats[i] ? mats[i].name : "None";
				_options.Add(new UnifiedOption
					{
						isMaterial = true,
						label = $"Material: {rend.GetType().Name}[{i}] • {matName}",
						renderer = rend,
						matIndex = i
					});
			}
		}
		
		if (_options.Count == 0) _selectedIndex = -1;
	}
	
	void SyncSelectionFromSerialized()
	{
		var mode = (AudioAmplitudeMapper.TargetMode)_targetMode.enumValueIndex;
		if (mode == AudioAmplitudeMapper.TargetMode.Material)
		{
			var r = _ma_renderer.objectReferenceValue as Renderer;
			int i = _ma_materialIndex.intValue;
			_selectedIndex = _options.FindIndex(o => o.isMaterial && o.renderer == r && o.matIndex == i);
		}
		else
		{
			var c = _re_component.objectReferenceValue as Component;
			_selectedIndex = _options.FindIndex(o => !o.isMaterial && o.component == c);
		}
		if (_selectedIndex < 0 && _options.Count > 0) _selectedIndex = 0;
	}
	
	// ---------- Reflection (component member) ----------
	void DrawReflectionUI()
	{
		// We no longer auto-draw the component field separately; the unified dropdown sets it,
		// but we still show it (read-only) for clarity:
		using (new EditorGUI.DisabledScope(true))
		{
			EditorGUILayout.PropertyField(_re_component, new GUIContent("Component"));
		}
		
		RebuildMembers(); // ensure options up to date
		
		if (_re_component.objectReferenceValue == null)
		{
			EditorGUILayout.HelpBox("Pick a component in the Target menu to target a field/property.", MessageType.Info);
			return;
		}
		
		if (_memberOptions.Count == 0)
		{
			EditorGUILayout.HelpBox("No writable float/vector/color fields/properties found on this component.", MessageType.Warning);
			return;
		}
		
		string[] labels = _memberOptions.Select(m => m.display).ToArray();
		int newIdx = EditorGUILayout.Popup("Member", _memberIndex, labels);
		if (newIdx != _memberIndex && newIdx >= 0)
		{
			_memberIndex = newIdx;
			var sel = _memberOptions[_memberIndex];
			_re_memberName.stringValue = sel.name;
			_re_memberKind.enumValueIndex = (int)sel.kind;
			_re_valueKind.enumValueIndex = (int)sel.valueKind;
			_re_subChannel.enumValueIndex = (int)sel.subChannel;
		}
	}
	
	void RebuildMembersOrShaderProps()
	{
		var mode = (AudioAmplitudeMapper.TargetMode)_targetMode.enumValueIndex;
		if (mode == AudioAmplitudeMapper.TargetMode.Material) RebuildShaderProps();
		else RebuildMembers();
	}
	
	private void RebuildMembers()
	{
		_memberOptions.Clear();
		_memberIndex = -1;
		
		var comp = _re_component.objectReferenceValue as Component;
		if (comp == null) return;
		
		var t = comp.GetType();
		
		void Add(string suffix, AudioAmplitudeMapper.ValueKind vk, AudioAmplitudeMapper.SubChannel ch, string name, AudioAmplitudeMapper.MemberKind kind)
		{
			_memberOptions.Add(new MemberOption
				{
					display = $"{name}{suffix}",
					name = name,
					kind = kind,
					valueKind = vk,
					subChannel = ch
				});
		}
		
		foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
		{
			if (f.IsInitOnly) continue;
			var ft = f.FieldType;
			if (ft == typeof(float) || ft == typeof(int) || ft == typeof(double)) Add(" (float)", AudioAmplitudeMapper.ValueKind.Float, AudioAmplitudeMapper.SubChannel.None, f.Name, AudioAmplitudeMapper.MemberKind.Field);
			else if (ft == typeof(Vector2)) { Add(" (Vector2).x", AudioAmplitudeMapper.ValueKind.Vector2, AudioAmplitudeMapper.SubChannel.X, f.Name, AudioAmplitudeMapper.MemberKind.Field); Add(" (Vector2).y", AudioAmplitudeMapper.ValueKind.Vector2, AudioAmplitudeMapper.SubChannel.Y, f.Name, AudioAmplitudeMapper.MemberKind.Field); }
			else if (ft == typeof(Vector3)) { Add(" (Vector3).x", AudioAmplitudeMapper.ValueKind.Vector3, AudioAmplitudeMapper.SubChannel.X, f.Name, AudioAmplitudeMapper.MemberKind.Field); Add(" (Vector3).y", AudioAmplitudeMapper.ValueKind.Vector3, AudioAmplitudeMapper.SubChannel.Y, f.Name, AudioAmplitudeMapper.MemberKind.Field); Add(" (Vector3).z", AudioAmplitudeMapper.ValueKind.Vector3, AudioAmplitudeMapper.SubChannel.Z, f.Name, AudioAmplitudeMapper.MemberKind.Field); }
			else if (ft == typeof(Vector4)) { Add(" (Vector4).x", AudioAmplitudeMapper.ValueKind.Vector4, AudioAmplitudeMapper.SubChannel.X, f.Name, AudioAmplitudeMapper.MemberKind.Field); Add(" (Vector4).y", AudioAmplitudeMapper.ValueKind.Vector4, AudioAmplitudeMapper.SubChannel.Y, f.Name, AudioAmplitudeMapper.MemberKind.Field); Add(" (Vector4).z", AudioAmplitudeMapper.ValueKind.Vector4, AudioAmplitudeMapper.SubChannel.Z, f.Name, AudioAmplitudeMapper.MemberKind.Field); Add(" (Vector4).w", AudioAmplitudeMapper.ValueKind.Vector4, AudioAmplitudeMapper.SubChannel.W, f.Name, AudioAmplitudeMapper.MemberKind.Field); }
			else if (ft == typeof(Color))  { Add(" (Color).r", AudioAmplitudeMapper.ValueKind.Color, AudioAmplitudeMapper.SubChannel.R, f.Name, AudioAmplitudeMapper.MemberKind.Field); Add(" (Color).g", AudioAmplitudeMapper.ValueKind.Color, AudioAmplitudeMapper.SubChannel.G, f.Name, AudioAmplitudeMapper.MemberKind.Field); Add(" (Color).b", AudioAmplitudeMapper.ValueKind.Color, AudioAmplitudeMapper.SubChannel.B, f.Name, AudioAmplitudeMapper.MemberKind.Field); Add(" (Color).a", AudioAmplitudeMapper.ValueKind.Color, AudioAmplitudeMapper.SubChannel.A, f.Name, AudioAmplitudeMapper.MemberKind.Field); }
		}
		
		foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
		{
			if (!p.CanRead || !p.CanWrite) continue;
			var pt = p.PropertyType;
			if (pt == typeof(float) || pt == typeof(int) || pt == typeof(double)) Add(" (float)", AudioAmplitudeMapper.ValueKind.Float, AudioAmplitudeMapper.SubChannel.None, p.Name, AudioAmplitudeMapper.MemberKind.Property);
			else if (pt == typeof(Vector2)) { Add(" (Vector2).x", AudioAmplitudeMapper.ValueKind.Vector2, AudioAmplitudeMapper.SubChannel.X, p.Name, AudioAmplitudeMapper.MemberKind.Property); Add(" (Vector2).y", AudioAmplitudeMapper.ValueKind.Vector2, AudioAmplitudeMapper.SubChannel.Y, p.Name, AudioAmplitudeMapper.MemberKind.Property); }
			else if (pt == typeof(Vector3)) { Add(" (Vector3).x", AudioAmplitudeMapper.ValueKind.Vector3, AudioAmplitudeMapper.SubChannel.X, p.Name, AudioAmplitudeMapper.MemberKind.Property); Add(" (Vector3).y", AudioAmplitudeMapper.ValueKind.Vector3, AudioAmplitudeMapper.SubChannel.Y, p.Name, AudioAmplitudeMapper.MemberKind.Property); Add(" (Vector3).z", AudioAmplitudeMapper.ValueKind.Vector3, AudioAmplitudeMapper.SubChannel.Z, p.Name, AudioAmplitudeMapper.MemberKind.Property); }
			else if (pt == typeof(Vector4)) { Add(" (Vector4).x", AudioAmplitudeMapper.ValueKind.Vector4, AudioAmplitudeMapper.SubChannel.X, p.Name, AudioAmplitudeMapper.MemberKind.Property); Add(" (Vector4).y", AudioAmplitudeMapper.ValueKind.Vector4, AudioAmplitudeMapper.SubChannel.Y, p.Name, AudioAmplitudeMapper.MemberKind.Property); Add(" (Vector4).z", AudioAmplitudeMapper.ValueKind.Vector4, AudioAmplitudeMapper.SubChannel.Z, p.Name, AudioAmplitudeMapper.MemberKind.Property); Add(" (Vector4).w", AudioAmplitudeMapper.ValueKind.Vector4, AudioAmplitudeMapper.SubChannel.W, p.Name, AudioAmplitudeMapper.MemberKind.Property); }
			else if (pt == typeof(Color))  { Add(" (Color).r", AudioAmplitudeMapper.ValueKind.Color, AudioAmplitudeMapper.SubChannel.R, p.Name, AudioAmplitudeMapper.MemberKind.Property); Add(" (Color).g", AudioAmplitudeMapper.ValueKind.Color, AudioAmplitudeMapper.SubChannel.G, p.Name, AudioAmplitudeMapper.MemberKind.Property); Add(" (Color).b", AudioAmplitudeMapper.ValueKind.Color, AudioAmplitudeMapper.SubChannel.B, p.Name, AudioAmplitudeMapper.MemberKind.Property); Add(" (Color).a", AudioAmplitudeMapper.ValueKind.Color, AudioAmplitudeMapper.SubChannel.A, p.Name, AudioAmplitudeMapper.MemberKind.Property); }
		}
		
		string current = _re_memberName.stringValue;
		if (!string.IsNullOrEmpty(current))
		{
			_memberIndex = _memberOptions.FindIndex(m =>
				m.name == current &&
				(int)m.kind == _re_memberKind.enumValueIndex &&
				(int)m.valueKind == _re_valueKind.enumValueIndex &&
				(int)m.subChannel == _re_subChannel.enumValueIndex);
		}
		if (_memberIndex < 0 && _memberOptions.Count > 0) _memberIndex = 0;
	}
	
	// ---------- Material (shader property) ----------
	void DrawMaterialUI()
	{
		// Renderer and Material Index are *not* drawn; they’re set by the unified dropdown.
		
		RebuildShaderProps();
		
		if (_shaderPropDisplay == null || _shaderPropDisplay.Length == 0)
		{
			EditorGUILayout.HelpBox("No supported shader properties (Float/Range/Vector/Color) found.", MessageType.Warning);
			return;
		}
		
		int newIdx = EditorGUILayout.Popup("Shader Property", _shaderPropIndex, _shaderPropDisplay);
		if (newIdx != _shaderPropIndex && newIdx >= 0)
		{
			_shaderPropIndex = newIdx;
			// Display is "name (Type)" → store pure name + detected type
			string display = _shaderPropDisplay[_shaderPropIndex];
			string pure = display.Split(' ')[0];
			_ma_propertyName.stringValue = pure;
			_ma_propertyType.enumValueIndex = (int)_shaderPropTypes[_shaderPropIndex];
		}
		
		// Channel UI only when relevant
		var type = (ShaderPropertyType)_ma_propertyType.enumValueIndex;
		if (type == ShaderPropertyType.Vector)
		{
			EditorGUILayout.PropertyField(_ma_channel, new GUIContent("Channel (X/Y/Z/W)"));
			if (_ma_channel.enumValueIndex < (int)AudioAmplitudeMapper.SubChannel.X || _ma_channel.enumValueIndex > (int)AudioAmplitudeMapper.SubChannel.W)
			_ma_channel.enumValueIndex = (int)AudioAmplitudeMapper.SubChannel.X;
		}
		else if (type == ShaderPropertyType.Color)
		{
			EditorGUILayout.PropertyField(_ma_channel, new GUIContent("Channel (R/G/B/A)"));
			if (_ma_channel.enumValueIndex < (int)AudioAmplitudeMapper.SubChannel.R || _ma_channel.enumValueIndex > (int)AudioAmplitudeMapper.SubChannel.A)
			_ma_channel.enumValueIndex = (int)AudioAmplitudeMapper.SubChannel.R;
		}
	}
	
	void RebuildShaderProps()
	{
		_shaderPropDisplay = Array.Empty<string>();
		_shaderPropTypes = Array.Empty<ShaderPropertyType>();
		_shaderPropIndex = -1;
		
		var rend = _ma_renderer.objectReferenceValue as Renderer;
		if (rend == null) return;
		
		int idx = Mathf.Clamp(_ma_materialIndex.intValue, 0, Mathf.Max(0, (rend.sharedMaterials?.Length ?? 1) - 1));
		var mat = (rend.sharedMaterials != null && idx < rend.sharedMaterials.Length) ? rend.sharedMaterials[idx] : null;
		if (mat == null || mat.shader == null) return;
		
		var shader = mat.shader;
		int count = shader.GetPropertyCount();
		
		var names = new List<string>();
		var types = new List<ShaderPropertyType>();
		
		for (int i = 0; i < count; i++)
		{
			var type = shader.GetPropertyType(i);
			if (type == ShaderPropertyType.Float || type == ShaderPropertyType.Range ||
				type == ShaderPropertyType.Vector || type == ShaderPropertyType.Color)
			{
				string name = shader.GetPropertyName(i);
				string display = name + type switch
				{
					ShaderPropertyType.Float => " (Float)",
					ShaderPropertyType.Range => " (Range)",
					ShaderPropertyType.Vector => " (Vector)",
					ShaderPropertyType.Color => " (Color)",
					_ => ""
				};
				names.Add(display);
				types.Add(type);
			}
		}
		
		_shaderPropDisplay = names.ToArray();
		_shaderPropTypes = types.ToArray();
		
		// Keep selection if possible
		string currentName = _ma_propertyName.stringValue;
		if (!string.IsNullOrEmpty(currentName))
		{
			for (int i = 0; i < _shaderPropDisplay.Length; i++)
			{
				var pure = _shaderPropDisplay[i].Split(' ')[0];
				if (pure == currentName) { _shaderPropIndex = i; break; }
			}
		}
		if (_shaderPropIndex < 0 && _shaderPropDisplay.Length > 0)
		{
			_shaderPropIndex = 0;
			var firstPure = _shaderPropDisplay[0].Split(' ')[0];
			_ma_propertyName.stringValue = firstPure;
			_ma_propertyType.enumValueIndex = (int)_shaderPropTypes[0];
		}
	}
}
#endif