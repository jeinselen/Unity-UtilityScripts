using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Rendering;  // for ShaderUtil
using static PropertySetBuilder;

[CustomEditor(typeof(PropertySetBuilder))]
public class PropertySetBuilderEditor : Editor {
	private PropertySetBuilder builder;
	private ReorderableList setsList;
	private Dictionary<PropertySet, ReorderableList> targetLists = new Dictionary<PropertySet, ReorderableList>();
	private Dictionary<SetTarget, ReorderableList> parameterLists = new Dictionary<SetTarget, ReorderableList>();
	
	void OnEnable() {
		builder = (PropertySetBuilder)target;
		SetupSetsList();
	}
	
	void SetupSetsList() {
		setsList = new ReorderableList(builder.propertySets, typeof(PropertySet), true, true, true, true);
		
		setsList.drawHeaderCallback = (Rect rect) => {
			EditorGUI.LabelField(rect, "Property Sets");
		};
		
		setsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
			var propertySet = builder.propertySets[index];
			rect.y += 2;
			rect.height = EditorGUIUtility.singleLineHeight;
			
			// Property set name
			propertySet.name = EditorGUI.TextField(rect, propertySet.name);
			
			// Targets list
			rect.y += EditorGUIUtility.singleLineHeight + 4;
			rect.height = GetTargetListHeight(propertySet);
			
			if (!targetLists.ContainsKey(propertySet)) {
				SetupTargetsList(propertySet);
			}
			
			targetLists[propertySet].DoList(rect);
		};
		
		setsList.elementHeightCallback = (int index) => {
			var propertySet = builder.propertySets[index];
			float height = EditorGUIUtility.singleLineHeight + 6; // Header line
			height += GetTargetListHeight(propertySet) + 4; // Targets list
			return height;
		};
		
		setsList.onAddCallback = (ReorderableList list) => {
			Undo.RecordObject(builder, "Add Property Set");
			builder.propertySets.Add(new PropertySet { name = "New Property Set" });
			EditorUtility.SetDirty(builder);
		};
		
		setsList.onRemoveCallback = (ReorderableList list) => {
			if (list.index >= 0 && list.index < builder.propertySets.Count) {
				Undo.RecordObject(builder, "Remove Property Set");
				var propertySet = builder.propertySets[list.index];
				if (targetLists.ContainsKey(propertySet)) {
					targetLists.Remove(propertySet);
				}
				builder.propertySets.RemoveAt(list.index);
				EditorUtility.SetDirty(builder);
			}
		};
	}
	
	void SetupTargetsList(PropertySet propertySet) {
		var targetList = new ReorderableList(propertySet.targets, typeof(SetTarget), true, true, true, true);
		
		targetList.drawHeaderCallback = (Rect rect) => {
			EditorGUI.LabelField(rect, "Targets");
		};
		
		targetList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
			var target = propertySet.targets[index];
			rect.y += 2;
			
			// First line: GameObject | Component
			float spacing = 4f;
			float goWidth = rect.width * 0.5f - spacing;
			float compWidth = rect.width * 0.5f;
			
			Rect goRect = new Rect(rect.x, rect.y, goWidth, EditorGUIUtility.singleLineHeight);
			Rect compRect = new Rect(rect.x + goWidth + spacing, rect.y, compWidth, EditorGUIUtility.singleLineHeight);
			
			// GameObject field
			target.targetGameObject = (GameObject)EditorGUI.ObjectField(goRect, target.targetGameObject, typeof(GameObject), true);
			
			// Component dropdown
			DrawComponentDropdownNoLabel(compRect, target);
			
			rect.y += EditorGUIUtility.singleLineHeight + 6;
			
			// Second line: Parameters list
			rect.height = GetParameterListHeight(target);
			if (!parameterLists.ContainsKey(target)) {
				SetupParametersList(target);
			}
			parameterLists[target].DoList(rect);
		};
		
		targetList.elementHeightCallback = (int index) => {
			var target = propertySet.targets[index];
			float height = EditorGUIUtility.singleLineHeight + 8; // First line for GameObject and Component
			height += GetParameterListHeight(target) + 6; // Second line: Parameters list
			return height;
		};
		
		targetList.onAddCallback = (ReorderableList list) => {
			Undo.RecordObject(builder, "Add Target");
			var newTarget = new SetTarget();
			propertySet.targets.Add(newTarget);
			EditorUtility.SetDirty(builder);
		};
		
		targetList.onRemoveCallback = (ReorderableList list) => {
			if (list.index >= 0 && list.index < propertySet.targets.Count) {
				Undo.RecordObject(builder, "Remove Target");
				var target = propertySet.targets[list.index];
				if (parameterLists.ContainsKey(target)) {
					parameterLists.Remove(target);
				}
				propertySet.targets.RemoveAt(list.index);
				EditorUtility.SetDirty(builder);
			}
		};
		
		targetLists[propertySet] = targetList;
	}
	
	void SetupParametersList(SetTarget target) {
		var paramList = new ReorderableList(target.parameters, typeof(Parameter), true, true, true, true);
		
		paramList.drawHeaderCallback = (Rect rect) => {
			EditorGUI.LabelField(rect, "Parameters");
		};
		
		paramList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
			var param = target.parameters[index];
			rect.y += 2;
			rect.height = EditorGUIUtility.singleLineHeight;
			
			// Property selection and value on same line
			float spacing = 4f;
			float propWidth = rect.width * 0.4f;
			float valueWidth = rect.width * 0.6f - spacing;
			
			Rect propRect = new Rect(rect.x, rect.y, propWidth, rect.height);
			Rect valueRect = new Rect(rect.x + propWidth + spacing, rect.y, valueWidth, rect.height);
			
			// Property selection
			DrawPropertyDropdownNoLabel(propRect, target, param);
			
			// Value field
			DrawValueFieldNoLabel(valueRect, param);
		};
		
		paramList.elementHeightCallback = (int index) => {
			return EditorGUIUtility.singleLineHeight + 4; // Single line for property + value
		};
		
		paramList.onAddCallback = (ReorderableList list) => {
			Undo.RecordObject(builder, "Add Parameter");
			target.parameters.Add(new Parameter());
			EditorUtility.SetDirty(builder);
		};
		
		paramList.onRemoveCallback = (ReorderableList list) => {
			if (list.index >= 0 && list.index < target.parameters.Count) {
				Undo.RecordObject(builder, "Remove Parameter");
				target.parameters.RemoveAt(list.index);
				EditorUtility.SetDirty(builder);
			}
		};
		
		parameterLists[target] = paramList;
	}
	
	void DrawComponentDropdownNoLabel(Rect rect, SetTarget target) {
		// Build component list including materials
		var compOptions = new List<string> { "None" };
		var compRefs = new List<Component> { null };
		var materialSlots = new List<int> { -1 };
		var isMaterialFlags = new List<bool> { false };
		
		if (target.targetGameObject != null) {
			Component[] comps = target.targetGameObject.GetComponents<Component>();
			
			// Add regular components
			for (int ci = 0; ci < comps.Length; ci++) {
				if (comps[ci] != null) {
					compOptions.Add(comps[ci].GetType().Name);
					compRefs.Add(comps[ci]);
					materialSlots.Add(-1);
					isMaterialFlags.Add(false);
					
					// If it's a Renderer, also add its materials
					if (comps[ci] is Renderer rend && rend.sharedMaterials != null) {
						for (int mi = 0; mi < rend.sharedMaterials.Length; mi++) {
							var mat = rend.sharedMaterials[mi];
							string matName = mat != null ? mat.name : "null";
							compOptions.Add($"Material {mi}: {matName}");
							compRefs.Add(rend);
							materialSlots.Add(mi);
							isMaterialFlags.Add(true);
						}
					}
				}
			}
		}
		
		// Find current selection
		int curSelection = 0;
		if (target.targetComponent != null) {
			for (int si = 1; si < compRefs.Count; si++) {
				if (compRefs[si] == target.targetComponent &&
					isMaterialFlags[si] == target.isMaterialProperty &&
					(!target.isMaterialProperty || materialSlots[si] == target.materialSlot)) {
						curSelection = si;
						break;
					}
			}
		}
		
		int newSelection = EditorGUI.Popup(rect, curSelection, compOptions.ToArray());
		if (newSelection != curSelection) {
			Undo.RecordObject(builder, "Change Component");
			target.targetComponent = compRefs[newSelection];
			target.isMaterialProperty = isMaterialFlags[newSelection];
			target.materialSlot = materialSlots[newSelection];
			// Clear parameters when component changes
			target.parameters.Clear();
			// Remove old parameter list
			if (parameterLists.ContainsKey(target)) {
				parameterLists.Remove(target);
			}
			EditorUtility.SetDirty(builder);
		}
	}
	
	void DrawPropertyDropdownNoLabel(Rect rect, SetTarget target, Parameter param) {
		if (target.targetComponent == null) return;
		
		if (target.isMaterialProperty) {
			// MATERIAL PROPERTIES
			var rend = target.targetComponent as Renderer;
			if (rend != null && rend.sharedMaterials != null &&
				target.materialSlot >= 0 && target.materialSlot < rend.sharedMaterials.Length) {
					var mat = rend.sharedMaterials[target.materialSlot];
					if (mat != null) {
						int count = ShaderUtil.GetPropertyCount(mat.shader);
						var matProps = new List<string>();
						for (int pi = 0; pi < count; pi++) {
							var pt = ShaderUtil.GetPropertyType(mat.shader, pi);
							// Include all shader property types, including textures
							matProps.Add(ShaderUtil.GetPropertyName(mat.shader, pi));
						}
						
						// Add "None" option at the beginning
						matProps.Insert(0, "None");
						
						int cur = string.IsNullOrEmpty(param.propertyName) ? 0 : matProps.IndexOf(param.propertyName);
						if (cur < 0) cur = 0; // Fallback if property not found
						
						int sel = EditorGUI.Popup(rect, cur, matProps.ToArray());
						if (sel != cur) {
							Undo.RecordObject(builder, "Change Material Property");
							if (sel == 0) {
								param.propertyName = "";
							} else {
								param.propertyName = matProps[sel];
								// Find the property index to get its type
								for (int pi = 0; pi < count; pi++) {
									if (ShaderUtil.GetPropertyName(mat.shader, pi) == param.propertyName) {
										param.paramType = MapShaderType(ShaderUtil.GetPropertyType(mat.shader, pi));
										break;
									}
								}
							}
							EditorUtility.SetDirty(builder);
						}
					}
				}
		} else {
			// COMPONENT PROPERTIES
			Type ct = target.targetComponent.GetType();
			var names = new List<string> { "None" }; // Add "None" option
			var types = new List<ParamType> { ParamType.Float }; // Default type for "None"
			
			// public & [SerializeField] fields
			foreach (var f in ct.GetFields(
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
					if ((f.IsPublic || Attribute.IsDefined(f, typeof(SerializeField))) &&
						IsSupported(f.FieldType)) {
							names.Add(f.Name);
							types.Add(MapType(f.FieldType));
						}
				}
			// public properties
			foreach (var prop in ct.GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
				if (prop.CanWrite && prop.GetIndexParameters().Length == 0 &&
					IsSupported(prop.PropertyType)) {
						names.Add(prop.Name);
						types.Add(MapType(prop.PropertyType));
					}
			}
			
			int curP = string.IsNullOrEmpty(param.propertyName) ? 0 : names.IndexOf(param.propertyName);
			if (curP < 0) curP = 0; // Fallback if property not found
			
			int selP = EditorGUI.Popup(rect, curP, names.ToArray());
			if (selP != curP) {
				Undo.RecordObject(builder, "Change Property");
				if (selP == 0) {
					param.propertyName = "";
				} else {
					param.propertyName = names[selP];
					param.paramType = types[selP];
				}
				EditorUtility.SetDirty(builder);
			}
		}
	}
	
	float GetTargetListHeight(PropertySet propertySet) {
		if (!targetLists.ContainsKey(propertySet)) return 60f;
		return targetLists[propertySet].GetHeight();
	}
	
	float GetParameterListHeight(SetTarget target) {
		if (!parameterLists.ContainsKey(target)) return 60f;
		return parameterLists[target].GetHeight();
	}
	
	public override void OnInspectorGUI() {
		if (builder.propertySets == null) builder.propertySets = new List<PropertySet>();
		
		if (setsList == null) SetupSetsList();
		setsList.DoLayoutList();
	}
	
	private bool IsSupported(Type t) {
		return t == typeof(float) || t == typeof(Vector2) ||
		t == typeof(Vector3) || t == typeof(Vector4) ||
		t == typeof(Color) || t == typeof(Quaternion) ||
		t == typeof(Rect) || t == typeof(Texture2D) ||
		t == typeof(Texture3D) || t == typeof(Cubemap) ||
		t == typeof(AudioClip) || t == typeof(Mesh) ||
		t == typeof(Material) || t == typeof(GameObject) ||
		t == typeof(string) || t == typeof(bool) || t == typeof(int);
	}
	
	private ParamType MapType(Type t) {
		if (t == typeof(float)) return ParamType.Float;
		if (t == typeof(Vector2)) return ParamType.Vector2;
		if (t == typeof(Vector3)) return ParamType.Vector3;
		if (t == typeof(Vector4)) return ParamType.Vector4;
		if (t == typeof(Color)) return ParamType.Color;
		if (t == typeof(Quaternion)) return ParamType.Quaternion;
		if (t == typeof(Rect)) return ParamType.Rect;
		if (t == typeof(Texture2D)) return ParamType.Texture2D;
		if (t == typeof(Texture3D)) return ParamType.Texture3D;
		if (t == typeof(Cubemap)) return ParamType.Cubemap;
		if (t == typeof(AudioClip)) return ParamType.AudioClip;
		if (t == typeof(Mesh)) return ParamType.Mesh;
		if (t == typeof(Material)) return ParamType.Material;
		if (t == typeof(GameObject)) return ParamType.GameObject;
		if (t == typeof(string)) return ParamType.String;
		if (t == typeof(bool)) return ParamType.Bool;
		if (t == typeof(int)) return ParamType.Int;
		return ParamType.Float;
	}
	
	private ParamType MapShaderType(ShaderUtil.ShaderPropertyType pt) {
		switch (pt) {
			case ShaderUtil.ShaderPropertyType.Color: return ParamType.Color;
			case ShaderUtil.ShaderPropertyType.Vector: return ParamType.Vector4;
			case ShaderUtil.ShaderPropertyType.Float:
			case ShaderUtil.ShaderPropertyType.Range: return ParamType.Float;
			case ShaderUtil.ShaderPropertyType.TexEnv: return ParamType.Texture2D;
			case ShaderUtil.ShaderPropertyType.Int: return ParamType.Int;
			default: return ParamType.Float;
		}
	}
	
	private void DrawValueFieldNoLabel(Rect rect, Parameter param) {
		switch (param.paramType) {
			case ParamType.Float:
			param.valueFloat = EditorGUI.FloatField(rect, param.valueFloat);
			break;
			case ParamType.Vector2:
			// Use compact single-line format
			float spacing = 2f;
			float fieldWidth = (rect.width - spacing) * 0.5f;
			Rect xRect = new Rect(rect.x, rect.y, fieldWidth, rect.height);
			Rect yRect = new Rect(rect.x + fieldWidth + spacing, rect.y, fieldWidth, rect.height);
			param.valueVector2.x = EditorGUI.FloatField(xRect, param.valueVector2.x);
			param.valueVector2.y = EditorGUI.FloatField(yRect, param.valueVector2.y);
			break;
			case ParamType.Vector3:
			// Use compact single-line format
			float spacing3 = 2f;
			float fieldWidth3 = (rect.width - spacing3 * 2) / 3f;
			Rect xRect3 = new Rect(rect.x, rect.y, fieldWidth3, rect.height);
			Rect yRect3 = new Rect(rect.x + fieldWidth3 + spacing3, rect.y, fieldWidth3, rect.height);
			Rect zRect3 = new Rect(rect.x + (fieldWidth3 + spacing3) * 2, rect.y, fieldWidth3, rect.height);
			param.valueVector3.x = EditorGUI.FloatField(xRect3, param.valueVector3.x);
			param.valueVector3.y = EditorGUI.FloatField(yRect3, param.valueVector3.y);
			param.valueVector3.z = EditorGUI.FloatField(zRect3, param.valueVector3.z);
			break;
			case ParamType.Vector4:
			// Use compact single-line format
			float spacing4 = 1f;
			float fieldWidth4 = (rect.width - spacing4 * 3) / 4f;
			Rect xRect4 = new Rect(rect.x, rect.y, fieldWidth4, rect.height);
			Rect yRect4 = new Rect(rect.x + fieldWidth4 + spacing4, rect.y, fieldWidth4, rect.height);
			Rect zRect4 = new Rect(rect.x + (fieldWidth4 + spacing4) * 2, rect.y, fieldWidth4, rect.height);
			Rect wRect4 = new Rect(rect.x + (fieldWidth4 + spacing4) * 3, rect.y, fieldWidth4, rect.height);
			param.valueVector4.x = EditorGUI.FloatField(xRect4, param.valueVector4.x);
			param.valueVector4.y = EditorGUI.FloatField(yRect4, param.valueVector4.y);
			param.valueVector4.z = EditorGUI.FloatField(zRect4, param.valueVector4.z);
			param.valueVector4.w = EditorGUI.FloatField(wRect4, param.valueVector4.w);
			break;
			case ParamType.Color:
			param.valueColor = EditorGUI.ColorField(rect, param.valueColor);
			break;
			case ParamType.Quaternion:
			// Use compact single-line format for euler angles
			Vector3 euler = param.valueQuaternion.eulerAngles;
			float spacingQ = 2f;
			float fieldWidthQ = (rect.width - spacingQ * 2) / 3f;
			Rect xRectQ = new Rect(rect.x, rect.y, fieldWidthQ, rect.height);
			Rect yRectQ = new Rect(rect.x + fieldWidthQ + spacingQ, rect.y, fieldWidthQ, rect.height);
			Rect zRectQ = new Rect(rect.x + (fieldWidthQ + spacingQ) * 2, rect.y, fieldWidthQ, rect.height);
			euler.x = EditorGUI.FloatField(xRectQ, euler.x);
			euler.y = EditorGUI.FloatField(yRectQ, euler.y);
			euler.z = EditorGUI.FloatField(zRectQ, euler.z);
			param.valueQuaternion = Quaternion.Euler(euler);
			break;
			case ParamType.Rect:
			// Use compact single-line format
			float spacingR = 1f;
			float fieldWidthR = (rect.width - spacingR * 3) / 4f;
			Rect xRectR = new Rect(rect.x, rect.y, fieldWidthR, rect.height);
			Rect yRectR = new Rect(rect.x + fieldWidthR + spacingR, rect.y, fieldWidthR, rect.height);
			Rect wRectR = new Rect(rect.x + (fieldWidthR + spacingR) * 2, rect.y, fieldWidthR, rect.height);
			Rect hRectR = new Rect(rect.x + (fieldWidthR + spacingR) * 3, rect.y, fieldWidthR, rect.height);
			param.valueRect.x = EditorGUI.FloatField(xRectR, param.valueRect.x);
			param.valueRect.y = EditorGUI.FloatField(yRectR, param.valueRect.y);
			param.valueRect.width = EditorGUI.FloatField(wRectR, param.valueRect.width);
			param.valueRect.height = EditorGUI.FloatField(hRectR, param.valueRect.height);
			break;
			case ParamType.Texture2D:
			param.valueTexture2D = (Texture2D)EditorGUI.ObjectField(rect, param.valueTexture2D, typeof(Texture2D), false);
			break;
			case ParamType.Texture3D:
			param.valueTexture3D = (Texture3D)EditorGUI.ObjectField(rect, param.valueTexture3D, typeof(Texture3D), false);
			break;
			case ParamType.Cubemap:
			param.valueCubemap = (Cubemap)EditorGUI.ObjectField(rect, param.valueCubemap, typeof(Cubemap), false);
			break;
			case ParamType.AudioClip:
			param.valueAudioClip = (AudioClip)EditorGUI.ObjectField(rect, param.valueAudioClip, typeof(AudioClip), false);
			break;
			case ParamType.Mesh:
			param.valueMesh = (Mesh)EditorGUI.ObjectField(rect, param.valueMesh, typeof(Mesh), false);
			break;
			case ParamType.Material:
			param.valueMaterial = (Material)EditorGUI.ObjectField(rect, param.valueMaterial, typeof(Material), false);
			break;
			case ParamType.GameObject:
			param.valueGameObject = (GameObject)EditorGUI.ObjectField(rect, param.valueGameObject, typeof(GameObject), true);
			break;
			case ParamType.String:
			param.valueString = EditorGUI.TextField(rect, param.valueString);
			break;
			case ParamType.Bool:
			param.valueBool = EditorGUI.Toggle(rect, param.valueBool);
			break;
			case ParamType.Int:
			param.valueInt = EditorGUI.IntField(rect, param.valueInt);
			break;
		}
	}
}
