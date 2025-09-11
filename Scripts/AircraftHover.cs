using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AircraftHover : MonoBehaviour {
	
	public Vector2 angle = new Vector2(5.0f, 15.0f);
	public Vector2 speed = new Vector2(0.1f, 0.2f);
	[Range(0.0f, 1.0f)] public float react = 0.25f;
	[Range(0.0f, 1.0f)] public float decay = 0.75f;
	public bool lateUpdate = false;
	public bool softLimit = false;
	public Vector2 limit = new Vector2(0.4f, 0.4f);
	[Range(0.0f, 1.0f)] public float softness = 0.5f;
	
	private float seed;
	private Vector2 noise;
	private Vector2 accum;
	private Vector3 rot;
	private Vector3 pos;
	
	void Start () {
		seed = transform.position.x * 7.0f + transform.position.y * 11.0f + transform.position.z * 13.0f;
		pos = transform.localPosition;
		rot = transform.localRotation.eulerAngles;
		accum = new Vector2(0.0f, 0.0f);
	}
	
	void Update () {
		if (!lateUpdate) {
			hoverUpdate();
		}
	}
	
	void LateUpdate () {
		if (lateUpdate) {
			rot = transform.localRotation.eulerAngles;
			pos = transform.localPosition;
			hoverUpdate();
		}
	}
	
	private void hoverUpdate () {
		// Get perlin noise values and accumulate positional offsets
		noise = new Vector2(
			Mathf.PerlinNoise(Time.time * speed.x, -1.7f + seed) * 2.0f - 1.0f,
			Mathf.PerlinNoise(Time.time * speed.y,  1.7f + seed) * 2.0f - 1.0f
		);
		accum += new Vector2(
			noise.x * angle.x * react * Time.deltaTime * -0.01f,
			noise.y * angle.y * react * Time.deltaTime * -0.01f
		);
		accum *= 1.0f - decay * 0.001f;
		
		// Update element rotation and position values
		transform.localRotation = Quaternion.Euler(
			rot.x + noise.x * angle.x,
			rot.y,
			rot.z + noise.y * angle.y
		);
		
		// Limit accumulated position offset if enabled
		if (softLimit) {
			accum = smoothLimit(accum);
		}
		
		transform.localPosition = pos + new Vector3(accum.y, accum.x, 0.0f);
	}
	
	private float smoothMin(float a, float b, float c) {
		if (c != 0.0) {
			float h = Mathf.Max(c - Mathf.Abs(a - b), 0.0f) / c;
			return Mathf.Min(a, b) - h * h * h * c * (1.0f / 6.0f);
		}
		else {
			return Mathf.Min(a, b);
		}
	}
	
	private float smoothMax (float a, float b, float c) {
		return -smoothMin(-a, -b, c);
	}
	
	private Vector2 smoothLimit (Vector2 input) {
		input.x = smoothMin(smoothMax(input.x, -limit.x, limit.x * softness), limit.x, limit.x * softness);
		input.y = smoothMin(smoothMax(input.y, -limit.y, limit.y * softness), limit.y, limit.y * softness);
		return input;
	}
}