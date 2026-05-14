using UnityEngine;

public class ClickColorChange : MonoBehaviour
{
    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    private void OnMouseDown()
    {
        if (_renderer == null)
            return;

        _renderer.material.color = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.7f, 1f);
    }
}
