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
        [SerializeField] private float _separatedLegsRadius = 1.2f;

        [Group("Separado")]
        [SerializeField] private float _separatedTorsoWeight = 1f;

        [Group("Separado")]
        [SerializeField] private float _separatedTorsoRadius = 1f;

        [Group("Fusionado")]
        [SerializeField] private float _fusedLegsWeight = 1f;

        [Group("Fusionado")]
        [SerializeField] private float _fusedLegsRadius = 1.5f;

        [Group("Fusionado")]
        [SerializeField] private float _fusedTorsoWeight = 0f;

        [Group("Fusionado")]
        [SerializeField] private float _fusedTorsoRadius = 0f;

        [Group("Transición")]
        [SerializeField] private float _transitionSpeed = 3f;

        private bool _isFused;
        private float _currentTorsoWeight = 1f;
        private float _currentTorsoRadius = 1f;
        private float _currentLegsRadius = 1.2f;

        private CinemachineTargetGroup.Target _legsMember;
        private CinemachineTargetGroup.Target _torsoMember;

        public bool IsFused => _isFused;
        public CinemachineTargetGroup TargetGroup => _targetGroup;
        public Transform LegsTarget => _legsTarget;
        public Transform TorsoTarget => _torsoTarget;

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
            if (_legsMember == null || _torsoMember == null)
            {
                EnsureTargets();
                RebuildMembers();
                if (_legsMember == null || _torsoMember == null)
                {
                    return;
                }
            }

            float targetTorsoWeight = _isFused ? _fusedTorsoWeight : _separatedTorsoWeight;
            float targetTorsoRadius = _isFused ? _fusedTorsoRadius : _separatedTorsoRadius;
            float targetLegsRadius = _isFused ? _fusedLegsRadius : _separatedLegsRadius;

            _currentTorsoWeight = Mathf.MoveTowards(_currentTorsoWeight, targetTorsoWeight, _transitionSpeed * Time.deltaTime);
            _currentTorsoRadius = Mathf.MoveTowards(_currentTorsoRadius, targetTorsoRadius, _transitionSpeed * Time.deltaTime);
            _currentLegsRadius = Mathf.MoveTowards(_currentLegsRadius, targetLegsRadius, _transitionSpeed * Time.deltaTime);

            _legsMember.Weight = _isFused ? _fusedLegsWeight : _separatedLegsWeight;
            _legsMember.Radius = _currentLegsRadius;

            _torsoMember.Weight = _currentTorsoWeight;
            _torsoMember.Radius = _currentTorsoRadius;
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
            if (_legsTarget == null)
            {
                var legs = FindFirstObjectByType<LegsController>();
                if (legs != null)
                {
                    _legsTarget = legs.transform;
                }
            }

            if (_torsoTarget == null)
            {
                var torso = FindFirstObjectByType<TorsoController>();
                if (torso != null)
                {
                    _torsoTarget = torso.transform;
                }
            }
        }

        private void RebuildMembers()
        {
            if (_targetGroup == null)
            {
                return;
            }

            _targetGroup.Targets.Clear();

            if (_legsTarget != null)
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

            if (_torsoTarget != null)
            {
                _torsoMember = new CinemachineTargetGroup.Target
                {
                    Object = _torsoTarget,
                    Weight = _currentTorsoWeight,
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
