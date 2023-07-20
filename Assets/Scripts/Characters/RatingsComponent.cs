using Unity.Entities;


public struct RatingsComponent : IComponentData
{
    public int tag;
    public float speed;
    public float hitPower;
    public float maxHealth;
    public float shootRangeDistance;
    public float chaseRangeDistance;
    public float stopRangeDistance;
    public float combatRangeDistance;
    public float gameSpeed;
    public float gameWeaponPower;
    public float WeaponPower;


}


