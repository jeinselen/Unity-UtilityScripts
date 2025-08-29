// PrimeTweenBuilderEditor.cs
// Initial script: ChatGPT o4-mini-high with research
// Script modifications: Claude 4 Sonnet with extended thinking

using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Rendering;  // for ShaderUtil
using PrimeTween;
using static PrimeTweenBuilder;

[CustomEditor(typeof(PrimeTweenBuilder))]
public class PrimeTweenBuilderEditor : Editor {
	private PrimeTweenBuilder player;
	private ReorderableList clipsList;
	private Dictionary<Clip, ReorderableList> targetLists = new Dictionary<Clip, ReorderableList>();
	private Dictionary<ClipTarget, ReorderableList> parameterLists = new Dictionary<ClipTarget, ReorderableList>();
	
	void OnEnable() {
		player = (PrimeTweenBuilder)target;
		SetupClipsList();
	}
	
	void SetupClipsList() {
		clipsList = new ReorderableList(player.clips, typeof(Clip), true, true, true, true);
		
		clipsList.drawHeaderCallback = (Rect rect) => {
			EditorGUI.LabelField(rect, "Animation Clips");
		};
		
		clipsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
			var clip = player.clips[index];
			rect.y += 2;
			rect.height = EditorGUIUtility.singleLineHeight;
			
			// Clip name only (duration and easing moved to target level)
			clip.name = EditorGUI.TextField(rect, clip.name);
			
			// Targets list
			rect.y += EditorGUIUtility.singleLineHeight + 4;
			rect.height = GetTargetListHeight(clip);
			
			if (!targetLists.ContainsKey(clip)) {
				SetupTargetsList(clip);
			}
			
			targetLists[clip].DoList(rect);
		};
		
		clipsList.elementHeightCallback = (int index) => {
			var clip = player.clips[index];
			float height = EditorGUIUtility.singleLineHeight + 6; // Header line
			height += GetTargetListHeight(clip) + 4; // Targets list
			return height;
		};
		
		clipsList.onAddCallback = (ReorderableList list) => {
			Undo.RecordObject(player, "Add Clip");
			player.clips.Add(new Clip { name = "New Clip" });
			EditorUtility.SetDirty(player);
		};
		
		clipsList.onRemoveCallback = (ReorderableList list) => {
			if (list.index >= 0 && list.index < player.clips.Count) {
				Undo.RecordObject(player, "Remove Clip");
				var clip = player.clips[list.index];
				if (targetLists.ContainsKey(clip)) {
					targetLists.Remove(clip);
				}
				player.clips.RemoveAt(list.index);
				EditorUtility.SetDirty(player);
			}
		};
	}
	
	void SetupTargetsList(Clip clip) {
		var targetList = new ReorderableList(clip.targets, typeof(ClipTarget), true, true, true, true);
		
		targetList.drawHeaderCallback = (Rect rect) => {
			EditorGUI.LabelField(rect, "Targets");
		};
		
		targetList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
			var target = clip.targets[index];
			rect.y += 2;
			
			// First line: GameObject | Component | Complete Existing Tweens Checkbox
			float spacing = 4f;
			float goWidth = rect.width * 0.4f - spacing;
			float compWidth = rect.width * 0.4f - spacing;
			float completeWidth = rect.width * 0.2f;
			
			Rect goRect = new Rect(rect.x, rect.y, goWidth, EditorGUIUtility.singleLineHeight);
			Rect compRect = new Rect(rect.x + goWidth + spacing, rect.y, compWidth, EditorGUIUtility.singleLineHeight);
			Rect completeRect = new Rect(rect.x + goWidth + compWidth + spacing * 2, rect.y, completeWidth, EditorGUIUtility.singleLineHeight);
			
			// GameObject field (no label)
			target.targetGameObject = (GameObject)EditorGUI.ObjectField(goRect, target.targetGameObject, typeof(GameObject), true);
			
			// Component dropdown (no label)
			DrawComponentDropdownNoLabel(compRect, target);
			
			// Complete existing tweens checkbox
			GUIContent completeContent = new GUIContent("replace", "Complete all existing tweens using the global tween tracking system (prevents conflicts from any source)");
			target.completeExistingTweens = EditorGUI.ToggleLeft(completeRect, completeContent, target.completeExistingTweens);
			
			rect.y += EditorGUIUtility.singleLineHeight + 6;
			
			// Second line: Delay | Duration | Ease Type | Ease Dir | On Start | On End
			float delayWidth = rect.width * 0.10f;
			float durationWidth = rect.width * 0.10f;
			float easeTypeWidth = rect.width * 0.15f;
			float easeDirWidth = rect.width * 0.15f;
			float startWidth = rect.width * 0.25f - spacing * 2.5f;
			float endWidth = rect.width * 0.25f - spacing * 2.5f;
			
			Rect delayRect = new Rect(rect.x, rect.y, delayWidth, EditorGUIUtility.singleLineHeight);
			Rect durationRect = new Rect(rect.x + delayWidth + spacing, rect.y, durationWidth, EditorGUIUtility.singleLineHeight);
			Rect easeTypeRect = new Rect(rect.x + delayWidth + durationWidth + spacing * 2, rect.y, easeTypeWidth, EditorGUIUtility.singleLineHeight);
			Rect easeDirRect = new Rect(rect.x + delayWidth + durationWidth + easeTypeWidth + spacing * 3, rect.y, easeDirWidth, EditorGUIUtility.singleLineHeight);
			Rect startRect = new Rect(rect.x + delayWidth + durationWidth + easeTypeWidth + easeDirWidth + spacing * 4, rect.y, startWidth, EditorGUIUtility.singleLineHeight);
			Rect endRect = new Rect(rect.x + delayWidth + durationWidth + easeTypeWidth + easeDirWidth + startWidth + spacing * 5, rect.y, endWidth, EditorGUIUtility.singleLineHeight);
			
			target.delay = Mathf.Max(0f, EditorGUI.FloatField(delayRect, target.delay));
			target.duration = Mathf.Max(0f, EditorGUI.FloatField(durationRect, target.duration));
			
			// Two separate easing dropdowns
			target.easeType = (EaseType)EditorGUI.EnumPopup(easeTypeRect, target.easeType);
			
			// Only show direction dropdown if not Linear
			if (target.easeType == EaseType.Linear) {
				EditorGUI.BeginDisabledGroup(true);
				EditorGUI.EnumPopup(easeDirRect, EaseDirection.InOut);
				EditorGUI.EndDisabledGroup();
			} else {
				target.easeDirection = (EaseDirection)EditorGUI.EnumPopup(easeDirRect, target.easeDirection);
			}
			
			// Activation behaviors with custom labels
			string[] startOptions = { "-", "Enable On Start", "Disable On Start" };
			string[] endOptions = { "-", "Enable On End", "Disable On End" };
			
			int startIndex = (int)target.onStartAction;
			int endIndex = (int)target.onEndAction;
			
			startIndex = EditorGUI.Popup(startRect, startIndex, startOptions);
			endIndex = EditorGUI.Popup(endRect, endIndex, endOptions);
			
			target.onStartAction = (ActivationBehavior)startIndex;
			target.onEndAction = (ActivationBehavior)endIndex;
			
			rect.y += EditorGUIUtility.singleLineHeight + 6;
			
			// Third line: Parameters list
			rect.height = GetParameterListHeight(target);
			if (!parameterLists.ContainsKey(target)) {
				SetupParametersList(target);
			}
			parameterLists[target].DoList(rect);
		};
		
		targetList.elementHeightCallback = (int index) => {
			var target = clip.targets[index];
			float height = EditorGUIUtility.singleLineHeight + 8; // First line for GameObject and Component
			height += EditorGUIUtility.singleLineHeight + 6; // Second line for delay, duration, easing, and activation behaviors
			height += GetParameterListHeight(target) + 6; // Third line: Parameters list
			return height;
		};
		
		targetList.onAddCallback = (ReorderableList list) => {
			Undo.RecordObject(player, "Add Target");
			var newTarget = new ClipTarget {
				easeType = EaseType.Linear,
				easeDirection = EaseDirection.InOut,
				duration = 1f, // Set sensible default duration
				completeExistingTweens = true // Default to true
			};
			clip.targets.Add(newTarget);
			EditorUtility.SetDirty(player);
		};
		
		targetList.onRemoveCallback = (ReorderableList list) => {
			if (list.index >= 0 && list.index < clip.targets.Count) {
				Undo.RecordObject(player, "Remove Target");
				var target = clip.targets[list.index];
				if (parameterLists.ContainsKey(target)) {
					parameterLists.Remove(target);
				}
				clip.targets.RemoveAt(list.index);
				EditorUtility.SetDirty(player);
			}
		};
		
		targetLists[clip] = targetList;
	}
	
	void SetupParametersList(ClipTarget target) {
		var paramList = new ReorderableList(target.parameters, typeof(Parameter), true, true, true, true);
		
		paramList.drawHeaderCallback = (Rect rect) => {
			EditorGUI.LabelField(rect, "Parameters");
		};
		
		paramList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
			var param = target.parameters[index];
			rect.y += 2;
			rect.height = EditorGUIUtility.singleLineHeight;
			
			// Property selection and target value on same line
			float spacing = 4f;
			float propWidth = rect.width * 0.4f;
			float valueWidth = rect.width * 0.6f - spacing;
			
			Rect propRect = new Rect(rect.x, rect.y, propWidth, rect.height);
			Rect valueRect = new Rect(rect.x + propWidth + spacing, rect.y, valueWidth, rect.height);
			
			// Property selection (no label)
			DrawPropertyDropdownNoLabel(propRect, target, param);
			
			// Target value (no label)
			DrawEndValueFieldNoLabel(valueRect, param);
		};
		
		paramList.elementHeightCallback = (int index) => {
			return EditorGUIUtility.singleLineHeight + 4; // Single line for property + value
		};
		
		paramList.onAddCallback = (ReorderableList list) => {
			Undo.RecordObject(player, "Add Parameter");
			target.parameters.Add(new Parameter());
			EditorUtility.SetDirty(player);
		};
		
		paramList.onRemoveCallback = (ReorderableList list) => {
			if (list.index >= 0 && list.index < target.parameters.Count) {
				Undo.RecordObject(player, "Remove Parameter");
				target.parameters.RemoveAt(list.index);
				EditorUtility.SetDirty(player);
			}
		};
		
		parameterLists[target] = paramList;
	}
	
	void DrawComponentDropdown(Rect rect, ClipTarget target) {
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
		
		int newSelection = EditorGUI.Popup(rect, "Component", curSelection, compOptions.ToArray());
		if (newSelection != curSelection) {
			Undo.RecordObject(player, "Change Component");
			target.targetComponent = compRefs[newSelection];
			target.isMaterialProperty = isMaterialFlags[newSelection];
			target.materialSlot = materialSlots[newSelection];
			// Clear parameters when component changes
			target.parameters.Clear();
			// Remove old parameter list
			if (parameterLists.ContainsKey(target)) {
				parameterLists.Remove(target);
			}
			EditorUtility.SetDirty(player);
		}
	}
	
	void DrawComponentDropdownNoLabel(Rect rect, ClipTarget target) {
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
			Undo.RecordObject(player, "Change Component");
			target.targetComponent = compRefs[newSelection];
			target.isMaterialProperty = isMaterialFlags[newSelection];
			target.materialSlot = materialSlots[newSelection];
			// Clear parameters when component changes
			target.parameters.Clear();
			// Remove old parameter list
			if (parameterLists.ContainsKey(target)) {
				parameterLists.Remove(target);
			}
			EditorUtility.SetDirty(player);
		}
	}
	
	void DrawPropertyDropdown(Rect rect, ClipTarget target, Parameter param) {
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
							if (pt == ShaderUtil.ShaderPropertyType.Color ||
								pt == ShaderUtil.ShaderPropertyType.Vector ||
								pt == ShaderUtil.ShaderPropertyType.Float ||
								pt == ShaderUtil.ShaderPropertyType.Range) {
									matProps.Add(ShaderUtil.GetPropertyName(mat.shader, pi));
								}
						}
						
						// Add "None" option at the beginning
						matProps.Insert(0, "None");
						
						int cur = string.IsNullOrEmpty(param.propertyName) ? 0 : matProps.IndexOf(param.propertyName);
						if (cur < 0) cur = 0; // Fallback if property not found
						
						int sel = EditorGUI.Popup(rect, "Material Property", cur, matProps.ToArray());
						if (sel != cur) {
							Undo.RecordObject(player, "Change Material Property");
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
							EditorUtility.SetDirty(player);
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
			
			int selP = EditorGUI.Popup(rect, "Property", curP, names.ToArray());
			if (selP != curP) {
				Undo.RecordObject(player, "Change Property");
				if (selP == 0) {
					param.propertyName = "";
				} else {
					param.propertyName = names[selP];
					param.paramType = types[selP];
				}
				EditorUtility.SetDirty(player);
			}
		}
	}
	
	void DrawPropertyDropdownNoLabel(Rect rect, ClipTarget target, Parameter param) {
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
							if (pt == ShaderUtil.ShaderPropertyType.Color ||
								pt == ShaderUtil.ShaderPropertyType.Vector ||
								pt == ShaderUtil.ShaderPropertyType.Float ||
								pt == ShaderUtil.ShaderPropertyType.Range) {
									matProps.Add(ShaderUtil.GetPropertyName(mat.shader, pi));
								}
						}
						
						// Add "None" option at the beginning
						matProps.Insert(0, "None");
						
						int cur = string.IsNullOrEmpty(param.propertyName) ? 0 : matProps.IndexOf(param.propertyName);
						if (cur < 0) cur = 0; // Fallback if property not found
						
						int sel = EditorGUI.Popup(rect, cur, matProps.ToArray());
						if (sel != cur) {
							Undo.RecordObject(player, "Change Material Property");
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
							EditorUtility.SetDirty(player);
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
				Undo.RecordObject(player, "Change Property");
				if (selP == 0) {
					param.propertyName = "";
				} else {
					param.propertyName = names[selP];
					param.paramType = types[selP];
				}
				EditorUtility.SetDirty(player);
			}
		}
	}
	
	float GetTargetListHeight(Clip clip) {
		if (!targetLists.ContainsKey(clip)) return 60f;
		return targetLists[clip].GetHeight();
	}
	
	float GetParameterListHeight(ClipTarget target) {
		if (!parameterLists.ContainsKey(target)) return 60f;
		return parameterLists[target].GetHeight();
	}
	
	float GetValueFieldHeight(ParamType paramType) {
		switch (paramType) {
			case ParamType.Vector3:
			case ParamType.Vector4:
			case ParamType.Quaternion:
			case ParamType.Rect:
			return EditorGUIUtility.singleLineHeight + 16; // Multi-line fields
			default:
			return EditorGUIUtility.singleLineHeight;
		}
	}
	
	public override void OnInspectorGUI() {
		if (player.clips == null) player.clips = new List<Clip>();
		
		if (clipsList == null) SetupClipsList();
		clipsList.DoLayoutList();
	}
	
	private bool IsSupported(Type t) {
		return t == typeof(float) || t == typeof(Vector2) ||
		t == typeof(Vector3) || t == typeof(Vector4) ||
		t == typeof(Color) || t == typeof(Quaternion) ||
		t == typeof(Rect);
	}
	
	private ParamType MapType(Type t) {
		if (t == typeof(float))     return ParamType.Float;
		if (t == typeof(Vector2))   return ParamType.Vector2;
		if (t == typeof(Vector3))   return ParamType.Vector3;
		if (t == typeof(Vector4))   return ParamType.Vector4;
		if (t == typeof(Color))     return ParamType.Color;
		if (t == typeof(Quaternion))return ParamType.Quaternion;
		if (t == typeof(Rect))      return ParamType.Rect;
		return ParamType.Float;
	}
	
	private ParamType MapShaderType(ShaderUtil.ShaderPropertyType pt) {
		switch (pt) {
			case ShaderUtil.ShaderPropertyType.Color:  return ParamType.Color;
			case ShaderUtil.ShaderPropertyType.Vector: return ParamType.Vector4;
			case ShaderUtil.ShaderPropertyType.Float:
			case ShaderUtil.ShaderPropertyType.Range:  return ParamType.Float;
			default: return ParamType.Float;
		}
	}
	
	private void DrawEndValueField(Rect rect, Parameter param) {
		switch (param.paramType) {
			case ParamType.Float:
			param.endFloat = EditorGUI.FloatField(rect, "Target Value", param.endFloat);
			break;
			case ParamType.Vector2:
			param.endVector2 = EditorGUI.Vector2Field(rect, "Target Value", param.endVector2);
			break;
			case ParamType.Vector3:
			param.endVector3 = EditorGUI.Vector3Field(rect, "Target Value", param.endVector3);
			break;
			case ParamType.Vector4:
			param.endVector4 = EditorGUI.Vector4Field(rect, "Target Value", param.endVector4);
			break;
			case ParamType.Color:
			param.endColor = EditorGUI.ColorField(rect, "Target Value", param.endColor);
			break;
			case ParamType.Quaternion:
			Vector3 euler = EditorGUI.Vector3Field(rect, "Target Value (Euler)", param.endQuaternion.eulerAngles);
			param.endQuaternion = Quaternion.Euler(euler);
			break;
			case ParamType.Rect:
			param.endRect = EditorGUI.RectField(rect, "Target Value", param.endRect);
			break;
		}
	}
	
	private void DrawEndValueFieldNoLabel(Rect rect, Parameter param) {
		switch (param.paramType) {
			case ParamType.Float:
			param.endFloat = EditorGUI.FloatField(rect, param.endFloat);
			break;
			case ParamType.Vector2:
			// Use compact single-line format
			float spacing = 2f;
			float fieldWidth = (rect.width - spacing) * 0.5f;
			Rect xRect = new Rect(rect.x, rect.y, fieldWidth, rect.height);
			Rect yRect = new Rect(rect.x + fieldWidth + spacing, rect.y, fieldWidth, rect.height);
			param.endVector2.x = EditorGUI.FloatField(xRect, param.endVector2.x);
			param.endVector2.y = EditorGUI.FloatField(yRect, param.endVector2.y);
			break;
			case ParamType.Vector3:
			// Use compact single-line format
			float spacing3 = 2f;
			float fieldWidth3 = (rect.width - spacing3 * 2) / 3f;
			Rect xRect3 = new Rect(rect.x, rect.y, fieldWidth3, rect.height);
			Rect yRect3 = new Rect(rect.x + fieldWidth3 + spacing3, rect.y, fieldWidth3, rect.height);
			Rect zRect3 = new Rect(rect.x + (fieldWidth3 + spacing3) * 2, rect.y, fieldWidth3, rect.height);
			param.endVector3.x = EditorGUI.FloatField(xRect3, param.endVector3.x);
			param.endVector3.y = EditorGUI.FloatField(yRect3, param.endVector3.y);
			param.endVector3.z = EditorGUI.FloatField(zRect3, param.endVector3.z);
			break;
			case ParamType.Vector4:
			// Use compact single-line format
			float spacing4 = 1f;
			float fieldWidth4 = (rect.width - spacing4 * 3) / 4f;
			Rect xRect4 = new Rect(rect.x, rect.y, fieldWidth4, rect.height);
			Rect yRect4 = new Rect(rect.x + fieldWidth4 + spacing4, rect.y, fieldWidth4, rect.height);
			Rect zRect4 = new Rect(rect.x + (fieldWidth4 + spacing4) * 2, rect.y, fieldWidth4, rect.height);
			Rect wRect4 = new Rect(rect.x + (fieldWidth4 + spacing4) * 3, rect.y, fieldWidth4, rect.height);
			param.endVector4.x = EditorGUI.FloatField(xRect4, param.endVector4.x);
			param.endVector4.y = EditorGUI.FloatField(yRect4, param.endVector4.y);
			param.endVector4.z = EditorGUI.FloatField(zRect4, param.endVector4.z);
			param.endVector4.w = EditorGUI.FloatField(wRect4, param.endVector4.w);
			break;
			case ParamType.Color:
			param.endColor = EditorGUI.ColorField(rect, param.endColor);
			break;
			case ParamType.Quaternion:
			// Use compact single-line format for euler angles
			Vector3 euler = param.endQuaternion.eulerAngles;
			float spacingQ = 2f;
			float fieldWidthQ = (rect.width - spacingQ * 2) / 3f;
			Rect xRectQ = new Rect(rect.x, rect.y, fieldWidthQ, rect.height);
			Rect yRectQ = new Rect(rect.x + fieldWidthQ + spacingQ, rect.y, fieldWidthQ, rect.height);
			Rect zRectQ = new Rect(rect.x + (fieldWidthQ + spacingQ) * 2, rect.y, fieldWidthQ, rect.height);
			euler.x = EditorGUI.FloatField(xRectQ, euler.x);
			euler.y = EditorGUI.FloatField(yRectQ, euler.y);
			euler.z = EditorGUI.FloatField(zRectQ, euler.z);
			param.endQuaternion = Quaternion.Euler(euler);
			break;
			case ParamType.Rect:
			// Use compact single-line format
			float spacingR = 1f;
			float fieldWidthR = (rect.width - spacingR * 3) / 4f;
			Rect xRectR = new Rect(rect.x, rect.y, fieldWidthR, rect.height);
			Rect yRectR = new Rect(rect.x + fieldWidthR + spacingR, rect.y, fieldWidthR, rect.height);
			Rect wRectR = new Rect(rect.x + (fieldWidthR + spacingR) * 2, rect.y, fieldWidthR, rect.height);
			Rect hRectR = new Rect(rect.x + (fieldWidthR + spacingR) * 3, rect.y, fieldWidthR, rect.height);
			param.endRect.x = EditorGUI.FloatField(xRectR, param.endRect.x);
			param.endRect.y = EditorGUI.FloatField(yRectR, param.endRect.y);
			param.endRect.width = EditorGUI.FloatField(wRectR, param.endRect.width);
			param.endRect.height = EditorGUI.FloatField(hRectR, param.endRect.height);
			break;
		}
	}
}