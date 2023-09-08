using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ParentBlend : MonoBehaviour {

	public GameObject parentA;
	public GameObject parentB;
	[Range(0.0f, 1.0f)]
	public float blend;

	void Update () {
		if (blend == 0.0f) {
			transform.position = parentA.transform.position;
			transform.rotation = parentA.transform.rotation;
		} else if (blend == 1.0f) {
			transform.position = parentB.transform.position;
			transform.rotation = parentB.transform.rotation;
		} else {
			transform.position = Vector3.Lerp(parentA.transform.position, parentB.transform.position, blend);
			transform.rotation = Quaternion.Lerp(parentA.transform.rotation, parentB.transform.rotation, blend);
		}
	}
}
