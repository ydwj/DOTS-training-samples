using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[InternalBufferCapacity(4)]
public struct GoalReference : IBufferElementData
{
    public Entity Goal;
}