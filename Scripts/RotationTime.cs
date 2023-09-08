using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode]
public class RotationTime : MonoBehaviour {

	public Vector3 speed;
	private Vector3 rot;

	void Start () {
		rot = transform.localRotation.eulerAngles;
	}

	void Update () {
		transform.localRotation = Quaternion.Euler(rot.x + Time.time * speed.x,
											rot.y + Time.time * speed.y,
											rot.z + Time.time * speed.z);
	}
}

// Resources:
// https://www.c-sharpcorner.com/article/transforming-objects-using-c-sharp-scripts-in-unity/
// https://www.youtube.com/watch?v=YfIOPWuUjn8
// https://docs.unity3d.com/ScriptReference/Transform-rotation.html
// https://docs.unity3d.com/ScriptReference/Transform-position.html
// https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html
