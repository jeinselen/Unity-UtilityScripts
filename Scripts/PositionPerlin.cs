using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode]
public class PositionPerlin : MonoBehaviour {

	public Vector3 range;
	public Vector3 speed;
	private Vector3 pos;
	private float off;

	void Start () {
		off = transform.position.x * 7.0f + transform.position.y * 11.0f + transform.position.z * 13.0f;
		pos = transform.localPosition;
	}

	void Update () {
		transform.localPosition = pos + new Vector3(range.x * (Mathf.PerlinNoise(Time.time * speed.x, 0.3f + off) * 2.0f - 1.0f),
											range.y * (Mathf.PerlinNoise(Time.time * speed.y, 1.7f + off) * 2.0f - 1.0f),
											range.z * (Mathf.PerlinNoise(Time.time * speed.z, 3.1f + off) * 2.0f - 1.0f));
	}
}

// Resources:
// https://www.c-sharpcorner.com/article/transforming-objects-using-c-sharp-scripts-in-unity/
// https://www.youtube.com/watch?v=YfIOPWuUjn8
// https://docs.unity3d.com/ScriptReference/Transform-rotation.html
// https://docs.unity3d.com/ScriptReference/Transform-position.html
// https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html
