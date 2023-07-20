using Unity.Entities;

[RequireMatchingQueriesForUpdate]
public partial class DoorOpenSystem : SystemBase
{
    protected override void OnUpdate()
    {

            Entities.WithoutBurst().WithStructuralChanges().ForEach((ref Entity e, ref DoorComponent doorComponent) =>
                {
                   if(LevelManager.instance.currentLevelCompleted == doorComponent.area)
                    {
                        EntityManager.DestroyEntity(e);
                    }
                }
            ).Run();
    }
}
