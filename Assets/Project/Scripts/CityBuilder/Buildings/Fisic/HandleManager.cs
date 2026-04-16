using UnityEngine;

public sealed class HandleManager : MonoBehaviour
{
    [SerializeField] private Transform _handleUp;
    [SerializeField] private Transform _handleRight;
    [SerializeField] private Transform _handleFront;
    [SerializeField] private float _offset = 0.05f;

    public void Configure(Transform handleUp, Transform handleRight, Transform handleFront, float offset)
    {
        _handleUp = handleUp;
        _handleRight = handleRight;
        _handleFront = handleFront;
        _offset = offset;
    }

    private void LateUpdate()
    {
        if (!TryGetCombinedBounds(out Bounds bounds))
            return;

        UpdateHandle(_handleUp, bounds, Vector3.up);
        UpdateHandle(_handleRight, bounds, Vector3.right);
        UpdateHandle(_handleFront, bounds, Vector3.forward);
    }

    private bool TryGetCombinedBounds(out Bounds bounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (ShouldIgnoreRenderer(renderer))
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

    private static bool ShouldIgnoreRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return true;
        }

        if (renderer is LineRenderer)
        {
            return true;
        }

        return renderer.GetComponentInParent<ScaleHandle>() != null;
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
