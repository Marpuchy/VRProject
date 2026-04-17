using CityBuilder;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public sealed class GridMovementConstraint : MonoBehaviour
{
    [SerializeField] private GridDefinition _grid;

    private Rigidbody _rigidbody;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;
    private MapBoundary _mapBoundary;

    private bool _isGrabbed;
    private float _fixedY;

    private Vector2Int _currentCell;

    public void SetGrid(GridDefinition grid)
    {
        _grid = grid;
        TryAlignToConstraints(preserveFixedY: false);
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        ResolveSpatialDependencies();

        _grabInteractable.selectEntered.AddListener(OnGrab);
        _grabInteractable.selectExited.AddListener(OnRelease);

        _currentCell = GetCell(transform.position);
    }

    private void OnEnable()
    {
        ResolveSpatialDependencies();
        TryAlignToConstraints(preserveFixedY: false);
    }

    private void OnDestroy()
    {
        if (_grabInteractable != null)
        {
            _grabInteractable.selectEntered.RemoveListener(OnGrab);
            _grabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        _isGrabbed = true;
        _fixedY = transform.position.y;

        _rigidbody.useGravity = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _isGrabbed = false;

        SnapImmediately();

        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;

        _rigidbody.useGravity = true;
    }

    private void FixedUpdate()
    {
        bool gridResolvedThisFrame = ResolveSpatialDependencies();
        if (gridResolvedThisFrame && !_isGrabbed)
        {
            TryAlignToConstraints(preserveFixedY: false);
        }

        if (!_isGrabbed)
            return;

        Vector3 position = _rigidbody.position;

        // bloquear Y sin interferir en XZ
        position.y = _fixedY;

        _rigidbody.MovePosition(position);

        // comprobar cambio de celda
        Vector2Int newCell = GetCell(position);

        if (newCell != _currentCell)
        {
            _currentCell = newCell;
            SnapImmediately();
        }

        _rigidbody.angularVelocity = Vector3.zero;
    }

    private void SnapImmediately()
    {
        TryAlignToConstraints(preserveFixedY: true);
    }

    private Vector2Int GetCell(Vector3 worldPosition)
    {
        if (_grid == null)
            return Vector2Int.zero;

        float size = _grid.CellSize;
        Vector3 origin = _grid.Origin;

        int x = Mathf.RoundToInt((worldPosition.x - origin.x) / size);
        int z = Mathf.RoundToInt((worldPosition.z - origin.z) / size);

        return new Vector2Int(x, z);
    }

    private bool ResolveSpatialDependencies()
    {
        bool resolvedGridThisFrame = false;

        if (_grid == null)
        {
            _grid = FindFirstObjectByType<GridDefinition>();
            resolvedGridThisFrame = _grid != null;
        }

        if (_mapBoundary == null)
        {
            MapBoundary.TryGetActiveBoundary(out _mapBoundary);
        }

        return resolvedGridThisFrame;
    }

    private void TryAlignToConstraints(bool preserveFixedY)
    {
        if (_rigidbody == null)
            return;

        Vector3 constrainedPosition = GetConstrainedPosition(_rigidbody.position);
        if (preserveFixedY)
        {
            constrainedPosition.y = _fixedY;
        }

        _rigidbody.position = constrainedPosition;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        transform.position = constrainedPosition;
        _fixedY = constrainedPosition.y;
        _currentCell = GetCell(constrainedPosition);
    }

    private Vector3 GetConstrainedPosition(Vector3 worldPosition)
    {
        Vector3 constrainedPosition = worldPosition;

        if (_mapBoundary != null)
        {
            constrainedPosition = _mapBoundary.ClampWorldPosition(constrainedPosition);
        }

        if (_grid != null)
        {
            constrainedPosition = _grid.Snap(constrainedPosition);

            if (_mapBoundary != null && !_mapBoundary.ContainsWorldPosition(constrainedPosition))
            {
                constrainedPosition = FindNearestSnappedPositionInsideBoundary(constrainedPosition);
            }
        }

        return constrainedPosition;
    }

    private Vector3 FindNearestSnappedPositionInsideBoundary(Vector3 worldPosition)
    {
        if (_grid == null || _mapBoundary == null)
        {
            return worldPosition;
        }

        Vector3 clampedPosition = _mapBoundary.ClampWorldPosition(worldPosition);
        Vector3 snappedPosition = _grid.Snap(clampedPosition);
        if (_mapBoundary.ContainsWorldPosition(snappedPosition))
        {
            return snappedPosition;
        }

        float cellSize = Mathf.Max(0.01f, _grid.CellSize);
        Vector3 bestCandidate = clampedPosition;
        float bestDistance = float.MaxValue;

        for (int xOffset = -3; xOffset <= 3; xOffset++)
        {
            for (int zOffset = -3; zOffset <= 3; zOffset++)
            {
                Vector3 candidate = snappedPosition + new Vector3(xOffset * cellSize, 0f, zOffset * cellSize);
                candidate.y = clampedPosition.y;
                if (!_mapBoundary.ContainsWorldPosition(candidate))
                {
                    continue;
                }

                float distance = (candidate - clampedPosition).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }
}
