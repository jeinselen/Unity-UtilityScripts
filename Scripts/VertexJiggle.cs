using UnityEngine;

/// <summary>
/// VertexJiggle applies a simple spring-based jiggle deformation to a mesh's vertices.
/// Vertices are moved in world-space like they are attached by springs to their original positions.
/// A selected vertex attribute (UV or color channel) controls the jiggle amount per vertex:
/// - Vertices with 0 jiggle amount remain fixed to their original position.
/// - Higher jiggle amount means the vertex is looser and will bounce/jiggle more (in world units).
/// 
/// This script works with a MeshFilter/MeshRenderer (static mesh). It updates the mesh each frame,
/// recalculating normals as the shape changes. (For skinned meshes, additional handling would be needed.)


/// VertexJiggle: Applies soft spring physics to a mesh's vertices based on UVs or vertex colors.
/// Supports soft displacement limits, damping, per-frame movement capping, and world-space jiggle.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VertexJiggle : MonoBehaviour
{
    /// <summary>Selectable vertex data sources for jiggle influence.</summary>
    public enum InputSource { UV_X, UV_Y, Color_R, Color_G, Color_B, Color_A };

    [Header("Jiggle Input Source")]
    [Tooltip("Vertex attribute channel that controls the jiggle amount for each vertex.")]
    public InputSource inputSource = InputSource.UV_X;

    [Header("Input Range (Remap)")]
    [Tooltip("Minimum input value that maps to the Output Min jiggle amount.")]
    public float inputMin = 0f;
    [Tooltip("Maximum input value that maps to the Output Max jiggle amount.")]
    public float inputMax = 1f;

    [Header("Output Range (Jiggle Amount)")]
    [Tooltip("Jiggle amount corresponding to the minimum input value (scene units of movement).")]
    public float outputMin = 0f;
    [Tooltip("Jiggle amount corresponding to the maximum input value (scene units of movement).")]
    public float outputMax = 1f;

    [Header("Spring Physics Settings")]
    [Tooltip("Jiggle oscillation frequency (how fast the spring oscillates, in Hz).")]
    public float frequency = 2f;
    [Tooltip("Damping factor to reduce jiggle over time (higher values = quicker damping).")]
    public float damping = 0.5f;

    // Original vertex positions in local space (mesh reference state)
    [Header("Stability Settings")]
    public float softLimitStrength = 10f; // Higher = tighter correction back within allowed distance
    public float maxMovePerFrameMultiplier = 0.5f; // Fraction of maxDistance

    // Mesh reference
    private Mesh mesh;
    private Vector3[] originalVertices;
    // Current vertex positions in world space (for physics simulation)
    private Vector3[] currentWorldPositions;
    // Current velocity of each vertex (in world space)
    private Vector3[] previousWorldPositions;
    private Vector3[] vertexVelocities;
    // Jiggle amount (weight) per vertex after input remapping
    // Working array for deformed vertex positions in local space (to update mesh)
    private Vector3[] deformedVertices;
    private float[] jiggleAmount;

    void Start()
    {
        // Get the mesh from MeshFilter (ensure we have a unique instance to modify)
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.mesh == null)
        {
            Debug.LogError("VertexJiggle: No MeshFilter with a mesh found on this object.");
            enabled = false;
            return;
        }
        mesh = mf.mesh;  // This will instantiate a copy if the mesh is shared by other objects

        // Cache original vertices (local space positions) and initialize arrays
        originalVertices = mesh.vertices;
        int vertexCount = originalVertices.Length;
        currentWorldPositions = new Vector3[vertexCount];
        previousWorldPositions = new Vector3[vertexCount];
        vertexVelocities = new Vector3[vertexCount];
        deformedVertices = new Vector3[vertexCount];
        jiggleAmount = new float[vertexCount];

        Vector2[] uv = mesh.uv;
        Color[] colors = mesh.colors;
        Color32[] colors32 = mesh.colors32;
        bool hasUV = uv != null && uv.Length == vertexCount;
        bool hasColors = (colors != null && colors.Length == vertexCount) || (colors32 != null && colors32.Length == vertexCount);

        for (int i = 0; i < vertexCount; i++)
        {
            currentWorldPositions[i] = transform.TransformPoint(originalVertices[i]);
            previousWorldPositions[i] = currentWorldPositions[i];

            float sourceValue = 0f;
            switch (inputSource)
            {
                case InputSource.UV_X: if (hasUV) sourceValue = uv[i].x; break;
                case InputSource.UV_Y: if (hasUV) sourceValue = uv[i].y; break;
                case InputSource.Color_R: if (hasColors) sourceValue = (colors != null) ? colors[i].r : colors32[i].r / 255f; break;
                case InputSource.Color_G: if (hasColors) sourceValue = (colors != null) ? colors[i].g : colors32[i].g / 255f; break;
                case InputSource.Color_B: if (hasColors) sourceValue = (colors != null) ? colors[i].b : colors32[i].b / 255f; break;
                case InputSource.Color_A: if (hasColors) sourceValue = (colors != null) ? colors[i].a : colors32[i].a / 255f; break;
            }

            // Remap sourceValue from [inputMin, inputMax] to [outputMin, outputMax]
            float clamped = Mathf.Clamp(sourceValue, inputMin, inputMax);
            float normalized = (inputMax != inputMin) ? (clamped - inputMin) / (inputMax - inputMin) : 0f;
            jiggleAmount[i] = Mathf.Lerp(outputMin, outputMax, normalized);
        }
    }

    void LateUpdate()
    {
        if (mesh == null) return;

        float dt = Time.deltaTime;
        // Convert frequency to angular frequency (omega) and derive spring stiffness (omega^2).
        // This effectively controls how strong the spring pull is.
        float omega = 2f * Mathf.PI * frequency;
        float springStiffness = omega * omega;

        // Update each vertex's position using spring-damper physics
        for (int i = 0; i < currentWorldPositions.Length; i++)
        {
            // Compute target world position of the vertex (where it should be without jiggle, i.e., fully attached)
            Vector3 targetWorldPos = transform.TransformPoint(originalVertices[i]);
            float maxDistance = jiggleAmount[i];
            Vector3 restDelta = currentWorldPositions[i] - targetWorldPos;

            if (maxDistance <= 0f)
            {
                // No jiggle: keep vertex fixed at target position
                currentWorldPositions[i] = targetWorldPos;
                vertexVelocities[i] = Vector3.zero;
            }
            else
            {
                // Calculate spring force towards the target position (Hooke's law: F = k * displacement)
                Vector3 displacement = targetWorldPos - currentWorldPositions[i];
                // Adjust stiffness by the vertex's jiggle amount (looser vertices = lower effective stiffness)
                float adjustedStiffness = springStiffness / Mathf.Max(maxDistance, 0.0001f);

                Vector3 acceleration = displacement * adjustedStiffness;
                vertexVelocities[i] += acceleration * dt;
                // Apply damping to reduce velocity (simple linear damping)
                vertexVelocities[i] *= Mathf.Max(1f - damping * dt, 0f);
                currentWorldPositions[i] += vertexVelocities[i] * dt;

                // --- SOFT LIMIT: apply restoring force if outside maximum distance ---
                restDelta = currentWorldPositions[i] - targetWorldPos;
                float sqrDist = restDelta.sqrMagnitude;
                if (sqrDist > maxDistance * maxDistance)
                {
                    Vector3 correctionDir = restDelta.normalized;
                    float correctionStrength = softLimitStrength * (Mathf.Sqrt(sqrDist) - maxDistance);
                    vertexVelocities[i] -= correctionDir * correctionStrength * dt;
                    currentWorldPositions[i] -= correctionDir * correctionStrength * dt;
                }

                // --- FRAME-DELTA LIMIT: cap how far a vertex can move in one frame ---
                Vector3 frameDelta = currentWorldPositions[i] - previousWorldPositions[i];
                float maxMovePerFrame = maxDistance * maxMovePerFrameMultiplier;
                if (frameDelta.sqrMagnitude > maxMovePerFrame * maxMovePerFrame)
                {
                    frameDelta = frameDelta.normalized * maxMovePerFrame;
                    currentWorldPositions[i] = previousWorldPositions[i] + frameDelta;
                    vertexVelocities[i] = frameDelta / Mathf.Max(dt, 0.0001f);
                }
            }

            // Convert updated world position back to local space for mesh
            deformedVertices[i] = transform.InverseTransformPoint(currentWorldPositions[i]);
            previousWorldPositions[i] = currentWorldPositions[i];
        }

        // Apply the deformed vertices to the mesh
        mesh.vertices = deformedVertices;
        //mesh.RecalculateNormals();  // update surface normals to match the new shape
        //mesh.RecalculateBounds();   // update mesh bounds (important for correct rendering/culling)
    }
}
