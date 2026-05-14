using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShadowManager : MonoBehaviour
{
    [Header("��������")]
    public Light dirLight;
    public GameObject receiveShadowObj; 
    public List<GameObject> castShadowObjs = new List<GameObject>();
    public LayerMask casterLayer; 

    private struct NPlane { public Plane plane; public Vector3 O, U, V; }
    private NPlane shadowPlane;
    private List<Vector2[]> allHulls = new List<Vector2[]>();
    private Vector3 lastLightDir;
    private List<Vector3> lastObjPositions = new List<Vector3>();

    void Update()
    {
        if (dirLight.transform.forward != lastLightDir || DidObjectsMove())
        {
            UpdateAllShadowHulls();
            CacheStates();
        }
    }

    // ��ȡ��ǰλ���������ض�Ӱ��Դ
    public GameObject GetShadowSource(Vector3 worldPoint)
    {
        if (receiveShadowObj == null) return null;
        Vector2 p = To2D(worldPoint, shadowPlane);
        
        for (int i = 0; i < allHulls.Count; i++)
        {
            if (IsPointInConvexPolygon(allHulls[i], p))
            {
                if (i >= castShadowObjs.Count) continue;
                GameObject candidate = castShadowObjs[i];
                if (candidate == null || !candidate.activeInHierarchy) continue;

                Vector3 reverseLightDir = -dirLight.transform.forward;
                Vector3 rayStart = worldPoint + Vector3.up * 0.1f; 
                if (Physics.Raycast(rayStart, reverseLightDir, out RaycastHit hit, 100f, casterLayer))
                {
                    if (hit.collider.gameObject == candidate || hit.transform.IsChildOf(candidate.transform))
                        return candidate;
                }
            }
        }
        return null;
    }

    public bool IsInProjectedArea(Vector3 worldPoint)
    {
        Vector2 p = To2D(worldPoint, shadowPlane);
        return allHulls.Any(hull => IsPointInConvexPolygon(hull, p));
    }

    private bool DidObjectsMove()
    {
        if (castShadowObjs.Count != lastObjPositions.Count) return true;
        for (int i = 0; i < castShadowObjs.Count; i++)
        {
            if (castShadowObjs[i] == null) continue;
            if ((castShadowObjs[i].transform.position - lastObjPositions[i]).sqrMagnitude > 0.0001f) return true;
        }
        return false;
    }

    private void CacheStates()
    {
        lastLightDir = dirLight.transform.forward;
        lastObjPositions = castShadowObjs.Select(obj => obj != null ? obj.transform.position : Vector3.zero).ToList();
    }

    public void UpdateAllShadowHulls()
    {
        if (receiveShadowObj == null) return;
        shadowPlane = GetReceivePlane(receiveShadowObj);
        allHulls.Clear();
        Vector3 lightDir = dirLight.transform.forward;

        foreach (var obj in castShadowObjs)
        {
            if (obj == null || !obj.activeInHierarchy || obj.transform.localScale.sqrMagnitude < 0.001f)
            {
                allHulls.Add(new Vector2[0]); 
                continue;
            }
            var points = GetBoundsWorldPoints(obj);
            var projected = points.Select(v => ProjectPointToPlane(v, shadowPlane.plane, lightDir));
            var points2D = projected.Select(x => To2D(x, shadowPlane)).ToList();
            var hull = GrahamScan(points2D);
            allHulls.Add(hull.ToArray());
        }
    }

    private NPlane GetReceivePlane(GameObject obj) {
        Vector3 up = obj.transform.up; Vector3 pos = obj.transform.position;
        Plane p = new Plane(up, pos); Vector3 n = p.normal;
        Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
        Vector3 v = Vector3.Cross(u, n);
        return new NPlane { plane = p, O = pos, U = u, V = v };
    }
    private Vector3 ProjectPointToPlane(Vector3 pt, Plane pl, Vector3 d) {
        float dot = Vector3.Dot(pl.normal, d);
        if (Mathf.Abs(dot) < 0.0001f) return pt;
        float t = Vector3.Dot(pl.normal, pl.ClosestPointOnPlane(pt) - pt) / dot;
        return pt + t * d;
    }
    private Vector2 To2D(Vector3 p, NPlane pl) => new Vector2(Vector3.Dot(p - pl.O, pl.U), Vector3.Dot(p - pl.O, pl.V));
    private float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (b.x - o.x) * (a.y - o.y);
    private List<Vector2> GrahamScan(List<Vector2> pts) {
        if (pts.Count < 3) return pts;
        pts = pts.OrderBy(v => v.y).ThenBy(v => v.x).ToList();
        Vector2 s = pts[0];
        var sorted = pts.Skip(1).OrderBy(p => Mathf.Atan2(p.y - s.y, p.x - s.x)).ToList();
        List<Vector2> h = new List<Vector2> { s, sorted[0] };
        for (int i = 1; i < sorted.Count; i++) {
            while (h.Count >= 2 && Cross(h[h.Count - 2], h.Last(), sorted[i]) <= 0) h.RemoveAt(h.Count - 1);
            h.Add(sorted[i]);
        }
        return h;
    }
    private bool IsPointInConvexPolygon(Vector2[] h, Vector2 p) {
        if (h.Length < 3) return false;
        for (int i = 0; i < h.Length; i++) {
            if (Cross(h[i], h[(i + 1) % h.Length], p) < -0.001f) return false;
        }
        return true;
    }

    public bool IsNearProjectedArea(Vector3 worldPoint, float tolerance)
    {
        Vector2 p = To2D(worldPoint, shadowPlane);
        foreach (var hull in allHulls)
        {
            if (IsPointNearConvexPolygon(hull, p, tolerance))
                return true;
        }
        return false;
    }

    // 获取指定投射物阴影区域内的安全位置（凸包重心，保证在阴影内部）
    public Vector3 GetSafePositionInShadow(GameObject caster)
    {
        if (caster == null || receiveShadowObj == null)
            return Vector3.zero;

        int index = castShadowObjs.IndexOf(caster);
        if (index < 0 || index >= allHulls.Count)
            return Vector3.zero;

        Vector2[] hull = allHulls[index];
        if (hull.Length < 3)
            return Vector3.zero;

        // 凸包重心一定在多边形内部
        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < hull.Length; i++)
            centroid += hull[i];
        centroid /= hull.Length;

        // 2D → 3D（投影回接收平面）
        return shadowPlane.O + shadowPlane.U * centroid.x + shadowPlane.V * centroid.y;
    }

    private bool IsPointNearConvexPolygon(Vector2[] h, Vector2 p, float tolerance)
    {
        if (h.Length < 3) return false;
        if (IsPointInConvexPolygon(h, p)) return true;

        for (int i = 0; i < h.Length; i++)
        {
            Vector2 a = h[i];
            Vector2 b = h[(i + 1) % h.Length];
            if (PointToSegmentDistance(p, a, b) < tolerance)
                return true;
        }
        return false;
    }

    private float PointToSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        float t = Vector2.Dot(ap, ab) / Mathf.Max(Vector2.Dot(ab, ab), 0.0001f);
        t = Mathf.Clamp01(t);
        Vector2 closest = a + t * ab;
        return Vector2.Distance(p, closest);
    }
    private IEnumerable<Vector3> GetBoundsWorldPoints(GameObject obj) {
        Bounds b = new Bounds(obj.transform.position, Vector3.zero);
        var rs = obj.GetComponentsInChildren<Renderer>();
        foreach (var r in rs) b.Encapsulate(r.bounds);
        Vector3 mi = b.min; Vector3 ma = b.max;
        return new Vector3[] { new Vector3(mi.x, mi.y, mi.z), new Vector3(mi.x, mi.y, ma.z), new Vector3(mi.x, ma.y, mi.z), new Vector3(mi.x, ma.y, ma.z), new Vector3(ma.x, mi.y, mi.z), new Vector3(ma.x, mi.y, ma.z), new Vector3(ma.x, ma.y, mi.z), new Vector3(ma.x, ma.y, ma.z) };
    }
}