using System;
using System.Collections.Generic;
using UnityEngine;
using static MainScript;

[Serializable]
public struct SpriteSheet
{
#if UNITY_EDITOR
    [TextArea(0, 3)]
    public string name;
#endif

    public List<Sprite> spriteSheet;
}

[Serializable]
public struct AnimationClip
{
#if UNITY_EDITOR
    [TextArea(0,3)]
    public string name;
#endif

    public int startIndex;
    public int endIndex;
    public bool isRepeat;

    public readonly int GetDuration => endIndex - startIndex + 1;
    public readonly bool IsValid => startIndex <= endIndex;
}

[Serializable]
public struct AnimationComponent
{
    public SpriteSheetIndex spriteSheetIndex;
    public AnimationClipIndex animationClipIndex;
    public float currentIndex;
    public SpriteRenderer spriteRenderer;
}

[Serializable]
public struct MoveComponent
{
    public Rigidbody2D rigidbody;
    public Vector2 target;
    public float speed;
}

[Serializable]
public struct UnitEntity
{
    public Transform transform;
    public AnimationComponent animation;
    public MoveComponent move;
}

[Serializable]
public class HashSetID : SerializableCollections.HashSet<ID> { }

public class MainScript : MonoBehaviour
{
    public const float ANIMATION_FRAMERATE = 10f;
    public const int EXPECTED_SELECT_COUNT = 8;

    public enum SpriteSheetIndex { WarriorBlue};
    [Header("Assets")]
    public List<SpriteSheet> spriteSheets;

    public enum AnimationClipIndex { WarriorStand, WarriorWalk};
    public List<AnimationClip> animationClips;

    [Header("References")]
    public Camera cameraMain;
    public GameObject selectIconPrefab;
    public SpriteRenderer boxSelector;

#if UNITY_EDITOR
    public List<GameObject> unitsDefaultDebug;
#endif

    [Header("Pathfinding")]
    public OverlapGrid overlapGrid;

    [Header("Selection")]
    public HashSetID currentSelectIDs;
    public List<GameObject> selectIcons;

    [Header("Box Select")]
    public LayerMask boxSelectLayerMask;
    public Vector2 boxSelectPositionStart;
    public bool isBoxSelect;
    public Collider2D[] boxSelectQuery;
    public HashSetID boxSelectIDs;

    [Header("Entities")]
    public VersionedPool<UnitEntity> units;

#if UNITY_EDITOR
    void DefaultEnumNamesIterate<TEnum, TStruct>(ref List<TStruct> outList, Func<TStruct, string, TStruct> setName) where TStruct : struct
    {
        string[] names = Enum.GetNames(typeof(TEnum));

        if (outList == null)
        {
            outList = new List<TStruct>(names.Length);
        }

        while (outList.Count < names.Length)
        {
            outList.Add(new TStruct());
        }

        for (int i = 0; i < outList.Count; i++)
        {
            if (i < names.Length)
            {
                outList[i] = setName(outList[i], names[i]);
            }
            else
            {
                outList[i] = setName(outList[i], "");
            }
        }
    }

    [ContextMenu("_DefaultEnumNames")]
    void DefaultEnumNames()
    {
        DefaultEnumNamesIterate<SpriteSheetIndex, SpriteSheet>(ref spriteSheets, (inS, inName) => { inS.name = inName; return inS; });
        DefaultEnumNamesIterate<AnimationClipIndex, AnimationClip>(ref animationClips, (inS, inName) => { inS.name = inName; return inS; });
    }

    [ContextMenu("_Prepopulate Pools")]
    void PrepopulatePools()
    {
        Pool.Prepopulate(selectIcons, selectIconPrefab, EXPECTED_SELECT_COUNT);
    }
#endif

    void Start()
    {
        overlapGrid.Snapshot();

        QualitySettings.vSyncCount = 1;
        boxSelectQuery = new Collider2D[EXPECTED_SELECT_COUNT];

        //TODO THINK
        pathfinding = new List<Vector2Int>();
        pathfindingVisitedFrom = new Dictionary<Vector2Int, Vector2Int>();
        pathfindingScores = new List<GreedyPathfind.Score>();

#if UNITY_EDITOR
        //Application.targetFrameRate = 30;

        units.SpawnRange(unitsDefaultDebug,
            (unitGameObject, id) =>
            {
                unitGameObject.GetComponent<IDComponent>().id = id;

                return new UnitEntity
                {
                    transform = unitGameObject.transform,
                    animation = new AnimationComponent
                    {
                        spriteSheetIndex = SpriteSheetIndex.WarriorBlue,
                        animationClipIndex = AnimationClipIndex.WarriorStand,
                        currentIndex = 0f,
                        spriteRenderer = unitGameObject.GetComponent<SpriteRenderer>(),
                    },
                    move = new MoveComponent
                    {
                        rigidbody = unitGameObject.GetComponent<Rigidbody2D>(),
                        target = unitGameObject.transform.position,
                        speed = 10f,
                    },
                };
            });
#endif
    }

    //static readonly ProfilerMarker s_PreparePerfMarker = new ProfilerMarker("MainScript");

    void Update()
    {
        // Animation update
        {
            Span<UnitEntity> unitsSpan = units.elements.AsSpan();
            foreach (ref UnitEntity unit in unitsSpan)
            {
                ref AnimationComponent animationComponent = ref unit.animation;

                List<Sprite> spriteSheet = spriteSheets[(int)animationComponent.spriteSheetIndex].spriteSheet;
                AnimationClip clip = animationClips[(int)animationComponent.animationClipIndex];

                if (!clip.IsValid)
                    continue;

                float clipDuration = clip.GetDuration;

                animationComponent.currentIndex += Time.deltaTime * ANIMATION_FRAMERATE;
                if (clip.isRepeat)
                {
                    while (clipDuration < animationComponent.currentIndex)
                    {
                        animationComponent.currentIndex -= clipDuration;
                    }
                }
                else
                {
                    animationComponent.currentIndex = Mathf.Clamp(animationComponent.currentIndex, 0f, clipDuration);
                }

                int spriteIndex = Mathf.Clamp(Mathf.FloorToInt(animationComponent.currentIndex) + clip.startIndex, clip.startIndex, clip.endIndex);
                animationComponent.spriteRenderer.sprite = spriteSheet[spriteIndex];
            }
        }

        // Requires mouse world position
        {
            Vector2? mouseWorldPosition = null;

            Vector2 GetMouseWorldPosition(ref Vector2? mouseWorldPosition)
            {
                if (!mouseWorldPosition.HasValue)
                {
                    mouseWorldPosition = cameraMain.ScreenToWorldPoint(Input.mousePosition);
                }
                return mouseWorldPosition.Value;
            }

            // Box selecting update
            {
                bool isCurrentlyBoxSelecting = Input.GetMouseButton(0);

                if (isCurrentlyBoxSelecting != isBoxSelect)
                {
                    if (isBoxSelect)
                    {
                        // end
                        boxSelector.enabled = false;
                        (currentSelectIDs, boxSelectIDs) = (boxSelectIDs, currentSelectIDs);
                        boxSelectIDs.Clear();
                    }
                    else
                    {
                        //start
                        boxSelectPositionStart = GetMouseWorldPosition(ref mouseWorldPosition);
                        boxSelector.transform.position = boxSelectPositionStart;
                        boxSelector.enabled = true;
                    }
                }

                // we want to query at end too
                if (isBoxSelect || isCurrentlyBoxSelecting)
                {
                    boxSelectIDs.Clear();

                    Vector3 boxSelectPositionCurrent = GetMouseWorldPosition(ref mouseWorldPosition);
                    int queryCount = Physics2D.OverlapAreaNonAlloc(
                        boxSelectPositionStart,
                        boxSelectPositionCurrent,
                        boxSelectQuery,
                        boxSelectLayerMask);

                    for (int i = 0; i < queryCount; ++i)
                    {
                        if (boxSelectQuery[i])
                        {
                            IDComponent idComp = boxSelectQuery[i].GetComponent<IDComponent>();
                            if (idComp)
                            {
                                if (units.IsValidID(idComp.id))
                                {
                                    boxSelectIDs.Add(idComp.id);
                                }
                            }
                        }
                    }

                    if (boxSelectQuery.Length <= queryCount)
                    {
                        // boxSelectQuery too small
                        boxSelectQuery = new Collider2D[queryCount << 1];
                    }
                    else
                    {
                        // refs no longer used, help GC out
                        Array.Clear(boxSelectQuery, 0, boxSelectQuery.Length);
                    }

                    if (isCurrentlyBoxSelecting)
                    {
                        boxSelector.size = new Vector2(
                            boxSelectPositionCurrent.x - boxSelectPositionStart.x,
                           -boxSelectPositionCurrent.y + boxSelectPositionStart.y);
                    }
                }

                isBoxSelect = isCurrentlyBoxSelecting;
            }

            // Select Icons Placement
            {
                void PlaceSelectIcon(in ID id, ref int selectIconIndex)
                {
                    if (units.IsValidID(id))
                    {
                        if (selectIcons.Count == selectIconIndex)
                        {
                            selectIcons.Add(Instantiate(selectIconPrefab));
                        }

                        GameObject selectIcon = selectIcons[selectIconIndex];
                        selectIcon.SetActive(true);
                        selectIcon.transform.SetParent(units.elements[id.index].transform, false);

                        selectIconIndex++;
                    }
                }

                int selectIconIndex = 0;
                foreach (ID id in currentSelectIDs)
                {
                    PlaceSelectIcon(id, ref selectIconIndex);
                }

                foreach (ID id in boxSelectIDs)
                {
                    if (!currentSelectIDs.Contains(id))
                    {
                        PlaceSelectIcon(id, ref selectIconIndex);
                    }
                }

                for (int i = selectIconIndex; i < selectIcons.Count; i++)
                {
                    GameObject selectIcon = selectIcons[i];
                    selectIcon.SetActive(false);
                    selectIcon.transform.SetParent(null, false);
                }
            }

            // Right-click update
            {
                if (Input.GetMouseButtonDown(1))
                {
                    foreach (ID id in currentSelectIDs)
                    {
                        if (units.IsValidID(id))
                        {
                            ref UnitEntity unit = ref units.elements.AsSpan()[id.index];
                            unit.move.target = GetMouseWorldPosition(ref mouseWorldPosition);
                        }
                    }
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            GizmosMode = 1 - GizmosMode;
        }

        if (Input.mouseScrollDelta.y > 0)
        {
            pathfindingSteps--;
        }
        if (Input.mouseScrollDelta.y < 0)
        {
            pathfindingSteps++;
        }
    }

    public List<Vector2Int> pathfinding;
    public Dictionary<Vector2Int, Vector2Int> pathfindingVisitedFrom;
    public List<GreedyPathfind.Score> pathfindingScores;

    public int pathfindingSteps = 0;

    void FixedUpdate()
    {
        Vector2 halfElementSize = overlapGrid.GetElementSize() * 0.5f;
        
        // movement update
        {
            Span<UnitEntity> unitsSpan = units.elements.AsSpan();
            foreach (ref UnitEntity unit in unitsSpan)
            {
                Vector2 unitPosition = (Vector2)unit.transform.position;
                Collider2D col = unit.transform.GetComponent<Collider2D>();

                if (col != null &&
                    GreedyPathfind.FindGreedy(overlapGrid, new Bounds2D(col.bounds), unitPosition, unit.move.target, pathfinding, pathfindingVisitedFrom, pathfindingScores))
                {
                    continue;
                    Vector2 nextPathPosition = 0 < pathfinding.Count ?
                        (Vector2)overlapGrid.GetElementWorldPosition(overlapGrid.bounds.size, halfElementSize, pathfinding[0]) :
                        unit.move.target;

                    Vector2 delta = nextPathPosition - unitPosition;
                    float maxDelta = unit.move.speed * Time.fixedDeltaTime;

                    if (maxDelta * maxDelta < delta.sqrMagnitude)
                    {
                        // too far
                        delta.Normalize();
                        delta *= maxDelta;
                        unit.move.rigidbody.MovePosition(delta + unitPosition);

                        // pathfind

                    }
                    else
                    {
                        // close enough
                        unit.move.rigidbody.MovePosition(nextPathPosition);
                    }
                }
            }
        }
    }

    public int GizmosMode = 0;

    private void OnDrawGizmos()
    {
        Vector2 halfElementSize = overlapGrid.GetElementSize() / 2.0f;
        Vector2 boundsSize = overlapGrid.bounds.size;

        if (GizmosMode == 0 && pathfinding != null)
        {
            Gizmos.color = Color.red;
            for (int i = 1; i < pathfinding.Count; i++)
            {
                GizmosMore.DrawArrow(
                    overlapGrid.GetElementWorldPosition(boundsSize, halfElementSize, pathfinding[i - 1]),
                    overlapGrid.GetElementWorldPosition(boundsSize, halfElementSize, pathfinding[i]));
            }
        }
        if (GizmosMode == 1 && pathfindingVisitedFrom != null) {

            Gizmos.color = Color.green;
            foreach (var kvp in pathfindingVisitedFrom)
            {
                GizmosMore.DrawArrow(
                    overlapGrid.GetElementWorldPosition(boundsSize, halfElementSize, kvp.Value),
                    overlapGrid.GetElementWorldPosition(boundsSize, halfElementSize, kvp.Key));
            }
        }
}
}

