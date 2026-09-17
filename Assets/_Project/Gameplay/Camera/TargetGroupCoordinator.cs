using System;
using System.Collections.Generic;
using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Torso;
using TriInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay.Camera
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Referencias")]
    [DeclareBoxGroup("Separado")]
    [DeclareBoxGroup("Fusionado")]
    [DeclareBoxGroup("Transición")]
    public sealed class TargetGroupCoordinator : MonoBehaviour
    {
        [Group("Referencias")]
        [SerializeField] private CinemachineTargetGroup _targetGroup;

        [Group("Referencias")]
        [SerializeField] private Transform _legsTarget;

        [Group("Referencias")]
        [SerializeField] private Transform _torsoTarget;

        [Group("Separado")]
        [SerializeField] private float _separatedLegsWeight = 1f;

        [Group("Separado")]
        [SerializeField] private float _separatedLegsRadius = 3.5f;

        [Group("Separado")]
        [SerializeField] private float _separatedTorsoWeight = 1f;

        [Group("Separado")]
        [SerializeField] private float _separatedTorsoRadius = 3f;

        [Group("Fusionado")]
        [SerializeField] private float _fusedLegsWeight = 1f;

        [Group("Fusionado")]
        [SerializeField] private float _fusedLegsRadius = 4f;

        [Group("Fusionado")]
        [SerializeField] private float _fusedTorsoWeight = 0f;

        [Group("Fusionado")]
        [SerializeField] private float _fusedTorsoRadius = 0f;

        [Group("Transición")]
        [SerializeField] private float _transitionSpeed = 3f;

        private bool _isFused;
        private float _currentTorsoWeight = 1f;
        private float _currentTorsoRadius = 3f;
        private float _currentLegsRadius = 3.5f;

        private CinemachineTargetGroup.Target _legsMember;
        private CinemachineTargetGroup.Target _torsoMember;

        private Transform _cachedLegsTarget;
        private Transform _cachedTorsoTarget;

        public bool IsFused => _isFused;
        public CinemachineTargetGroup TargetGroup => _targetGroup;
        public Transform LegsTarget => _legsTarget;
        public Transform TorsoTarget => _torsoTarget;

        private static bool IsTargetValid(Transform t)
        {
            return t != null && t.gameObject != null && t.gameObject.activeInHierarchy;
        }

        private void Awake()
        {
            if (_targetGroup == null)
            {
                _targetGroup = GetComponent<CinemachineTargetGroup>();
            }

            _currentTorsoWeight = _separatedTorsoWeight;
            _currentTorsoRadius = _separatedTorsoRadius;
            _currentLegsRadius = _separatedLegsRadius;
        }

        private void Start()
        {
            EnsureTargets();
            RebuildMembers();
        }

        private void Update()
        {
            EnsureTargets();

            float targetTorsoWeight = _isFused ? _fusedTorsoWeight : _separatedTorsoWeight;
            float targetTorsoRadius = _isFused ? _fusedTorsoRadius : _separatedTorsoRadius;
            float targetLegsRadius = _isFused ? _fusedLegsRadius : _separatedLegsRadius;

            _currentTorsoWeight = Mathf.MoveTowards(_currentTorsoWeight, targetTorsoWeight, _transitionSpeed * Time.deltaTime);
            _currentTorsoRadius = Mathf.MoveTowards(_currentTorsoRadius, targetTorsoRadius, _transitionSpeed * Time.deltaTime);
            _currentLegsRadius = Mathf.MoveTowards(_currentLegsRadius, targetLegsRadius, _transitionSpeed * Time.deltaTime);

            if (_legsMember != null && IsTargetValid(_legsTarget))
            {
                _legsMember.Weight = _isFused ? _fusedLegsWeight : _separatedLegsWeight;
                _legsMember.Radius = _currentLegsRadius;
            }

            if (_torsoMember != null && IsTargetValid(_torsoTarget))
            {
                _torsoMember.Weight = _currentTorsoWeight;
                _torsoMember.Radius = _currentTorsoRadius;
            }
        }

        public void SetTargets(Transform legs, Transform torso)
        {
            _legsTarget = legs;
            _torsoTarget = torso;
            RebuildMembers();
        }

        public void SetFused(bool isFused)
        {
            _isFused = isFused;
        }

        private void EnsureTargets()
        {
            bool needsRebuild = false;

            if (!IsTargetValid(_legsTarget))
            {
                _legsTarget = null;
                var legs = FindFirstObjectByType<LegsController>();
                if (legs != null && IsTargetValid(legs.transform))
                {
                    _legsTarget = legs.transform;
                }
            }

            if (!IsTargetValid(_torsoTarget))
            {
                _torsoTarget = null;
                var torso = FindFirstObjectByType<TorsoController>();
                if (torso != null && IsTargetValid(torso.transform))
                {
                    _torsoTarget = torso.transform;
                }
            }

            if (_legsTarget != _cachedLegsTarget || _torsoTarget != _cachedTorsoTarget)
            {
                needsRebuild = true;
            }

            if (needsRebuild)
            {
                RebuildMembers();
            }
        }

        private void RebuildMembers()
        {
            if (_targetGroup == null)
            {
                return;
            }

            _targetGroup.Targets.Clear();
            _cachedLegsTarget = _legsTarget;
            _cachedTorsoTarget = _torsoTarget;

            bool hasLegs = IsTargetValid(_legsTarget);
            bool hasTorso = IsTargetValid(_torsoTarget);

            if (hasLegs)
            {
                _legsMember = new CinemachineTargetGroup.Target
                {
                    Object = _legsTarget,
                    Weight = _isFused ? _fusedLegsWeight : _separatedLegsWeight,
                    Radius = _currentLegsRadius
                };
                _targetGroup.Targets.Add(_legsMember);
            }
            else
            {
                _legsMember = null;
            }

            if (hasTorso)
            {
                _torsoMember = new CinemachineTargetGroup.Target
                {
                    Object = _torsoTarget,
                    Weight = _isFused ? _fusedTorsoWeight : _separatedTorsoWeight,
                    Radius = _currentTorsoRadius
                };
                _targetGroup.Targets.Add(_torsoMember);
            }
            else
            {
                _torsoMember = null;
            }
        }
    }
}
