using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class SpirographLine : MonoBehaviour
{
	[Header("Base Circle Settings")]
	public int resolution = 128;
	public float radius = 1f;
	public int baseFrequency = 3;
	
	[Header("Edit Mode Settings")]
	public bool animateInEditMode = false;
	
	[System.Serializable]
	public class DisplacementLayer
	{
		public enum DisplacementType { XYRadial, XAxis, YAxis, ZAxis }
		public enum PatternType { SineWave, CosineWave }
		
		public DisplacementType displacement;
		public float displacementAmount = 0.1f;
		public PatternType pattern;
		public int patternFrequency = 4;
		[Range(-1f, 1f)]public float patternOffset = 0.0f;
		[Range(-1f, 1f)] public float offsetSpeed = 0.1f;
		
		[HideInInspector] public float animatedOffset = 0f;
	}
	
	[Header("Displacement Layers")]
	public List<DisplacementLayer> layers = new List<DisplacementLayer>();
	
	private LineRenderer lineRenderer;
	
	void Awake()
	{
		EnsureLineRenderer();
	}
	
	void Update()
	{
		if (Application.isPlaying || animateInEditMode)
		{
			foreach (var layer in layers)
			{
				layer.animatedOffset += layer.offsetSpeed * Time.deltaTime;
				if (layer.animatedOffset > 1f) layer.animatedOffset -= 1f;
				else if (layer.animatedOffset < -1f) layer.animatedOffset += 1f;
			}
			UpdateLine();
		}
	}
	
	void OnValidate()
	{
		EnsureLineRenderer();
		UpdateLine();
	}
	
	void EnsureLineRenderer()
	{
		if (!lineRenderer)
		{
			lineRenderer = GetComponent<LineRenderer>();
			lineRenderer.loop = true;
			lineRenderer.useWorldSpace = false;
		}
	}
	
	void UpdateLine()
	{
		if (resolution < 4) resolution = 4;
		Vector3[] points = new Vector3[resolution];
		float deltaAngle = 2f * Mathf.PI * baseFrequency / resolution;
		
		for (int i = 0; i < resolution; i++)
		{
			float factor = (float)i / resolution;
			float angle = deltaAngle * i;
			float currentRadius = radius;
			float xOffset = 0f;
			float yOffset = 0f;
			float zOffset = 0f;
			
			foreach (var layer in layers)
			{
				float waveInput = (factor * layer.patternFrequency + layer.patternOffset + layer.animatedOffset) * (Mathf.PI * 2.0f);
				float wave = (layer.pattern == DisplacementLayer.PatternType.SineWave)
					? Mathf.Sin(waveInput)
					: Mathf.Cos(waveInput);
				float displacement = wave * layer.displacementAmount;
				
				switch (layer.displacement)
				{
					case DisplacementLayer.DisplacementType.XYRadial:
						currentRadius += displacement;
						break;
					case DisplacementLayer.DisplacementType.XAxis:
						xOffset += displacement;
						break;
					case DisplacementLayer.DisplacementType.YAxis:
						yOffset += displacement;
						break;
					case DisplacementLayer.DisplacementType.ZAxis:
						zOffset += displacement;
						break;
				}
			}
			
//			float x = Mathf.Cos(angle) * currentRadius;
//			float y = Mathf.Sin(angle) * currentRadius;
			points[i] = new Vector3(
				Mathf.Cos(angle) * currentRadius + xOffset,
				Mathf.Sin(angle) * currentRadius + yOffset,
				zOffset
			);
		}
		
		lineRenderer.positionCount = resolution;
		lineRenderer.SetPositions(points);
	}
}
