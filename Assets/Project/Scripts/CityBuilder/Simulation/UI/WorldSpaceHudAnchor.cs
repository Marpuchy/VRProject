using UnityEngine;

namespace CityBuilderVR
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class WorldSpaceHudAnchor : MonoBehaviour
    {
        [SerializeField] Transform m_FollowTargetOverride;
        [SerializeField] bool m_FollowView = true;
        [SerializeField] bool m_SetCanvasToWorldSpace = true;
        [SerializeField] bool m_AssignWorldCamera = true;
        [SerializeField] Vector3 m_LocalPositionOffset = new(0f, -0.18f, 1.2f);
        [SerializeField] Vector3 m_LocalEulerOffset = Vector3.zero;
        [SerializeField] bool m_ApplyRootScale = true;
        [SerializeField, Min(0.0001f)] float m_RootScale = 0.001f;

        Canvas m_Canvas;
        Transform m_FollowTarget;

        void Awake()
        {
            m_Canvas = GetComponent<Canvas>();
        }

        void OnEnable()
        {
            ResolveFollowTarget();
            ConfigureCanvas();
            ApplyHudTransform();
        }

        void LateUpdate()
        {
            if (!m_FollowView)
            {
                return;
            }

            if (m_FollowTarget == null)
            {
                ResolveFollowTarget();
                ConfigureCanvas();
            }

            ApplyHudTransform();
        }

        [ContextMenu("Snap HUD To View")]
        public void SnapHudToView()
        {
            ResolveFollowTarget();
            ConfigureCanvas();
            ApplyHudTransform();
        }

        void ResolveFollowTarget()
        {
            if (m_FollowTargetOverride != null)
            {
                m_FollowTarget = m_FollowTargetOverride;
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindFirstObjectByType<Camera>();
            }

            m_FollowTarget = mainCamera != null ? mainCamera.transform : null;
        }

        void ConfigureCanvas()
        {
            if (m_Canvas == null)
            {
                m_Canvas = GetComponent<Canvas>();
            }

            if (m_SetCanvasToWorldSpace && m_Canvas.renderMode != RenderMode.WorldSpace)
            {
                m_Canvas.renderMode = RenderMode.WorldSpace;
            }

            if (!m_AssignWorldCamera || m_FollowTarget == null)
            {
                return;
            }

            if (m_FollowTarget.TryGetComponent(out Camera camera))
            {
                m_Canvas.worldCamera = camera;
            }
        }

        void ApplyHudTransform()
        {
            if (m_FollowTarget == null)
            {
                return;
            }

            Transform hudTransform = transform;
            hudTransform.position = m_FollowTarget.TransformPoint(m_LocalPositionOffset);
            hudTransform.rotation = m_FollowTarget.rotation * Quaternion.Euler(m_LocalEulerOffset);

            if (m_ApplyRootScale)
            {
                hudTransform.localScale = Vector3.one * Mathf.Max(0.0001f, m_RootScale);
            }
        }
    }
}
