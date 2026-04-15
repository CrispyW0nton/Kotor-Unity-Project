using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Abilities;
using KotORUnity.Core;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

namespace KotORUnity.AI.Companion
{
    /// <summary>
    /// Order types that can be queued for companion execution in RTS mode.
    /// </summary>
    public enum OrderType
    {
        Move,
        Attack,
        UseAbility,
        HoldPosition,
        Follow
    }

    /// <summary>
    /// A single queued order for a companion.
    /// </summary>
    public class CompanionOrder
    {
        public OrderType Type { get; }
        public Vector3 Destination { get; }
        public GameObject Target { get; }
        public AbilityBase Ability { get; }
        public bool IsCompleted { get; private set; }

        public CompanionOrder(OrderType type, Vector3 destination)
        {
            Type = type;
            Destination = destination;
        }

        public CompanionOrder(OrderType type, GameObject target)
        {
            Type = type;
            Target = target;
        }

        public CompanionOrder(OrderType type, AbilityBase ability, GameObject target)
        {
            Type = type;
            Ability = ability;
            Target = target;
        }

        public void Complete() => IsCompleted = true;
    }

    /// <summary>
    /// Manages the queue of orders for a companion in RTS mode.
    /// Orders execute sequentially; hold-position is checked to know when to advance.
    /// 
    /// The queue is cleared when switching to Action mode (Tier 2 autonomous).
    /// </summary>
    public class OrderQueue
    {
        private readonly Queue<CompanionOrder> _queue = new Queue<CompanionOrder>();
        private CompanionOrder _currentOrder;
        private readonly CompanionAI _companion;

        // Completion tolerances
        private const float MOVE_COMPLETE_DISTANCE = 0.5f;
        private const float ATTACK_COMPLETE_DISTANCE = 1.5f;

        public bool IsEmpty => _queue.Count == 0 && _currentOrder == null;
        public int Count => _queue.Count + (_currentOrder != null ? 1 : 0);

        public OrderQueue(CompanionAI companion)
        {
            _companion = companion;
        }

        // ── ENQUEUE ────────────────────────────────────────────────────────────
        public void EnqueueMove(Vector3 destination)
        {
            _queue.Enqueue(new CompanionOrder(OrderType.Move, destination));
            EventBus.Publish(EventBus.EventType.CompanionOrderIssued);
        }

        public void EnqueueAttack(GameObject target)
        {
            if (target == null) return;
            _queue.Enqueue(new CompanionOrder(OrderType.Attack, target));
            EventBus.Publish(EventBus.EventType.CompanionOrderIssued);
        }

        public void EnqueueAbility(AbilityBase ability, GameObject target)
        {
            if (ability == null) return;
            _queue.Enqueue(new CompanionOrder(OrderType.UseAbility, ability, target));
            EventBus.Publish(EventBus.EventType.CompanionOrderIssued);
        }

        public void EnqueueHoldPosition()
        {
            _queue.Enqueue(new CompanionOrder(OrderType.HoldPosition, Vector3.zero));
        }

        // ── EXECUTE ────────────────────────────────────────────────────────────
        /// <summary>
        /// Called every tick by CompanionAI.
        /// Executes the current order; advances to next when complete.
        /// </summary>
        public void ExecuteNext()
        {
            // Get current order if we don't have one
            if (_currentOrder == null || _currentOrder.IsCompleted)
            {
                if (_queue.Count == 0) return;
                _currentOrder = _queue.Dequeue();
            }

            switch (_currentOrder.Type)
            {
                case OrderType.Move:
                    ExecuteMoveOrder(_currentOrder);
                    break;
                case OrderType.Attack:
                    ExecuteAttackOrder(_currentOrder);
                    break;
                case OrderType.UseAbility:
                    ExecuteAbilityOrder(_currentOrder);
                    break;
                case OrderType.HoldPosition:
                    // Hold — mark complete immediately (used as a "stop" signal)
                    var agent = _companion.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null) agent.ResetPath();
                    _currentOrder.Complete();
                    break;
            }
        }

        private void ExecuteMoveOrder(CompanionOrder order)
        {
            var agent = _companion.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null) { order.Complete(); return; }

            float dist = Vector3.Distance(_companion.transform.position, order.Destination);
            if (dist > MOVE_COMPLETE_DISTANCE)
                agent.SetDestination(order.Destination);
            else
                order.Complete();
        }

        private void ExecuteAttackOrder(CompanionOrder order)
        {
            if (order.Target == null || !order.Target.activeInHierarchy)
            {
                order.Complete();
                return;
            }

            var targetStats = order.Target.GetComponent<Player.PlayerStatsBehaviour>()?.Stats;
            if (targetStats == null || !targetStats.IsAlive)
            {
                order.Complete();
                EventBus.Publish(EventBus.EventType.CompanionOrderCompleted);
                return;
            }

            float dist = Vector3.Distance(_companion.transform.position, order.Target.transform.position);
            var weapon = _companion.GetComponent<Weapons.WeaponBase>();

            if (weapon != null && dist <= 20f)
            {
                // Auto-attack at full efficiency (RTS mode)
                weapon.RTSAttackTick(order.Target, Time.deltaTime);
            }
            else
            {
                // Move toward target
                var agent = _companion.GetComponent<UnityEngine.AI.NavMeshAgent>();
                agent?.SetDestination(order.Target.transform.position);
            }
        }

        private void ExecuteAbilityOrder(CompanionOrder order)
        {
            if (order.Ability == null) { order.Complete(); return; }
            if (!order.Ability.IsReady) return; // Wait for ability

            order.Ability.Execute(_companion.gameObject, order.Target, GameMode.RTS);
            order.Complete();
            EventBus.Publish(EventBus.EventType.CompanionOrderCompleted);
        }

        // ── MANAGEMENT ─────────────────────────────────────────────────────────
        public void Clear()
        {
            _queue.Clear();
            _currentOrder = null;
        }

        public List<string> GetOrderSummary()
        {
            var summary = new List<string>();
            if (_currentOrder != null)
                summary.Add($"[Active] {_currentOrder.Type}");
            foreach (var order in _queue)
                summary.Add($"[Queued] {order.Type}");
            return summary;
        }
    }
}
