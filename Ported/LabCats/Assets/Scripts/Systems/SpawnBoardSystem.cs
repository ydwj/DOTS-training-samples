using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateBefore(typeof(EndInitializationEntityCommandBufferSystem))]
[UpdateAfter(typeof(CopyInitialTransformFromGameObjectSystem))] //maybe not necessary but I’m afraid it might create issues
public class SpawnBoardSystem : SystemBase
{
    static Dir GetRandomDirection(Unity.Mathematics.Random random)
    {
        int randomDirection = random.NextInt(0, 4);
        if (randomDirection == 0)
            return Dir.Up;
        if (randomDirection == 1)
            return Dir.Right;
        if (randomDirection == 2)
            return Dir.Down;
        return Dir.Left;
    }

    static Dir GetOppositeDirection(Dir direction)
    {
        if (direction == Dir.Up)
            return Dir.Down;
        if (direction == Dir.Down)
            return Dir.Up;
        if (direction == Dir.Left)
            return Dir.Right;
        return Dir.Left;
    }

    static int HasWallBoundariesInDirection(WallBoundaries walls, Dir dir)
    {
        switch (dir)
        {
            case Dir.Up:
                return (int)(walls & WallBoundaries.WallUp);
            case Dir.Down:
                return (int)(walls & WallBoundaries.WallDown);
            case Dir.Left:
                return (int)(walls & WallBoundaries.WallLeft);
            case Dir.Right:
                return (int)(walls & WallBoundaries.WallRight);
        }

        return 0;
    }

    static float halfWallThickness = 0.025f;
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        Entities
            .WithNone<BoardInitializedTag>()
            .WithoutBurst()
            .ForEach((Entity entity, ref DynamicBuffer<GridCellContent> gridContent, in BoardDefinition boardDefinition, in BoardPrefab boardPrefab,
                in DynamicSpawnerDefinition dynamicSpawnerDefinition, in GameInitParams gameInitParams) =>
            {
                var random = new Unity.Mathematics.Random(gameInitParams.BoardGenerationSeed);
                int numberColumns = boardDefinition.NumberColumns;
                int numberRows = boardDefinition.NumberRows;
                // Store the grid world position
                var firstCellPosition = new FirstCellPosition
                {
                    // TODO: Also get the following from authoring:
                    Value = new float3(0, 0, 0)
                };
                ecb.AddComponent(entity, firstCellPosition);

                ecb.AddComponent(entity, new GameTime(){AccumulatedTime = -5.0f});

                // Create the player entities
                var playerReferenceBuffer = ecb.AddBuffer<PlayerReference>(entity);
                playerReferenceBuffer.Capacity = 4;
                for (int i = 0; i < 4; ++i)
                {
                    // var spawnedEntity = ecb.Instantiate(boardPrefab.CursorPrefab);
                    var spawnedEntity = ecb.CreateEntity();
                    ecb.SetName(spawnedEntity, "Player " + i);
                    ecb.AddComponent<Translation>(spawnedEntity);
                    ecb.AddComponent(spawnedEntity, new PlayerIndex() { Value = i });
                    ecb.AddComponent<Score>(spawnedEntity);
                    ecb.AddBuffer<ArrowReference>(spawnedEntity);
                    ecb.AppendToBuffer(entity, new PlayerReference() { Player = spawnedEntity });
                    if (i != 0)
                        ecb.AddComponent<AITargetCell>(spawnedEntity);

                    ecb.SetComponent(spawnedEntity, new Translation
                    {
                        Value = new float3(0.0f, 1.0f, 0.0f)
                    });
                    ecb.AddComponent(spawnedEntity, new PlayerIndex(){Value = i});
                    ecb.AddComponent<URPMaterialPropertyBaseColor>(spawnedEntity);
                    ecb.AddComponent<ShouldSetupColor>(spawnedEntity);
                    ecb.AddComponent<NextArrowIndex>(spawnedEntity);
                    ecb.AddComponent<RandomContainer>(spawnedEntity, new RandomContainer(){Value = new Random(gameInitParams.AIControllerSeed + (uint)i)});
                    if (i != 0)
                    {
                        ecb.AddComponent(spawnedEntity, new AITargetCell()
                        {
                            X = random.NextInt(0, numberColumns),
                            Y = random.NextInt(0, numberRows),
                        });
                    }

                    //Setup arrows for each character
                    for (int l = 0; l < 3; l++)
                    {
                        Entity arrowPrefab = boardPrefab.ArrowPrefab;
                        var posX = 0;
                        var posY = 0;
                        var arrow = ecb.Instantiate(arrowPrefab);
                        //
                        ecb.SetComponent(arrow, new Translation
                        {
                            Value = new float3(posY*boardDefinition.CellSize, 0.501f, posX*boardDefinition.CellSize)
                        });
                        ecb.AddComponent(arrow, new GridPosition(){X=posX,Y=posY});
                        ecb.AddComponent(arrow, new PlayerIndex(){Value = i});
                        ecb.AddComponent(arrow, new Direction(){Value = Dir.Right});
                        ecb.AddComponent<URPMaterialPropertyBaseColor>(arrow);
                        ecb.AddComponent<PropagateColor>(arrow);
                        ecb.AddComponent<ShouldSetupColor>(arrow);
                        ecb.AddComponent<MovableTag>(arrow);
                        ecb.AppendToBuffer(spawnedEntity, new ArrowReference(){Value = arrow});
                    }
                }

                int player1GoalIndex = GridCellContent.Get1DIndexFromGridPosition(boardDefinition.GoalPlayer1, numberColumns);
                int player2GoalIndex = GridCellContent.Get1DIndexFromGridPosition(boardDefinition.GoalPlayer2, numberColumns);
                int player3GoalIndex = GridCellContent.Get1DIndexFromGridPosition(boardDefinition.GoalPlayer3, numberColumns);
                int player4GoalIndex = GridCellContent.Get1DIndexFromGridPosition(boardDefinition.GoalPlayer4, numberColumns);

                int numberCells = numberColumns * numberRows;
                for (int boardIndex = 0; boardIndex < numberCells; ++boardIndex)
                {
                    WallBoundaries borderWall = WallBoundaries.NoWall;
                    int j = GridCellContent.GetColumnIndexFrom1DIndex(boardIndex, numberColumns);
                    int i = GridCellContent.GetRowIndexFrom1DIndex(boardIndex, numberColumns);

                    if(i == 0)
                        borderWall |= WallBoundaries.WallUp;
                    if (i == numberRows - 1)
                        borderWall |= WallBoundaries.WallDown;
                    if (j == 0)
                        borderWall |= WallBoundaries.WallLeft;
                    if (j == numberColumns - 1)
                        borderWall |= WallBoundaries.WallRight;
                    var gridCellType = GridCellType.None;
                    if (boardIndex == player1GoalIndex || boardIndex == player2GoalIndex || boardIndex == player3GoalIndex || boardIndex == player4GoalIndex)
                        gridCellType = GridCellType.Goal;
                    gridContent.Add(new GridCellContent() { Type = gridCellType, Walls = borderWall});
                }

                int numWalls = (int)(numberCells * gameInitParams.WallDensity);
                for (int c = 0; c < numWalls; ++c)
                {
                    int wallCellIndex = random.NextInt(0, numberCells);
                    var randomDirection = GetRandomDirection(random);

                    int neighbourCellIndex = GridCellContent.GetNeighbour1DIndexWithDirection(wallCellIndex,randomDirection,numberRows, numberColumns);
                    int wallBoundaryInCell = HasWallBoundariesInDirection(gridContent[wallCellIndex].Walls, randomDirection);
                    int neighbourBoundary = 0;
                    if (neighbourCellIndex != -1)
                        neighbourBoundary = HasWallBoundariesInDirection(gridContent[neighbourCellIndex].Walls, GetOppositeDirection(randomDirection));
                    if (wallBoundaryInCell + neighbourBoundary > 0)
                        --c;
                    else
                    {
                        var cellContent = gridContent[wallCellIndex];
                        cellContent.Walls |= GridCellContent.GetWallBoundariesFromDirection(randomDirection);
                        gridContent[wallCellIndex] = cellContent;
                        if (neighbourCellIndex != -1)
                        {
                            var neighbourCellContent = gridContent[neighbourCellIndex];
                            neighbourCellContent.Walls |= GridCellContent.GetWallBoundariesFromDirection(GetOppositeDirection(randomDirection));
                            gridContent[neighbourCellIndex] = neighbourCellContent;
                        }
                    }
                }

                int numHoles = random.NextInt(0, numberCells/gameInitParams.MaximumNumberCellsPerHole);
                for (int hole = 0; hole < numHoles; ++hole)
                {
                    int holeIndex = random.NextInt(0, numberCells);
                    var gridContentCell = gridContent[holeIndex];
                    if (gridContentCell.Type != GridCellType.None)
                    {
                        --hole;
                    }
                    else
                    {
                        gridContentCell.Type = GridCellType.Hole;
                        gridContent[holeIndex] = gridContentCell;
                    }
                }

                //create the board cell entities
                for (int boardIndex = 0; boardIndex < numberCells; ++boardIndex)
                {
                    int j = GridCellContent.GetColumnIndexFrom1DIndex(boardIndex, numberColumns);
                    int i = GridCellContent.GetRowIndexFrom1DIndex(boardIndex, numberColumns);
                    Entity cellPrefab = (j % 2 == i % 2 ? boardPrefab.DarkCellPrefab : boardPrefab.LightCellPrefab);
                    if (gridContent[boardIndex].Type != GridCellType.Hole)
                    {
                        var cell = ecb.Instantiate(cellPrefab);

                        ecb.SetComponent(cell, new Translation
                        {
                            Value = new float3(i * boardDefinition.CellSize, 0, j * boardDefinition.CellSize)
                        });
                        ecb.AddComponent(cell, new GridPosition() { X = j, Y = i });
                    }

                    var wallBoundaries = gridContent[boardIndex].Walls;
                    if ((wallBoundaries & WallBoundaries.WallUp) != 0)
                    {
                        var wallEntity = ecb.Instantiate(boardPrefab.WallPrefab);
                        ecb.SetComponent(wallEntity, new Translation{Value = new float3(i*boardDefinition.CellSize - 0.5f - halfWallThickness, 0.5f, j*boardDefinition.CellSize)});
                    }

                    if (i == numberRows - 1 && (wallBoundaries & WallBoundaries.WallDown) != 0)
                    {
                        var wallEntity = ecb.Instantiate(boardPrefab.WallPrefab);
                        ecb.SetComponent(wallEntity, new Translation{Value = new float3(i*boardDefinition.CellSize + 0.5f + halfWallThickness, 0.5f, j*boardDefinition.CellSize)});
                    }

                    if ((wallBoundaries & WallBoundaries.WallLeft) != 0)
                    {
                        var wallEntity = ecb.Instantiate(boardPrefab.WallPrefab);
                        ecb.SetComponent(wallEntity, new Translation{Value = new float3(i*boardDefinition.CellSize, 0.5f, j*boardDefinition.CellSize - 0.5f - halfWallThickness)});
                        ecb.SetComponent(wallEntity, new Rotation{Value = quaternion.LookRotation(new float3(1.0f, 0f, 0f), new float3(0f, 1f, 0f))});
                    }

                    if (j == numberColumns - 1 && (wallBoundaries & WallBoundaries.WallRight) != 0)
                    {
                        var wallEntity = ecb.Instantiate(boardPrefab.WallPrefab);
                        ecb.SetComponent(wallEntity, new Translation{Value = new float3(i*boardDefinition.CellSize, 0.5f, j*boardDefinition.CellSize + 0.5f + halfWallThickness)});
                        ecb.SetComponent(wallEntity, new Rotation{Value = quaternion.LookRotation(new float3(1.0f, 0f, 0f), new float3(0f, 1f, 0f))});
                    }

                }


                var goalReferenceBuffer = ecb.AddBuffer<GoalReference>(entity);
                goalReferenceBuffer.Capacity = 4;
                for (int k = 0; k < 4; k++)
                {
                    Entity goalPrefab = boardPrefab.GoalPrefab;
                    var goalEntity = ecb.Instantiate(goalPrefab);

                    int posX = 0;
                    int posY = 0;
                    if (k == 0)
                    {
                        posX = boardDefinition.GoalPlayer1.X;
                        posY = boardDefinition.GoalPlayer1.Y;
                    }
                    else if (k == 1)
                    {
                        posX = boardDefinition.GoalPlayer2.X;
                        posY = boardDefinition.GoalPlayer2.Y;
                    }
                    else if (k == 2)
                    {
                        posX = boardDefinition.GoalPlayer3.X;
                        posY = boardDefinition.GoalPlayer3.Y;
                    }
                    else
                    {
                        posX = boardDefinition.GoalPlayer4.X;
                        posY = boardDefinition.GoalPlayer4.Y;
                    }

                    ecb.AddComponent<GoalTag>(goalEntity);
                    ecb.SetComponent(goalEntity, new Translation
                    {
                        Value = new float3(posY*boardDefinition.CellSize, 0.5f, posX*boardDefinition.CellSize)
                    });
                    ecb.AddComponent(goalEntity, new GridPosition(){X=posX,Y=posY});
                    ecb.AddComponent(goalEntity, new PlayerIndex(){Value = k});
                    ecb.AddComponent<URPMaterialPropertyBaseColor>(goalEntity);
                    ecb.AddComponent<PropagateColor>(goalEntity);
                    ecb.AddComponent<ShouldSetupColor>(goalEntity);

                    ecb.AppendToBuffer(entity, new GoalReference() { Goal = goalEntity });
                }

                // Set up spawners
                var spawner1 = ecb.CreateEntity();
                ecb.AddComponent(spawner1, new SpawnerData()
                {
                    Timer = 0f,
                    Frequency = dynamicSpawnerDefinition.MouseFrequency,
                    Direction = Dir.Up,
                    Type = SpawnerType.MouseSpawner,
                    X = 0,
                    Y = 0
                });
                ecb.SetName(spawner1, "Mouse Spawner 1");

                var spawner2 = ecb.CreateEntity();
                ecb.AddComponent(spawner2, new SpawnerData()
                {
                    Timer = 0f,
                    Frequency = dynamicSpawnerDefinition.MouseFrequency,
                    Direction = Dir.Down,
                    Type = SpawnerType.MouseSpawner,
                    X = numberColumns - 1,
                    Y = numberRows - 1
                });
                ecb.SetName(spawner2, "Mouse Spawner 2");

                var spawner3 = ecb.CreateEntity();
                ecb.AddComponent(spawner3, new SpawnerData()
                {
                    Timer = 0f,
                    Frequency = dynamicSpawnerDefinition.CatFrequency,
                    Direction = Dir.Left,
                    Type = SpawnerType.CatSpawner,
                    X = numberColumns - 1,
                    Y = 0
                });
                ecb.SetName(spawner3, "CatSpawner 1");

                var spawner4 = ecb.CreateEntity();
                ecb.AddComponent(spawner4, new SpawnerData()
                {
                    Timer = 0f,
                    Frequency = dynamicSpawnerDefinition.CatFrequency,
                    Direction = Dir.Right,
                    Type = SpawnerType.CatSpawner,
                    X = 0,
                    Y = numberRows - 1
                });
                ecb.SetName(spawner4, "CatSpawner 2");

                // Setup camera
                var gameObjectRefs = this.GetSingleton<GameObjectRefs>();
                var camera = gameObjectRefs.Camera;
                camera.orthographic = true;
                var overheadFactor = 1.5f;

                var maxSize = Mathf.Max(numberRows, numberColumns);
                var maxCellSize = boardDefinition.CellSize;
                camera.orthographicSize = maxSize * maxCellSize * .65f;

                // scale based on board dimensions - james
                var posXZ = Vector2.Scale(new Vector2(numberRows, numberColumns) * 0.5f, new Vector2(boardDefinition.CellSize, boardDefinition.CellSize));

                // hold position value adjusted by dimensions of board
                float3 camPosition = new Vector3(0, maxSize * maxCellSize * overheadFactor, 0);
                camera.transform.position = camPosition;

                // set camera to look at board center
                camera.transform.LookAt(new Vector3(posXZ.x, 0f, posXZ.y));

                // Setup the raycast quad
                var boardQuad = gameObjectRefs.BoardQuad;
                var xSize = boardDefinition.CellSize * ((float)boardDefinition.NumberRows);
                var ySize = boardDefinition.CellSize * ((float)boardDefinition.NumberColumns);
                boardQuad.transform.localScale = new Vector3(
                    xSize,
                    ySize,
                    1.0f
                );

                boardQuad.transform.position = new Vector3(
                    (xSize / 2.0f) - .5f,
                    boardQuad.transform.position.y,
                    (ySize / 2.0f) - .5f
                );

                // Only run on first frame the BoardInitializedTag is not found. Add it so we don't run again
                ecb.AddComponent(entity, new BoardInitializedTag());
                ecb.SetName(entity, "Board");
            }).Run();
        ecb.Playback(EntityManager);
        ecb.Dispose();

        var ecb2 = new EntityCommandBuffer(Allocator.Temp);

        Entities.WithAll<GridPosition, PlayerIndex, URPMaterialPropertyBaseColor>().
                 WithAll<ShouldSetupColor>()
                .ForEach((Entity e, DynamicBuffer<LinkedEntityGroup> linkedEntities) =>
        {
            foreach (var linkedEntity in linkedEntities)
            {
                ecb2.AddComponent<URPMaterialPropertyBaseColor>(linkedEntity.Value);
            }
        }).Run();

        ecb2.Playback(EntityManager);
        ecb2.Dispose();


    }
}