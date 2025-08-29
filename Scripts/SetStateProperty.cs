// SetStateProperty.cs
// Unity 6-compatible StateMachineBehaviour that sets a chosen property when a state is entered.
// It supports selecting a scene GameObject *indirectly* (name/tag/child path from the Animator),
// choosing a Component/Material, picking a property, and assigning a value of the appropriate type.
//
// Editor UI provides a scene picker and dynamic dropdowns for components/materials/properties, but
// only saves string/enum data that can be resolved at runtime (no scene refs are serialized).

using System;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
#endif

[Serializable]
public class SetStateProperty : StateMachineBehaviour
{
	public enum TargetResolveMode
	{
		Self,           // animator.gameObject
		Parent,         // animator.transform.parent
		ChildPath,      // animator.transform.Find(childPath)
		NameInScene,    // GameObject.Find(name)
		TagInScene      // GameObject.FindWithTag(tag)
	}
	
	public enum TargetMemberKind
	{
		ComponentField,
		ComponentProperty,
		MaterialProperty
	}
	
	public enum ValueKind
	{
		Integer,
		Float,
		Bool,
		String,
		Vector2,
		Vector3,
		Vector4,
		Color,
		Texture,
		AudioClip
	}
	
	[Serializable]
	public struct SceneTarget
	{
		public TargetResolveMode mode;
		public string pathOrNameOrTag; // child path OR name OR tag depending on mode
		
		public GameObject Resolve(Animator animator)
		{
			if (animator == null) return null;
			switch (mode)
			{
				case TargetResolveMode.Self:
				return animator.gameObject;
				case TargetResolveMode.Parent:
				return animator.transform.parent ? animator.transform.parent.gameObject : animator.gameObject;
				case TargetResolveMode.ChildPath:
				if (string.IsNullOrEmpty(pathOrNameOrTag)) return null;
				var t = animator.transform.Find(pathOrNameOrTag);
				return t ? t.gameObject : null;
				case TargetResolveMode.NameInScene:
				if (string.IsNullOrEmpty(pathOrNameOrTag)) return null;
				return GameObject.Find(pathOrNameOrTag);
				case TargetResolveMode.TagInScene:
				if (string.IsNullOrEmpty(pathOrNameOrTag)) return null;
				return GameObject.FindWithTag(pathOrNameOrTag);
				default:
				return null;
			}
		}
	}
	
	[Serializable]
	public struct TargetMember
	{
		public TargetMemberKind kind;
		
		// For Component* kinds
		public string componentTypeName; // Assembly-qualified type name
		public string memberName;        // Field or Property name
		public bool isProperty;          // true: Property, false: Field
		
		// For MaterialProperty kind
		public int rendererIndex;        // When multiple renderers are present; often 0
		public int materialIndex;        // Index into renderer's materials array
		public string materialPropertyName; // Shader property name (e.g., _BaseColor)
		
		// Shared
		public ValueKind valueKind;
		public bool usePropertyBlock;    // For materials: use MPB instead of instantiating material
	}
	
	[Serializable]
	public struct SerializableValue
	{
		public int intValue;
		public float floatValue;
		public bool boolValue;
		public string stringValue;
		public Vector2 vector2Value;
		public Vector3 vector3Value;
		public Vector4 vector4Value;
		public Color colorValue;
		public Texture textureValue;     // assets OK
		public AudioClip audioClipValue; // assets OK
		
		public object Boxed(ValueKind kind)
		{
			switch (kind)
			{
				case ValueKind.Integer: return intValue;
				case ValueKind.Float: return floatValue;
				case ValueKind.Bool: return boolValue;
				case ValueKind.String: return stringValue;
				case ValueKind.Vector2: return vector2Value;
				case ValueKind.Vector3: return vector3Value;
				case ValueKind.Vector4: return vector4Value;
				case ValueKind.Color: return colorValue;
				case ValueKind.Texture: return textureValue;
				case ValueKind.AudioClip: return audioClipValue;
				default: return null;
			}
		}
	}
	
	[Header("Target to affect (resolved at runtime)")]
	public SceneTarget sceneTarget = new SceneTarget { mode = TargetResolveMode.Self, pathOrNameOrTag = string.Empty };
	
	[Header("What to set")]
	public TargetMember targetMember = new TargetMember
	{
		kind = TargetMemberKind.ComponentProperty,
		componentTypeName = typeof(Transform).AssemblyQualifiedName,
		memberName = "",
		isProperty = true,
		rendererIndex = 0,
		materialIndex = 0,
		materialPropertyName = "",
		valueKind = ValueKind.Float,
		usePropertyBlock = true
	};
	
	[Header("Value to assign on state enter")]
	public SerializableValue value;
	
	public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
	{
		var go = sceneTarget.Resolve(animator);
		if (!go)
		{
			Debug.LogWarning($"[SetStateProperty] Target could not be resolved (mode={sceneTarget.mode}, key='{sceneTarget.pathOrNameOrTag}').");
			return;
		}
		
		switch (targetMember.kind)
		{
			case TargetMemberKind.ComponentField:
			case TargetMemberKind.ComponentProperty:
			ApplyToComponent(go);
			break;
			case TargetMemberKind.MaterialProperty:
			ApplyToMaterial(go);
			break;
		}
	}
	
	void ApplyToComponent(GameObject go)
	{
		var type = !string.IsNullOrEmpty(targetMember.componentTypeName) ? Type.GetType(targetMember.componentTypeName) : null;
		if (type == null)
		{
			Debug.LogWarning($"[SetStateProperty] Unknown component type '{targetMember.componentTypeName}'.");
			return;
		}
		
		var comp = go.GetComponent(type);
		if (comp == null)
		{
			Debug.LogWarning($"[SetStateProperty] Component '{type.Name}' not found on '{go.name}'.");
			return;
		}
		
		try
		{
			object boxed = value.Boxed(targetMember.valueKind);
			if (targetMember.isProperty)
			{
				var pi = type.GetProperty(targetMember.memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (pi == null || !pi.CanWrite)
				{
					Debug.LogWarning($"[SetStateProperty] Writable property '{targetMember.memberName}' not found on '{type.Name}'.");
					return;
				}
				var coerced = Coerce(boxed, pi.PropertyType);
				pi.SetValue(comp, coerced);
			}
			else
			{
				var fi = type.GetField(targetMember.memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (fi == null)
				{
					Debug.LogWarning($"[SetStateProperty] Field '{targetMember.memberName}' not found on '{type.Name}'.");
					return;
				}
				var coerced = Coerce(boxed, fi.FieldType);
				fi.SetValue(comp, coerced);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[SetStateProperty] Failed to set {targetMember.memberName} on {go.name}: {ex.Message}");
		}
	}
	
	void ApplyToMaterial(GameObject go)
	{
		var renderers = go.GetComponentsInChildren<Renderer>(true);
		if (renderers == null || renderers.Length == 0)
		{
			Debug.LogWarning($"[SetStateProperty] No Renderer found under '{go.name}'.");
			return;
		}
		int rIdx = Mathf.Clamp(targetMember.rendererIndex, 0, renderers.Length - 1);
		var renderer = renderers[rIdx];
		
		string prop = targetMember.materialPropertyName;
		if (string.IsNullOrEmpty(prop))
		{
			Debug.LogWarning("[SetStateProperty] Material property name is empty.");
			return;
		}
		
		if (targetMember.usePropertyBlock)
		{
			var mpb = new MaterialPropertyBlock();
			renderer.GetPropertyBlock(mpb, targetMember.materialIndex);
			AssignToMPB(mpb, prop, targetMember.valueKind, value);
			renderer.SetPropertyBlock(mpb, targetMember.materialIndex);
		}
		else
		{
			var mats = renderer.materials; // instanced
			int mIdx = Mathf.Clamp(targetMember.materialIndex, 0, mats.Length - 1);
			var mat = mats[mIdx];
			AssignToMaterial(mat, prop, targetMember.valueKind, value);
		}
	}
	
	static void AssignToMPB(MaterialPropertyBlock mpb, string prop, ValueKind kind, SerializableValue v)
	{
		switch (kind)
		{
			case ValueKind.Integer: mpb.SetInt(prop, v.intValue); break;
			case ValueKind.Float: mpb.SetFloat(prop, v.floatValue); break;
			case ValueKind.Color: mpb.SetColor(prop, v.colorValue); break;
			case ValueKind.Vector2: mpb.SetVector(prop, v.vector2Value); break;
			case ValueKind.Vector3: mpb.SetVector(prop, v.vector3Value); break;
			case ValueKind.Vector4: mpb.SetVector(prop, v.vector4Value); break;
			case ValueKind.Texture: mpb.SetTexture(prop, v.textureValue); break;
			default:
			Debug.LogWarning($"[SetStateProperty] ValueKind {kind} is not supported for MaterialPropertyBlock.");
			break;
		}
	}
	
	static void AssignToMaterial(Material mat, string prop, ValueKind kind, SerializableValue v)
	{
		switch (kind)
		{
			case ValueKind.Integer: mat.SetInt(prop, v.intValue); break;
			case ValueKind.Float: mat.SetFloat(prop, v.floatValue); break;
			case ValueKind.Color: mat.SetColor(prop, v.colorValue); break;
			case ValueKind.Vector2: mat.SetVector(prop, v.vector2Value); break;
			case ValueKind.Vector3: mat.SetVector(prop, v.vector3Value); break;
			case ValueKind.Vector4: mat.SetVector(prop, v.vector4Value); break;
			case ValueKind.Texture: mat.SetTexture(prop, v.textureValue); break;
			default:
			Debug.LogWarning($"[SetStateProperty] ValueKind {kind} is not supported for Material.");
			break;
		}
	}
	
	static object Coerce(object boxed, Type targetType)
	{
		if (boxed == null) return null;
		var bType = boxed.GetType();
		if (targetType.IsAssignableFrom(bType)) return boxed;
		
		try
		{
			if (targetType.IsEnum && boxed is int i)
			return Enum.ToObject(targetType, i);
			if (targetType == typeof(int) && boxed is float f)
			return (int)f;
			if (targetType == typeof(float) && boxed is int i2)
			return (float)i2;
			if (targetType == typeof(bool) && boxed is int i3)
			return i3 != 0;
			
			// Try Convert.ChangeType for primitives & strings
			return Convert.ChangeType(boxed, targetType);
		}
		catch
		{
			return boxed; // best effort
		}
	}
	
	#if UNITY_EDITOR
	// ---------------------
	// Custom Inspector (Editor-only)
	// ---------------------
	[CustomEditor(typeof(SetStateProperty))]
	public class SetStatePropertyEditor : Editor
	{
		// Editor-only, not serialized into asset
		GameObject previewGO;
		Component previewComponent;
		Renderer previewRenderer;
		int previewMaterialIndex;
		
		SerializedProperty sceneTargetProp;
		SerializedProperty targetMemberProp;
		SerializedProperty valueProp;
		
		void OnEnable()
		{
			sceneTargetProp = serializedObject.FindProperty("sceneTarget");
			targetMemberProp = serializedObject.FindProperty("targetMember");
			valueProp = serializedObject.FindProperty("value");
		}
		
		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			
			EditorGUILayout.LabelField("Target to affect (runtime-resolved)", EditorStyles.boldLabel);
			DrawSceneTarget(sceneTargetProp);
			EditorGUILayout.Space(6);
			
			EditorGUILayout.LabelField("Preview (editor-only)", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("Use a scene object here to populate the component/material/property dropdowns. The reference is NOT saved in the controller asset.", MessageType.Info);
			using (new EditorGUI.IndentLevelScope())
			{
				previewGO = (GameObject)EditorGUILayout.ObjectField("Scene Object", previewGO, typeof(GameObject), true);
			}
			
			EditorGUILayout.Space(6);
			EditorGUILayout.LabelField("What to set", EditorStyles.boldLabel);
			DrawTargetMember(targetMemberProp, previewGO);
			
			EditorGUILayout.Space(6);
			EditorGUILayout.LabelField("Value", EditorStyles.boldLabel);
			DrawValueField(targetMemberProp, valueProp);
			
			serializedObject.ApplyModifiedProperties();
		}
		
		static void DrawSceneTarget(SerializedProperty sceneTargetProp)
		{
			var modeProp = sceneTargetProp.FindPropertyRelative("mode");
			var keyProp = sceneTargetProp.FindPropertyRelative("pathOrNameOrTag");
			
			EditorGUILayout.PropertyField(modeProp);
			switch ((TargetResolveMode)modeProp.enumValueIndex)
			{
				case TargetResolveMode.ChildPath:
				keyProp.stringValue = EditorGUILayout.TextField(new GUIContent("Child Path", "Path under the Animator (e.g., 'Armature/Spine/Head')"), keyProp.stringValue);
				break;
				case TargetResolveMode.NameInScene:
				keyProp.stringValue = EditorGUILayout.TextField(new GUIContent("Object Name"), keyProp.stringValue);
				break;
				case TargetResolveMode.TagInScene:
				keyProp.stringValue = EditorGUILayout.TagField(new GUIContent("Tag"), string.IsNullOrEmpty(keyProp.stringValue) ? "Untagged" : keyProp.stringValue);
				break;
				default:
				EditorGUILayout.HelpBox("Self = Animator's GameObject. Parent = Animator's parent GameObject.", MessageType.None);
				break;
			}
		}
		
		void DrawTargetMember(SerializedProperty targetMember, GameObject preview)
		{
			var kindProp = targetMember.FindPropertyRelative("kind");
			EditorGUILayout.PropertyField(kindProp);
			var kind = (TargetMemberKind)kindProp.enumValueIndex;
			
			if (kind == TargetMemberKind.MaterialProperty)
			{
				using (new EditorGUI.IndentLevelScope())
				{
					// Renderer index (for runtime resolution across multiple renderers under the target)
					var rIdxProp = targetMember.FindPropertyRelative("rendererIndex");
					rIdxProp.intValue = EditorGUILayout.IntField(new GUIContent("Renderer Index", "Index into all child Renderers of the target"), Mathf.Max(0, rIdxProp.intValue));
					
					// Material index
					var mIdxProp = targetMember.FindPropertyRelative("materialIndex");
					// If preview has a renderer, provide a dropdown of material slots
					int newMatIndex = mIdxProp.intValue;
					string[] matOptions = null;
					
					if (preview)
					{
						var rends = preview.GetComponentsInChildren<Renderer>(true);
						if (rends.Length > 0)
						{
							int safeR = Mathf.Clamp(rIdxProp.intValue, 0, rends.Length - 1);
							var mats = rends[safeR].sharedMaterials;
							matOptions = new string[Mathf.Max(1, mats.Length)];
							for (int i = 0; i < matOptions.Length; i++)
							{
								var m = i < mats.Length ? mats[i] : null;
								matOptions[i] = $"[{i}] " + (m ? m.name : "<null>");
							}
							newMatIndex = EditorGUILayout.Popup(new GUIContent("Material Slot"), Mathf.Clamp(mIdxProp.intValue, 0, matOptions.Length - 1), matOptions);
						}
					}
					
					if (matOptions == null)
					newMatIndex = EditorGUILayout.IntField(new GUIContent("Material Slot"), Mathf.Max(0, mIdxProp.intValue));
					
					mIdxProp.intValue = newMatIndex;
					
					// Shader property name
					var propNameProp = targetMember.FindPropertyRelative("materialPropertyName");
					string chosen = propNameProp.stringValue;
					
					// Try to populate shader properties if we have a preview material
					#if UNITY_EDITOR
					if (preview)
					{
						var rends = preview.GetComponentsInChildren<Renderer>(true);
						if (rends.Length > 0)
						{
							int safeR = Mathf.Clamp(rIdxProp.intValue, 0, rends.Length - 1);
							var mats = rends[safeR].sharedMaterials;
							Material mat = newMatIndex < mats.Length ? mats[newMatIndex] : null;
							if (mat && mat.shader)
							{
								var names = GetShaderPropertyNames(mat.shader);
								int idx = Mathf.Max(0, Array.IndexOf(names, string.IsNullOrEmpty(chosen) ? names[0] : chosen));
								idx = EditorGUILayout.Popup(new GUIContent("Shader Property"), idx, names);
								chosen = names.Length > 0 ? names[idx] : chosen;
								propNameProp.stringValue = chosen;
								
								// Guess kind from property type if possible
								var kindGuess = GuessValueKindForShader(mat.shader, chosen);
								targetMember.FindPropertyRelative("valueKind").enumValueIndex = (int)kindGuess;
							}
							else
							{
								propNameProp.stringValue = EditorGUILayout.TextField(new GUIContent("Shader Property"), propNameProp.stringValue);
							}
						}
						else
						{
							propNameProp.stringValue = EditorGUILayout.TextField(new GUIContent("Shader Property"), propNameProp.stringValue);
						}
					}
					else
					{
						propNameProp.stringValue = EditorGUILayout.TextField(new GUIContent("Shader Property"), propNameProp.stringValue);
					}
					#endif
					EditorGUILayout.PropertyField(targetMember.FindPropertyRelative("usePropertyBlock"), new GUIContent("Use MaterialPropertyBlock"));
					
					// Value kind (overridable)
					EditorGUILayout.PropertyField(targetMember.FindPropertyRelative("valueKind"));
				}
			}
			else
			{
				using (new EditorGUI.IndentLevelScope())
				{
					var typeNameProp = targetMember.FindPropertyRelative("componentTypeName");
					var isPropertyProp = targetMember.FindPropertyRelative("isProperty");
					var memberNameProp = targetMember.FindPropertyRelative("memberName");
					
					// Component type selection via preview
					Type compType = null;
					if (preview)
					{
						var comps = preview.GetComponents<Component>();
						var names = new List<string>();
						var types = new List<Type>();
						foreach (var c in comps)
						{
							if (c == null) continue;
							types.Add(c.GetType());
							names.Add(c.GetType().Name);
						}
						int cur = 0;
						if (!string.IsNullOrEmpty(typeNameProp.stringValue))
						{
							var t = Type.GetType(typeNameProp.stringValue);
							if (t != null) cur = Mathf.Max(0, types.IndexOf(t));
						}
						cur = EditorGUILayout.Popup("Component", cur, names.ToArray());
						compType = types.Count > 0 ? types[Mathf.Clamp(cur, 0, types.Count - 1)] : null;
						typeNameProp.stringValue = compType != null ? compType.AssemblyQualifiedName : typeNameProp.stringValue;
					}
					else
					{
						EditorGUILayout.HelpBox("Assign a Preview Scene Object above to choose from its components.", MessageType.None);
						EditorGUILayout.TextField("Component Type", typeNameProp.stringValue);
					}
					
					// Field/Property toggle
					isPropertyProp.boolValue = EditorGUILayout.Toggle(new GUIContent("Use Property (unchecked = Field)"), isPropertyProp.boolValue);
					
					// Member selection (from preview)
					if (compType != null)
					{
						if (isPropertyProp.boolValue)
						{
							var props = compType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
							var writeable = new List<PropertyInfo>();
							foreach (var p in props) if (p.CanWrite) writeable.Add(p);
							var names = writeable.ConvertAll(p => $"{p.Name} : {p.PropertyType.Name}").ToArray();
							int idx = 0;
							var currentName = memberNameProp.stringValue;
							for (int i = 0; i < writeable.Count; i++) if (writeable[i].Name == currentName) { idx = i; break; }
							idx = EditorGUILayout.Popup("Property", idx, names);
							if (writeable.Count > 0)
							{
								var chosen = writeable[Mathf.Clamp(idx, 0, writeable.Count - 1)];
								memberNameProp.stringValue = chosen.Name;
								var vk = GuessValueKindForType(chosen.PropertyType);
								targetMember.FindPropertyRelative("valueKind").enumValueIndex = (int)vk;
							}
						}
						else
						{
							var fields = compType.GetFields(BindingFlags.Instance | BindingFlags.Public);
							var names = new List<string>();
							foreach (var f in fields) names.Add($"{f.Name} : {f.FieldType.Name}");
							int idx = 0; var curName = memberNameProp.stringValue;
							for (int i = 0; i < fields.Length; i++) if (fields[i].Name == curName) { idx = i; break; }
							idx = EditorGUILayout.Popup("Field", idx, names.ToArray());
							if (fields.Length > 0)
							{
								var chosen = fields[Mathf.Clamp(idx, 0, fields.Length - 1)];
								memberNameProp.stringValue = chosen.Name;
								var vk = GuessValueKindForType(chosen.FieldType);
								targetMember.FindPropertyRelative("valueKind").enumValueIndex = (int)vk;
							}
						}
					}
					
					// Show ValueKind (in case user wants to override)
					EditorGUILayout.PropertyField(targetMember.FindPropertyRelative("valueKind"));
				}
			}
		}
		
		void DrawValueField(SerializedProperty targetMember, SerializedProperty value)
		{
			var kind = (ValueKind)targetMember.FindPropertyRelative("valueKind").enumValueIndex;
			using (new EditorGUI.IndentLevelScope())
			{
				switch (kind)
				{
					case ValueKind.Integer:
					var iProp = value.FindPropertyRelative("intValue");
					iProp.intValue = EditorGUILayout.IntField("Integer", iProp.intValue);
					break;
					case ValueKind.Float:
					var fProp = value.FindPropertyRelative("floatValue");
					fProp.floatValue = EditorGUILayout.FloatField("Float", fProp.floatValue);
					break;
					case ValueKind.Bool:
					var bProp = value.FindPropertyRelative("boolValue");
					bProp.boolValue = EditorGUILayout.Toggle("Bool", bProp.boolValue);
					break;
					case ValueKind.String:
					var sProp = value.FindPropertyRelative("stringValue");
					sProp.stringValue = EditorGUILayout.TextField("String", sProp.stringValue);
					break;
					case ValueKind.Vector2:
					var v2 = value.FindPropertyRelative("vector2Value");
					v2.vector2Value = EditorGUILayout.Vector2Field("Vector2", v2.vector2Value);
					break;
					case ValueKind.Vector3:
					var v3 = value.FindPropertyRelative("vector3Value");
					v3.vector3Value = EditorGUILayout.Vector3Field("Vector3", v3.vector3Value);
					break;
					case ValueKind.Vector4:
					var v4 = value.FindPropertyRelative("vector4Value");
					v4.vector4Value = EditorGUILayout.Vector4Field("Vector4", v4.vector4Value);
					break;
					case ValueKind.Color:
					var cProp = value.FindPropertyRelative("colorValue");
					cProp.colorValue = EditorGUILayout.ColorField("Color", cProp.colorValue);
					break;
					case ValueKind.Texture:
					var tProp = value.FindPropertyRelative("textureValue");
					tProp.objectReferenceValue = EditorGUILayout.ObjectField("Texture", tProp.objectReferenceValue, typeof(Texture), false);
					break;
					case ValueKind.AudioClip:
					var aProp = value.FindPropertyRelative("audioClipValue");
					aProp.objectReferenceValue = EditorGUILayout.ObjectField("Audio Clip", aProp.objectReferenceValue, typeof(AudioClip), false);
					break;
				}
			}
		}
		
		static ValueKind GuessValueKindForType(Type t)
		{
			if (t == typeof(int)) return ValueKind.Integer;
			if (t == typeof(float)) return ValueKind.Float;
			if (t == typeof(bool)) return ValueKind.Bool;
			if (t == typeof(string)) return ValueKind.String;
			if (t == typeof(Vector2)) return ValueKind.Vector2;
			if (t == typeof(Vector3)) return ValueKind.Vector3;
			if (t == typeof(Vector4)) return ValueKind.Vector4;
			if (t == typeof(Color)) return ValueKind.Color;
			if (typeof(Texture).IsAssignableFrom(t)) return ValueKind.Texture;
			if (typeof(AudioClip).IsAssignableFrom(t)) return ValueKind.AudioClip;
			return ValueKind.String; // fallback; user can override
		}
		
		static ValueKind GuessValueKindForShader(Shader shader, string propName)
		{
			#if UNITY_EDITOR
			int count = ShaderUtil.GetPropertyCount(shader);
			for (int i = 0; i < count; i++)
			{
				if (ShaderUtil.GetPropertyName(shader, i) == propName)
				{
					var type = ShaderUtil.GetPropertyType(shader, i);
					switch (type)
					{
						case ShaderUtil.ShaderPropertyType.Color: return ValueKind.Color;
						case ShaderUtil.ShaderPropertyType.Vector: return ValueKind.Vector4;
						case ShaderUtil.ShaderPropertyType.Float:
						case ShaderUtil.ShaderPropertyType.Range: return ValueKind.Float;
						case ShaderUtil.ShaderPropertyType.TexEnv: return ValueKind.Texture;
						default: return ValueKind.Float;
					}
				}
			}
			#endif
			return ValueKind.Float;
		}
		
		static string[] GetShaderPropertyNames(Shader shader)
		{
			#if UNITY_EDITOR
			int count = ShaderUtil.GetPropertyCount(shader);
			var list = new string[count];
			for (int i = 0; i < count; i++)
			list[i] = ShaderUtil.GetPropertyName(shader, i);
			if (list.Length == 0) return new[] { "_BaseColor" };
			return list;
			#else
			return new[] { "_BaseColor" };
			#endif
		}
	}
	#endif
}
