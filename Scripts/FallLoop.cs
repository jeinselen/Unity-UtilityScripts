using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode]
public class FallLoop : MonoBehaviour {

	public float rangeX = 1.0f;
	public float limitY = 1.0f;
	public float speedMin = 0.1f;
	public float speedMax = 0.2f;
	public bool randomStart = true;
	private Vector3 start;
	private Vector3 pos;
	private Vector3 dir;

	void Start () {
		start = transform.localPosition;
		pos = start;
		dir = new Vector3(0.0f, Random.Range(speedMin, speedMax), 0.0f);
		if (randomStart)
		{
			pos.x = start.x + Random.Range(-rangeX, rangeX);
			pos.y = Random.Range(-limitY, limitY);
			dir = new Vector3(0.0f, Random.Range(speedMin, speedMax), 0.0f);
		}
	}

	void Update () {
		pos -= dir * Time.deltaTime;
		if (pos.y < -limitY)
		{
			pos.x = start.x + Random.Range(-rangeX, rangeX);
			pos.y = limitY;
			dir = new Vector3(0.0f, Random.Range(speedMin, speedMax), 0.0f);
		}
		transform.localPosition = pos;
	}
}

// Resources:
// https://www.c-sharpcorner.com/article/transforming-objects-using-c-sharp-scripts-in-unity/
// https://www.youtube.com/watch?v=YfIOPWuUjn8
// https://docs.unity3d.com/ScriptReference/Transform-rotation.html
// https://docs.unity3d.com/ScriptReference/Transform-position.html
// https://docs.unity3d.com/ScriptReference/Mathf.PerlinNoise.html
