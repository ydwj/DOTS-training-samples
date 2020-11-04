using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class MoveCarOnLaneSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        float elapsedTime = Time.DeltaTime;

        ComponentDataFromEntity<CarPosition> carPositionGetter = GetComponentDataFromEntity<CarPosition>(false);
        ComponentDataFromEntity<CarSpeed> carSpeedGetter = GetComponentDataFromEntity<CarSpeed>(false);
        
        Entities
            .ForEach((Entity entity, ref Lane lane, ref DynamicBuffer<MyBufferElement> buffer) =>
            {
                DynamicBuffer<Entity> carEntities = buffer.Reinterpret<Entity>();
                // A lane doesn't actually care about the first car, that's being handled by the Intersection
                if (carEntities.Length <= 1)
                    return;

                float previousCarPosition = 0.0f;
                
                for(int i = 1; i < carEntities.Length; i++)
                {
	                Entity car = carEntities[i];

	                CarPosition carPosition = carPositionGetter[car];
	                CarSpeed carSpeed = carSpeedGetter[car];

	                float newPosition = carPosition.Value + carSpeed.NormalizedValue * CarSpeed.MAX_SPEED * elapsedTime;
	                if(newPosition > lane.Length)
		                newPosition = lane.Length;
	                
	                float approachMaxSpeed = 1f;
	                
	                float maxCarPosition = previousCarPosition - CarSpeed.CAR_SPACING;
	                if (newPosition > maxCarPosition) {
		                // Break for the car
		                newPosition = maxCarPosition;
		                carSpeed.NormalizedValue = 0f;
		                approachMaxSpeed = 0;
	                } else {
		                // Slow down when approaching another car
		                approachMaxSpeed = (maxCarPosition - newPosition)*5f;
	                }

	                if (carSpeed.NormalizedValue > approachMaxSpeed) {
		                carSpeed.NormalizedValue = approachMaxSpeed;
	                }

                    ecb.SetComponent(car, new CarPosition{Value = newPosition});
                    ecb.SetComponent(car, carSpeed);

                    previousCarPosition = newPosition;
                }
            }).Run();

        ecb.Playback(EntityManager);
    }
}