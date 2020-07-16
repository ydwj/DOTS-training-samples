﻿using System.Collections.Generic;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[RequireComponent(typeof(ColorAuthoring))]
public class TrackAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [System.Serializable]
    struct TrackPointDefinition
    {
        public Transform Transform;
        public bool IsPlatformStart;
    }

    struct PlatformDefinition
    {
        public float2 Times;
        // public float2 InboundTimes;
        public float3 Position;
        public quaternion Rotation;
    }

    struct StationDefinition
    {
        public PlatformDefinition Outbound;
        public PlatformDefinition Inbound;
    }
    
    [SerializeField, Range(0.5f, 10.0f)] float m_Radius = 3.0f;
    [SerializeField, Range(0.1f, 3.0f)] float m_PlatformOffset = 0.5f;
    [SerializeField, Range(1, 20)] int m_SplinePointsPerSegment = 5;
    [SerializeField] TrackPointDefinition[] m_Points;
    
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var pointsLength = m_Points.Length;
        var track = new NativeList<float3>(pointsLength, Allocator.TempJob);
        var stationDefs = new NativeList<StationDefinition>(pointsLength, Allocator.TempJob);
        var spline = new NativeList<float3>((pointsLength + 2) * m_SplinePointsPerSegment, Allocator.TempJob);
        var isPlatform = new NativeBitArray(pointsLength, Allocator.TempJob);

        try
        {
            for (var i = 0; i < pointsLength; i++)
            {
                var inputTrackPoint = m_Points[i];

                track.Add(inputTrackPoint.Transform.position);
                isPlatform.Set(i, inputTrackPoint.IsPlatformStart);
            }
        
            new GenerateSplineAndStationsJob
            {
                Spline = spline,
                Stations = stationDefs,
                TrackPoints = track,
                IsPlatform = isPlatform,
                Radius = m_Radius,
                PlatformOffset = m_PlatformOffset,
                SplinePointsPerSegment = m_SplinePointsPerSegment
            }.Run();
            
            var stationsLength = stationDefs.Length;
            var stations = new NativeArray<TrackStation>(stationsLength, Allocator.Temp);
            
            for (var i = 0; i < stationsLength; i++)
            {
                var stationDef = stationDefs[i];

                stations[i] = new TrackStation
                {
                    Outbound = new TrackPlatform
                    {
                        StartT = stationDef.Outbound.Times.x,
                        EndT = stationDef.Outbound.Times.y
                    },
                    Inbound = new TrackPlatform
                    {
                        StartT = stationDef.Inbound.Times.x,
                        EndT = stationDef.Inbound.Times.y,
                    }
                };
            }

            var stationsBuffer = dstManager.AddBuffer<TrackStation>(entity);
            stationsBuffer.AddRange(stations);
        
            var splinePointsBuffer = dstManager.AddBuffer<TrackPoint>(entity);
            splinePointsBuffer.AddRange(((NativeArray<float3>)spline).Reinterpret<TrackPoint>());
        }
        finally
        {
            track.Dispose();
            stationDefs.Dispose();
            spline.Dispose();
            isPlatform.Dispose();
        }
    }

    [BurstCompile]
    struct GenerateSplineAndStationsJob : IJob
    {
        public NativeList<float3> Spline;
        
        public NativeList<StationDefinition> Stations;
        [ReadOnly] public NativeArray<float3> TrackPoints;
        [ReadOnly] public NativeBitArray IsPlatform;
        [ReadOnly] public float Radius;
        [ReadOnly] public float PlatformOffset;
        [ReadOnly] public float SplinePointsPerSegment; // TODO: actual use this
        
        public void Execute()
        {
            // #region Generate Spline
            var trackPointsLength = TrackPoints.Length;

            switch (trackPointsLength)
            {
                case 0:
                    Spline.Add(float3.zero);
                    return;
                case 1:
                    Spline.Add(TrackPoints[0]);
                    return;
            }
            
            var outboundPlatforms = new NativeList<PlatformDefinition>(trackPointsLength, Allocator.Temp);
            var inBoundPlatforms = new NativeList<PlatformDefinition>(trackPointsLength, Allocator.Temp);

            var offsetDir = math.normalize(-(TrackPoints[1] - TrackPoints[0]));
            var point = math.mad(Radius, offsetDir, TrackPoints[0]);
            Spline.Add(point);

            var up = math.up();
            var last = trackPointsLength - 1;

            var step = 1.0f / math.mad(trackPointsLength, 2, 2);
            var t = step; 
            
            // outbound
            for (var i = 0; i < last; i++)
            {
                var current = TrackPoints[i];
                var next = TrackPoints[i + 1];
                var toNext = math.normalize(next - current);

                offsetDir = math.cross(up, toNext);
                point = math.mad(Radius, offsetDir, current);
                Spline.Add(point);

                if (IsPlatform.GetBits(i) == 0)
                    continue;

                outboundPlatforms.Add(new PlatformDefinition
                {
                    Times = new float2(0.5f * (i + 2) / (last + 2), 0.5f * (i + 2) / (last + 2) + step),
                    Position = math.mad(offsetDir, PlatformOffset, point),
                    Rotation = quaternion.LookRotation(toNext, up),
                });
            }

            offsetDir = math.normalize(TrackPoints[last] - TrackPoints[last - 1]);

            point = math.mad(Radius, math.cross(up, offsetDir), TrackPoints[last]);
            Spline.Add(point);
            
            point = math.mad(Radius, offsetDir, TrackPoints[last]);
            Spline.Add(point);

            t = math.mad(step, 2, t);
            
            // inbound
            for (var i = last; i >= 1; i--)
            {
                var current = TrackPoints[i];
                var next = TrackPoints[i - 1];
                var toNext = math.normalize(next - current);

                offsetDir = math.cross(up, toNext);
                point = math.mad(Radius, offsetDir, current);
                Spline.Add(point);
                
                if (IsPlatform.GetBits(i) == 0)
                    continue;

                inBoundPlatforms.Add(new PlatformDefinition
                {
                    Times = new float2(1.0f - (0.5f * (i + 2) / (last + 2)) + step, 1.0f - (0.5f * (i + 3) / (last + 2))),
                    Position = math.mad(offsetDir, PlatformOffset, point),
                    Rotation = quaternion.LookRotation(toNext, up),
                });
            }

            offsetDir = math.normalize(TrackPoints[0] - TrackPoints[1]);

            point = math.mad(Radius, math.cross(up, offsetDir), TrackPoints[0]);
            Spline.Add(point);
            
            Assert.IsTrue(outboundPlatforms.Length == inBoundPlatforms.Length);

            var platformsLength = outboundPlatforms.Length;
            for (var i = 0; i < platformsLength; i++)
            {
                var outboundIndex = i;
                var inBoundIndex = platformsLength - i - 1;
                Stations.Add(new StationDefinition
                {
                    Outbound = outboundPlatforms[outboundIndex],
                    Inbound = inBoundPlatforms[inBoundIndex],
                });
            }
        }
    }
}

[InternalBufferCapacity(8)]
public struct TrackStation : IBufferElementData
{
    public TrackPlatform Outbound;
    public TrackPlatform Inbound;
}

public struct TrackPlatform
{
    public float StartT;
    public float EndT;
}
