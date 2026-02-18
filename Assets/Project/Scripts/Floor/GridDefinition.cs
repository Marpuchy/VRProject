using UnityEngine;

public sealed class GridDefinition : MonoBehaviour
{
    [SerializeField] private float _cellSize = 1f;

    public float CellSize => _cellSize;
    public Vector3 Origin => transform.position;

    public Vector3 Snap(Vector3 worldPosition)
    {
        float x = Mathf.Round((worldPosition.x - Origin.x) / _cellSize) * _cellSize + Origin.x;
        float z = Mathf.Round((worldPosition.z - Origin.z) / _cellSize) * _cellSize + Origin.z;

        return new Vector3(x, worldPosition.y, z);
    }
}