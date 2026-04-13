
# Message Bus System

A lightweight, type-safe messaging/event system for Unity. Attach a single **MessageBus** component to the root of an object hierarchy (e.g., a player GameObject) and decouple event publishers from subscribers.

---

## Installation

1. Copy **MessageBus.cs** and **MessageBusExtensions.cs** into your project (e.g., `Assets/Scripts/Utilities/`).
2. On the root GameObject (e.g., Player), add the **MessageBus** component.

---

## Defining Events

Create simple structs or classes to represent messages:

```csharp
public struct PlayerDiedEvent {}
public struct PlayerHitEvent { public int Damage; }
```

Subscribing to Events
In any MonoBehaviour under the bus hierarchy:
```csharp
private void OnEnable() {
    gameObject.SubscribeMessage<PlayerDiedEvent>(OnPlayerDied);
}

private void OnDisable() {
    gameObject.UnsubscribeMessage<PlayerDiedEvent>(OnPlayerDied);
}

private void OnPlayerDied(PlayerDiedEvent evt) {
    // play death animation, sound, VFX, etc.
}
```
Publishing Events
From any GameObject (even outside the bus hierarchy):
```csharp
// Assuming `playerRoot` has the MessageBus component:
playerRoot.PublishMessage(new PlayerHitEvent { Damage = 5 });
```

Core API
Subscribe<T>(Action<T>) – Listen for messages of type T.

Unsubscribe<T>(Action<T>) – Stop listening.

Publish<T>(T message) – Broadcast a message to all subscribers of T.

You can call these directly on an MessageBus instance or via the extension methods on GameObject:
```csharp
var bus = myGameObject.GetMessageBus();
bus.Subscribe<PlayerHitEvent>(OnHit);
bus.Publish(new PlayerHitEvent { Damage = 3 });
```

Tips & Best Practices
Type safety: The system is generic—no string keys or manual casting.

Decoupling: Publishers do not need direct references to subscribers.

Performance: Suitable for gameplay events; avoid very high-frequency publications (e.g., per‑frame) on large listener sets.