using UnityEngine;

[RequireComponent(typeof(Light))]
[ExecuteAlways]
public class VolumetricSpotlight : MonoBehaviour
{
    [Header("Beam")]
    [Range(0, 5)] public float beamIntensity = 1f;
    [Range(0f, 1)] public float edgeSoftness = 0f;

    [Header("Mesh")]
    [Range(8, 64)] public int segments = 40;

    [Header("Override (optional)")]
    public Material customMaterial;

    private Light _light;
    private GameObject _beamObj;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Material _material;
    private Mesh _mesh;

    private float _lastRange;
    private float _lastAngle;
    private float _lastIntensity;
    private float _lastSoftness;
    private Color _lastColor;

    // ------------------------------------------------------------
    void OnEnable()
    {
        _light = GetComponent<Light>();
        if (_light == null) return;

        CreateBeamObject();
        RebuildMesh();
        ApplyMaterial();
    }

    void OnDisable()
    {
        if (_mesh != null)
        {
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
            _mesh = null;
        }

        if (_material != null && customMaterial == null)
        {
            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
            _material = null;
        }

        if (_beamObj != null)
        {
            if (Application.isPlaying) Destroy(_beamObj);
            else DestroyImmediate(_beamObj);
            _beamObj = null;
        }
    }

    void OnDestroy()
    {
        // Safety cleanup — OnDisable may not fire in all cases
        if (_beamObj != null)
        {
            if (Application.isPlaying) Destroy(_beamObj);
            else DestroyImmediate(_beamObj);
        }
    }

    void Update()
    {
        if (_light == null) return;

        bool meshDirty = !Mathf.Approximately(_light.range, _lastRange) ||
                         !Mathf.Approximately(_light.spotAngle, _lastAngle);

        if (meshDirty) RebuildMesh();

        bool matDirty = !Mathf.Approximately(beamIntensity, _lastIntensity) ||
                        !Mathf.Approximately(edgeSoftness, _lastSoftness) ||
                        _light.color != _lastColor;

        if (matDirty || _material == null)
            ApplyMaterial();
    }

    // ---- beam object lifecycle --------------------------------------------
    void CreateBeamObject()
    {
        if (_beamObj != null) return;

        _beamObj = new GameObject("VolumetricBeam");
        _beamObj.transform.SetParent(transform);
        _beamObj.transform.localPosition = Vector3.zero;
        _beamObj.transform.localRotation = Quaternion.identity;
        _beamObj.transform.localScale = Vector3.one;
        _beamObj.hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;

        _meshFilter = _beamObj.AddComponent<MeshFilter>();
        _meshRenderer = _beamObj.AddComponent<MeshRenderer>();
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // ---- material ---------------------------------------------------------
    void ApplyMaterial()
    {
        if (_material == null)
        {
            if (customMaterial != null)
            {
                _material = customMaterial;
            }
            else
            {
                var shader = Shader.Find("Custom/VolumetricBeam");
                if (shader != null)
                    _material = new Material(shader);
            }

            if (_material != null && _meshRenderer != null)
                _meshRenderer.sharedMaterial = _material;
        }

        if (_material == null) return;

        Color c = _light.color;
        c.a *= beamIntensity;
        _material.SetColor("_Color", c);
        _material.SetFloat("_EdgeSoftness", edgeSoftness);

        _lastIntensity = beamIntensity;
        _lastSoftness = edgeSoftness;
        _lastColor = _light.color;
    }

    // ---- cone mesh --------------------------------------------------------
    void RebuildMesh()
    {
        if (_light == null) return;

        if (_mesh == null)
            _mesh = new Mesh();
        else
            _mesh.Clear();

        float range = _light.range;
        float halfAngleRad = _light.spotAngle * 0.5f * Mathf.Deg2Rad;
        float farRadius = Mathf.Tan(halfAngleRad) * range;
        float nearRadius = 0.002f;

        int rings = segments + 1;
        Vector3[] verts = new Vector3[rings * 2];
        Vector2[] uvs = new Vector2[rings * 2];
        int[] tris = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float a = t * Mathf.PI * 2f;
            float cx = Mathf.Cos(a);
            float cy = Mathf.Sin(a);

            // near ring (at light origin, tiny radius)
            verts[i * 2]     = new Vector3(cx * nearRadius, cy * nearRadius, 0f);
            uvs[i * 2]       = new Vector2(t, 0f);

            // far ring (at full range, full radius)
            verts[i * 2 + 1] = new Vector3(cx * farRadius,  cy * farRadius,  range);
            uvs[i * 2 + 1]   = new Vector2(t, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int n0 = i * 2;
            int f0 = i * 2 + 1;
            int n1 = (i + 1) * 2;
            int f1 = (i + 1) * 2 + 1;

            int ti = i * 6;
            tris[ti]     = n0;
            tris[ti + 1] = f0;
            tris[ti + 2] = f1;
            tris[ti + 3] = n0;
            tris[ti + 4] = f1;
            tris[ti + 5] = n1;
        }

        _mesh.vertices = verts;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
        _mesh.name = "VolumetricBeam_Mesh";

        if (_meshFilter != null)
            _meshFilter.sharedMesh = _mesh;

        _lastRange = _light.range;
        _lastAngle = _light.spotAngle;
    }
}
