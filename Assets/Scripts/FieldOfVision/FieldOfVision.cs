using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEditor.TerrainTools;



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
    public bool showOnGuiLogs = false;

    private void Awake() {
        //Newing mesh prevents mem leaks.
        mesh = new();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    [SerializeField] float scannerInterval = 0.1f;
    private float scannerLastTime = 0;
    private void Update() {

        //if (Time.time >= scannerInterval + scannerLastTime) {
        //    scannerLastTime = Time.time;
        //    GetDetectedColliders();
        //    GetClosestDetection();
        //    if (showLogs) {
        //        Debug.LogWarning("Scann Thick!");
        //    }
        //}
    }

    //LateUpdate fixes weird behaviours of rendering.
    private void LateUpdate() {
        GetArcLength();
        SetNumberOfRegulatedRays();
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

    [SerializeField]
    [Range(0, 50)]
    private int rootIterationForInnerRays = 0;


    public int[] triangles;
    public void RecalculateGeometry() {

        numberOfTrianlges = rayDetectionPoints.Count - 2;
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
        RayInfo castOld = RayInfo.Default();
        rayDetectionPoints.Clear();
        rayDetectionPoints.Add(Vector3.zero);
        for (int i = 0; i < numberOfLines; i++) {

            Vector3 pnt = GetVector3FromAngle(angleInterval * i - rewind);

            if (canShowRays) {
                Debug.DrawRay(transform.position, transform.TransformDirection(pnt * fieldOfViewRadius), Color.green);
                Debug.LogWarning("Hitting");
            }

            RayInfo castNew = FireRaycast(pnt);
            RayInfo castReg = RayInfo.Default();
            if(i > 0) {
                if (castOld.instanceId != castNew.instanceId) {
                    RayInfo oldone, newone;
                    (castReg, castNew) = FindRootRay(castOld, castNew, rootIterationForInnerRays);
                    if (castReg.hitLocalPosition != Vector3.zero) {
                        rayDetectionPoints.Add(castReg.hitLocalPosition);
                    }

                    if (castNew.hitLocalPosition != Vector3.zero) {
                        rayDetectionPoints.Add(castNew.hitLocalPosition);
                    }
                }
            }



            rayDetectionPoints.Add(castNew.hitLocalPosition);
            castOld = castNew;
        }
    }

    private (RayInfo, RayInfo) FindRootRay(RayInfo castOldInfo, RayInfo castNewInfo, int iteration) {

        RayInfo newCaster;
        //Yonlu vektor oldugundan hangisi ray cast once veya sonra min onem farketmez. O yone dogru aralama yapar.
        Vector3 maxtomin = (castOldInfo.unitDirVector + castNewInfo.unitDirVector) * 0.5f;
        newCaster = FireRaycast(maxtomin);

        if (castOldInfo.instanceId == newCaster.instanceId) {
            //Debug.LogWarning("old, new > " + castOldInfo.instanceId + " " + castNewInfo.instanceId);
            castOldInfo = newCaster;
        }
        else {
            castNewInfo = newCaster;
        }

        if (iteration == 0) {
            return (castOldInfo, castNewInfo);
        }
        else {
            return FindRootRay(castOldInfo, castNewInfo, iteration - 1);
        }
    }

    public RayInfo FireRaycast(Vector3 localToWorld) {
        RaycastHit hit;
        RayInfo rayInfo;
        if (Physics.Raycast(transform.position, transform.TransformDirection(localToWorld), out hit, fieldOfViewRadius, obstructionLayer) == true) {
            rayInfo = RayInfo.Get(true, hit.distance, localToWorld, transform.InverseTransformPoint(hit.point), hit.colliderInstanceID , hit.collider);
            //rayDetectionPoints.Add(transform.InverseTransformPoint(hit.point));
        }
        else {
            rayInfo = RayInfo.Get(false, hit.distance, localToWorld, localToWorld * fieldOfViewRadius, int.MaxValue, hit.collider);
            //rayDetectionPoints.Add(localToWorld * fieldOfViewRadius);
        }
        return rayInfo;
    }

    [Header("Arc Length")]
    [SerializeField] float arcLength = 0;

    [Range(0, 100f)]
    [SerializeField] float arcResolution = 0;

    //Arc Length = Q * r;
    private void GetArcLength() {
        arcLength = fieldOfViewRadius * Mathf.Deg2Rad * fieldOfViewAngle;
    }

    private void SetNumberOfRegulatedRays() {
        numberOfLines = Mathf.CeilToInt(arcLength * arcResolution * 0.005f);
        if (numberOfLines < 4) {
            numberOfLines = 4;
        }
    }
}

public struct RayInfo {
    //To take info to global
    public bool ishit;
    public float distance;
    public Vector3 unitDirVector, hitLocalPosition;
    public int instanceId;
    public Collider col;
    private RayInfo(bool _isHit, float _distance, Vector3 _unitDirVector, Vector3 _hitLocalPosition, int _instanceId, Collider _col) {
        ishit = _isHit;
        distance = _distance;
        hitLocalPosition = _hitLocalPosition;
        unitDirVector = _unitDirVector;
        instanceId = _instanceId;
        col = _col;
    }
    public static RayInfo Get(bool _isHit, float _distance, Vector3 _unitDirVector, Vector3 _hitLocalPosition, int _instanceId, Collider _col) {
        return new RayInfo(_isHit, _distance, _unitDirVector, _hitLocalPosition, _instanceId, _col);
    }
    public static RayInfo Default() {
        return default;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FieldOfVision))]
public class FieldOfVisionEditor : Editor {

    FieldOfVision fov;
    SerializedProperty showGuiLabels;
    private void OnEnable() {
        fov = (FieldOfVision)target;
        showGuiLabels = serializedObject.FindProperty(nameof(FieldOfVision.showOnGuiLogs));
    }

    public override void OnInspectorGUI() {
        serializedObject.Update();
        base.OnInspectorGUI();      
        serializedObject.ApplyModifiedProperties();
        
    }

    public void OnSceneGUI() {
        if(showGuiLabels.boolValue == true) {
            if (fov.rayDetectionPoints != null) {
                Vector3 oldPoint = Vector3.zero;
                for (int i = 0; i < fov.rayDetectionPoints.Count; i++) {
                    if(Vector3.Distance(oldPoint, fov.transform.TransformPoint(fov.rayDetectionPoints[i])) < 0.2f) {
                        Handles.Label((fov.transform.TransformPoint(fov.rayDetectionPoints[i]) - fov.transform.position) * 0.60f, $", {i} POINTS");
                        oldPoint = fov.transform.TransformPoint(fov.rayDetectionPoints[i]);
                    }
                    else {
                        Handles.Label(fov.transform.TransformPoint(fov.rayDetectionPoints[i]), $", {i} POINTS");
                    }
                    
                }
            }
        }

    }
}
#endif