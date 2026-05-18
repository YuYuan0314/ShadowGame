using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShadowManager : MonoBehaviour
{
    [Header("Shadow References")]
    public Light dirLight;
    public GameObject receiveShadowObj;
    public List<GameObject> castShadowObjs = new List<GameObject>();
    public LayerMask casterLayer;

    [Header("Projection Accuracy")]
    [Tooltip("Use mesh vertices instead of only renderer bounds. This makes moving and rotated caster shadows tighter.")]
    public bool useMeshVerticesForProjection = true;
    [Tooltip("Maximum vertices sampled from each mesh when building a shadow hull.")]
    public int maxProjectionVerticesPerMesh = 256;

    private struct NPlane
    {
        public Plane plane;
        public Vector3 origin;
        public Vector3 u;
        public Vector3 v;
    }

    private NPlane shadowPlane;
    private readonly List<Vector2[]> allHulls = new List<Vector2[]>();
    private Vector3 lastLightDir;
    private readonly List<Vector3> lastObjPositions = new List<Vector3>();
    private readonly List<Quaternion> lastObjRotations = new List<Quaternion>();
    private readonly List<Vector3> lastObjScales = new List<Vector3>();
    private bool hullsDirty = true;

    void Update()
    {
        EnsureShadowHullsCurrent();
    }

    public GameObject GetProjectedShadowSource(Vector3 worldPoint)
    {
        EnsureShadowHullsCurrent();
        if (receiveShadowObj == null) return null;

        Vector2 p = To2D(worldPoint, shadowPlane);
        for (int i = 0; i < allHulls.Count; i++)
        {
            if (i >= castShadowObjs.Count) continue;

            GameObject candidate = castShadowObjs[i];
            if (candidate == null || !candidate.activeInHierarchy) continue;

            if (IsPointInConvexPolygon(allHulls[i], p))
                return candidate;
        }

        return null;
    }

    public GameObject GetShadowSource(Vector3 worldPoint)
    {
        EnsureShadowHullsCurrent();
        if (receiveShadowObj == null || dirLight == null) return null;

        Vector2 p = To2D(worldPoint, shadowPlane);
        for (int i = 0; i < allHulls.Count; i++)
        {
            if (!IsPointInConvexPolygon(allHulls[i], p)) continue;
            if (i >= castShadowObjs.Count) continue;

            GameObject candidate = castShadowObjs[i];
            if (candidate == null || !candidate.activeInHierarchy) continue;

            Vector3 reverseLightDir = -dirLight.transform.forward;
            Vector3 rayStart = worldPoint + reverseLightDir * -0.1f;
            if (Physics.Raycast(rayStart, reverseLightDir, out RaycastHit hit, 100f, casterLayer))
            {
                if (hit.collider.gameObject == candidate || hit.transform.IsChildOf(candidate.transform))
                    return candidate;
            }
        }

        return null;
    }

    public bool IsInProjectedArea(Vector3 worldPoint)
    {
        EnsureShadowHullsCurrent();
        if (receiveShadowObj == null) return false;

        Vector2 p = To2D(worldPoint, shadowPlane);
        return allHulls.Any(hull => IsPointInConvexPolygon(hull, p));
    }

    public bool IsNearProjectedArea(Vector3 worldPoint, float tolerance)
    {
        EnsureShadowHullsCurrent();
        if (receiveShadowObj == null) return false;

        Vector2 p = To2D(worldPoint, shadowPlane);
        foreach (var hull in allHulls)
        {
            if (IsPointNearConvexPolygon(hull, p, tolerance))
                return true;
        }

        return false;
    }

    public Vector3 GetSafePositionInShadow(GameObject caster)
    {
        EnsureShadowHullsCurrent();
        if (caster == null || receiveShadowObj == null)
            return Vector3.zero;

        int index = castShadowObjs.IndexOf(caster);
        if (index < 0 || index >= allHulls.Count)
            return Vector3.zero;

        Vector2[] hull = allHulls[index];
        if (hull.Length < 3)
            return Vector3.zero;

        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < hull.Length; i++)
            centroid += hull[i];
        centroid /= hull.Length;

        return shadowPlane.origin + shadowPlane.u * centroid.x + shadowPlane.v * centroid.y;
    }

    public void UpdateAllShadowHulls()
    {
        if (receiveShadowObj == null || dirLight == null) return;

        shadowPlane = GetReceivePlane(receiveShadowObj);
        allHulls.Clear();
        Vector3 lightDir = dirLight.transform.forward;

        foreach (var obj in castShadowObjs)
        {
            if (obj == null || !obj.activeInHierarchy || obj.transform.lossyScale.sqrMagnitude < 0.001f)
            {
                allHulls.Add(new Vector2[0]);
                continue;
            }

            List<Vector2> points2D = GetProjectionSourcePoints(obj)
                .Select(point => ProjectPointToPlane(point, shadowPlane.plane, lightDir))
                .Select(point => To2D(point, shadowPlane))
                .ToList();

            allHulls.Add(GrahamScan(points2D).ToArray());
        }
    }

    private void EnsureShadowHullsCurrent()
    {
        if (dirLight == null || receiveShadowObj == null) return;

        if (hullsDirty || dirLight.transform.forward != lastLightDir || DidObjectsMove())
        {
            UpdateAllShadowHulls();
            CacheStates();
            hullsDirty = false;
        }
    }

    private bool DidObjectsMove()
    {
        if (castShadowObjs.Count != lastObjPositions.Count) return true;

        for (int i = 0; i < castShadowObjs.Count; i++)
        {
            GameObject obj = castShadowObjs[i];
            if (obj == null) continue;

            if ((obj.transform.position - lastObjPositions[i]).sqrMagnitude > 0.0001f) return true;
            if (Quaternion.Angle(obj.transform.rotation, lastObjRotations[i]) > 0.01f) return true;
            if ((obj.transform.lossyScale - lastObjScales[i]).sqrMagnitude > 0.0001f) return true;
        }

        return false;
    }

    private void CacheStates()
    {
        lastLightDir = dirLight != null ? dirLight.transform.forward : Vector3.zero;

        lastObjPositions.Clear();
        lastObjRotations.Clear();
        lastObjScales.Clear();
        foreach (var obj in castShadowObjs)
        {
            lastObjPositions.Add(obj != null ? obj.transform.position : Vector3.zero);
            lastObjRotations.Add(obj != null ? obj.transform.rotation : Quaternion.identity);
            lastObjScales.Add(obj != null ? obj.transform.lossyScale : Vector3.zero);
        }
    }

    private NPlane GetReceivePlane(GameObject obj)
    {
        Vector3 normal = obj.transform.up;
        Vector3 origin = obj.transform.position;
        Plane plane = new Plane(normal, origin);

        Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
        Vector3 v = Vector3.Cross(u, normal).normalized;

        return new NPlane { plane = plane, origin = origin, u = u, v = v };
    }

    private Vector3 ProjectPointToPlane(Vector3 point, Plane plane, Vector3 direction)
    {
        float dot = Vector3.Dot(plane.normal, direction);
        if (Mathf.Abs(dot) < 0.0001f)
            return point;

        float t = -plane.GetDistanceToPoint(point) / dot;
        return point + direction * t;
    }

    private Vector2 To2D(Vector3 point, NPlane plane)
    {
        Vector3 local = point - plane.origin;
        return new Vector2(Vector3.Dot(local, plane.u), Vector3.Dot(local, plane.v));
    }

    private float Cross(Vector2 origin, Vector2 a, Vector2 b)
    {
        return (a.x - origin.x) * (b.y - origin.y) - (b.x - origin.x) * (a.y - origin.y);
    }

    private List<Vector2> GrahamScan(List<Vector2> points)
    {
        points = points
            .Where(point => float.IsFinite(point.x) && float.IsFinite(point.y))
            .Distinct()
            .ToList();

        if (points.Count < 3)
            return points;

        points = points.OrderBy(point => point.x).ThenBy(point => point.y).ToList();

        List<Vector2> lower = new List<Vector2>();
        foreach (Vector2 point in points)
        {
            while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], point) <= 0f)
                lower.RemoveAt(lower.Count - 1);
            lower.Add(point);
        }

        List<Vector2> upper = new List<Vector2>();
        for (int i = points.Count - 1; i >= 0; i--)
        {
            Vector2 point = points[i];
            while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], point) <= 0f)
                upper.RemoveAt(upper.Count - 1);
            upper.Add(point);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    private bool IsPointInConvexPolygon(Vector2[] hull, Vector2 point)
    {
        if (hull == null || hull.Length < 3) return false;

        for (int i = 0; i < hull.Length; i++)
        {
            if (Cross(hull[i], hull[(i + 1) % hull.Length], point) < -0.001f)
                return false;
        }

        return true;
    }

    private bool IsPointNearConvexPolygon(Vector2[] hull, Vector2 point, float tolerance)
    {
        if (hull == null || hull.Length < 3) return false;
        if (IsPointInConvexPolygon(hull, point)) return true;

        for (int i = 0; i < hull.Length; i++)
        {
            Vector2 a = hull[i];
            Vector2 b = hull[(i + 1) % hull.Length];
            if (PointToSegmentDistance(point, a, b) < tolerance)
                return true;
        }

        return false;
    }

    private float PointToSegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(point - a, ab) / Mathf.Max(Vector2.Dot(ab, ab), 0.0001f);
        t = Mathf.Clamp01(t);
        return Vector2.Distance(point, a + ab * t);
    }

    private IEnumerable<Vector3> GetProjectionSourcePoints(GameObject obj)
    {
        if (useMeshVerticesForProjection)
        {
            List<Vector3> meshPoints = new List<Vector3>();
            MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;
                if (!meshFilter.sharedMesh.isReadable) continue;

                Vector3[] vertices = meshFilter.sharedMesh.vertices;
                int step = Mathf.Max(1, vertices.Length / Mathf.Max(1, maxProjectionVerticesPerMesh));
                for (int i = 0; i < vertices.Length; i += step)
                    meshPoints.Add(meshFilter.transform.TransformPoint(vertices[i]));
            }

            if (meshPoints.Count >= 3)
                return meshPoints;
        }

        return GetBoundsWorldPoints(obj);
    }

    private IEnumerable<Vector3> GetBoundsWorldPoints(GameObject obj)
    {
        Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
            bounds.Encapsulate(renderer.bounds);

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        return new Vector3[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };
    }
}
