using UnityEngine;

[ExecuteInEditMode]
public class MaterialSetPosition : MonoBehaviour
{
    Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer> ();
    }

    void Update()
    {
        // transform.position
        // rend.material.SetVector("_Position", new Vector4(1.0f, 0.8f, 0.6f, 1.0f));
        rend.material.SetVector("_Position", transform.position);
    }
}