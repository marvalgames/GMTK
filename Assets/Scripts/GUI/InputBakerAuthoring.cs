using Unity.Entities;
using UnityEngine;

public class InputBakerAuthoring : MonoBehaviour
{
    // Start is called before the first frame update
    public float maxTapTime = 0.3f;
    
    
    
}


public class InputBaker : Baker<InputBakerAuthoring>
{
   public override void Bake(InputBakerAuthoring authoring)
   {
       var e = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);
       AddComponent(e, new InputControllerComponent() {maxTapTime = authoring.maxTapTime} );
   }
}
