using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode]
public class PositionWave : MonoBehaviour {

	public Vector3 range;
	public Vector3 speed;
	private Vector3 pos;
	private float off;

	void Start () {
		off = transform.position.x * 7.0f + transform.position.y * 11.0f + transform.position.z * 13.0f;
		pos = transform.localPosition;
	}

	void Update () {
		transform.localPosition = pos + new Vector3(range.x * Mathf.Sin((Time.time + 3.0f + off) * speed.x),
											range.y * Mathf.Sin((Time.time + 17.0f + off) * speed.y),
											range.z * Mathf.Sin((Time.time + 31.0f + off) * speed.z));
	}
}

// Resources:
// https://www.c-sharpcorner.com/article/transforming-objects-using-c-sharp-scripts-in-unity/
// https://www.youtube.com/watch?v=YfIOPWuUjn8
// https://docs.unity3d.com/ScriptReference/Transform-rotation.html
// https://docs.unity3d.com/ScriptReference/Transform-position.html
// https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html
