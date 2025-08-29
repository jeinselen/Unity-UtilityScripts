using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

public class PropertySetBuilder : MonoBehaviour {
	public enum ParamType {
		Float, Vector2, Vector3, Vector4, Color, Quaternion, Rect,
		Texture2D, Texture3D, Cubemap, AudioClip, Mesh, Material, GameObject, String, Bool, Int
	}
	
	
	[Serializable]
	public class Parameter {
		public string propertyName;
		public ParamType paramType;
		
		// Value storage for different types:
		public float valueFloat;
		public Vector2 valueVector2;
		public Vector3 valueVector3;
		public Vector4 valueVector4;
		public Color valueColor;
		public Quaternion valueQuaternion;
		public Rect valueRect;
		public Texture2D valueTexture2D;
		public Texture3D valueTexture3D;
		public Cubemap valueCubemap;
		public AudioClip valueAudioClip;
		public Mesh valueMesh;
		public Material valueMaterial;
		public GameObject valueGameObject;
		public string valueString;
		public bool valueBool;
		public int valueInt;
	}
	
	[Serializable]
	public class SetTarget {
		public GameObject targetGameObject;
		public Component targetComponent;
		public bool isMaterialProperty;
		public int materialSlot; // which material slot (0, 1, 2, etc.)
		
		public List<Parameter> parameters = new List<Parameter>();
	}
	
	[Serializable]
	public class PropertySet {
		public string name;
		public List<SetTarget> targets = new List<SetTarget>();
	}
	
	public List<PropertySet> propertySets = new List<PropertySet>();
	
	public void ApplyPropertySet(string setName) {
		foreach (var propertySet in propertySets) {
			if (propertySet.name != setName) continue;
			
			foreach (var target in propertySet.targets) {
				// Apply all parameters for this target
				foreach (var param in target.parameters) {
					if (!string.IsNullOrEmpty(param.propertyName)) {
						if (target.isMaterialProperty) {
							// MATERIAL PROPERTIES
							var rend = (target.targetComponent as Renderer)
							?? target.targetGameObject?.GetComponent<Renderer>();
							if (rend != null && rend.materials != null && target.materialSlot < rend.materials.Length) {
								var mat = rend.materials[target.materialSlot];
								if (mat != null) {
									ApplyMaterialParameter(mat, param);
								}
							}
						} else {
							// COMPONENT PROPERTIES
							var comp = target.targetComponent;
							if (comp != null) {
								ApplyComponentParameter(comp, param);
							}
						}
					}
				}
			}
			break;
		}
	}
	
	private void ApplyMaterialParameter(Material mat, Parameter param) {
		switch (param.paramType) {
			case ParamType.Float:
			mat.SetFloat(param.propertyName, param.valueFloat);
			break;
			case ParamType.Color:
			mat.SetColor(param.propertyName, param.valueColor);
			break;
			case ParamType.Vector2:
			mat.SetVector(param.propertyName, param.valueVector2);
			break;
			case ParamType.Vector3:
			mat.SetVector(param.propertyName, param.valueVector3);
			break;
			case ParamType.Vector4:
			mat.SetVector(param.propertyName, param.valueVector4);
			break;
			case ParamType.Texture2D:
			mat.SetTexture(param.propertyName, param.valueTexture2D);
			break;
			case ParamType.Texture3D:
			mat.SetTexture(param.propertyName, param.valueTexture3D);
			break;
			case ParamType.Cubemap:
			mat.SetTexture(param.propertyName, param.valueCubemap);
			break;
			case ParamType.Int:
			mat.SetInt(param.propertyName, param.valueInt);
			break;
		}
	}
	
	private void ApplyComponentParameter(Component comp, Parameter param) {
		Type ct = comp.GetType();
		var fi = ct.GetField(param.propertyName,
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		var pi = (fi == null)
		? ct.GetProperty(param.propertyName,
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
		: null;
		
		if (fi != null || (pi != null && pi.CanWrite)) {
			object value = GetParameterValue(param);
			if (value != null) {
				SetMember(comp, fi, pi, value);
			}
		}
	}
	
	private object GetParameterValue(Parameter param) {
		switch (param.paramType) {
			case ParamType.Float: return param.valueFloat;
			case ParamType.Vector2: return param.valueVector2;
			case ParamType.Vector3: return param.valueVector3;
			case ParamType.Vector4: return param.valueVector4;
			case ParamType.Color: return param.valueColor;
			case ParamType.Quaternion: return param.valueQuaternion;
			case ParamType.Rect: return param.valueRect;
			case ParamType.Texture2D: return param.valueTexture2D;
			case ParamType.Texture3D: return param.valueTexture3D;
			case ParamType.Cubemap: return param.valueCubemap;
			case ParamType.AudioClip: return param.valueAudioClip;
			case ParamType.Mesh: return param.valueMesh;
			case ParamType.Material: return param.valueMaterial;
			case ParamType.GameObject: return param.valueGameObject;
			case ParamType.String: return param.valueString;
			case ParamType.Bool: return param.valueBool;
			case ParamType.Int: return param.valueInt;
			default: return null;
		}
	}
	
	private void SetMember<T>(Component comp, FieldInfo fi, PropertyInfo pi, T value) {
		if (fi != null) fi.SetValue(comp, value);
		else if (pi != null) pi.SetValue(comp, value, null);
	}
}
