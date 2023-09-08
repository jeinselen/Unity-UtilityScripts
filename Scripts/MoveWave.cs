using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode]
public class MoveWave : MonoBehaviour {

	public Vector3 range;
	public Vector3 rangeSpeed;
	public Vector3 angle;
	public Vector3 angleSpeed;
	private Vector3 pos;
	private Vector3 rot;
	private float off;

	void Start () {
		off = transform.position.x * 7.0f + transform.position.y * 11.0f + transform.position.z * 13.0f;
		pos = transform.localPosition;
		rot = transform.localRotation.eulerAngles;
	}

	void Update () {
		transform.localPosition = pos + new Vector3(range.x * Mathf.Sin((Time.time + 3.0f + off) * rangeSpeed.x),
											range.y * Mathf.Sin((Time.time + 17.0f + off) * rangeSpeed.y),
											range.z * Mathf.Sin((Time.time + 31.0f + off) * rangeSpeed.z));
		transform.localRotation = Quaternion.Euler(rot.x + angle.x * Mathf.Sin((Time.time + 3.0f + off + 4.0f) * angleSpeed.x),
											rot.y + angle.y * Mathf.Sin((Time.time + 17.0f + off + 4.0f) * angleSpeed.y),
											rot.z + angle.z * Mathf.Sin((Time.time + 31.0f + off + 4.0f) * angleSpeed.z));
	}
}

// Resources:
// https://www.c-sharpcorner.com/article/transforming-objects-using-c-sharp-scripts-in-unity/
// https://www.youtube.com/watch?v=YfIOPWuUjn8
// https://docs.unity3d.com/ScriptReference/Transform-rotation.html
// https://docs.unity3d.com/ScriptReference/Transform-position.html
// https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html
