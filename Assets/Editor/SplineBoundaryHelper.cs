// using UnityEngine;
//
// public static class SplineBoundaryHelper
// {
//     /// <summary>
//     /// Hilfsklasse um Boundaries für KI-Splines zu visualisieren oder zu validieren.
//     /// </summary>
//     public static void DrawSplineBoundaries(Vector3 center, Vector3 size)
//     {
//         // Diese Methode könnte von der KI aufgerufen werden, um einen visuellen Rahmen zu setzen
//         // bevor ein Spline generiert wird.
//         GameObject boundary = GameObject.CreatePrimitive(PrimitiveType.Cube);
//         boundary.name = "AI_Spline_Boundary";
//         boundary.transform.position = center;
//         boundary.transform.localScale = size;
//         
//         var renderer = boundary.GetComponent<Renderer>();
//         if (renderer != null)
//         {
//             // Transparent machen für Visualisierung
//             var mat = new Material(Shader.Find("Transparent/Diffuse"));
//             mat.color = new Color(0, 1, 0, 0.2f);
//             renderer.sharedMaterial = mat;
//         }
//         
//         Debug.Log($"Boundary gesetzt: {center} mit Größe {size}. Hier sollte der Spline bleiben.");
//     }
//
//     public static bool IsInBounds(Vector3 point, Vector3 center, Vector3 size)
//     {
//         Bounds b = new Bounds(center, size);
//         return b.Contains(point);
//     }
// }
