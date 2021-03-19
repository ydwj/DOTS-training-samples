﻿using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

struct BucketInfo {
    public float3 position;
    public Entity randomBee;
};

public class BeeBehavior: SystemBase
{
    private EntityQueryDesc allBeesQueryDesc; 
    private EntityQuery FoodQuery;
    protected override void OnCreate()
    {
        FoodQuery=GetEntityQuery(ComponentType.ReadOnly<Food>(),ComponentType.ReadOnly<Grounded>());
        allBeesQueryDesc = new EntityQueryDesc
        {
            All = new ComponentType[] {typeof(Bee)} //, ComponentType.ReadOnly<WorldRenderBounds>()
        };

    }
    
    /*protected override void OnUpdate()
    {
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        var foodEntities = FoodQuery.ToEntityArray(Allocator.Temp);
        var random = new Unity.Mathematics.Random(1 + (uint)(Time.ElapsedTime*10000));                

        EntityQuery allBeesQuery = GetEntityQuery(allBeesQueryDesc);
        NativeArray<Entity> allBees = allBeesQuery.ToEntityArray(Allocator.Temp);

        Entities
            .WithNone<GoingForFood,Attacking,BringingFoodBack>()
            .ForEach((Entity entity, in Bee bee, in Translation translation, in Team team) =>
            {
                int r = random.NextInt() % 100;
                if(r < 98)                
                {
                    int foodCount = foodEntities.Length;
                    if (foodCount == 0)
                        return;
                    commandBuffer.AddComponent<GoingForFood>(entity);
                    commandBuffer.AddComponent<FoodTarget>(entity, new FoodTarget(){Value = foodEntities[random.NextInt(foodCount)] } );
                } else {
                    var enemyTeam = !team.index;
                    // go for attack
                    // find nearest enemy bee
                    float minDistance = 0xFFFFFF;
                    Entity enemyBee = Entity.Null;
                    foreach(Entity anotherBee in allBees) {
                        //if( !HasComponent<Team>(anotherBee) ) continue;
                        if( GetComponent<Team>(anotherBee).index != enemyTeam) continue;
                        float distance = math.distance(GetComponent<Translation>(anotherBee).Value,translation.Value);
                        if(distance<minDistance) {
                            minDistance = distance;
                            enemyBee = anotherBee;
                        }
                    }
                    // add Attacking component 
                    if(enemyBee != Entity.Null) {
                        commandBuffer.AddComponent(entity,new Attacking() { TargetBee = enemyBee });
                    }                    
                }
            }).Run();
        
        
        commandBuffer.Playback(EntityManager);
        commandBuffer.Dispose();
    }*/

    protected override void OnUpdate()
    {
        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        var foodEntities = FoodQuery.ToEntityArray(Allocator.Temp);
        var random = new Unity.Mathematics.Random(1 + (uint)(Time.ElapsedTime*10000));                

        EntityQuery allBeesQuery = GetEntityQuery(allBeesQueryDesc);

        const float partitionSize = 10.0f;
        int totalBeesCount = allBeesQuery.CalculateEntityCount();
        NativeHashMap<int,BucketInfo> partitionsA = new NativeHashMap<int, BucketInfo>( (int)math.sqrt(totalBeesCount), Allocator.TempJob);
        NativeHashMap<int,BucketInfo> partitionsB = new NativeHashMap<int, BucketInfo>( (int)math.sqrt(totalBeesCount), Allocator.TempJob);
        Entities
            .WithNone<Attacking>()
            .WithAll<Bee>()
            .ForEach((Entity bee, in Translation translation, in Team team) =>
            {
                var partIndex = translation.Value / partitionSize;
                int index =  (int)partIndex.x + ((int)partIndex.y)*1024 + ((int)partIndex.z)*1024*1024;

                var bucket = new BucketInfo();

                if( team.index == false ) {
                    if(partitionsA.TryGetValue(index, out bucket)) {
                        if(random.NextInt(100)<10) {
                            bucket.randomBee = bee;
                            partitionsA[index] = bucket;
                        }
                    } else {
                        partitionsA[index] = new BucketInfo() {randomBee=bee, position=new float3(
                            partitionSize/2 + ((int)partIndex.x) * partitionSize, 
                            partitionSize/2 + ((int)partIndex.y) * partitionSize, 
                            partitionSize/2 + ((int)partIndex.z) * partitionSize
                            )};
                    }
                } else {
                    if(partitionsB.TryGetValue(index, out bucket)) {
                        if(random.NextInt(100)<10) {
                            bucket.randomBee = bee;
                            partitionsB[index] = bucket;
                        }
                    } else {
                        partitionsB[index] = new BucketInfo() {randomBee=bee, position=new float3(
                            partitionSize/2 + ((int)partIndex.x) * partitionSize, 
                            partitionSize/2 + ((int)partIndex.y) * partitionSize, 
                            partitionSize/2 + ((int)partIndex.z) * partitionSize
                            )};
                    }
                }
            }).Run();        

        Entities
            .WithNone<GoingForFood,Attacking,BringingFoodBack>()
            .WithDisposeOnCompletion(partitionsA)
            .WithDisposeOnCompletion(partitionsB)
            .ForEach((Entity entity, in Translation translation, in Team team) =>
            {
                int r = random.NextInt() % 100;
                if(r < 50) {}
                else if(r < 98)                
                {
                    int foodCount = foodEntities.Length;
                    if (foodCount == 0)
                        return;
                    commandBuffer.AddComponent<GoingForFood>(entity);
                    commandBuffer.AddComponent<FoodTarget>(entity, new FoodTarget(){Value = foodEntities[random.NextInt(foodCount)] } );
                } else {
                    var bucketEnum = (team.index==false) ? partitionsB.GetEnumerator() : partitionsA.GetEnumerator();

                    // go for attack
                    // find nearest enemy bee
                    float minDistance = 0xFFFFFF;
                    Entity enemyBee = Entity.Null;
                    while(bucketEnum.MoveNext()) {
                        float distance = math.distance(bucketEnum.Current.Value.position,translation.Value);
                        if(distance<minDistance) {
                            minDistance = distance;
                            enemyBee = bucketEnum.Current.Value.randomBee;
                        }
                    }
                    // add Attacking component 
                    if(enemyBee != Entity.Null) {
                        commandBuffer.AddComponent(entity,new Attacking() { TargetBee = enemyBee });
                    }                    
                }
            }).Run();
        
        
        commandBuffer.Playback(EntityManager);
        commandBuffer.Dispose();
    }
}