using System.Collections.Generic;
using Game.Gameplay.Interactables;
using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Robot;
using Game.Gameplay.Player.Torso;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Camera
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración de Recorte")]
    [DeclareBoxGroup("Configuración de Techos")]
    [DeclareBoxGroup("Referencias de Jugadores")]
    public sealed class WallTransparencyCoordinator : MonoBehaviour
    {
        private static readonly int Player1PosId = Shader.PropertyToID("_Player1Pos");
        private static readonly int Player2PosId = Shader.PropertyToID("_Player2Pos");
        private static readonly int CutoutParamsId = Shader.PropertyToID("_CutoutParams");
        private static readonly int PlayerFloorYId = Shader.PropertyToID("_PlayerFloorY");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Group("Configuración de Recorte")]
        [Range(0.5f, 8.0f)]
        [SerializeField] private float _defaultRadius = 2.2f;

        [Group("Configuración de Recorte")]
        [Range(0.5f, 10.0f)]
        [SerializeField] private float _fusedRadius = 3.0f;

        [Group("Configuración de Recorte")]
        [Range(0.05f, 3.0f)]
        [SerializeField] private float _softness = 0.8f;

        [Group("Configuración de Recorte")]
        [Range(0f, 1f)]
        [SerializeField] private float _minAlpha = 0.18f;

        [Group("Configuración de Recorte")]
        [Range(0.3f, 1.5f)]
        [SerializeField] private float _coneTaper = 0.85f;

        [Group("Configuración de Recorte")]
        [SerializeField] private float _heightOffset = 0.85f;

        [Group("Configuración de Recorte")]
        [SerializeField] private float _transitionSpeed = 5f;

        [Group("Configuración de Techos")]
        [Range(0.5f, 3.0f)]
        [SerializeField] private float _ceilingRadiusMultiplier = 1.35f;

        [Group("Configuración de Recorte")]
        [Range(-0.5f, 0.5f)]
        [SerializeField] private float _backWallCutoffMargin = -0.05f;

        [Group("Configuración de Techos")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float _floorProtectionMargin = 0.3f;

        [Group("Configuración de Techos")]
        [SerializeField] private bool _autoUpgradeOccludersInScene = true;

        [Group("Referencias de Jugadores")]
        [SerializeField] private Transform _player1Transform;

        [Group("Referencias de Jugadores")]
        [SerializeField] private Transform _player2Transform;

        [Group("Referencias de Jugadores")]
        [SerializeField] private RobotCoordinator _robotCoordinator;

        private readonly Dictionary<Material, Material> _upgradedMaterials = new(8);
        private float _currentRadius1;
        private float _currentRadius2;
        private Vector3 _currentPos1;
        private Vector3 _currentPos2;

        public void SetTargets(Transform player1, Transform player2)
        {
            _player1Transform = player1;
            _player2Transform = player2;
        }

        public void SetCoordinator(RobotCoordinator coordinator)
        {
            _robotCoordinator = coordinator;
        }

        private void Awake()
        {
            if (_robotCoordinator == null)
            {
                _robotCoordinator = FindFirstObjectByType<RobotCoordinator>();
            }

            ResolveTargetsFromCoordinator();
        }

        private void Start()
        {
            if (_autoUpgradeOccludersInScene)
            {
                UpgradeSceneOccluders();
            }
        }

        private void OnDisable()
        {
            Shader.SetGlobalVector(Player1PosId, Vector4.zero);
            Shader.SetGlobalVector(Player2PosId, Vector4.zero);
            Shader.SetGlobalVector(CutoutParamsId, Vector4.zero);
            Shader.SetGlobalVector(PlayerFloorYId, Vector4.zero);
        }

        private void OnDestroy()
        {
            foreach (var kvp in _upgradedMaterials)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            _upgradedMaterials.Clear();
        }

        private void LateUpdate()
        {
            ResolveTargetsFromCoordinator();

            bool isFused = _robotCoordinator != null && _robotCoordinator.IsFused;

            float targetRadius1 = 0f;
            float targetRadius2 = 0f;
            Vector3 targetPos1 = _currentPos1;
            Vector3 targetPos2 = _currentPos2;
            float player1MinY = -9999f;
            float player2MinY = -9999f;

            if (isFused)
            {
                if (_robotCoordinator != null && _robotCoordinator.Socket != null)
                {
                    Transform socketT = _robotCoordinator.Socket.transform;
                    Vector3 up = socketT.up;
                    targetPos1 = socketT.position + up * _heightOffset;
                    player1MinY = up.y > 0.2f ? (socketT.position.y + _floorProtectionMargin) : -9999f;
                }
                else if (_player1Transform != null)
                {
                    Vector3 up = _player1Transform.up;
                    targetPos1 = _player1Transform.position + up * _heightOffset;
                    player1MinY = up.y > 0.2f ? (_player1Transform.position.y + _floorProtectionMargin) : -9999f;
                }

                targetRadius1 = _fusedRadius;
                targetRadius2 = 0f;
            }
            else
            {
                if (_player1Transform != null)
                {
                    Vector3 up1 = _player1Transform.up;
                    targetPos1 = _player1Transform.position + up1 * _heightOffset;
                    targetRadius1 = _defaultRadius;
                    player1MinY = up1.y > 0.2f ? (_player1Transform.position.y + _floorProtectionMargin) : -9999f;
                }

                if (_player2Transform != null)
                {
                    Vector3 up2 = _player2Transform.up;
                    targetPos2 = _player2Transform.position + up2 * _heightOffset;
                    targetRadius2 = _defaultRadius;
                    player2MinY = up2.y > 0.2f ? (_player2Transform.position.y + _floorProtectionMargin) : -9999f;
                }
            }

            float dt = Time.deltaTime;
            _currentRadius1 = Mathf.MoveTowards(_currentRadius1, targetRadius1, _transitionSpeed * dt);
            _currentRadius2 = Mathf.MoveTowards(_currentRadius2, targetRadius2, _transitionSpeed * dt);

            _currentPos1 = targetPos1;
            _currentPos2 = targetPos2;

            Shader.SetGlobalVector(Player1PosId, new Vector4(_currentPos1.x, _currentPos1.y, _currentPos1.z, _currentRadius1));
            Shader.SetGlobalVector(Player2PosId, new Vector4(_currentPos2.x, _currentPos2.y, _currentPos2.z, _currentRadius2));
            Shader.SetGlobalVector(CutoutParamsId, new Vector4(_softness, _minAlpha, _ceilingRadiusMultiplier, _coneTaper));
            Shader.SetGlobalVector(PlayerFloorYId, new Vector4(player1MinY, player2MinY, _backWallCutoffMargin, _floorProtectionMargin));
        }

        [Button("Actualizar Materiales de Techos y Paredes en Escena")]
        public void UpgradeSceneOccluders()
        {
            Shader seeThroughShader = Shader.Find("TPOB/Environment/SeeThroughWall");
            if (seeThroughShader == null)
            {
                return;
            }

            var renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null || !ShouldUpgradeRenderer(r))
                {
                    continue;
                }

                Material sharedMat = r.sharedMaterial;
                if (sharedMat == null || sharedMat.shader == seeThroughShader)
                {
                    continue;
                }

                if (sharedMat.renderQueue >= 2500)
                {
                    continue;
                }

                bool isMagneticWall = r.GetComponentInParent<MagneticWall>() != null;
                if (!_upgradedMaterials.TryGetValue(sharedMat, out Material upgradedMat) || upgradedMat == null)
                {
                    upgradedMat = new Material(seeThroughShader)
                    {
                        name = $"{sharedMat.name}_SeeThrough"
                    };

                    if (sharedMat.HasProperty(BaseMapId) && sharedMat.GetTexture(BaseMapId) != null)
                    {
                        upgradedMat.SetTexture(BaseMapId, sharedMat.GetTexture(BaseMapId));
                        upgradedMat.SetTextureScale(BaseMapId, sharedMat.GetTextureScale(BaseMapId));
                        upgradedMat.SetTextureOffset(BaseMapId, sharedMat.GetTextureOffset(BaseMapId));
                    }
                    else if (sharedMat.HasProperty(MainTexId) && sharedMat.GetTexture(MainTexId) != null)
                    {
                        upgradedMat.SetTexture(BaseMapId, sharedMat.GetTexture(MainTexId));
                        upgradedMat.SetTextureScale(BaseMapId, sharedMat.GetTextureScale(MainTexId));
                        upgradedMat.SetTextureOffset(BaseMapId, sharedMat.GetTextureOffset(MainTexId));
                    }

                    if (sharedMat.HasProperty(BaseColorId))
                    {
                        upgradedMat.SetColor(BaseColorId, sharedMat.GetColor(BaseColorId));
                    }
                    else if (sharedMat.HasProperty(ColorId))
                    {
                        upgradedMat.SetColor(BaseColorId, sharedMat.GetColor(ColorId));
                    }

                    if (sharedMat.HasProperty(SmoothnessId))
                    {
                        upgradedMat.SetFloat(SmoothnessId, sharedMat.GetFloat(SmoothnessId));
                    }

                    if ((isMagneticWall || sharedMat.IsKeywordEnabled("_EMISSION")) && sharedMat.HasProperty(EmissionColorId))
                    {
                        Color em = sharedMat.GetColor(EmissionColorId);
                        em.a = 1f;
                        upgradedMat.SetColor(EmissionColorId, em);
                    }
                    else
                    {
                        upgradedMat.SetColor(EmissionColorId, Color.clear);
                    }

                    _upgradedMaterials[sharedMat] = upgradedMat;
                }

                r.sharedMaterial = upgradedMat;
            }
        }

        private static bool ShouldUpgradeRenderer(MeshRenderer r)
        {
            if (r.GetComponentInParent<LegsController>() != null ||
                r.GetComponentInParent<TorsoController>() != null ||
                r.GetComponentInParent<PlayerXRaySilhouette>() != null)
            {
                return false;
            }

            if (r.GetComponentInParent<MagneticWall>() != null ||
                r.GetComponentInParent<SlidingDoor>() != null ||
                r.GetComponentInParent<BreakableWall>() != null)
            {
                return true;
            }

            if (r.GetComponentInParent<GrabbableObject>() != null ||
                r.GetComponentInParent<MagneticKey>() != null ||
                r.GetComponentInParent<TargetButton>() != null ||
                r.GetComponentInParent<LeverMechanism>() != null ||
                r.GetComponentInParent<WeightPlatform>() != null ||
                r.GetComponentInParent<PuzzleSocket>() != null ||
                r.GetComponentInParent<CatapultMechanism>() != null ||
                r.GetComponentInParent<Rigidbody>() != null)
            {
                return false;
            }

            string objName = r.gameObject.name;
            string parentName = r.transform.parent != null ? r.transform.parent.name : string.Empty;

            if (objName.Contains("Ceiling") || objName.Contains("Wall") || objName.Contains("Lintel") ||
                parentName.Contains("Ceiling") || parentName.Contains("Catwalk") || parentName.Contains("Balcony") ||
                parentName.Contains("Wall") || parentName.Contains("Duct"))
            {
                return true;
            }

            // Elevated static surfaces (ceilings, upper decks, high bridges) above ground level
            Bounds b = r.bounds;
            return b.max.y > 1.8f && (b.size.x >= 1.5f || b.size.z >= 1.5f || b.size.y >= 1.5f);
        }

        private void ResolveTargetsFromCoordinator()
        {
            if (_player1Transform != null && _player2Transform != null)
            {
                return;
            }

            if (_robotCoordinator != null)
            {
                if (_player1Transform == null && _robotCoordinator.Legs != null)
                {
                    _player1Transform = _robotCoordinator.Legs.transform;
                }

                if (_player2Transform == null && _robotCoordinator.Torso != null)
                {
                    _player2Transform = _robotCoordinator.Torso.transform;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            float ceilingRadius = _defaultRadius * _ceilingRadiusMultiplier;

            if (_player1Transform != null)
            {
                Vector3 p1 = _player1Transform.position + _player1Transform.up * _heightOffset;
                Gizmos.color = new Color(0f, 0.85f, 1f, 0.65f);
                Gizmos.DrawWireSphere(p1, _defaultRadius);
                Gizmos.color = new Color(0f, 1f, 0.6f, 0.45f);
                Gizmos.DrawWireSphere(p1, ceilingRadius);
            }

            if (_player2Transform != null)
            {
                Vector3 p2 = _player2Transform.position + _player2Transform.up * _heightOffset;
                Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.65f);
                Gizmos.DrawWireSphere(p2, _defaultRadius);
                Gizmos.color = new Color(0f, 1f, 0.6f, 0.45f);
                Gizmos.DrawWireSphere(p2, ceilingRadius);
            }
        }
    }
}
