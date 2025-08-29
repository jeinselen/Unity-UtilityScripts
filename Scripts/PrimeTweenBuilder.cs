// PrimeTweenBuilder.cs
// Initial script: ChatGPT o4-mini-high with research
// Script modifications: Claude 4 Sonnet with extended thinking
// Updated: Fixed easing bug and other issues

using UnityEngine;
//using UnityEngine.Rendering;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using PrimeTween;

/// <summary>
/// Global singleton that tracks all active tweens across all scripts and systems
/// Ensures comprehensive conflict prevention regardless of tween source
/// </summary>
public class GlobalTweenManager : MonoBehaviour {
	private static GlobalTweenManager _instance;
	public static GlobalTweenManager Instance {
		get {
			if (_instance == null) {
				_instance = FindFirstObjectByType<GlobalTweenManager>();
				if (_instance == null) {
					GameObject go = new GameObject("GlobalTweenManager");
					_instance = go.AddComponent<GlobalTweenManager>();
					DontDestroyOnLoad(go);
				}
			}
			return _instance;
		}
	}
	
	// Track all active tweens by their targets
	private Dictionary<GameObject, HashSet<Tween>> gameObjectTweens = new Dictionary<GameObject, HashSet<Tween>>();
	private Dictionary<Component, HashSet<Tween>> componentTweens = new Dictionary<Component, HashSet<Tween>>();
	
	void Awake() {
		if (_instance != null && _instance != this) {
			Destroy(gameObject);
			return;
		}
		_instance = this;
		DontDestroyOnLoad(gameObject);
	}
	
	/// <summary>
	/// Register a tween for tracking
	/// </summary>
	public void RegisterTween(Tween tween, GameObject gameObject, Component component = null) {
		if (!tween.isAlive) return;
		
		if (gameObject != null) {
			if (!gameObjectTweens.ContainsKey(gameObject)) {
				gameObjectTweens[gameObject] = new HashSet<Tween>();
			}
			gameObjectTweens[gameObject].Add(tween);
		}
		
		if (component != null) {
			if (!componentTweens.ContainsKey(component)) {
				componentTweens[component] = new HashSet<Tween>();
			}
			componentTweens[component].Add(tween);
		}
		
		// Note: DO NOT add OnComplete callback here - PrimeTween only allows one callback per tween
		// Cleanup will be handled by the main callback or periodic cleanup
	}
	
	/// <summary>
	/// Unregister a tween from tracking
	/// </summary>
	public void UnregisterTween(Tween tween, GameObject gameObject, Component component = null) {
		if (gameObject != null && gameObjectTweens.ContainsKey(gameObject)) {
			gameObjectTweens[gameObject].Remove(tween);
			if (gameObjectTweens[gameObject].Count == 0) {
				gameObjectTweens.Remove(gameObject);
			}
		}
		
		if (component != null && componentTweens.ContainsKey(component)) {
			componentTweens[component].Remove(tween);
			if (componentTweens[component].Count == 0) {
				componentTweens.Remove(component);
			}
		}
	}
	
	/// <summary>
	/// Complete ALL tweens on specified GameObjects and their components
	/// Most comprehensive conflict prevention possible
	/// </summary>
	public void CompleteAllTweensOnGameObjects(IEnumerable<GameObject> gameObjects) {
		foreach (var gameObject in gameObjects) {
			if (gameObject == null) continue;
			
			// Method 1: Use our tracking system (most reliable for registered tweens)
			if (gameObjectTweens.ContainsKey(gameObject)) {
				var tweensToComplete = gameObjectTweens[gameObject].ToList();
				foreach (var tween in tweensToComplete) {
					if (tween.isAlive) {
						tween.Complete();
					}
				}
			}
			
			// Method 2: Complete all tweens on all components (catches unregistered tweens)
			Component[] allComponents = gameObject.GetComponents<Component>();
			foreach (var component in allComponents) {
				if (component != null) {
					// Complete via component tracking
					if (componentTweens.ContainsKey(component)) {
						var tweensToComplete = componentTweens[component].ToList();
						foreach (var tween in tweensToComplete) {
							if (tween.isAlive) {
								tween.Complete();
							}
						}
					}
					
					// Fallback: Use PrimeTween's built-in completion (catches external tweens)
					Tween.CompleteAll(onTarget: component);
				}
			}
			
			// Method 3: Global fallback for any remaining tweens
			Tween.CompleteAll(onTarget: gameObject.transform);
		}
		
		// Clean up completed tweens from tracking
		CleanupCompletedTweens();
		
		// Force processing of completion callbacks
		if (Application.isPlaying) {
			UnityEngine.Canvas.ForceUpdateCanvases();
		}
	}
	
	/// <summary>
	/// Clean up any dead tweens from our tracking system
	/// </summary>
	private void CleanupCompletedTweens() {
		// Clean GameObject tweens
		var gameObjectsToRemove = new List<GameObject>();
		foreach (var kvp in gameObjectTweens) {
			kvp.Value.RemoveWhere(tween => !tween.isAlive);
			if (kvp.Value.Count == 0) {
				gameObjectsToRemove.Add(kvp.Key);
			}
		}
		foreach (var go in gameObjectsToRemove) {
			gameObjectTweens.Remove(go);
		}
		
		// Clean Component tweens
		var componentsToRemove = new List<Component>();
		foreach (var kvp in componentTweens) {
			if (kvp.Key == null) {
				componentsToRemove.Add(kvp.Key);
				continue;
			}
			kvp.Value.RemoveWhere(tween => !tween.isAlive);
			if (kvp.Value.Count == 0) {
				componentsToRemove.Add(kvp.Key);
			}
		}
		foreach (var comp in componentsToRemove) {
			componentTweens.Remove(comp);
		}
	}
	
	/// <summary>
	/// Get all GameObjects that currently have active tweens
	/// Useful for debugging and monitoring
	/// </summary>
	public List<GameObject> GetActiveGameObjects() {
		CleanupCompletedTweens();
		return gameObjectTweens.Keys.Where(go => go != null).ToList();
	}
	
	/// <summary>
	/// Get all Components that currently have active tweens
	/// </summary>
	public List<Component> GetActiveComponents() {
		CleanupCompletedTweens();
		return componentTweens.Keys.Where(comp => comp != null).ToList();
	}
	
	void Update() {
		// More frequent cleanup since we're not using OnComplete for auto-cleanup
		if (Time.frameCount % 60 == 0) { // Every second at 60fps
			CleanupCompletedTweens();
		}
	}
}

public class PrimeTweenBuilder : MonoBehaviour {
	public enum ParamType { Float, Vector2, Vector3, Vector4, Color, Quaternion, Rect }
	public enum ActivationBehavior { Nothing, Enable, Disable }
	public enum EaseType { Linear, Sine, Quad, Cubic, Quart, Quint, Expo, Circ, Back, Elastic, Bounce }
	public enum EaseDirection { In, Out, InOut }
	
	[Serializable]
	public class Parameter {
		public string     propertyName;
		public ParamType  paramType;
		
		// End (target) values:
		public float     endFloat;
		public Vector2   endVector2;
		public Vector3   endVector3;
		public Vector4   endVector4;
		public Color     endColor;
		public Quaternion endQuaternion;
		public Rect      endRect;
	}
	
	[Serializable]
	public class ClipTarget {
		public GameObject targetGameObject;
		public Component  targetComponent;
		public bool       isMaterialProperty;
		public int        materialSlot; // which material slot (0, 1, 2, etc.)
		public bool       completeExistingTweens = true; // Global tween completion
		public float      delay = 0f;
		public float      duration = 1f;
		public EaseType   easeType = EaseType.Linear;
		public EaseDirection easeDirection = EaseDirection.InOut;
		public ActivationBehavior onStartAction;
		public ActivationBehavior onEndAction;
		public List<Parameter> parameters = new List<Parameter>();
		
		// Helper property to get the combined PrimeTween Ease
		public Ease ease => CombineEase(easeType, easeDirection);
	}
	
	[Serializable]
	public class Clip {
		public string name;
		public List<ClipTarget> targets = new List<ClipTarget>();
	}
	
	public List<Clip> clips = new List<Clip>();
	
	public void PlayClip(string clipName) {
		foreach (var clip in clips) {
			if (clip.name != clipName) continue;
			
			// First pass: Complete all existing tweens using global tracking system
			bool shouldCompleteExisting = false;
			HashSet<GameObject> allGameObjectsInClip = new HashSet<GameObject>();
			
			// Collect all GameObjects and check if any target wants completion
			foreach (var t in clip.targets) {
				if (t.completeExistingTweens) {
					shouldCompleteExisting = true;
				}
				if (t.targetGameObject != null) {
					allGameObjectsInClip.Add(t.targetGameObject);
				}
			}
			
			// Use global tween manager for comprehensive completion
			if (shouldCompleteExisting && allGameObjectsInClip.Count > 0) {
				GlobalTweenManager.Instance.CompleteAllTweensOnGameObjects(allGameObjectsInClip);
			}
			
			// Second pass: Start the new animations
			foreach (var t in clip.targets) {
				ApplyActivation(t.targetGameObject, t.targetComponent, t.onStartAction);
				
				// Track if we created any tweens for this target (for OnComplete callback)
				bool tweensCreated = false;
				Tween lastTween = default;
				Component actualComponentUsed = t.targetComponent; // Track the actual component used for registration
				
				// Handle all parameters for this target
				foreach (var param in t.parameters) {
					if (!string.IsNullOrEmpty(param.propertyName)) {
						float del = t.delay;   // Use target-level delay
						float d = t.duration + 0.0001f;  // Use target-level duration
						Ease e = t.ease;       // Use target-level ease
						
						if (t.isMaterialProperty) {
							// MATERIAL
							var rend = (t.targetComponent as Renderer)
							?? t.targetGameObject?.GetComponent<Renderer>();
							if (rend != null && rend.materials != null && t.materialSlot < rend.materials.Length) {
								actualComponentUsed = rend; // Use Renderer for material tweens
								var mat = rend.materials[t.materialSlot];
								if (mat != null) {
									switch (param.paramType) {
										case ParamType.Float:
										float startF = mat.GetFloat(param.propertyName);
										lastTween = Tween.Custom(startF, param.endFloat, d,
											val => mat.SetFloat(param.propertyName, val),
											ease: e, startDelay: del);
										// Register with global manager
										GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
										tweensCreated = true;
										break;
										case ParamType.Color:
										Color startC = mat.GetColor(param.propertyName);
										lastTween = Tween.Custom(startC, param.endColor, d,
											val => mat.SetColor(param.propertyName, val),
											ease: e, startDelay: del);
										GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
										tweensCreated = true;
										break;
										case ParamType.Vector4:
										Vector4 startV4 = mat.GetVector(param.propertyName);
										lastTween = Tween.Custom(startV4, param.endVector4, d,
											val => mat.SetVector(param.propertyName, val),
											ease: e, startDelay: del);
										GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
										tweensCreated = true;
										break;
										case ParamType.Vector3:
										Vector4 v3Start = mat.GetVector(param.propertyName);
										// Preserve w component from original vector
										Vector4 v3End = new Vector4(param.endVector3.x, param.endVector3.y, param.endVector3.z, v3Start.w);
										lastTween = Tween.Custom(v3Start, v3End, d,
											val => mat.SetVector(param.propertyName, val),
											ease: e, startDelay: del);
										GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
										tweensCreated = true;
										break;
										case ParamType.Vector2:
										Vector4 v2Start = mat.GetVector(param.propertyName);
										// Preserve z and w components from original vector
										Vector4 v2End = new Vector4(param.endVector2.x, param.endVector2.y, v2Start.z, v2Start.w);
										lastTween = Tween.Custom(v2Start, v2End, d,
											val => mat.SetVector(param.propertyName, val),
											ease: e, startDelay: del);
										GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
										tweensCreated = true;
										break;
										// Rect & Quaternion not supported on materials
									}
								}
							}
						} else {
							// COMPONENT
							var comp = t.targetComponent;
							if (comp != null) {
								actualComponentUsed = comp; // Use the target component
								Type ct = comp.GetType();
								var fi = ct.GetField(param.propertyName,
									BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								var pi = (fi == null)
								? ct.GetProperty(param.propertyName,
									BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
								: null;
								if ((fi != null || (pi != null && pi.CanWrite))) {
									switch (param.paramType) {
										case ParamType.Float: {
											float startVal = (fi != null)
											? (float)fi.GetValue(comp)
											: (float)pi.GetValue(comp, null);
											lastTween = Tween.Custom(startVal, param.endFloat, d,
												v => SetMember(comp, fi, pi, v),
												ease: e, startDelay: del);
											GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
											tweensCreated = true;
											break;
										}
										case ParamType.Vector2: {
											Vector2 start2 = (Vector2)(fi != null
												? fi.GetValue(comp)
												: pi.GetValue(comp, null));
											lastTween = Tween.Custom(start2, param.endVector2, d,
												v => SetMember(comp, fi, pi, v),
												ease: e, startDelay: del);
											GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
											tweensCreated = true;
											break;
										}
										case ParamType.Vector3: {
											Vector3 start3 = (Vector3)(fi != null
												? fi.GetValue(comp)
												: pi.GetValue(comp, null));
											lastTween = Tween.Custom(start3, param.endVector3, d,
												v => SetMember(comp, fi, pi, v),
												ease: e, startDelay: del);
											GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
											tweensCreated = true;
											break;
										}
										case ParamType.Vector4: {
											Vector4 start4 = (Vector4)(fi != null
												? fi.GetValue(comp)
												: pi.GetValue(comp, null));
											lastTween = Tween.Custom(start4, param.endVector4, d,
												v => SetMember(comp, fi, pi, v),
												ease: e, startDelay: del);
											GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
											tweensCreated = true;
											break;
										}
										case ParamType.Color: {
											Color startCol = (Color)(fi != null
												? fi.GetValue(comp)
												: pi.GetValue(comp, null));
											lastTween = Tween.Custom(startCol, param.endColor, d,
												v => SetMember(comp, fi, pi, v),
												ease: e, startDelay: del);
											GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
											tweensCreated = true;
											break;
										}
										case ParamType.Quaternion: {
											Quaternion startQ = (Quaternion)(fi != null
												? fi.GetValue(comp)
												: pi.GetValue(comp, null));
											lastTween = Tween.Custom(startQ, param.endQuaternion, d,
												v => SetMember(comp, fi, pi, v),
												ease: e, startDelay: del);
											GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
											tweensCreated = true;
											break;
										}
										case ParamType.Rect: {
											Rect startR = (Rect)(fi != null
												? fi.GetValue(comp)
												: pi.GetValue(comp, null));
											lastTween = Tween.Custom(startR, param.endRect, d,
												v => SetMember(comp, fi, pi, v),
												ease: e, startDelay: del);
											GlobalTweenManager.Instance.RegisterTween(lastTween, t.targetGameObject, actualComponentUsed);
											tweensCreated = true;
											break;
										}
									}
								}
							}
						}
					}
				}
				
				// Apply OnEnd activation - attach to last tween if available, otherwise create dummy tween
				// IMPORTANT: Each tween can only have ONE OnComplete callback, so we consolidate everything
				if (t.onEndAction != ActivationBehavior.Nothing) {
					if (tweensCreated && lastTween.isAlive) {
						lastTween.OnComplete(() => {
							ApplyActivation(t.targetGameObject, t.targetComponent, t.onEndAction);
							GlobalTweenManager.Instance.UnregisterTween(lastTween, t.targetGameObject, actualComponentUsed);
						});
					} else {
						// Fallback: create dummy tween if no parameters were animated
						var dummyTween = Tween.Delay(t.delay + t.duration + 0.0001f);
						GlobalTweenManager.Instance.RegisterTween(dummyTween, t.targetGameObject, actualComponentUsed);
						dummyTween.OnComplete(() => {
							ApplyActivation(t.targetGameObject, t.targetComponent, t.onEndAction);
							GlobalTweenManager.Instance.UnregisterTween(dummyTween, t.targetGameObject, actualComponentUsed);
						});
					}
				} else {
					// Even if no end action, we still need to handle cleanup
					if (tweensCreated && lastTween.isAlive) {
						lastTween.OnComplete(() => {
							GlobalTweenManager.Instance.UnregisterTween(lastTween, t.targetGameObject, actualComponentUsed);
						});
					} else {
						// Create dummy tween for cleanup tracking
						var dummyTween = Tween.Delay(t.delay + t.duration + 0.0001f);
						GlobalTweenManager.Instance.RegisterTween(dummyTween, t.targetGameObject, actualComponentUsed);
						dummyTween.OnComplete(() => {
							GlobalTweenManager.Instance.UnregisterTween(dummyTween, t.targetGameObject, actualComponentUsed);
						});
					}
				}
			}
			break;
		}
	}
	
	private void SetMember<T>(Component comp, FieldInfo fi, PropertyInfo pi, T v) {
		if (fi != null) fi.SetValue(comp, v);
		else if (pi != null) pi.SetValue(comp, v, null);
	}
	
	private void ApplyActivation(GameObject go, Component comp, ActivationBehavior behavior) {
		if (behavior == ActivationBehavior.Nothing) return;
		bool on = (behavior == ActivationBehavior.Enable);
		if (comp != null) {
			var prop = comp.GetType().GetProperty("enabled",
				BindingFlags.Instance | BindingFlags.Public);
			if (prop != null && prop.PropertyType == typeof(bool)) {
				prop.SetValue(comp, on);
				return;
			}
		}
		if (go != null) go.SetActive(on);
	}
	
	// Helper methods for easing conversion
	public static Ease CombineEase(EaseType type, EaseDirection direction) {
		if (type == EaseType.Linear) return Ease.Linear;
		
		// FIXED: PrimeTween uses DirectionType format (e.g., "InOutSine"), not TypeDirection
		string easeName = direction.ToString() + type.ToString();
		if (System.Enum.TryParse<Ease>(easeName, out Ease result)) {
			return result;
		}
		return Ease.Linear; // fallback
	}
	
	public static (EaseType, EaseDirection) SplitEase(Ease ease) {
		if (ease == Ease.Linear) return (EaseType.Linear, EaseDirection.InOut);
		
		string easeName = ease.ToString();
		
		// Determine direction
		EaseDirection direction = EaseDirection.InOut;
		if (easeName.StartsWith("In") && !easeName.StartsWith("InOut")) {
			direction = EaseDirection.In;
			easeName = easeName.Substring(2);
		} else if (easeName.StartsWith("Out")) {
			direction = EaseDirection.Out;
			easeName = easeName.Substring(3);
		} else if (easeName.StartsWith("InOut")) {
			direction = EaseDirection.InOut;
			easeName = easeName.Substring(5);
		}
		
		// Determine type
		if (System.Enum.TryParse<EaseType>(easeName, out EaseType type)) {
			return (type, direction);
		}
		
		return (EaseType.Linear, EaseDirection.InOut); // fallback
	}
}