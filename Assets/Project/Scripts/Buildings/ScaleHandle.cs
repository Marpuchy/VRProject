using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class ScaleHandle : MonoBehaviour
{
    [SerializeField] private Transform _targetCube;   // Cubo que modificamos
    [SerializeField] private GridDefinition _grid;
    [SerializeField] private Vector3 _axis = Vector3.up; // Eje que modifica este handle
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

    public void SetGrid(GridDefinition grid)
    {
        _grid = grid;
    }

    private void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
        if (_targetCube != null)
        {
            _targetGrabInteractable = _targetCube.GetComponent<XRGrabInteractable>();
            _targetCubeColliders = _targetCube.GetComponents<Collider>();
        }

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
        if (!_interactable.isSelected)
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
        Vector3 currentScale = _targetCube.localScale;
        Vector3 scaleDelta = newScale - currentScale;

        _targetCube.localScale = newScale;

        Vector3 worldOffset = Vector3.Scale(scaleDelta * 0.5f, _axis);

        _targetCube.position += worldOffset;
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
    

    private static float SafeDivide(float numerator, float denominator)
    {
        return Mathf.Approximately(denominator, 0f) ? 1f : numerator / denominator;
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
