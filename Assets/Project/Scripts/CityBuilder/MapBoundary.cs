using UnityEngine;
using UnityEngine.Rendering;

namespace CityBuilder
{
    /// <summary>
    /// Creates four translucent wall colliders around the play area and expands them when the player levels up.
    /// Attach this to the Ground GameObject (or any centred object).
    /// Requires a LevelManager in the scene.
    ///
    /// NOTE: The parent transform must have scale (1,1,1) for wall sizes to be accurate.
    /// </summary>
    public class MapBoundary : MonoBehaviour
    {
        const string k_AutoSpawnName = "MapBoundary (Auto)";

        [Header("Boundary Size")]
        [Tooltip("Half-size (metres) of the playable square at level 1.")]
        [SerializeField] private float _initialHalfSize = 10f;

        [Tooltip("How many extra metres the boundary expands per level.")]
        [SerializeField] private float _expansionPerLevel = 5f;

        [Header("Wall Dimensions")]
        [Tooltip("Height of the visible/collidable walls.")]
        [SerializeField] private float _wallHeight = 10f;

        [Tooltip("Thickness of each wall.")]
        [SerializeField] private float _wallThickness = 0.5f;

        [Header("Appearance")]
        [Tooltip("Base colour of the translucent walls. Alpha controls opacity.")]
        [SerializeField] private Color _wallColor = new Color(0.25f, 0.65f, 1f, 0.25f);

        [Header("Animation")]
        [Tooltip("Seconds the expansion animation takes.")]
        [SerializeField, Min(0f)] private float _expandDuration = 1.2f;

        [Header("Physical Player Constraint")]
        [Tooltip("If enabled, physical HMD movement is clamped inside the map boundary.")]
        [SerializeField] private bool _blockPhysicalWalkThroughWalls = true;

        [Tooltip("Extra inside margin (metres) kept between the headset and boundary edge.")]
        [SerializeField, Min(0f)] private float _physicalHeadPadding = 0.2f;

        // One struct per wall keeps collider + transform paired
        private Transform _north, _south, _east, _west;

        // Shared material (created once at runtime)
        private Material _wallMaterial;

        // Expansion state
        private float _currentHalfSize;
        private float _targetHalfSize;
        private float _startHalfSize;
        private float _expandTimer;
        private bool  _isExpanding;
        private Transform _cachedHead;
        private Transform _cachedPlayerRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureBoundaryExists()
        {
            if (!Application.isPlaying)
                return;

            if (FindFirstObjectByType<MapBoundary>() != null)
                return;

            LevelManager levelManager = FindFirstObjectByType<LevelManager>();
            if (levelManager == null)
                return;

            Vector3 center = ResolveAutoBoundaryCenter(levelManager);
            GameObject boundaryRoot = new(k_AutoSpawnName);
            boundaryRoot.transform.SetPositionAndRotation(center, Quaternion.identity);
            boundaryRoot.transform.localScale = Vector3.one;
            boundaryRoot.AddComponent<MapBoundary>();

            Debug.Log($"[MapBoundary] Auto-created because no MapBoundary existed in scene. Center: {center}");
        }

        static Vector3 ResolveAutoBoundaryCenter(LevelManager levelManager)
        {
            GridDefinition grid = FindFirstObjectByType<GridDefinition>();
            if (grid != null)
            {
                Vector3 origin = grid.Origin;
                return new Vector3(origin.x, 0f, origin.z);
            }

            Vector3 levelManagerPosition = levelManager.transform.position;
            return new Vector3(levelManagerPosition.x, 0f, levelManagerPosition.z);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Unity lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _currentHalfSize = _initialHalfSize;
            _targetHalfSize  = _initialHalfSize;

            _wallMaterial = CreateWallMaterial();
            _north = CreateWall("Boundary_North");
            _south = CreateWall("Boundary_South");
            _east  = CreateWall("Boundary_East");
            _west  = CreateWall("Boundary_West");

            ApplyBoundarySize(_currentHalfSize);
        }

        private void OnDestroy()
        {
            if (_wallMaterial != null)
                Destroy(_wallMaterial);
        }

        private void OnEnable()
        {
            if (LevelManager.Instance != null)
                LevelManager.Instance.OnLevelUp += HandleLevelUp;
        }

        private void OnDisable()
        {
            if (LevelManager.Instance != null)
                LevelManager.Instance.OnLevelUp -= HandleLevelUp;
        }

        private void Update()
        {
            if (!_isExpanding) return;

            _expandTimer += Time.deltaTime;
            float t = (_expandDuration > 0f) ? Mathf.Clamp01(_expandTimer / _expandDuration) : 1f;
            t = Mathf.SmoothStep(0f, 1f, t);

            _currentHalfSize = Mathf.Lerp(_startHalfSize, _targetHalfSize, t);
            ApplyBoundarySize(_currentHalfSize);

            if (t >= 1f)
                _isExpanding = false;
        }

        private void LateUpdate()
        {
            if (!_blockPhysicalWalkThroughWalls)
                return;

            if (!TryResolvePlayerRig(out Transform playerRoot, out Transform head))
                return;

            float clampedHalfSize = Mathf.Max(0.01f, _currentHalfSize - _physicalHeadPadding);
            Vector3 localHead = transform.InverseTransformPoint(head.position);
            float clampedX = Mathf.Clamp(localHead.x, -clampedHalfSize, clampedHalfSize);
            float clampedZ = Mathf.Clamp(localHead.z, -clampedHalfSize, clampedHalfSize);

            Vector3 localCorrection = new Vector3(clampedX - localHead.x, 0f, clampedZ - localHead.z);
            if (localCorrection.sqrMagnitude <= 0.0000001f)
                return;

            Vector3 worldCorrection = transform.TransformVector(localCorrection);
            playerRoot.position += worldCorrection;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Level-up handler
        // ──────────────────────────────────────────────────────────────────────

        private void HandleLevelUp(int newLevel)
        {
            _startHalfSize  = _currentHalfSize;
            _targetHalfSize = _initialHalfSize + (newLevel - 1) * _expansionPerLevel;
            _expandTimer    = 0f;
            _isExpanding    = true;

            Debug.Log($"[MapBoundary] Expanding to half-size {_targetHalfSize}m (level {newLevel}).");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Wall construction
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a wall using a Cube primitive so it has a MeshRenderer + BoxCollider out of the box.
        /// The collider size stays (1,1,1); we control world size through localScale.
        /// </summary>
        private Transform CreateWall(string wallName)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = wallName;
            go.transform.SetParent(transform, worldPositionStays: false);

            go.GetComponent<MeshRenderer>().sharedMaterial = _wallMaterial;

            // Mark as non-convex trigger-free solid wall — default BoxCollider is fine.
            return go.transform;
        }

        /// <summary>Repositions and scales the four walls to match the given half-size.</summary>
        private void ApplyBoundarySize(float half)
        {
            // Centre of each wall face sits at half + thickness/2 from the origin
            float edge = half + _wallThickness * 0.5f;

            // Walls running along X (north / south) are wide in X, thin in Z
            float xSpan = half * 2f + _wallThickness * 2f; // cover the corners too

            float yCenter = _wallHeight * 0.5f;

            // North (+Z), South (−Z): wide in X
            SetWall(_north, new Vector3(0f, yCenter,  edge), new Vector3(xSpan,      _wallHeight, _wallThickness));
            SetWall(_south, new Vector3(0f, yCenter, -edge), new Vector3(xSpan,      _wallHeight, _wallThickness));
            // East (+X), West (−X): wide in Z
            SetWall(_east,  new Vector3( edge, yCenter, 0f), new Vector3(_wallThickness, _wallHeight, xSpan));
            SetWall(_west,  new Vector3(-edge, yCenter, 0f), new Vector3(_wallThickness, _wallHeight, xSpan));
        }

        private static void SetWall(Transform wall, Vector3 localPos, Vector3 localScale)
        {
            wall.localPosition = localPos;
            wall.localScale    = localScale;
        }

        private bool TryResolvePlayerRig(out Transform playerRoot, out Transform head)
        {
            head = _cachedHead;
            if (head == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    playerRoot = null;
                    return false;
                }

                head = mainCamera.transform;
                _cachedHead = head;
            }

            playerRoot = _cachedPlayerRoot;
            if (playerRoot == null)
            {
                CharacterController characterController = head.GetComponentInParent<CharacterController>();
                if (characterController != null)
                {
                    playerRoot = characterController.transform;
                }
                else
                {
                    playerRoot = head.root;
                }

                _cachedPlayerRoot = playerRoot;
            }

            return playerRoot != null;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Translucent material (URP-compatible, created at runtime)
        // ──────────────────────────────────────────────────────────────────────

        private Material CreateWallMaterial()
        {
            // Try URP Lit first, then fall back to Standard
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader) { name = "MapBoundary_Wall" };

            // ---- Transparent / alpha-blend mode ----
            // Works for both URP Lit and Standard
            mat.SetFloat("_Surface", 1f);       // URP: 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend", 0f);          // URP Alpha blend
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)RenderQueue.Transparent;

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            // ---- Colour ----
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", _wallColor);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", _wallColor);

            // Render both faces so the wall looks solid from inside and outside
            mat.SetFloat("_Cull", (float)CullMode.Off);

            return mat;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Editor gizmos
        // ──────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            float half = Application.isPlaying ? _currentHalfSize : _initialHalfSize;
            Gizmos.color = new Color(_wallColor.r, _wallColor.g, _wallColor.b, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(
                new Vector3(0f, _wallHeight * 0.5f, 0f),
                new Vector3(half * 2f, _wallHeight, half * 2f)
            );
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void OnDrawGizmosSelected()
        {
            int levels = Mathf.CeilToInt((_expansionPerLevel > 0f) ? 200f / _expansionPerLevel : 6f);
            levels = Mathf.Clamp(levels, 2, 8);
            for (int lvl = 1; lvl <= levels; lvl++)
            {
                float half  = _initialHalfSize + (lvl - 1) * _expansionPerLevel;
                float alpha = 0.06f + lvl * 0.05f;
                Gizmos.color = new Color(0f, 1f, 0.4f, alpha);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(
                    new Vector3(0f, _wallHeight * 0.5f, 0f),
                    new Vector3(half * 2f, _wallHeight, half * 2f)
                );
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
