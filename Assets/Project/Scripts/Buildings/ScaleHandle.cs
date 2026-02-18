using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public sealed class ScaleHandle : MonoBehaviour
{
    [SerializeField] private Transform _targetCube;   // Cubo que modificamos
    [SerializeField] private GridDefinition _grid;
    [SerializeField] private Vector3 _axis = Vector3.up; // Eje que modifica este handle
    [Header("Handle Setup")]
    [SerializeField] private bool _keepWorldScale = true;
    [SerializeField] private bool _useTriggerCollider = true;

    private XRGrabInteractable _grabInteractable;
    private Rigidbody _rigidbody;
    private Collider _collider;
    private Vector3 _initialGrabPos;
    private Vector3 _initialScale;
    private Vector3 _initialLocalScale;
    private Vector3 _initialParentScale;

    private void Awake()
    {
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();

        _initialLocalScale = transform.localScale;
        _initialParentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;

        ConfigureGrab();
        ConfigurePhysics();

        _grabInteractable.selectEntered.AddListener(OnGrab);
        _grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        _grabInteractable.selectEntered.RemoveListener(OnGrab);
        _grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        _initialGrabPos = args.interactorObject.transform.position;
        _initialScale = _targetCube.localScale;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        SnapScaleToGrid();
    }

    private void FixedUpdate()
    {
        if (!_grabInteractable.isSelected) return;

        var interactor = _grabInteractable.GetOldestInteractorSelecting();
        if (interactor == null) return;

        Vector3 delta = interactor.transform.position - _initialGrabPos;
        float deltaAmount = Vector3.Dot(delta, _axis);

        Vector3 newScale = _initialScale + _axis * deltaAmount;

        newScale = Vector3.Max(newScale, Vector3.one * 0.1f);

        _targetCube.localScale = newScale;
    }

    private void LateUpdate()
    {
        if (!_keepWorldScale)
            return;

        Vector3 parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            _initialLocalScale.x * SafeDivide(_initialParentScale.x, parentScale.x),
            _initialLocalScale.y * SafeDivide(_initialParentScale.y, parentScale.y),
            _initialLocalScale.z * SafeDivide(_initialParentScale.z, parentScale.z)
        );
    }

    private void SnapScaleToGrid()
    {
        Vector3 scale = _targetCube.localScale;
        float cellSize = _grid.CellSize;

        scale.x = Mathf.Round(scale.x / cellSize) * cellSize;
        scale.y = Mathf.Round(scale.y / cellSize) * cellSize;
        scale.z = Mathf.Round(scale.z / cellSize) * cellSize;

        _targetCube.localScale = scale;
    }

    private void ConfigureGrab()
    {
        _grabInteractable.useDynamicAttach = false;
        _grabInteractable.matchAttachPosition = false;
        _grabInteractable.matchAttachRotation = false;
        _grabInteractable.snapToColliderVolume = false;
        _grabInteractable.trackPosition = false;
        _grabInteractable.trackRotation = false;
        _grabInteractable.trackScale = false;
        _grabInteractable.throwOnDetach = false;
        _grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
    }

    private void ConfigurePhysics()
    {
        if (_rigidbody != null)
        {
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (_collider != null && _useTriggerCollider)
            _collider.isTrigger = true;

        if (_collider != null && _targetCube != null)
        {
            foreach (var targetCollider in _targetCube.GetComponentsInChildren<Collider>())
            {
                if (targetCollider != null)
                    Physics.IgnoreCollision(_collider, targetCollider, true);
            }
        }
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        return Mathf.Approximately(denominator, 0f) ? 1f : numerator / denominator;
    }
}
