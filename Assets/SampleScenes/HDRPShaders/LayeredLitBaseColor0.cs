using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace SampleScenes.TestHDRPShaders
{
    [GenerateAuthoringComponent]
    [MaterialProperty("_BaseColor0")]
    public struct LayeredLitBaseColor0 : IComponentData
    {
        public float4 Value;
    }
}
