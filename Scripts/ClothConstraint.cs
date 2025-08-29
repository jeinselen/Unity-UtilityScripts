using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Cloth))]
[RequireComponent(typeof(SkinnedMeshRenderer))]
public class ClothConstraint : MonoBehaviour
{
	public enum InputSource { UV_X, UV_Y, VertexColor_R, VertexColor_G, VertexColor_B, VertexColor_A }
	
	[Header("Input Settings")]
	public InputSource inputSource = InputSource.UV_X;
	public float inputMin = 0f;
	public float inputMax = 1f;
	
	[Header("Output Settings")]
	public float outputMin = 0f;
	public float outputMax = 1f;
	
	[Header("Matching Settings")]
	public float maxDistanceThreshold = 0.001f;
	
	public void ApplyConstraints()
	{
		Cloth cloth = GetComponent<Cloth>();
		SkinnedMeshRenderer smr = GetComponent<SkinnedMeshRenderer>();
		Mesh mesh = smr.sharedMesh;
		
		if (!mesh.isReadable)
		{
			Debug.LogError("Mesh must be readable.");
			return;
		}
		
		Vector3[] meshVertices = mesh.vertices;    // LOCAL SPACE
		Vector3 scale = smr.transform.lossyScale;
		
		for (int i = 0; i < meshVertices.Length; i++)
		{
			meshVertices[i] = Vector3.Scale(meshVertices[i], scale);
		}
		
		Vector2[] uvs = mesh.uv;
		Color[] colors = mesh.colors;
		
		// Cloth vertex access (local space)
		Vector3[] clothVertices = cloth.vertices;
		
		ClothSkinningCoefficient[] newCoefficients = new ClothSkinningCoefficient[clothVertices.Length];
		int unmatchedCount = 0;
		
		for (int i = 0; i < clothVertices.Length; i++)
		{
			int bestMatch = -1;
			float bestDistSqr = maxDistanceThreshold * maxDistanceThreshold;
			
			for (int j = 0; j < meshVertices.Length; j++)
			{
				float distSqr = (clothVertices[i] - meshVertices[j]).sqrMagnitude;
				if (distSqr <= bestDistSqr)
				{
					bestDistSqr = distSqr;
					bestMatch = j;
				}
			}
			
			float inputValue = 0f;
			
			if (bestMatch >= 0)
			{
				switch (inputSource)
				{
					case InputSource.UV_X: if (uvs.Length > bestMatch) inputValue = uvs[bestMatch].x; break;
					case InputSource.UV_Y: if (uvs.Length > bestMatch) inputValue = uvs[bestMatch].y; break;
					case InputSource.VertexColor_R: if (colors.Length > bestMatch) inputValue = colors[bestMatch].r; break;
					case InputSource.VertexColor_G: if (colors.Length > bestMatch) inputValue = colors[bestMatch].g; break;
					case InputSource.VertexColor_B: if (colors.Length > bestMatch) inputValue = colors[bestMatch].b; break;
					case InputSource.VertexColor_A: if (colors.Length > bestMatch) inputValue = colors[bestMatch].a; break;
				}
			}
			else
			{
				unmatchedCount++;
			}
			
			float normalized = Mathf.InverseLerp(inputMin, inputMax, inputValue);
			float mapped = Mathf.Lerp(outputMin, outputMax, normalized);
			
			newCoefficients[i].maxDistance = mapped;
			newCoefficients[i].collisionSphereDistance = 0f;
		}
		
		cloth.coefficients = newCoefficients;
		
		#if UNITY_EDITOR
		UnityEditor.SceneView.RepaintAll();
		#endif
		
		Debug.Log($"Cloth constraints applied. {unmatchedCount} unmatched of {clothVertices.Length}.");
//		Debug.Log($"Cloth vertex count: {cloth.coefficients.Length}, mesh vertex count: {mesh.vertexCount}");
//		Debug.Log($"First cloth vertex (local): {clothVertices[0]}");
//		Debug.Log($"First mesh vertex (local): {meshVertices[0]}");
//		Debug.Log($"Distance between first verts: {(clothVertices[0] - meshVertices[0]).magnitude}");
	}
}
