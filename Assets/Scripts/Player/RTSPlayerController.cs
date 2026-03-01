using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.AI.Companion;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Player
{
    /// <summary>
    /// RTS Mode player controller.
    /// Handles:
    ///   - Elevated camera panning
    ///   - Unit selection (click-to-select companion/player)
    ///   - Move order issuance (right-click terrain)
    ///   - Ability queueing on selected unit
    ///   - Formation commands
    /// 
    /// In RTS mode, the player clicks to command units rather than directly moving.
    /// </summary>
    public class RTSPlayerController : MonoBehaviour
    {
        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Camera Pan")]
        [SerializeField] private float panSpeed = 20f;
        [SerializeField] private float edgeScrollThreshold = 10f; // pixels from screen edge
        [SerializeField] private bool edgeScrollEnabled = true;

        [Header("Selection")]
        [SerializeField] private LayerMask selectableLayerMask;
        [SerializeField] private LayerMask terrainLayerMask;
        [SerializeField] private float selectionRaycastRange = 100f;

        [Header("Companions")]
        [SerializeField] private List<CompanionAI> squadMembers = new List<CompanionAI>();

        // ── STATE ──────────────────────────────────────────────────────────────
        private Camera _rtsCamera;
        private GameObject _selectedUnit;
        private CompanionAI _selectedCompanion;
        private Formation _currentFormation = Formation.Spread;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            // RTS camera will be assigned when RTS camera activates
            EventBus.Subscribe(EventBus.EventType.ModeTransitionCompleted, OnModeChanged);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.ModeTransitionCompleted, OnModeChanged);
        }

        private void Update()
        {
            if (!IsActiveMode()) return;

            HandleEdgeScroll();
        }

        // ── INPUT API (called by InputHandler) ─────────────────────────────────
        public void PanCamera(float h, float v)
        {
            if (_rtsCamera == null) return;
            Vector3 pan = new Vector3(h, 0f, v) * panSpeed * Time.unscaledDeltaTime;
            _rtsCamera.transform.Translate(pan, Space.World);
        }

        public void SelectUnit()
        {
            if (_rtsCamera == null) return;

            Ray ray = _rtsCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, selectionRaycastRange, selectableLayerMask))
            {
                _selectedUnit = hit.collider.gameObject;
                _selectedCompanion = _selectedUnit.GetComponent<CompanionAI>();
                Debug.Log($"[RTS] Selected: {_selectedUnit.name}");
            }
            else
            {
                _selectedUnit = null;
                _selectedCompanion = null;
            }
        }

        public void IssueMouseClickOrder()
        {
            if (_rtsCamera == null || _selectedUnit == null) return;

            Ray ray = _rtsCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, selectionRaycastRange, terrainLayerMask))
            {
                IssueMoveOrder(_selectedUnit, hit.point);
            }
            else if (Physics.Raycast(ray, out RaycastHit enemyHit, selectionRaycastRange))
            {
                // Check if it's an enemy
                var enemy = enemyHit.collider.GetComponent<AI.Enemy.EnemyAI>();
                if (enemy != null)
                    IssueAttackOrder(_selectedUnit, enemy.gameObject);
            }
        }

        private void IssueMoveOrder(GameObject unit, Vector3 destination)
        {
            if (_selectedCompanion != null)
            {
                _selectedCompanion.OrderMoveTo(destination);
                Debug.Log($"[RTS] Move order issued to {unit.name} → {destination}");
                EventBus.Publish(EventBus.EventType.CompanionOrderIssued);
            }
            else
            {
                // Player character move
                var navAgent = unit.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navAgent != null)
                    navAgent.SetDestination(destination);
            }
        }

        private void IssueAttackOrder(GameObject unit, GameObject target)
        {
            if (_selectedCompanion != null)
            {
                _selectedCompanion.OrderAttack(target);
                Debug.Log($"[RTS] Attack order: {unit.name} → {target.name}");
                EventBus.Publish(EventBus.EventType.CompanionOrderIssued);
            }
        }

        public void QueueAbility(int slot)
        {
            if (_selectedCompanion != null)
                _selectedCompanion.QueueAbilityBySlot(slot);
            else
                Debug.Log("[RTS] No companion selected for ability queue.");
        }

        // ── FORMATION COMMANDS ─────────────────────────────────────────────────
        public void SetFormation(Formation formation)
        {
            _currentFormation = formation;
            foreach (var companion in squadMembers)
                companion?.SetFormation(formation);

            EventBus.Publish(EventBus.EventType.FormationChanged);
            Debug.Log($"[RTS] Formation set to: {formation}");
        }

        public void IssueSquadMoveOrder(Vector3 destination)
        {
            Vector3[] formationPositions = CalculateFormationPositions(destination, _currentFormation);
            for (int i = 0; i < squadMembers.Count && i < formationPositions.Length; i++)
                squadMembers[i]?.OrderMoveTo(formationPositions[i]);
        }

        private Vector3[] CalculateFormationPositions(Vector3 center, Formation formation)
        {
            Vector3[] positions = new Vector3[squadMembers.Count];
            float spacing = 2.5f;

            for (int i = 0; i < squadMembers.Count; i++)
            {
                switch (formation)
                {
                    case Formation.Spread:
                        float spreadAngle = (i - squadMembers.Count / 2f) * 45f;
                        positions[i] = center + Quaternion.Euler(0, spreadAngle, 0) * Vector3.forward * spacing;
                        break;
                    case Formation.Line:
                        positions[i] = center + Vector3.right * (i - squadMembers.Count / 2f) * spacing;
                        break;
                    case Formation.Wedge:
                        positions[i] = center + new Vector3(
                            (i - squadMembers.Count / 2f) * spacing,
                            0f,
                            -Mathf.Abs(i - squadMembers.Count / 2f) * spacing * 0.5f);
                        break;
                    case Formation.Column:
                        positions[i] = center - Vector3.forward * i * spacing;
                        break;
                    default:
                        positions[i] = center;
                        break;
                }
            }
            return positions;
        }

        // ── EDGE SCROLL ────────────────────────────────────────────────────────
        private void HandleEdgeScroll()
        {
            if (!edgeScrollEnabled || _rtsCamera == null) return;

            Vector3 pan = Vector3.zero;
            Vector3 mousePos = Input.mousePosition;

            if (mousePos.x <= edgeScrollThreshold) pan.x -= 1f;
            if (mousePos.x >= Screen.width - edgeScrollThreshold) pan.x += 1f;
            if (mousePos.y <= edgeScrollThreshold) pan.z -= 1f;
            if (mousePos.y >= Screen.height - edgeScrollThreshold) pan.z += 1f;

            if (pan != Vector3.zero)
                _rtsCamera.transform.Translate(pan.normalized * panSpeed * Time.unscaledDeltaTime, Space.World);
        }

        // ── HELPERS ────────────────────────────────────────────────────────────
        private bool IsActiveMode()
        {
            var msSystem = FindObjectOfType<ModeSwitchSystem>();
            return msSystem != null && msSystem.CurrentMode == GameMode.RTS;
        }

        private void OnModeChanged(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ModeSwitchEventArgs switchArgs && switchArgs.NewMode == GameMode.RTS)
            {
                // Find the RTS camera when entering RTS mode
                _rtsCamera = GameObject.FindWithTag("RTSCamera")?.GetComponent<Camera>()
                    ?? Camera.main;
            }
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────
        public void RegisterCompanion(CompanionAI companion)
        {
            if (!squadMembers.Contains(companion))
                squadMembers.Add(companion);
        }

        public void RemoveCompanion(CompanionAI companion)
        {
            squadMembers.Remove(companion);
        }

        public GameObject SelectedUnit => _selectedUnit;
    }
}
