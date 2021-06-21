using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Scenes.TestDuplicateProperties
{
    [GenerateAuthoringComponent]
    [MaterialProperty("_DuplicateColor")]
    public struct DuplicateTestColorA : IComponentData
    {
        public float4 Value;
    }
}
