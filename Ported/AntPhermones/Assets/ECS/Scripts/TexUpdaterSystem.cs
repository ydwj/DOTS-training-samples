﻿using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public class TexUpdaterSystem : SystemBase
{
    const float dt = 1.0f / 60;
    const float randomSteering = 0.1f;
    const float decay = 0.999f;

    Entity textureSingleton; //Use a singleton entity for the buffer entity


    protected override void OnCreate()
    {
        RequireSingletonForUpdate<TexSingleton>();
    }

    
    protected override void OnUpdate()
    {
        Vector2 bounds = AntMovementSystem.bounds;
        int TexSize = RefsAuthoring.TexSize;

        //Get the pheremones data 
        EntityQuery doesTextInitExist = GetEntityQuery(typeof(TexInitialiser));
        if(!doesTextInitExist.IsEmpty)
        {
            return;
        }
        var localPheromones = EntityManager.GetBuffer<PheromonesBufferData>(GetSingletonEntity<TexSingleton>()).AsNativeArray();

        Entities
            .WithNativeDisableParallelForRestriction(localPheromones)
            .ForEach((int entityInQueryIndex, ref Translation translation, ref Direction direction, ref RandState rand, in Speed speed) =>
            {
                Vector2 texelCoord = new Vector2(0.5f * (-translation.Value.x / bounds.x) + 0.5f, 0.5f * (-translation.Value.z / bounds.y) + 0.5f);
                localPheromones[(int)(texelCoord.y * TexSize) * TexSize + (int)(texelCoord.x * TexSize)] = 0.75f;
            })
            .ScheduleParallel();

        Entities
            .ForEach((Refs map) =>
            {
                for (int i = 0; i < TexSize; ++i)
                {
                    for (int j = 0; j < TexSize; ++j)
                    {
                        float preDecayValue = localPheromones[j * TexSize + i];
                        localPheromones[j * TexSize + i] = preDecayValue;
                        map.PheromoneMap.SetPixel(i, j, new Color(localPheromones[j * TexSize + i], 0, 0));
                    }
                }
                map.PheromoneMap.Apply();
            })
            .WithoutBurst()
            .Run();
        
    }

    protected override void OnDestroy()
    {
        //pheromones.Dispose();
    }
    
}