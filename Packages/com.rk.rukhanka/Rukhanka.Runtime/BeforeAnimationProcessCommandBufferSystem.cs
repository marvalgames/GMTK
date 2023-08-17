using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

/////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{

[DisableAutoCreation]
public partial class BeforeAnimationProcessCommandBufferSystem: EntityCommandBufferSystem
{
	public unsafe struct Singleton: IComponentData, IECBSingleton
	{
		internal UnsafeList<EntityCommandBuffer>* pendingBuffers;
		internal Allocator allocator;

		public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
		{
			var rv = EntityCommandBufferSystem.CreateCommandBuffer(ref *pendingBuffers, allocator, world);
			return rv;
		}

		public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
		{
			var ptr = UnsafeUtility.AddressOf(ref buffers);
			pendingBuffers = (UnsafeList<EntityCommandBuffer>*)ptr;
		}

		public void SetAllocator(Allocator alloc)
		{
			allocator = alloc;
		}
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////

	protected override void OnCreate()
	{
		base.OnCreate();
		this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
	}
}

}
