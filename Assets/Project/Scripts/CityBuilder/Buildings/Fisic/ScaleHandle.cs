using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class ScaleHandle : MonoBehaviour
{
    [SerializeField] private Transform _targetCube;
    [SerializeField] private GridDefinition _grid;
    [SerializeField] private Vector3 _axis = Vector3.up;
    [Header("Handle Setup")]
    [SerializeField] private bool _keepWorldScale = true;
    [SerializeField] private bool _useTriggerCollider = true;

    private XRSimpleInteractable _interactable;
    private XRGrabInteractable _targetGrabInteractable;
    private Collider[] _targetCubeColliders;
    private Rigidbody _rigidbody;
    private Collider _collider;
    private Vector3 _initialGrabPos;
    private Vector3 _initialScale;
    private Vector3 _initialLocalScale;
    private Vector3 _initialParentScale;
    private int _hoverCount;

    public void Configure(Transform targetCube, GridDefinition grid, Vector3 axis)
    {
        _targetCube = targetCube;
        _grid = grid;
        _axis = axis;
        CacheTargetReferences();
    }

    public void SetGrid(GridDefinition grid)
    {
        _grid = grid;

        if (_grid != null && _targetCube != null && (_interactable == null || !_interactable.isSelected))
        {
            SnapScaleToGrid();
        }
    }

    public void SetDesiredWorldScale(float worldScale)
    {
        Vector3 parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
        _initialParentScale = parentScale;
        _initialLocalScale = new Vector3(
            SafeDivide(Mathf.Max(0.01f, worldScale), parentScale.x),
            SafeDivide(Mathf.Max(0.01f, worldScale), parentScale.y),
            SafeDivide(Mathf.Max(0.01f, worldScale), parentScale.z)
        );

        transform.localScale = _initialLocalScale;
    }

    private void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
        CacheTargetReferences();
        TryResolveGrid();

        _initialLocalScale = transform.localScale;
        _initialParentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;

        _interactable.hoverEntered.AddListener(OnHoverEnter);
        _interactable.hoverExited.AddListener(OnHoverExit);
        _interactable.selectEntered.AddListener(OnGrab);
        _interactable.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        RestoreTargetCubeInteraction();
        _interactable.hoverEntered.RemoveListener(OnHoverEnter);
        _interactable.hoverExited.RemoveListener(OnHoverExit);
        _interactable.selectEntered.RemoveListener(OnGrab);
        _interactable.selectExited.RemoveListener(OnRelease);
    }

    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        _hoverCount++;
        BlockTargetCubeInteraction();
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        _hoverCount = Mathf.Max(0, _hoverCount - 1);
        if (_hoverCount == 0 && !_interactable.isSelected)
        {
            RestoreTargetCubeInteraction();
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (_targetCube == null)
            return;

        BlockTargetCubeInteraction();
        _initialGrabPos = args.interactorObject.transform.position;
        _initialScale = _targetCube.localScale;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (_hoverCount == 0)
        {
            RestoreTargetCubeInteraction();
        }

        SnapScaleToGrid();
    }

    private void FixedUpdate()
    {
        if (_targetCube == null || !_interactable.isSelected)
            return;

        var interactor = _interactable.firstInteractorSelecting;
        if (interactor == null)
            return;

        Vector3 delta = interactor.transform.position - _initialGrabPos;
        float deltaAmount = Vector3.Dot(delta, _axis);

        Vector3 newScale = _initialScale + _axis * deltaAmount;
        newScale = Vector3.Max(newScale, Vector3.one * 0.1f);

        ApplyUnidirectionalScale(newScale);
    }

    private void LateUpdate()
    {
        bool resolvedGridThisFrame = TryResolveGrid();
        if (resolvedGridThisFrame && _targetCube != null && (_interactable == null || !_interactable.isSelected))
        {
            SnapScaleToGrid();
        }

        if (!_keepWorldScale)
            return;

        Vector3 parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            _initialLocalScale.x * SafeDivide(_initialParentScale.x, parentScale.x),
            _initialLocalScale.y * SafeDivide(_initialParentScale.y, parentScale.y),
            _initialLocalScale.z * SafeDivide(_initialParentScale.z, parentScale.z)
        );
    }

    private void ApplyUnidirectionalScale(Vector3 newScale)
    {
        if (!TryGetTargetBounds(out Bounds previousBounds))
        {
            Vector3 currentScale = _targetCube.localScale;
            Vector3 scaleDelta = newScale - currentScale;

            _targetCube.localScale = newScale;

            Vector3 worldOffset = Vector3.Scale(scaleDelta * 0.5f, _axis);
            _targetCube.position += worldOffset;
            return;
        }

        float previousAnchor = GetAnchorCoordinate(previousBounds, _axis);
        _targetCube.localScale = newScale;

        if (!TryGetTargetBounds(out Bounds updatedBounds))
        {
            return;
        }

        float updatedAnchor = GetAnchorCoordinate(updatedBounds, _axis);
        Vector3 anchorCorrection = Vector3.Scale(_axis, Vector3.one * (previousAnchor - updatedAnchor));
        _targetCube.position += anchorCorrection;
    }

    private void SnapScaleToGrid()
    {
        if (_targetCube == null || _grid == null)
            return;

        Vector3 scale = _targetCube.localScale;
        float cellSize = Mathf.Max(0.01f, _grid.CellSize);

        scale.x = Mathf.Round(scale.x / cellSize) * cellSize;
        scale.y = Mathf.Round(scale.y / cellSize) * cellSize;
        scale.z = Mathf.Round(scale.z / cellSize) * cellSize;

        ApplyUnidirectionalScale(scale);
    }

    private void CacheTargetReferences()
    {
        if (_targetCube == null)
            return;

        _targetGrabInteractable = _targetCube.GetComponent<XRGrabInteractable>();
        _targetCubeColliders = _targetCube.GetComponents<Collider>();
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        return Mathf.Approximately(denominator, 0f) ? 1f : numerator / denominator;
    }

    private bool TryResolveGrid()
    {
        if (_grid != null)
        {
            return false;
        }

        _grid = FindFirstObjectByType<GridDefinition>();
        return _grid != null;
    }

    private bool TryGetTargetBounds(out Bounds bounds)
    {
        bounds = default;
        if (_targetCube == null)
        {
            return false;
        }

        Renderer[] renderers = _targetCube.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

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

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = _targetCube.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.GetComponentInParent<ScaleHandle>() != null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
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

    private static float GetAnchorCoordinate(Bounds bounds, Vector3 axis)
    {
        if (Mathf.Abs(axis.x) >= Mathf.Abs(axis.y) && Mathf.Abs(axis.x) >= Mathf.Abs(axis.z))
        {
            return axis.x >= 0f ? bounds.min.x : bounds.max.x;
        }

        if (Mathf.Abs(axis.z) >= Mathf.Abs(axis.y))
        {
            return axis.z >= 0f ? bounds.min.z : bounds.max.z;
        }

        return axis.y >= 0f ? bounds.min.y : bounds.max.y;
    }

    private void BlockTargetCubeInteraction()
    {
        if (_targetGrabInteractable != null)
        {
            _targetGrabInteractable.enabled = false;
        }

        if (_targetCubeColliders == null)
        {
            return;
        }

        for (int i = 0; i < _targetCubeColliders.Length; i++)
        {
            if (_targetCubeColliders[i] != null)
            {
                _targetCubeColliders[i].enabled = false;
            }
        }
    }

    private void RestoreTargetCubeInteraction()
    {
        if (_targetGrabInteractable != null)
        {
            _targetGrabInteractable.enabled = true;
        }

        if (_targetCubeColliders == null)
        {
            return;
        }

        for (int i = 0; i < _targetCubeColliders.Length; i++)
        {
            if (_targetCubeColliders[i] != null)
            {
                _targetCubeColliders[i].enabled = true;
            }
        }
    }
}
