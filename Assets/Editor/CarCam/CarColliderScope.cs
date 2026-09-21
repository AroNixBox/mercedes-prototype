using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CarCam
{
    /// <summary>Adds MeshColliders to car parts that have none, for the lifetime of the scope (path validation needs colliders).</summary>
    public sealed class CarColliderScope : IDisposable
    {
        readonly List<MeshCollider> m_Added = new();

        public int AddedCount => m_Added.Count;

        public CarColliderScope(Transform car)
        {
            foreach (var meshFilter in MissingColliders(car))
            {
                var meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.hideFlags = HideFlags.DontSave;
                m_Added.Add(meshCollider);
            }
            Physics.SyncTransforms();
        }

        public static int CountMissing(Transform car) => MissingColliders(car).Count();

        static IEnumerable<MeshFilter> MissingColliders(Transform car) =>
            car.GetComponentsInChildren<MeshFilter>()
                .Where(mf => mf.sharedMesh != null && mf.GetComponent<Collider>() == null);

        public void Dispose()
        {
            foreach (var meshCollider in m_Added)
            {
                if (meshCollider != null)
                {
                    Object.DestroyImmediate(meshCollider);
                }
            }
            m_Added.Clear();
            Physics.SyncTransforms();
        }
    }
}
