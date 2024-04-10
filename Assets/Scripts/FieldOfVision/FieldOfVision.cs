using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif
/// <summary>
/// Calculations will be calculated in world space and tranformed to local or world space according to operation.
/// </summary>
public class FieldOfVision : MonoBehaviour
{
    [Range(0, 360f)]
    public float fieldOfViewAngle = 30f;
    public float fieldOfViewRadius = 5f;
    [Range(0.001f, 20f)]
    public float lineThickness = 1f;
    public Mesh mesh;
    [SerializeField] bool canShowRays = false;
    [SerializeField] bool showLogs = false;

    private void Awake() {
        //Newing mesh prevents mem leaks.
        mesh = new();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    [SerializeField] float scannerInterval = 0.1f;
    private float scannerLastTime = 0;
    private void Update() {

        if (Time.time >= scannerInterval + scannerLastTime) {
            scannerLastTime = Time.time;
            GetDetectedColliders();
            GetClosestDetection();
            if (showLogs) {
                Debug.LogWarning("Scann Thick!");
            }
        }
    }

    //LateUpdate fixes weird behaviours of rendering.
    private void LateUpdate() {
        FireRaycastsInView();
        RecalculateGeometry();
    }

    public Vector3 GetVector3FromAngle(float alpha) {
        Vector3 z = Vector3.right * Mathf.Sin(alpha * Mathf.Deg2Rad);
        Vector3 x = Vector3.forward * Mathf.Cos(alpha * Mathf.Deg2Rad);
        return z + x;
    }

    public Vector3 GetVector3FromHalvedAngle(float alpha) {
        return GetVector3FromAngle(alpha * 0.5f);
    }

    [Range(2, 2000)]
    public int numberOfLines = 5;
    public int numberOfTrianlges = 0; 
    public int numberOfTriangleEdges = 0;
    public int numberOfVertices = 0;


    public int[] triangles;
    public void RecalculateGeometry() {

        numberOfTrianlges = numberOfLines - 1;
        numberOfTriangleEdges = 3 * numberOfTrianlges;

        triangles = new int[numberOfTriangleEdges];
        //Debug.LogWarning("Triangle RectangularGeometry > " + triangles.Length);
        for(int i = 0; i < numberOfTrianlges; i++) {
            triangles[3 * i] = 0;
            triangles[3 * i + 1] = i + 1;
            triangles[3 * i + 2] = i + 2;
        }
        mesh.Clear();
        mesh.vertices = rayDetectionPoints.ToArray();
        mesh.triangles = triangles;
        mesh.RecalculateNormals();      
    }

    Collider[] detectedColliders = new Collider[1000];
    [SerializeField] int numberOfDetectedColliders = 0;
    [SerializeField] LayerMask enemyLayer, obstructionLayer;
    public void GetDetectedColliders() {
        numberOfDetectedColliders = Physics.OverlapSphereNonAlloc(transform.position, fieldOfViewRadius, detectedColliders, enemyLayer);
        if(numberOfDetectedColliders == 0) {
            closestCollider = null;
        }
    }

    //The Closest one will be fetched here.
    [SerializeField] Collider closestCollider;
    public void GetClosestDetection() {
        float lastClosestDistance = float.MaxValue;
        for (int i = 0; i < numberOfDetectedColliders; i++) {
            float dist = Vector3.Distance(transform.position, detectedColliders[i].transform.position);
            if(dist < lastClosestDistance) {
                lastClosestDistance = dist;
                closestCollider = detectedColliders[i];
            }
        }
    }

    public List<Vector3> rayDetectionPoints;
    public void FireRaycastsInView() {
        float angleInterval = fieldOfViewAngle / (numberOfLines - 1);
        float rewind = fieldOfViewAngle * 0.5f;
        rayDetectionPoints.Clear();
        RaycastHit hit;
        rayDetectionPoints.Add(Vector3.zero);
        for (int i = 0; i < numberOfLines; i++) {

            Vector3 pnt = GetVector3FromAngle(angleInterval * i - rewind);

            if (canShowRays) {
                Debug.DrawRay(transform.position, transform.TransformDirection(pnt * fieldOfViewRadius), Color.green);
                Debug.LogWarning("Hitting");
            }

            if (Physics.Raycast(transform.position, transform.TransformDirection(pnt), out hit, fieldOfViewRadius, obstructionLayer) == true) {
                rayDetectionPoints.Add(transform.InverseTransformPoint(hit.point));
                
            }
            else {
                rayDetectionPoints.Add(pnt * fieldOfViewRadius);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FieldOfVision))]
public class FieldOfVisionEditor : Editor {

}
#endif