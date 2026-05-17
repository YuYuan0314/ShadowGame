using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class FloatingDustEffect : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField, Range(1, 30)] int maxParticles = 10;
    [SerializeField, Range(0.5f, 5f)] float emissionRate = 1.5f;
    [SerializeField, Range(1f, 10f)] float lifetime = 5f;
    [SerializeField, Range(0.005f, 0.5f)] float size = 0.03f;
    [SerializeField, Range(0, 1)] float brightness = 0.5f;

    [Header("Movement")]
    [SerializeField, Range(0.01f, 0.5f)] float floatSpeed = 0.1f;
    [SerializeField, Range(0, 0.1f)] float driftAmount = 0.03f;

    [Header("Spawn Area")]
    [SerializeField] Vector3 spawnArea = new Vector3(20, 12, 15);

    ParticleSystem _ps;
    ParticleSystemRenderer _renderer;

    void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
        _renderer = GetComponent<ParticleSystemRenderer>();
        ApplySettings();
    }

    void Reset()
    {
        _ps = GetComponent<ParticleSystem>();
        _renderer = GetComponent<ParticleSystemRenderer>();
        ApplySettings();
    }

    void OnValidate()
    {
        if (_ps == null) _ps = GetComponent<ParticleSystem>();
        if (_renderer == null) _renderer = GetComponent<ParticleSystemRenderer>();
        if (_ps == null) return;
        ApplySettings();
    }

    void ApplySettings()
    {
        if (_renderer != null && _renderer.sharedMaterial == null)
        {
            var tex = CreateGlowTexture(32);
            var shader = Shader.Find("Particles/Additive");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            var mat = new Material(shader);
            if (shader.name.Contains("Universal"))
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 2);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_BLENDMODE_ADDITIVE");
            }
            else
            {
                mat.SetTexture("_MainTex", tex);
                mat.SetColor("_TintColor", Color.white);
            }

            _renderer.sharedMaterial = mat;
            _renderer.renderMode = ParticleSystemRenderMode.Billboard;
            _renderer.sortingOrder = 9999;
        }

        var main = _ps.main;
        main.startLifetime = lifetime;
        main.startSpeed = floatSpeed;
        main.startSize = size;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.loop = true;

        var emission = _ps.emission;
        emission.rateOverTime = emissionRate;

        var shape = _ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = spawnArea;

        var col = _ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(Color.white, 0), new(Color.white, 1) },
            new GradientAlphaKey[] {
                new(0, 0f),
                new(brightness, 0.15f),
                new(brightness, 0.85f),
                new(0, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOver = _ps.sizeOverLifetime;
        sizeOver.enabled = true;
        sizeOver.size = new ParticleSystem.MinMaxCurve(1,
            new AnimationCurve(
                new Keyframe(0, 0.3f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.8f, 1f),
                new Keyframe(1, 0.3f)
            ));

        var velocity = _ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-driftAmount, driftAmount);
        velocity.y = new ParticleSystem.MinMaxCurve(driftAmount * 0.5f, driftAmount * 2f);
        velocity.z = new ParticleSystem.MinMaxCurve(-driftAmount * 0.5f, driftAmount * 0.5f);

        var noise = _ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(floatSpeed * 0.5f);
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.1f;
    }

    Texture2D CreateGlowTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Pow(1f - Mathf.Clamp01(dist), 2.5f);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
