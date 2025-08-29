using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ParentPositionLocal : MonoBehaviour {

	public GameObject parent;
	
	void Update () {
		if (parent) {
			transform.localPosition = parent.transform.localPosition;
		}
	}
}
