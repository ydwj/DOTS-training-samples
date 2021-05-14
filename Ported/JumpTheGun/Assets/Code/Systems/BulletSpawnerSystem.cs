using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityInput = UnityEngine.Input;


[UpdateBefore(typeof(TransformSystemGroup))]
public class BulletSpawnerSystem : SystemBase
{
    private EntityCommandBufferSystem m_ECBSystem;

    protected override void OnCreate()
    {
        var query = GetEntityQuery(typeof(Tank), typeof(BoardPosition));
        RequireForUpdate(query);

        GetEntityQuery(typeof(OffsetList));

        m_ECBSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        if (!TryGetSingletonEntity<Player>(out Entity playerEntity))
            return;

        if (!TryGetSingletonEntity<BoardSize>(out Entity boardEntity))
            return;

        var times = new double2(Time.ElapsedTime - Time.DeltaTime, Time.ElapsedTime);
        var reloadTime = GetSingleton<ReloadTime>().Value;
        var bulletPrefab = GetSingleton<BoardSpawnerData>().BulletPrefab;

        var boardSize = GetComponent<BoardSize>(boardEntity);
        var arcHeightFactor = GetComponent<BulletArcHeightFactor>(boardEntity);
        DynamicBuffer<OffsetList> offsets = GetBuffer<OffsetList>(boardEntity);

        float bulletRadius = GetComponent<Radius>(boardEntity).Value;
        float3 size = new float3(bulletRadius * 2F, bulletRadius * 2F, bulletRadius * 2F);

        var playerPosition = GetComponent<Translation>(playerEntity);

        float kDuration = GetComponent<BulletSpeed>(boardEntity).Value;
        
        var ecb = m_ECBSystem.CreateCommandBuffer().AsParallelWriter();
 
        Entities
            .WithAll<Tank, BoardPosition>()
            .WithReadOnly(offsets)
            .ForEach((
                int entityInQueryIndex,
                ref Translation translation,
                in TimeOffset timeOffset) =>
                {
                    var loopCounts = times - timeOffset.Value;
                    loopCounts = math.floor(loopCounts / reloadTime);

                    if (loopCounts.y > loopCounts.x)
                    {
                        var bulletEntity = ecb.Instantiate(entityInQueryIndex, bulletPrefab);

                        ecb.SetComponent(entityInQueryIndex, bulletEntity, new Translation {Value = translation.Value});
                        ecb.SetComponent(entityInQueryIndex, bulletEntity, new BoardTarget
                        {
                            Value = CoordUtils.WorldToBoardPosition(playerPosition.Value)
                        });

                        float distX = translation.Value.x - playerPosition.Value.x;
                        float distZ = translation.Value.z - playerPosition.Value.z;

                        float distance = math.sqrt((distX * distX) + (distZ * distZ));
                        
                        ecb.SetComponent(entityInQueryIndex, bulletEntity, new Time {StartTime = (float)times.y, EndTime = (float)times.y + distance / kDuration });
                        
                        var landingPos = new float3(0,0,0);
                        var bulletArc = new Arc();

                        TraceUtils.TraceArc(translation.Value, playerPosition.Value, boardSize, offsets, arcHeightFactor.Value, out landingPos, out bulletArc);
                        ecb.SetComponent(entityInQueryIndex, bulletEntity, new BallTrajectory
                            {
                                Source = translation.Value,
                                Destination = landingPos
                            }
                        );

                        ecb.SetComponent(entityInQueryIndex, bulletEntity, new NonUniformScale { Value = size });
                        ecb.SetComponent(entityInQueryIndex, bulletEntity, bulletArc);
                    }
                }).ScheduleParallel();
        
        m_ECBSystem.AddJobHandleForProducer(Dependency);
    }
}