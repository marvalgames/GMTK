using Unity.Entities;
using UnityEngine;

namespace Sandbox.Player
{
    public class PlayerRatings : MonoBehaviour
    {

        public PlayerRatingsScriptableObject Ratings;
        public float meleeWeaponPower = 1;
        public float hitPower = 10;//punch kick



       


    }

    public class PlayerRatingsBaker : Baker<PlayerRatings>
    {
        public override void Bake(PlayerRatings authoring)
        {
            var e = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);

            AddComponent( e,
                    new RatingsComponent
                    {
                        tag = 1, maxHealth = authoring.Ratings.maxHealth, 
                        speed = authoring.Ratings.speed,
                        gameSpeed =  authoring.Ratings.speed,
                        gameWeaponPower = authoring.meleeWeaponPower,
                        WeaponPower = authoring.meleeWeaponPower,
                        hitPower = authoring.hitPower
                    })
                ;

        }
    }

}
