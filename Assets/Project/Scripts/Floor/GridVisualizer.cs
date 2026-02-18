using UnityEngine;

[RequireComponent(typeof(GridDefinition))]
public sealed class GridVisualizer : MonoBehaviour
{
    [SerializeField] private int _width = 10;
    [SerializeField] private int _height = 10;
    [SerializeField] private Material _lineMaterial;

    private void Start()
    {
        DrawGrid();
    }

    private void DrawGrid()
    {
        var grid = GetComponent<GridDefinition>();

        float cellSize = grid.CellSize;
        Vector3 origin = grid.Origin;

        for (int x = 0; x <= _width; x++)
        {
            CreateLine(
                origin + new Vector3(x * cellSize, 0.01f, 0),
                origin + new Vector3(x * cellSize, 0.01f, _height * cellSize));
        }

        for (int z = 0; z <= _height; z++)
        {
            CreateLine(
                origin + new Vector3(0, 0.01f, z * cellSize),
                origin + new Vector3(_width * cellSize, 0.01f, z * cellSize));
        }
    }

    private void CreateLine(Vector3 start, Vector3 end)
    {
        var go = new GameObject("GridLine");
        go.transform.SetParent(transform);

        var lr = go.AddComponent<LineRenderer>();

        lr.material = _lineMaterial;
        lr.startWidth = 0.02f;
        lr.endWidth = 0.02f;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }
}