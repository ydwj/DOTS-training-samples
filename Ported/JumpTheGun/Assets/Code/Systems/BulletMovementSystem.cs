using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BulletSpawnerSystem))]
public class BulletMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float currentTime = (float)Time.ElapsedTime;

        Entities
            .WithAll<Bullet, Arc, Time>()
            .WithAll<Translation, BallTrajectory, TimeOffset>().ForEach(
            (ref Translation translation, in Time t, in BallTrajectory trajectory, in Arc arc, in TimeOffset timeOffset) =>
            {
                var timeInParabola = math.clamp((currentTime - t.StartTime - timeOffset.Value) / (t.EndTime - t.StartTime), 0.0f, 1.0f);
                float yInParabola = ParabolaUtil.Solve(arc.Value.x, arc.Value.y, arc.Value.z, timeInParabola);
                float3 position = math.lerp(trajectory.Source, trajectory.Destination, timeInParabola);
                position.y = yInParabola;
                
                translation.Value = position;
            }).ScheduleParallel();
    }
}