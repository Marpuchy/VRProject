using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class HandleVisualSetup : MonoBehaviour
{
    [SerializeField] private Color color = Color.yellow;
    [SerializeField] private Transform visual;

    private void Awake()
    {
        if (visual == null)
        {
            Debug.LogError("Visual child not assigned");
            return;
        }

        foreach (var r in visual.GetComponentsInChildren<MeshRenderer>())
        {
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = color;
            r.material = mat;
        }
    }
}