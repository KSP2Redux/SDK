#if UNITY_EDITOR && ENABLE_UNITY_COLLECTIONS_CHECKS
using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Internal
{
    public static partial class InternalCompilerInterface
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UncheckedRefRO<T> UnsafeGetUncheckedRefRO<T>(IntPtr ptr, int index)
            where T : unmanaged, IComponentData =>
            new(ptr + UnsafeUtility.SizeOf<T>() * index, default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UncheckedRefRW<T> UnsafeGetUncheckedRefRW<T>(IntPtr ptr, int index)
            where T : unmanaged, IComponentData =>
            new(ptr + UnsafeUtility.SizeOf<T>() * index, default);
    }
}
#endif
