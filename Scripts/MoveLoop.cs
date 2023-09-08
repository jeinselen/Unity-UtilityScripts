using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode]
public class MoveLoop : MonoBehaviour {

	public Vector3 rangeMin = new Vector3(-2.0f, -1.0f, 0.0f);
	public Vector3 rangeMax = new Vector3(2.0f, 1.0f, 0.0f);
	public Vector3 directionMin = new Vector3(0.0f, -0.1f, 0.0f);
	public Vector3 directionMax = new Vector3(0.0f, -0.2f, 0.0f);
	private Vector3 pos;
	private Vector3 dir;

	void Start () {
		pos = transform.localPosition;
		dir = new Vector3(
			Random.Range(directionMin.x, directionMax.x),
			Random.Range(directionMin.y, directionMax.y),
			Random.Range(directionMin.z, directionMax.z)
		);
	}

	void Update () {
		pos += dir * Time.deltaTime;
		
		if (pos.x < rangeMin.x)
		{
			pos.x = rangeMax.x;
			pos.y = Random.Range(directionMin.y, directionMax.y);
			pos.z = Random.Range(directionMin.z, directionMax.z);
		}
		else if (pos.x > rangeMax.x)
		{
			pos.x = rangeMin.x;
			pos.y = Random.Range(directionMin.y, directionMax.y);
			pos.z = Random.Range(directionMin.z, directionMax.z);
		}
		
		if (pos.y < rangeMin.y)
		{
			pos.x = Random.Range(directionMin.x, directionMax.x);
			pos.y = rangeMax.y;
			pos.z = Random.Range(directionMin.z, directionMax.z);
		}
		else if (pos.y > rangeMax.y)
		{
			pos.x = Random.Range(directionMin.x, directionMax.x);
			pos.y = rangeMin.y;
			pos.z = Random.Range(directionMin.z, directionMax.z);
		}
		
		if (pos.z < rangeMin.z)
		{
			pos.x = Random.Range(directionMin.x, directionMax.x);
			pos.y = Random.Range(directionMin.y, directionMax.y);
			pos.z = rangeMax.z;
		}
		else if (pos.z > rangeMax.z)
		{
			pos.x = Random.Range(directionMin.x, directionMax.x);
			pos.y = Random.Range(directionMin.y, directionMax.y);
			pos.z = rangeMin.z;
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
