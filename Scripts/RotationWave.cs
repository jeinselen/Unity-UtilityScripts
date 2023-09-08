using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode]
public class RotationWave : MonoBehaviour {

	public Vector3 angle;
	public Vector3 speed;
	public float offset = 0.0f;
	private Vector3 rot;
	// private float off;

	void Start () {
		if (offset == 0.0f) {
			offset = transform.position.x * 7.1f + transform.position.y * 11.3f + transform.position.z * 13.7f;
		}
		// off = transform.position.x * 7.0f + transform.position.y * 11.0f + transform.position.z * 13.0f;
		rot = transform.localRotation.eulerAngles;
	}

	void Update () {
		transform.localRotation = Quaternion.Euler(rot.x + angle.x * Mathf.Sin((Time.time + 3.0f + offset + 4.0f) * speed.x),
											rot.y + angle.y * Mathf.Sin((Time.time + 17.0f + offset + 4.0f) * speed.y),
											rot.z + angle.z * Mathf.Sin((Time.time + 31.0f + offset + 4.0f) * speed.z));
	}
}

// Resources:
// https://www.c-sharpcorner.com/article/transforming-objects-using-c-sharp-scripts-in-unity/
// https://www.youtube.com/watch?v=YfIOPWuUjn8
// https://docs.unity3d.com/ScriptReference/Transform-rotation.html
// https://docs.unity3d.com/ScriptReference/Transform-position.html
// https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html
