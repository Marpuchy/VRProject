using UnityEngine;

public sealed class HandleManager : MonoBehaviour
{
    [SerializeField] private Transform _handleUp;
    [SerializeField] private Transform _handleRight;
    [SerializeField] private Transform _handleFront;
    [SerializeField] private float _offset = 0.05f;

    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
    }

    private void LateUpdate()
    {
        if (_renderer == null)
            return;

        Bounds bounds = _renderer.bounds;

        UpdateHandle(_handleUp, bounds, Vector3.up);
        UpdateHandle(_handleRight, bounds, Vector3.right);
        UpdateHandle(_handleFront, bounds, Vector3.forward);
    }

    private void UpdateHandle(Transform handle, Bounds bounds, Vector3 direction)
    {
        if (handle == null)
            return;

        Vector3 worldPos = bounds.center + Vector3.Scale(bounds.extents, direction);
        worldPos += direction * _offset;

        handle.position = worldPos;
    }
}