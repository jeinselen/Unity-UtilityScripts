using UnityEngine;
using UnityEditor;
using UnityEngine.Splines;
using System.Collections.Generic;
using Unity.Mathematics; // For float3

[ExecuteInEditMode]
public class SplineImporter : MonoBehaviour
{
    public TextAsset csvFile; // TextAsset for the CSV file
    
    public enum SplineMode { Auto, Linear }
    public SplineMode splineMode = SplineMode.Auto; // Dropdown menu for spline mode
    
//  [ContextMenu("Import Spline From CSV")]
    public void ImportSplineFromCSV()
    {
        if (csvFile == null)
        {
            Debug.LogError("CSV file asset is not assigned.");
            return;
        }
        
        List<List<Vector3>> splinePoints = ReadCSV(csvFile);
        if (splinePoints == null || splinePoints.Count == 0)
        {
            Debug.LogError("No points found in the CSV file.");
            return;
        }
        
        SplineContainer splineContainer = GetComponent<SplineContainer>();
        if (splineContainer == null)
        {
            splineContainer = gameObject.AddComponent<SplineContainer>();
        }
        
        Spline spline = new Spline();
        foreach (var point in splinePoints)
        {
            if (point.Count > 1)
            {
                // Bézier Knot with tangents
                BezierKnot knot = new BezierKnot(point[0], point[1], point[2]);
                spline.Add(knot);
            }
            else
            {
                // Linear or AutoSmooth knot
                float3 position = point[0];
                spline.Add(position, splineMode == SplineMode.Auto ? TangentMode.AutoSmooth : TangentMode.Linear);
            }
        }
        
        splineContainer.Spline = spline;
        Debug.Log("Spline imported successfully.");
    }

    private List<List<Vector3>> ReadCSV(TextAsset csvFile)
    {
        List<List<Vector3>> points = new List<List<Vector3>>();
        
        try
        {
            string[] lines = csvFile.text.Split('\n');
            foreach (string line in lines)
            {
                string[] values = line.Split(',');
                if (values.Length >= 9) // Bézier curve with tangents
                {
                    if (float.TryParse(values[0], out float x) &&
                        float.TryParse(values[1], out float y) &&
                        float.TryParse(values[2], out float z) &&
                        float.TryParse(values[3], out float tangentInX) &&
                        float.TryParse(values[4], out float tangentInY) &&
                        float.TryParse(values[5], out float tangentInZ) &&
                        float.TryParse(values[6], out float tangentOutX) &&
                        float.TryParse(values[7], out float tangentOutY) &&
                        float.TryParse(values[8], out float tangentOutZ))
                    {
                        Vector3 position = new Vector3(x, y, z);
                        Vector3 tangentIn = new Vector3(tangentInX, tangentInY, tangentInZ);
                        Vector3 tangentOut = new Vector3(tangentOutX, tangentOutY, tangentOutZ);

                        points.Add(new List<Vector3> { position, tangentIn, tangentOut });
                    }
                }
                else if (values.Length >= 3) // Direct point
                {
                    if (float.TryParse(values[0], out float x) &&
                        float.TryParse(values[1], out float y) &&
                        float.TryParse(values[2], out float z))
                    {
                        Vector3 position = new Vector3(x, y, z);
                        points.Add(new List<Vector3> { position });
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error reading CSV file: {ex.Message}");
        }
        
        return points;
    }
}
