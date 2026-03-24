using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PerfectCubeSetup : MonoBehaviour
{
    public XRGrabInteractable grabInteractable;
    public Collider[] handleColliders;

    void Awake()
    {
        // Empieza con todas las capas que quieres permitir (por ejemplo "Default")
        InteractionLayerMask newMask = InteractionLayerMask.GetMask("Default");

        // Excluye cada handle
        foreach (var handle in handleColliders)
        {
            string layerName = LayerMask.LayerToName(handle.gameObject.layer);
            InteractionLayerMask handleMask = InteractionLayerMask.GetMask(layerName);

            // Substracción de máscara: se quita el handle
            newMask = newMask & ~handleMask;
        }

        grabInteractable.interactionLayers = newMask;
    }
}