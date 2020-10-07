﻿using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct GameState : IComponentData
{
    public int2 GridSize;
    public Entity PlainsPrefab;
    public Entity WaterPrefab;
    public float WaterProbability;
}