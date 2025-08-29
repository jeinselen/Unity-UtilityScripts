using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ObjectConnect : MonoBehaviour {

	public GameObject objectA;
	public GameObject objectB;
	[Range(0.0f, 1.0f)]
	public float padding;

	void Update () {
		if (objectA && objectB) {
			Vector3 direction = objectB.transform.position - objectA.transform.position;
			if (direction.magnitude > 0.1f) {
				transform.position = objectA.transform.position + (direction.normalized * ((objectA.transform.localScale.x * 0.5f) + (padding * 0.5f)));
				
				Vector3 newScale = transform.localScale;
				newScale.y = direction.magnitude - (objectA.transform.localScale.x * 0.5f) - (objectB.transform.localScale.x * 0.5f) - padding;
				transform.localScale = newScale;
				
				transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(90, 0, 0);
			}
		}
	}
}
