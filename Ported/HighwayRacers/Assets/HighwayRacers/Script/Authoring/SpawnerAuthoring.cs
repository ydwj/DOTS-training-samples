﻿using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public enum DriverProfile
{
    European = 0,
    American = 1,
    Random
}

public class SpawnerAuthoring : MonoBehaviour
    , IConvertGameObjectToEntity
    , IDeclareReferencedPrefabs
{
    public int CarCount;
    public GameObject CarPrefab;
    public GameObject TilePrefab;
    [Tooltip("European cars are blue. American cars are green")]
    public DriverProfile DriverProfile = DriverProfile.Random;

    // This function is required by IDeclareReferencedPrefabs
    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        // Conversion only converts the GameObjects in the scene.
        // This function allows us to inject extra GameObjects,
        // in this case prefabs that live in the assets folder.
        referencedPrefabs.Add(CarPrefab);
        referencedPrefabs.Add(TilePrefab);
    }

    // This function is required by IConvertGameObjectToEntity
    public void Convert(Entity entity, EntityManager dstManager
        , GameObjectConversionSystem conversionSystem)
    {
        // GetPrimaryEntity fetches the entity that resulted from the conversion of
        // the given GameObject, but of course this GameObject needs to be part of
        // the conversion, that's why DeclareReferencedPrefabs is important here.
        dstManager.AddComponentData(entity, new Spawner
        {
            CarCount = CarCount,
            CarPrefab = conversionSystem.GetPrimaryEntity(CarPrefab),
            TilePrefab = conversionSystem.GetPrimaryEntity(TilePrefab),
            DriverProfile = DriverProfile
        });
    }
}