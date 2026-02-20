using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class HandleRaycastFilter : MonoBehaviour
{
    [Header("Prioridad de capas")]
    public LayerMask handleLayer = 1 << 0; // Default
    public LayerMask cubeLayer = 1 << 8;   // Pon la layer del cubo aquí

    private XRRayInteractor rayInteractor;

    void Awake()
    {
        rayInteractor = GetComponent<XRRayInteractor>();
        rayInteractor.raycastMask = handleLayer; // Solo detecta handles
    }
}