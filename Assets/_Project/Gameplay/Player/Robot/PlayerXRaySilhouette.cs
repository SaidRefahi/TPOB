using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Gameplay.Player.Robot
{
    [DisallowMultipleComponent]
    public sealed class PlayerXRaySilhouette : MonoBehaviour
    {
        [SerializeField] private Color _silhouetteColor = new Color(0f, 0.9f, 1f, 0.85f);
        [SerializeField] private Material _customXRayMaterial;
        [SerializeField] private float _fadeSpeed = 8f;
        [SerializeField] private float _heightOffset = 0.85f;

        private Material _instancedMaterial;
        private readonly List<Renderer> _overlayRenderers = new(8);
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];
        private UnityEngine.Camera _cachedCamera;
        private bool _isOccluded;
        private float _currentAlpha;
        private int _baseColorId;

        public void SetColor(Color color)
        {
            _silhouetteColor = color;
            if (_instancedMaterial != null)
            {
                Color c = _silhouetteColor;
                c.a = _currentAlpha;
                _instancedMaterial.SetColor(_baseColorId, c);
            }
        }

        private void Awake()
        {
            _baseColorId = Shader.PropertyToID("_BaseColor");
            SetupSilhouette();
        }

        private void SetupSilhouette()
        {
            if (_customXRayMaterial != null)
            {
                _instancedMaterial = new Material(_customXRayMaterial);
            }
            else
            {
                Shader shader = Shader.Find("TPOB/Player/XRaySilhouette");
                if (shader == null)
                {
                    return;
                }
                _instancedMaterial = new Material(shader);
            }

            Color initialColor = _silhouetteColor;
            initialColor.a = 0f;
            _instancedMaterial.SetColor(_baseColorId, initialColor);

            var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                var original = meshRenderers[i];
                if (original.gameObject.name.Contains("[XRay_Silhouette]")) continue;

                var filter = original.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                GameObject overlayGo = new GameObject("[XRay_Silhouette]");
                overlayGo.transform.SetParent(original.transform, false);

                var overlayFilter = overlayGo.AddComponent<MeshFilter>();
                overlayFilter.sharedMesh = filter.sharedMesh;

                var overlayRenderer = overlayGo.AddComponent<MeshRenderer>();
                overlayRenderer.sharedMaterial = _instancedMaterial;
                overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
                overlayRenderer.receiveShadows = false;
                overlayRenderer.enabled = false;

                _overlayRenderers.Add(overlayRenderer);
            }

            var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                var original = skinnedRenderers[i];
                if (original.gameObject.name.Contains("[XRay_Silhouette]")) continue;
                if (original.sharedMesh == null) continue;

                GameObject overlayGo = new GameObject("[XRay_Silhouette]");
                overlayGo.transform.SetParent(original.transform, false);

                var overlaySkinned = overlayGo.AddComponent<SkinnedMeshRenderer>();
                overlaySkinned.sharedMesh = original.sharedMesh;
                overlaySkinned.bones = original.bones;
                overlaySkinned.rootBone = original.rootBone;
                overlaySkinned.sharedMaterial = _instancedMaterial;
                overlaySkinned.shadowCastingMode = ShadowCastingMode.Off;
                overlaySkinned.receiveShadows = false;
                overlaySkinned.enabled = false;

                _overlayRenderers.Add(overlaySkinned);
            }
        }

        private void LateUpdate()
        {
            if (_cachedCamera == null || !_cachedCamera.isActiveAndEnabled)
            {
                _cachedCamera = UnityEngine.Camera.main;
                if (_cachedCamera == null)
                {
                    return;
                }
            }

            Vector3 start = _cachedCamera.transform.position;
            Vector3 target = transform.position + Vector3.up * _heightOffset;
            Vector3 dir = target - start;
            float dist = dir.magnitude;

            bool occluded = false;
            if (dist > 0.05f)
            {
                int hitCount = Physics.RaycastNonAlloc(start, dir / dist, _hitBuffer, dist - 0.2f);
                for (int i = 0; i < hitCount; i++)
                {
                    var hitCollider = _hitBuffer[i].collider;
                    if (hitCollider != null && !hitCollider.isTrigger)
                    {
                        Transform hitTransform = hitCollider.transform;
                        if (hitTransform != transform && !hitTransform.IsChildOf(transform))
                        {
                            occluded = true;
                            break;
                        }
                    }
                }
            }

            _isOccluded = occluded;
            float targetAlpha = _isOccluded ? _silhouetteColor.a : 0f;
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, _fadeSpeed * Time.deltaTime);

            bool shouldRender = _currentAlpha > 0.005f;
            for (int i = 0; i < _overlayRenderers.Count; i++)
            {
                var r = _overlayRenderers[i];
                if (r != null && r.enabled != shouldRender)
                {
                    r.enabled = shouldRender;
                }
            }

            if (shouldRender && _instancedMaterial != null)
            {
                Color c = _silhouetteColor;
                c.a = _currentAlpha;
                _instancedMaterial.SetColor(_baseColorId, c);
            }
        }

        private void OnDestroy()
        {
            if (_instancedMaterial != null)
            {
                Destroy(_instancedMaterial);
                _instancedMaterial = null;
            }
        }
    }
}
