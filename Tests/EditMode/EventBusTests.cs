using NUnit.Framework;
using KotORUnity.Core;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Tests.EditMode
{
    /// <summary>
    /// Tests for EventBus — subscribe, publish, unsubscribe, clear, and exception isolation.
    /// </summary>
    public class EventBusTests
    {
        [SetUp]
        public void Setup() => EventBus.ClearAll();

        [TearDown]
        public void Teardown() => EventBus.ClearAll();

        // ── Subscribe & Publish ────────────────────────────────────────────────
        [Test]
        public void Publish_NoListeners_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                EventBus.Publish(EventBus.EventType.ModeSwitch));
        }

        [Test]
        public void Subscribe_ThenPublish_CallbackInvoked()
        {
            bool called = false;
            EventBus.Subscribe(EventBus.EventType.ModeSwitch,
                _ => called = true);

            EventBus.Publish(EventBus.EventType.ModeSwitch);
            Assert.IsTrue(called);
        }

        [Test]
        public void Subscribe_SameCallbackTwice_OnlyCalledOnce()
        {
            int count = 0;
            System.Action<EventBus.GameEventArgs> cb = _ => count++;

            EventBus.Subscribe(EventBus.EventType.ModeSwitch, cb);
            EventBus.Subscribe(EventBus.EventType.ModeSwitch, cb); // duplicate

            EventBus.Publish(EventBus.EventType.ModeSwitch);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Unsubscribe_AfterSubscribe_CallbackNotInvoked()
        {
            bool called = false;
            System.Action<EventBus.GameEventArgs> cb = _ => called = true;

            EventBus.Subscribe(EventBus.EventType.ModeSwitch, cb);
            EventBus.Unsubscribe(EventBus.EventType.ModeSwitch, cb);
            EventBus.Publish(EventBus.EventType.ModeSwitch);

            Assert.IsFalse(called);
        }

        [Test]
        public void Publish_PassesCorrectArgs_ToListener()
        {
            EventBus.ModeSwitchEventArgs received = null;
            EventBus.Subscribe(EventBus.EventType.ModeSwitch, args =>
            {
                received = args as EventBus.ModeSwitchEventArgs;
            });

            var sent = new EventBus.ModeSwitchEventArgs(GameMode.Action, GameMode.RTS);
            EventBus.Publish(EventBus.EventType.ModeSwitch, sent);

            Assert.IsNotNull(received);
            Assert.AreEqual(GameMode.Action, received.PreviousMode);
            Assert.AreEqual(GameMode.RTS, received.NewMode);
        }

        [Test]
        public void ListenerException_DoesNotPreventOtherListeners()
        {
            bool secondCalled = false;

            // First listener throws
            EventBus.Subscribe(EventBus.EventType.CombatStarted,
                _ => throw new System.Exception("Intentional test exception"));

            // Second listener should still fire
            EventBus.Subscribe(EventBus.EventType.CombatStarted,
                _ => secondCalled = true);

            Assert.DoesNotThrow(() =>
                EventBus.Publish(EventBus.EventType.CombatStarted));

            Assert.IsTrue(secondCalled);
        }

        [Test]
        public void Clear_SpecificType_OnlyRemovesThatType()
        {
            bool modeCallbackCalled = false;
            bool combatCallbackCalled = false;

            EventBus.Subscribe(EventBus.EventType.ModeSwitch, _ => modeCallbackCalled = true);
            EventBus.Subscribe(EventBus.EventType.CombatStarted, _ => combatCallbackCalled = true);

            EventBus.Clear(EventBus.EventType.ModeSwitch);

            EventBus.Publish(EventBus.EventType.ModeSwitch);
            EventBus.Publish(EventBus.EventType.CombatStarted);

            Assert.IsFalse(modeCallbackCalled);
            Assert.IsTrue(combatCallbackCalled);
        }

        [Test]
        public void ClearAll_RemovesAllListeners()
        {
            bool called = false;
            EventBus.Subscribe(EventBus.EventType.ModeSwitch, _ => called = true);
            EventBus.ClearAll();
            EventBus.Publish(EventBus.EventType.ModeSwitch);
            Assert.IsFalse(called);
        }

        [Test]
        public void AbilityEventArgs_CarriesCorrectData()
        {
            EventBus.AbilityEventArgs received = null;
            EventBus.Subscribe(EventBus.EventType.AbilityUsed, args =>
            {
                received = args as EventBus.AbilityEventArgs;
            });

            var sent = new EventBus.AbilityEventArgs(null, null, "Force Push", GameMode.RTS);
            EventBus.Publish(EventBus.EventType.AbilityUsed, sent);

            Assert.IsNotNull(received);
            Assert.AreEqual("Force Push", received.AbilityName);
            Assert.AreEqual(GameMode.RTS, received.ExecutionMode);
        }
    }
}
