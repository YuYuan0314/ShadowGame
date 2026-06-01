using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ShadowManager : MonoBehaviour
{
    [Header("Shadow References")]
    public Light dirLight;
    public GameObject receiveShadowObj;
    public List<GameObject> castShadowObjs = new List<GameObject>();
    public LayerMask casterLayer;
    public Transform player;

    [Header("Depth Shadow Map")]
    [Min(32)] public int shadowRes = 256;
    [Min(0.1f)] public float orthoSize = 10f;
    [Min(0.01f)] public float nearPlane = 0.1f;
    [Min(0.1f)] public float farPlane = 50f;
    [Min(0.1f)] public float lightCameraDistance = 20f;
    [Range(0f, 1f)] public float shadowRatioThreshold = 0.5f;
    public float depthBias = 0.0015f;
    public float readbackInterval = 0.05f;
    public bool useCastShadowObjectsOnly = true;
    public bool autoCollectRenderers = true;
    public bool debugLogRatio;

    [Header("Renderer Lists")]
    public List<Renderer> playerRenderers = new List<Renderer>();
    public List<Renderer> envRenderers = new List<Renderer>();

    public float CurrentShadowRatio { get; private set; }
    public bool IsPlayerInShadow { get; private set; }
    public bool HasDepthResult { get; private set; }
    public RenderTexture PlayerDepthTexture { get { return rtPlayer; } }
    public RenderTexture EnvironmentDepthTexture { get { return rtEnv; } }

    private RenderTexture rtPlayer;
    private RenderTexture rtEnv;
    private Material depthWriteMat;
    private CommandBuffer cmdPlayer;
    private CommandBuffer cmdEnv;
    private Matrix4x4 lightView;
    private Matrix4x4 lightProj;
    private Matrix4x4 lightViewProj;
    private float[] latestEnvDepth;
    private bool readbackPending;
    private float nextReadbackTime;
    private int currentShadowRes;
    private GameObject currentShadowSource;

    private struct NPlane
    {
        public Plane plane;
        public Vector3 origin;
        public Vector3 u;
        public Vector3 v;
    }

    private void Awake()
    {
        ResolvePlayer();
        EnsureResources();
        RefreshRendererLists();
    }

    private void OnEnable()
    {
        EnsureResources();
    }

    private void Update()
    {
        EnsureResources();

        if (autoCollectRenderers && (playerRenderers.Count == 0 || envRenderers.Count == 0))
        {
            RefreshRendererLists();
        }

        if (!readbackPending && Time.unscaledTime >= nextReadbackTime)
        {
            RenderAndReadbackDepthMaps();
        }
    }

    private void OnDisable()
    {
        ReleaseResources();
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    public void RefreshRendererLists()
    {
        ResolvePlayer();
        playerRenderers.Clear();
        envRenderers.Clear();

        if (player != null)
        {
            Renderer[] renderers = player.root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (IsDrawableDepthRenderer(renderers[i]))
                {
                    playerRenderers.Add(renderers[i]);
                }
            }
        }

        if (useCastShadowObjectsOnly && castShadowObjs != null && castShadowObjs.Count > 0)
        {
            for (int i = 0; i < castShadowObjs.Count; i++)
            {
                GameObject obj = castShadowObjs[i];
                if (obj == null)
                {
                    continue;
                }

                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
                for (int j = 0; j < renderers.Length; j++)
                {
                    Renderer renderer = renderers[j];
                    if (IsDrawableDepthRenderer(renderer) && !playerRenderers.Contains(renderer) && !envRenderers.Contains(renderer))
                    {
                        envRenderers.Add(renderer);
                    }
                }
            }
        }
        else
        {
            Renderer[] allRenderers = FindObjectsOfType<Renderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer renderer = allRenderers[i];
                if (!IsDrawableDepthRenderer(renderer))
                {
                    continue;
                }
                if (player != null && renderer.transform.root == player.root)
                {
                    continue;
                }
                if (renderer.GetComponentInParent<Canvas>() != null)
                {
                    continue;
                }

                envRenderers.Add(renderer);
            }
        }
    }

    public GameObject GetProjectedShadowSource(Vector3 worldPoint)
    {
        if (!IsInProjectedArea(worldPoint))
        {
            if (IsPlayerInShadow)
            {
                return currentShadowSource;
            }
            return null;
        }

        GameObject source = FindShadowSourceByRay(worldPoint);
        if (source != null)
        {
            currentShadowSource = source;
        }

        if (currentShadowSource != null)
        {
            return currentShadowSource;
        }
        return source;
    }

    public GameObject GetShadowSource(Vector3 worldPoint)
    {
        return GetProjectedShadowSource(worldPoint);
    }

    public bool IsInProjectedArea(Vector3 worldPoint)
    {
        float pointDepth;
        float envDepth;
        if (TrySampleEnvironmentDepth(worldPoint, out pointDepth, out envDepth))
        {
            return pointDepth > envDepth + depthBias;
        }

        return FindShadowSourceByRay(worldPoint) != null;
    }

    public bool IsNearProjectedArea(Vector3 worldPoint, float tolerance)
    {
        if (IsInProjectedArea(worldPoint))
        {
            return true;
        }

        Vector3 right = GetLightRight();
        Vector3 up = GetLightUp();
        return IsInProjectedArea(worldPoint + right * tolerance)
            || IsInProjectedArea(worldPoint - right * tolerance)
            || IsInProjectedArea(worldPoint + up * tolerance)
            || IsInProjectedArea(worldPoint - up * tolerance);
    }

    public Vector3 GetSafePositionInShadow(GameObject caster)
    {
        if (caster == null || receiveShadowObj == null || dirLight == null)
        {
            return Vector3.zero;
        }

        NPlane plane = GetReceivePlane(receiveShadowObj);
        Bounds bounds;
        if (!TryGetRendererBounds(caster, out bounds))
        {
            return Vector3.zero;
        }

        Vector3 lightDir = dirLight.transform.forward;
        return ProjectPointToPlane(bounds.center, plane.plane, lightDir);
    }

    public void RenderAndReadbackDepthMaps()
    {
        if (dirLight == null || player == null || depthWriteMat == null || rtPlayer == null || rtEnv == null)
        {
            return;
        }

        UpdateLightMatrices();
        RenderDepthMap(cmdPlayer, rtPlayer, playerRenderers);
        RenderDepthMap(cmdEnv, rtEnv, envRenderers);

        readbackPending = true;
        nextReadbackTime = Time.unscaledTime + Mathf.Max(0f, readbackInterval);

        AsyncGPUReadback.Request(rtPlayer, 0, TextureFormat.RFloat, OnPlayerDepthReadback);
    }

    private void OnPlayerDepthReadback(AsyncGPUReadbackRequest reqPlayer)
    {
        if (this == null)
        {
            return;
        }

        if (reqPlayer.hasError)
        {
            readbackPending = false;
            Debug.LogWarning("Player depth RT readback error", this);
            return;
        }

        float[] playerDepth = reqPlayer.GetData<float>().ToArray();
        AsyncGPUReadback.Request(rtEnv, 0, TextureFormat.RFloat, delegate(AsyncGPUReadbackRequest reqEnv)
        {
            OnEnvironmentDepthReadback(reqEnv, playerDepth);
        });
    }

    private void OnEnvironmentDepthReadback(AsyncGPUReadbackRequest reqEnv, float[] playerDepth)
    {
        if (this == null)
        {
            return;
        }

        readbackPending = false;
        if (reqEnv.hasError)
        {
            Debug.LogWarning("Environment depth RT readback error", this);
            return;
        }

        latestEnvDepth = reqEnv.GetData<float>().ToArray();
        EvaluateShadowRatio(playerDepth, latestEnvDepth);
    }

    private void EvaluateShadowRatio(float[] playerDepth, float[] envDepth)
    {
        int totalPlayerPixels = 0;
        int shadowedPixels = 0;
        int count = Mathf.Min(playerDepth.Length, envDepth.Length);

        for (int i = 0; i < count; i++)
        {
            float pDepth = playerDepth[i];
            if (pDepth >= 0.9999f)
            {
                continue;
            }

            totalPlayerPixels++;
            if (pDepth > envDepth[i] + depthBias)
            {
                shadowedPixels++;
            }
        }

        if (totalPlayerPixels > 0)
        {
            CurrentShadowRatio = shadowedPixels / (float)totalPlayerPixels;
        }
        else
        {
            CurrentShadowRatio = 0f;
        }

        IsPlayerInShadow = CurrentShadowRatio >= shadowRatioThreshold;
        HasDepthResult = totalPlayerPixels > 0;

        if (IsPlayerInShadow && player != null)
        {
            GameObject source = FindShadowSourceByRay(player.position + Vector3.up * 0.05f);
            if (source != null)
            {
                currentShadowSource = source;
            }
        }

        if (debugLogRatio)
        {
            Debug.Log("Player shadow ratio: " + CurrentShadowRatio.ToString("P1") + " -> " + (IsPlayerInShadow ? "In shadow" : "Exposed"), this);
        }
    }

    private void EnsureResources()
    {
        if (currentShadowRes != shadowRes || rtPlayer == null || rtEnv == null)
        {
            ReleaseRenderTextures();
            currentShadowRes = Mathf.Max(32, shadowRes);
            rtPlayer = CreateDepthTexture("PlayerDepthRT");
            rtEnv = CreateDepthTexture("EnvironmentDepthRT");
            latestEnvDepth = null;
            HasDepthResult = false;
        }

        if (depthWriteMat == null)
        {
            Shader depthShader = Shader.Find("Hidden/WriteDepth");
            if (depthShader != null)
            {
                depthWriteMat = new Material(depthShader);
                depthWriteMat.hideFlags = HideFlags.HideAndDontSave;
            }
            else
            {
                Debug.LogError("Shader Hidden/WriteDepth not found. Create Assets/Shaders/WriteDepth.shader.", this);
            }
        }

        if (cmdPlayer == null)
        {
            cmdPlayer = new CommandBuffer();
            cmdPlayer.name = "PlayerDepth";
        }
        if (cmdEnv == null)
        {
            cmdEnv = new CommandBuffer();
            cmdEnv.name = "EnvironmentDepth";
        }
    }

    private RenderTexture CreateDepthTexture(string textureName)
    {
        RenderTexture rt = new RenderTexture(currentShadowRes, currentShadowRes, 24, RenderTextureFormat.RFloat);
        rt.name = textureName;
        rt.filterMode = FilterMode.Point;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.useMipMap = false;
        rt.autoGenerateMips = false;
        rt.Create();
        return rt;
    }

    private void ReleaseResources()
    {
        ReleaseRenderTextures();

        if (cmdPlayer != null)
        {
            cmdPlayer.Release();
            cmdPlayer = null;
        }

        if (cmdEnv != null)
        {
            cmdEnv.Release();
            cmdEnv = null;
        }

        if (depthWriteMat != null)
        {
            if (Application.isPlaying)
            {
                Destroy(depthWriteMat);
            }
            else
            {
                DestroyImmediate(depthWriteMat);
            }
            depthWriteMat = null;
        }
    }

    private void ReleaseRenderTextures()
    {
        if (rtPlayer != null)
        {
            rtPlayer.Release();
            if (Application.isPlaying)
            {
                Destroy(rtPlayer);
            }
            else
            {
                DestroyImmediate(rtPlayer);
            }
            rtPlayer = null;
        }

        if (rtEnv != null)
        {
            rtEnv.Release();
            if (Application.isPlaying)
            {
                Destroy(rtEnv);
            }
            else
            {
                DestroyImmediate(rtEnv);
            }
            rtEnv = null;
        }
    }

    private void UpdateLightMatrices()
    {
        Vector3 center = player != null ? player.position : transform.position;
        Vector3 lightDir = dirLight.transform.forward.normalized;
        Vector3 lightPos = center - lightDir * Mathf.Max(lightCameraDistance, nearPlane + 0.1f);
        Vector3 up = Mathf.Abs(Vector3.Dot(lightDir, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;

        lightView = Matrix4x4.LookAt(lightPos, lightPos + lightDir, up);
        lightProj = Matrix4x4.Ortho(-orthoSize, orthoSize, -orthoSize, orthoSize, nearPlane, farPlane);
        lightViewProj = lightProj * lightView;
    }

    private void RenderDepthMap(CommandBuffer cmd, RenderTexture target, List<Renderer> renderers)
    {
        cmd.Clear();
        cmd.SetRenderTarget(target);
        cmd.ClearRenderTarget(true, true, Color.white);
        cmd.SetViewProjectionMatrices(lightView, lightProj);

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsDrawableDepthRenderer(renderer))
            {
                continue;
            }
            cmd.DrawRenderer(renderer, depthWriteMat);
        }

        Graphics.ExecuteCommandBuffer(cmd);
    }

    private bool TrySampleEnvironmentDepth(Vector3 worldPoint, out float pointDepth, out float envDepth)
    {
        pointDepth = 1f;
        envDepth = 1f;

        if (latestEnvDepth == null || latestEnvDepth.Length == 0 || currentShadowRes <= 0)
        {
            return false;
        }

        Vector4 clip = lightViewProj * new Vector4(worldPoint.x, worldPoint.y, worldPoint.z, 1f);
        if (Mathf.Abs(clip.w) < 0.0001f)
        {
            return false;
        }

        Vector3 ndc = new Vector3(clip.x / clip.w, clip.y / clip.w, clip.z / clip.w);
        float u = ndc.x * 0.5f + 0.5f;
        float v = ndc.y * 0.5f + 0.5f;
        pointDepth = ndc.z;

        if (u < 0f || u > 1f || v < 0f || v > 1f || pointDepth < 0f || pointDepth > 1f)
        {
            return false;
        }

        int x = Mathf.Clamp(Mathf.FloorToInt(u * currentShadowRes), 0, currentShadowRes - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(v * currentShadowRes), 0, currentShadowRes - 1);
        int index = y * currentShadowRes + x;
        if (index < 0 || index >= latestEnvDepth.Length)
        {
            return false;
        }

        envDepth = latestEnvDepth[index];
        return envDepth < 0.9999f;
    }

    private GameObject FindShadowSourceByRay(Vector3 worldPoint)
    {
        if (dirLight == null)
        {
            return null;
        }

        Vector3 lightDir = dirLight.transform.forward.normalized;
        Vector3 rayStart = worldPoint - lightDir * Mathf.Max(lightCameraDistance, farPlane * 0.5f);
        float rayDistance = Mathf.Max(farPlane, lightCameraDistance * 2f);
        RaycastHit hit;

        if (Physics.Raycast(rayStart, lightDir, out hit, rayDistance, casterLayer, QueryTriggerInteraction.Ignore))
        {
            for (int i = 0; i < castShadowObjs.Count; i++)
            {
                GameObject candidate = castShadowObjs[i];
                if (candidate == null)
                {
                    continue;
                }
                if (hit.collider.gameObject == candidate || hit.transform.IsChildOf(candidate.transform))
                {
                    return candidate;
                }
            }

            return hit.collider.gameObject;
        }

        return null;
    }

    private bool IsDrawableDepthRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (renderer is LineRenderer || renderer is TrailRenderer || renderer is ParticleSystemRenderer)
        {
            return false;
        }

        return renderer.GetComponent<MeshFilter>() != null || renderer is SkinnedMeshRenderer;
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            playerObject = GameObject.Find("Player");
        }

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private Vector3 GetLightRight()
    {
        if (dirLight == null)
        {
            return Vector3.right;
        }

        Vector3 forward = dirLight.transform.forward.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        return Vector3.Cross(up, forward).normalized;
    }

    private Vector3 GetLightUp()
    {
        if (dirLight == null)
        {
            return Vector3.forward;
        }

        Vector3 forward = dirLight.transform.forward.normalized;
        Vector3 right = GetLightRight();
        return Vector3.Cross(forward, right).normalized;
    }

    private NPlane GetReceivePlane(GameObject obj)
    {
        Vector3 normal = obj.transform.up;
        Vector3 origin = obj.transform.position;
        Plane plane = new Plane(normal, origin);
        Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
        Vector3 v = Vector3.Cross(u, normal).normalized;
        NPlane result = new NPlane();
        result.plane = plane;
        result.origin = origin;
        result.u = u;
        result.v = v;
        return result;
    }

    private Vector3 ProjectPointToPlane(Vector3 point, Plane plane, Vector3 direction)
    {
        float dot = Vector3.Dot(plane.normal, direction);
        if (Mathf.Abs(dot) < 0.0001f)
        {
            return point;
        }

        float t = -plane.GetDistanceToPoint(point) / dot;
        return point + direction * t;
    }

    private bool TryGetRendererBounds(GameObject obj, out Bounds bounds)
    {
        bounds = new Bounds(obj.transform.position, Vector3.zero);
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }
}
