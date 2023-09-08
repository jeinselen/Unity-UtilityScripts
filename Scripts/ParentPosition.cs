using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ParentPosition : MonoBehaviour {

	public GameObject parent;

	void Update () {
		transform.position = parent.transform.position;
	}
}
