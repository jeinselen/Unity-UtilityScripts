using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode]
public class RotationPerlin : MonoBehaviour {

	public Vector3 angle;
	public Vector3 speed;
	private Vector3 pos;
	private Vector3 rot;
	private float off;

	void Start () {
		off = transform.position.x * 7.0f + transform.position.y * 11.0f + transform.position.z * 13.0f;
		pos = transform.localPosition;
		rot = transform.localRotation.eulerAngles;
	}

	void Update () {
		transform.localRotation = Quaternion.Euler(rot.x + angle.x * (Mathf.PerlinNoise(Time.time * speed.x, 0.3f + off + 4.0f) * 2.0f - 1.0f),
											rot.y + angle.y * (Mathf.PerlinNoise(Time.time * speed.y, 1.7f + off + 4.0f) * 2.0f - 1.0f),
											rot.z + angle.z * (Mathf.PerlinNoise(Time.time * speed.z, 3.1f + off + 4.0f) * 2.0f - 1.0f));
	}
}

// Resources:
// https://www.c-sharpcorner.com/article/transforming-objects-using-c-sharp-scripts-in-unity/
// https://www.youtube.com/watch?v=YfIOPWuUjn8
// https://docs.unity3d.com/ScriptReference/Transform-rotation.html
// https://docs.unity3d.com/ScriptReference/Transform-position.html
// https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html
