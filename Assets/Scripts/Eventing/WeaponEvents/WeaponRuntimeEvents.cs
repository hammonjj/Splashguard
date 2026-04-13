using BitBox.Toymageddon.Weapons;
using UnityEngine;

namespace BitBox.Library.Eventing.WeaponEvents
{
    public sealed class WeaponSpinupStartedEvent
    {
        public WeaponSpinupStartedEvent(int playerIndex, WeaponDefinition weapon, GameObject weaponObject, float spinUpSeconds)
        {
            PlayerIndex = playerIndex;
            Weapon = weapon;
            WeaponObject = weaponObject;
            SpinUpSeconds = spinUpSeconds;
        }

        public int PlayerIndex { get; }
        public WeaponDefinition Weapon { get; }
        public GameObject WeaponObject { get; }
        public float SpinUpSeconds { get; }
    }

    public sealed class WeaponSpinupCancelledEvent
    {
        public WeaponSpinupCancelledEvent(int playerIndex, WeaponDefinition weapon, GameObject weaponObject)
        {
            PlayerIndex = playerIndex;
            Weapon = weapon;
            WeaponObject = weaponObject;
        }

        public int PlayerIndex { get; }
        public WeaponDefinition Weapon { get; }
        public GameObject WeaponObject { get; }
    }

    public sealed class WeaponFiredEvent
    {
        public WeaponFiredEvent(
            int playerIndex,
            WeaponDefinition weapon,
            AmmoDefinition ammo,
            ProjectileDefinition projectile,
            int remainingAmmo,
            bool infiniteAmmo)
        {
            PlayerIndex = playerIndex;
            Weapon = weapon;
            Ammo = ammo;
            Projectile = projectile;
            RemainingAmmo = remainingAmmo;
            InfiniteAmmo = infiniteAmmo;
        }

        public int PlayerIndex { get; }
        public WeaponDefinition Weapon { get; }
        public AmmoDefinition Ammo { get; }
        public ProjectileDefinition Projectile { get; }
        public int RemainingAmmo { get; }
        public bool InfiniteAmmo { get; }
    }

    public sealed class WeaponAmmoChangedEvent
    {
        public WeaponAmmoChangedEvent(
            int playerIndex,
            WeaponDefinition weapon,
            int currentAmmo,
            int clipCapacity,
            bool infiniteAmmo)
        {
            PlayerIndex = playerIndex;
            Weapon = weapon;
            CurrentAmmo = currentAmmo;
            ClipCapacity = clipCapacity;
            InfiniteAmmo = infiniteAmmo;
        }

        public int PlayerIndex { get; }
        public WeaponDefinition Weapon { get; }
        public int CurrentAmmo { get; }
        public int ClipCapacity { get; }
        public bool InfiniteAmmo { get; }
    }

    public sealed class WeaponDryFireEvent
    {
        public WeaponDryFireEvent(int playerIndex, WeaponDefinition weapon)
        {
            PlayerIndex = playerIndex;
            Weapon = weapon;
        }

        public int PlayerIndex { get; }
        public WeaponDefinition Weapon { get; }
    }

    public sealed class ProjectileSpawnedEvent
    {
        public ProjectileSpawnedEvent(
            int playerIndex,
            WeaponDefinition weapon,
            AmmoDefinition ammo,
            ProjectileDefinition projectile,
            PhysicalProjectile projectileInstance)
        {
            PlayerIndex = playerIndex;
            Weapon = weapon;
            Ammo = ammo;
            Projectile = projectile;
            ProjectileInstance = projectileInstance;
        }

        public int PlayerIndex { get; }
        public WeaponDefinition Weapon { get; }
        public AmmoDefinition Ammo { get; }
        public ProjectileDefinition Projectile { get; }
        public PhysicalProjectile ProjectileInstance { get; }
    }

    public sealed class ProjectileImpactEvent
    {
        public ProjectileImpactEvent(
            int playerIndex,
            WeaponDefinition weapon,
            AmmoDefinition ammo,
            ProjectileDefinition projectile,
            PhysicalProjectile projectileInstance,
            GameObject hitObject,
            Vector3 point,
            Vector3 normal)
        {
            PlayerIndex = playerIndex;
            Weapon = weapon;
            Ammo = ammo;
            Projectile = projectile;
            ProjectileInstance = projectileInstance;
            HitObject = hitObject;
            Point = point;
            Normal = normal;
        }

        public int PlayerIndex { get; }
        public WeaponDefinition Weapon { get; }
        public AmmoDefinition Ammo { get; }
        public ProjectileDefinition Projectile { get; }
        public PhysicalProjectile ProjectileInstance { get; }
        public GameObject HitObject { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public int Damage => Ammo != null ? Ammo.Damage : 0;
    }
}
