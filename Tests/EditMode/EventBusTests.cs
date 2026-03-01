using NUnit.Framework;
using System.Collections.Generic;
using KotORUnity.Core;

namespace KotORUnity.Tests.EditMode
{
    /// <summary>Unit tests for the EventBus.</summary>
    public class EventBusTests
    {
        [SetUp]
        public void Setup() => EventBus.ClearAll();
        [TearDown]
        public void TearDown() => EventBus.ClearAll();

        [Test]
        public void Subscribe_And_Publish_CallsCallback()
        {
            bool called = false;
            EventBus.Subscribe(EventBus.EventType.ModeSwitch, _ => called = true);
            EventBus.Publish(EventBus.EventType.ModeSwitch);
            Assert.IsTrue(called);
        }

        [Test]
        public void Unsubscribe_StopsCallback()
        {
            int callCount = 0;
            void Handler(EventBus.GameEventArgs _) => callCount++;
            EventBus.Subscribe(EventBus.EventType.ModeSwitch, Handler);
            EventBus.Publish(EventBus.EventType.ModeSwitch);
            EventBus.Unsubscribe(EventBus.EventType.ModeSwitch, Handler);
            EventBus.Publish(EventBus.EventType.ModeSwitch);
            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void Publish_WithArgs_PassesArgsToCallback()
        {
            EventBus.ModeSwitchEventArgs received = null;
            EventBus.Subscribe(EventBus.EventType.ModeSwitch, args =>
                received = args as EventBus.ModeSwitchEventArgs);

            var eventArgs = new EventBus.ModeSwitchEventArgs(
                GameEnums.GameMode.Action, GameEnums.GameMode.RTS);
            EventBus.Publish(EventBus.EventType.ModeSwitch, eventArgs);

            Assert.IsNotNull(received);
            Assert.AreEqual(GameEnums.GameMode.RTS, received.NewMode);
        }

        [Test]
        public void MultipleSubscribers_AllReceiveEvent()
        {
            int callCount = 0;
            EventBus.Subscribe(EventBus.EventType.PlayerDamaged, _ => callCount++);
            EventBus.Subscribe(EventBus.EventType.PlayerDamaged, _ => callCount++);
            EventBus.Subscribe(EventBus.EventType.PlayerDamaged, _ => callCount++);
            EventBus.Publish(EventBus.EventType.PlayerDamaged);
            Assert.AreEqual(3, callCount);
        }

        [Test]
        public void ClearAll_RemovesAllListeners()
        {
            int callCount = 0;
            EventBus.Subscribe(EventBus.EventType.ModeSwitch, _ => callCount++);
            EventBus.Subscribe(EventBus.EventType.PlayerDamaged, _ => callCount++);
            EventBus.ClearAll();
            EventBus.Publish(EventBus.EventType.ModeSwitch);
            EventBus.Publish(EventBus.EventType.PlayerDamaged);
            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void ExceptionInCallback_DoesNotPreventOtherCallbacks()
        {
            int successCount = 0;
            EventBus.Subscribe(EventBus.EventType.ModeSwitch, _ => throw new System.Exception("Test exception"));
            EventBus.Subscribe(EventBus.EventType.ModeSwitch, _ => successCount++);
            EventBus.Publish(EventBus.EventType.ModeSwitch);
            Assert.AreEqual(1, successCount);
        }
    }
}
