using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class SpotlightExposureZone : MonoBehaviour
{
    private static List<SpotlightExposureZone> _all = new List<SpotlightExposureZone>();

    private Light _light;
    private float _halfAngleCos;
    private bool _dirty = true;

    void OnEnable()
    {
        _all.Add(this);
        _light = GetComponent<Light>();
        _dirty = true;
    }

    void OnDisable()
    {
        _all.Remove(this);
    }

    void Update()
    {
        if (_dirty || Time.frameCount % 30 == 0)
            CacheData();
    }

    void CacheData()
    {
        if (_light != null)
        {
            _halfAngleCos = Mathf.Cos(_light.spotAngle * 0.5f * Mathf.Deg2Rad);
            _dirty = false;
        }
    }

    public bool Exposes(Vector3 worldPoint)
    {
        if (_light == null || !_light.enabled || _light.type != LightType.Spot)
            return false;

        CacheData();

        Vector3 toPoint = worldPoint - transform.position;
        float distSq = toPoint.sqrMagnitude;

        if (distSq > _light.range * _light.range)
            return false;

        if (distSq < 0.0001f)
            return true;

        Vector3 dir = toPoint / Mathf.Sqrt(distSq);
        return Vector3.Dot(transform.forward, dir) >= _halfAngleCos;
    }

    public static bool IsAnyPointExposed(Vector3 worldPoint)
    {
        for (int i = _all.Count - 1; i >= 0; i--)
        {
            if (_all[i] == null)
            {
                _all.RemoveAt(i);
                continue;
            }
            if (_all[i].Exposes(worldPoint))
                return true;
        }
        return false;
    }
}
