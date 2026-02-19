using UnityEngine;

public class HandleManager : MonoBehaviour
{
    [SerializeField] private Transform _handleUp;
    [SerializeField] private Transform _handleRight;
    [SerializeField] private Transform _handleFront;
    [SerializeField] private float _offset = 0.05f;

    private Transform _cube;

    private void Awake()
    {
        _cube = transform; 
    }

    private void LateUpdate()
    {
        Vector3 scale = _cube.localScale;

        _handleUp.localPosition = new Vector3(0, 0.5f * scale.y + _offset, 0);
        _handleRight.localPosition = new Vector3(0.5f * scale.x + _offset, 0, 0);
        _handleFront.localPosition = new Vector3(0, 0, 0.5f * scale.z + _offset);
    }
}