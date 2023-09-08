using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ParentPositionOffset : MonoBehaviour {

	public GameObject parent;
	public Vector3 offset;

	void Update () {
		transform.position = parent.transform.position + offset;
	}
}
