using System;
using System.Collections.Generic;
using BitBox.Library.Constants;
using UnityEngine;

namespace BitBox.Library.Eventing
{
    /// <summary>
    /// Attach this component to the root of an object tree (e.g., player root).
    /// It manages subscriptions and dispatches messages to all registered listeners.
    /// </summary>
    [DisallowMultipleComponent]
    public class MessageBus : MonoBehaviourBase
    {
        protected override void OnAwakened()
        {
            if (gameObject.tag == Tags.GameController)
            {
                LogInfo("Global Message Bus Initialized");
                GlobalStaticData.GlobalMessageBus = this;
            }
        }

        private readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<T>(Action<T> callback)
        {
            var type = typeof(T);

            if (_subscribers.TryGetValue(type, out var list) == false)
            {
                list = new List<Delegate>();
                _subscribers[type] = list;
            }

            if (list.Contains(callback) == false)
            {
                list.Add(callback);
            }
        }

        public int GetSubscriberCount<T>()
        {
            var type = typeof(T);
            return _subscribers.TryGetValue(type, out var list) ? list.Count : 0;
        }

        public void Unsubscribe<T>(Action<T> callback)
        {
            var type = typeof(T);

            if (_subscribers.TryGetValue(type, out var list))
            {
                list.Remove(callback);

                if (list.Count == 0)
                {
                    _subscribers.Remove(type);
                }
            }
        }

        public void Publish<T>(T message)
        {
            var type = typeof(T);

            LogDebug($"Publishing message of type {type.Name}: {message}");

            if (_subscribers.TryGetValue(type, out var list))
            {
                // Iterate over a copy in case subscribers modify during iteration
                foreach (var callback in list.ToArray())
                {
                    if (callback is Action<T> action)
                    {
                        action(message);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Extension methods to send and receive messages via a MessageBus on a GameObject.
    /// </summary>
    public static class MessageBusExtensions
    {
        /// <summary>
        /// Finds the nearest MessageBus in this GameObject's parent hierarchy.
        /// </summary>
        public static MessageBus GetMessageBus(this GameObject go)
        {
            return go.GetComponentInParent<MessageBus>();
        }

        /// <summary>
        /// Publishes a message on the nearest MessageBus.
        /// </summary>
        public static void PublishMessage<T>(this GameObject go, T message)
        {
            var bus = go.GetMessageBus();

            if (bus != null)
            {
                bus.Publish(message);
            }
            else
            {
                Debug.LogWarning($"MessageBus not found in parents of {go.name}");
            }
        }

        /// <summary>
        /// Subscribes to messages of type T on the nearest MessageBus.
        /// </summary>
        public static void SubscribeMessage<T>(this GameObject go, Action<T> callback)
        {
            var bus = go.GetMessageBus();

            if (bus != null)
            {
                bus.Subscribe(callback);
            }
            else
            {
                Debug.LogWarning($"MessageBus not found in parents of {go.name}");
            }
        }

        /// <summary>
        /// Unsubscribes from messages of type T on the nearest MessageBus.
        /// </summary>
        public static void UnsubscribeMessage<T>(this GameObject go, Action<T> callback)
        {
            var bus = go.GetMessageBus();

            if (bus != null)
            {
                bus.Unsubscribe(callback);
            }
        }
    }

    // Example message types:
    //public struct PlayerDiedEvent {}
    //public struct PlayerHitEvent { public int Damage; }

    /* Usage example:

    // In a consumer, e.g., AnimationController:
    private void OnEnable()
    {
        gameObject.SubscribeMessage<PlayerDiedEvent>(OnPlayerDied);
    }

    private void OnDisable()
    {
        gameObject.UnsubscribeMessage<PlayerDiedEvent>(OnPlayerDied);
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        // Play death animation
    }

    // In an external sender, e.g., Enemy:
    private void DealDamageToPlayer(GameObject playerRoot, int damage)
    {
        playerRoot.PublishMessage(new PlayerHitEvent { Damage = damage });
    }
    */
}
