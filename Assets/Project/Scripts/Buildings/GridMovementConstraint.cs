using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public sealed class GridMovementConstraint : MonoBehaviour
{
    [SerializeField] private GridDefinition _grid;

    private Rigidbody _rigidbody;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;

    private bool _isGrabbed;
    private float _fixedY;

    private Vector2Int _currentCell;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        _grabInteractable.selectEntered.AddListener(OnGrab);
        _grabInteractable.selectExited.AddListener(OnRelease);

        _currentCell = GetCell(transform.position);
    }

    private void OnDestroy()
    {
        _grabInteractable.selectEntered.RemoveListener(OnGrab);
        _grabInteractable.selectExited.RemoveListener(OnRelease);
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
        Vector3 snapped = _grid.Snap(_rigidbody.position);
        snapped.y = _fixedY;

        _rigidbody.position = snapped;
    }

    private Vector2Int GetCell(Vector3 worldPosition)
    {
        float size = _grid.CellSize;
        Vector3 origin = _grid.Origin;

        int x = Mathf.RoundToInt((worldPosition.x - origin.x) / size);
        int z = Mathf.RoundToInt((worldPosition.z - origin.z) / size);

        return new Vector2Int(x, z);
    }
}
