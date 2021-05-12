
using System.Collections.Generic;

using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityGameObject = UnityEngine.GameObject;

public class PlayerAuthoring : MonoBehaviour
    , IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<Arc>(entity);
        dstManager.AddComponent<Direction>(entity);
        dstManager.AddComponent<TargetPosition>(entity);
        dstManager.AddComponent<PlayerSpawnerTag>(entity);
        dstManager.AddComponentData(entity, new Player(){});
    }
}