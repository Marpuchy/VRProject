using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class RuntimeScaleHandleFactory
{
    const string HandlesRootName = "Handlers";

    static readonly Color UpColor = new(1f, 0.85f, 0.15f, 1f);
    static readonly Color RightColor = new(0.2f, 0.85f, 1f, 1f);
    static readonly Color ForwardColor = new(1f, 0.45f, 0.45f, 1f);

    public static void EnsureHandles(GameObject target, GridDefinition grid, float handleScale, float offset)
    {
        if (target == null)
        {
            return;
        }

        ScaleHandle[] existingHandles = target.GetComponentsInChildren<ScaleHandle>(true);
        if (existingHandles.Length > 0)
        {
            for (int i = 0; i < existingHandles.Length; i++)
            {
                if (existingHandles[i] != null)
                {
                    existingHandles[i].SetGrid(grid);
                    existingHandles[i].SetDesiredWorldScale(handleScale);
                }
            }

            return;
        }

        Transform handlersRoot = target.transform.Find(HandlesRootName);
        if (handlersRoot == null)
        {
            GameObject handlers = new(HandlesRootName);
            handlersRoot = handlers.transform;
            handlersRoot.SetParent(target.transform, false);
        }

        int handleLayer = ResolveHandleLayer(target.layer);
        handlersRoot.gameObject.layer = handleLayer;

        Transform handleUp = CreateHandle("Handle_Up", handlersRoot, target.transform, grid, Vector3.up, handleScale, handleLayer, UpColor);
        Transform handleRight = CreateHandle("Handle_Side_X", handlersRoot, target.transform, grid, Vector3.right, handleScale, handleLayer, RightColor);
        Transform handleFront = CreateHandle("Handle_Side_Z", handlersRoot, target.transform, grid, Vector3.forward, handleScale, handleLayer, ForwardColor);

        HandleManager handleManager = target.GetComponent<HandleManager>();
        if (handleManager == null)
        {
            handleManager = target.AddComponent<HandleManager>();
        }

        handleManager.Configure(handleUp, handleRight, handleFront, offset);
    }

    static Transform CreateHandle(
        string handleName,
        Transform parent,
        Transform target,
        GridDefinition grid,
        Vector3 axis,
        float handleScale,
        int layer,
        Color color)
    {
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        handle.name = handleName;
        handle.layer = layer;

        Transform handleTransform = handle.transform;
        handleTransform.SetParent(parent, false);

        if (handle.TryGetComponent(out MeshRenderer renderer))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material = CreateHandleMaterial(color);
        }

        if (!handle.TryGetComponent(out Rigidbody body))
        {
            body = handle.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeAll;

        XRSimpleInteractable interactable = handle.GetComponent<XRSimpleInteractable>();
        if (interactable == null)
        {
            interactable = handle.AddComponent<XRSimpleInteractable>();
        }

        ScaleHandle scaleHandle = handle.GetComponent<ScaleHandle>();
        if (scaleHandle == null)
        {
            scaleHandle = handle.AddComponent<ScaleHandle>();
        }

        scaleHandle.Configure(target, grid, axis);
        scaleHandle.SetDesiredWorldScale(handleScale);
        return handleTransform;
    }

    static Material CreateHandleMaterial(Color color)
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new(shader);
        if (material.HasProperty("_Color"))
        {
            material.color = color;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        return material;
    }

    static int ResolveHandleLayer(int fallbackLayer)
    {
        int handleLayer = LayerMask.NameToLayer("Handlers");
        return handleLayer >= 0 ? handleLayer : fallbackLayer;
    }
}
