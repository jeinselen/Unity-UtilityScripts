using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ParentPositionBlend : MonoBehaviour {

	public GameObject parentA;
	public GameObject parentB;
	[Range(0.0f, 1.0f)]
	public float blend;

	void Update () {
		if (blend == 0.0f) {
			transform.position = parentA.transform.position;
		} else if (blend == 1.0f) {
			transform.position = parentB.transform.position;
		} else {
			transform.position = Vector3.Lerp(parentA.transform.position, parentB.transform.position, blend);
		}
	}
}
