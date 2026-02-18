using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class HorizontalGrabConstraint : MonoBehaviour
{
    private Rigidbody _rigidbody;
    private XRGrabInteractable _grabInteractable;
    
    private Quaternion _fixedRotation;

    private float _fixedY;
    private bool _isGrabbed;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _grabInteractable = GetComponent<XRGrabInteractable>();
        
        _fixedRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

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
        _fixedY = transform.position.y;
        _isGrabbed = true;

        _rigidbody.useGravity = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _isGrabbed = false;

        // CLAVE: eliminar cualquier velocidad residual
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;

        _rigidbody.useGravity = true;
    }

    private void FixedUpdate()
    {
        if (!_isGrabbed)
            return;

        Vector3 position = _rigidbody.position;
        position.y = _fixedY;
        _rigidbody.MovePosition(position);
        
        Quaternion current = _rigidbody.rotation;
        Quaternion corrected = Quaternion.Euler(0f, current.eulerAngles.y, 0f);
        
        _rigidbody.MoveRotation(corrected);
        _rigidbody.angularVelocity = Vector3.zero;

        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;
        _rigidbody.linearVelocity = velocity;
    }
}