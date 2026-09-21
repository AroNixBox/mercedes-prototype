using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace CarCam.Tests
{
    /// <summary>Floor top at y = 0. Car "TestCar": 2 m wide, 1.4 m high, 4.5 m long, centered at the origin, front = +Z.</summary>
    public static class CarCamTestScene
    {
        public const string CarName = "TestCar";
        public static readonly Vector3 CarCenter = new(0f, 0.7f, 0f);

        public static void NewEmptyScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        public static Transform CreateCar(bool withColliders = true)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);

            var car = new GameObject(CarName).transform;
            Part(car, "Body", new Vector3(0f, 0.7f, 0f), new Vector3(2f, 1.4f, 4.5f), withColliders);
            Part(car, "Headlight_L", new Vector3(-0.7f, 0.6f, 2.2f), new Vector3(0.3f, 0.15f, 0.1f), withColliders);
            Part(car, "Wheel_FR", new Vector3(0.85f, 0.35f, 1.4f), new Vector3(0.3f, 0.7f, 0.7f), withColliders);
            Part(car, "Wheel_RR", new Vector3(0.85f, 0.35f, -1.4f), new Vector3(0.3f, 0.7f, 0.7f), withColliders);
            Physics.SyncTransforms();
            return car;
        }

        public static GameObject CreatePillar(Vector3 position)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pillar";
            pillar.transform.position = position;
            pillar.transform.localScale = new Vector3(0.5f, 3f, 0.5f);
            Physics.SyncTransforms();
            return pillar;
        }

        public static SplineContainer Spline(params Vector3[] points) =>
            AiCameraDirectorBridge.BuildSpline(new GameObject("TestPath"), points);

        static void Part(Transform car, string name, Vector3 position, Vector3 scale, bool withCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(car, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (!withCollider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
        }
    }
}
