using System;
using System.Collections.Generic;
using System.Text;

namespace gameManager.src.Memory 
{ 
    public sealed class MemoryResource 
    { 
        public string Name { get; init; } = string.Empty;
        public IntPtr Address { get; init; } 
        public string Type { get; init; } = string.Empty; 
        public int Length { get; init; } 
        public IReadOnlyList<IntPtr> Offsets { get; init; } = Array.Empty<IntPtr>(); 
        public bool IsPointerChain => Offsets.Count > 0; 
    } 
}
