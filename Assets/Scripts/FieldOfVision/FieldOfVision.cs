using UnityEngine;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif
/// <summary>
/// Calculations will be calculated in world space and tranformed to local or world space according to operation. 
/// Haluk OZGEN.
/// </summary>
public class FieldOfVision : MonoBehaviour
{
    [Range(0, 360f)]
    public float fieldOfViewAngle = 30f;
    [Range(0, 1000f)]
    public float fieldOfViewRadius = 5f;
    private Mesh mesh;

    [Header("Log Options")]
    [SerializeField] bool canShowRays = false;
    [SerializeField] bool showLogs = false;
    [SerializeField] bool showOnGuiLogs = false;

    [Header("Ray Informations")]
    [ReadOnlyAttr]
    public int numberOfLines = 5;
    [ReadOnlyAttr]
    public int numberOfTrianlges = 0;
    [ReadOnlyAttr]
    public int numberOfTriangleEdges = 0;
    [ReadOnlyAttr]
    public int numberOfVertices = 0;

    [Header("Arc Length")]
    [ReadOnlyAttr] public float arcLength = 0;
    [Range(0, 100f)]
    [SerializeField] float arcResolution = 0;

    [Header("Number Of Try To Find Root Rays")]
    [SerializeField]
    [Range(0, 50)]
    private int rootIterationForInnerRays = 0;

    [Header("Enemy And Obstruction Layers For Ray And OverlapSphereNonAlloc")]
    [SerializeField] LayerMask enemyLayer;
    [SerializeField] LayerMask obstructionLayer;

    [Header("OverlapSphereNonAlloc Detection")]
    [ReadOnlyAttr][SerializeField] int numberOfDetectedColliders = 0;
    [ReadOnlyAttr][SerializeField] Collider closestCollider;
    [SerializeField] [Range(0, 0.5f)] float scannerInterval = 0.1f;
    private float scannerLastTime = 0;
    Collider[] detectedColliders = new Collider[1000];

    [Header("Triangles and Detection Points By Raycasts")]
    [HideInInspector] public List<Vector3> rayDetectionPoints;
    [HideInInspector] int[] triangles;

    private void Awake() {
        //Newing mesh prevents mem leaks.
        mesh = new();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void Update() {
        StartRadarScan();
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

    public void GetDetectedColliders() {
        numberOfDetectedColliders = Physics.OverlapSphereNonAlloc(transform.position, fieldOfViewRadius, detectedColliders, enemyLayer);
        if(numberOfDetectedColliders == 0) {
            closestCollider = null;
        }
    }

    //The Closest one will be fetched here.
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

    //Scan with time intervals to find closest objects if you want.
    public void StartRadarScan() {
        if (Time.time >= scannerInterval + scannerLastTime) {
            scannerLastTime = Time.time;
            GetDetectedColliders();
            GetClosestDetection();
            if (showLogs) {
                Debug.LogWarning("Scann Thick!");
            }
        }
    }
   
    public void FireRaycastsInView() {
        float angleInterval = fieldOfViewAngle / (numberOfLines - 1);
        float rewind = fieldOfViewAngle * 0.5f;
        RayInfo castOld = RayInfo.Default();
        rayDetectionPoints.Clear();
        rayDetectionPoints.Add(Vector3.zero);
        int i = 0;
        while(i < numberOfLines) {

            Vector3 pnt = GetVector3FromAngle(angleInterval * i - rewind);

            if (canShowRays) {
                Debug.DrawRay(transform.position, transform.TransformDirection(pnt * fieldOfViewRadius), Color.green);
                Debug.LogWarning("Hitting");
            }

            RayInfo castNew = FireRaycast(pnt);

            //Compare new and old id and decide if both the same then go to the next standart ray if not then try to slide inner rays and find min max ray points.
            if (castOld.instanceId != castNew.instanceId) {
                (castOld, castNew) = FindRootRay(castOld, castNew, rootIterationForInnerRays);
                if (castOld.hitLocalPosition != Vector3.zero) {
                    rayDetectionPoints.Add(castOld.hitLocalPosition);
                }
                if (castNew.hitLocalPosition != Vector3.zero) {
                    rayDetectionPoints.Add(castNew.hitLocalPosition);
                }
            }
            else {
                //If standart sliced ray does not hit anything then go to the next standart ray and increase the number of i.
                i++;
            }
            rayDetectionPoints.Add(castNew.hitLocalPosition);
            castOld = castNew;
        }
    }

    //This part worls with unit vector points in local place. Finds the farthest and closest possible points.
    private (RayInfo, RayInfo) FindRootRay(RayInfo castOldInfo, RayInfo castNewInfo, int iteration) {
        RayInfo newCaster;
        Vector3 maxtomin = (castOldInfo.unitDirVector + castNewInfo.unitDirVector) * 0.5f;
        newCaster = FireRaycast(maxtomin);

        if (castOldInfo.instanceId == newCaster.instanceId) {
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

    //Fire raycast and fill up rayinfo about hit to use later.
    public RayInfo FireRaycast(Vector3 localToWorld) {
        RaycastHit hit;
        RayInfo rayInfo;
        if (Physics.Raycast(transform.position, transform.TransformDirection(localToWorld), out hit, fieldOfViewRadius, obstructionLayer) == true) {
            rayInfo = RayInfo.Get(true, hit.distance, localToWorld, transform.InverseTransformPoint(hit.point), hit.colliderInstanceID , hit.collider);
        }
        else {
            rayInfo = RayInfo.Get(false, hit.distance, localToWorld, localToWorld * fieldOfViewRadius, int.MaxValue, hit.collider);
        }
        return rayInfo;
    }

    //Arc Length = Q * r; Finds Arc Length to calculate proportional raycasts.
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

//To take info to global
public struct RayInfo {
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


[CustomPropertyDrawer(typeof(ReadOnlyAttr))]
public class ReadOnlyDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        // Özelliðin deðerini çizmeden önce saklayalým
        EditorGUI.BeginDisabledGroup(true);
        EditorGUI.PropertyField(position, property, label);
        EditorGUI.EndDisabledGroup();
    }
}

public class ReadOnlyAttr : PropertyAttribute { }

#endif

