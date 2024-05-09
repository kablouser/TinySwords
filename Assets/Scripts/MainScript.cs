using System;
using System.Collections.Generic;
using UnityEngine;
using static MainScript;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
    public Bounds2D currentBounds;
    public NavigationNode navigationNode;
}

[Serializable]
public class HashSetID : SerializableCollections.HashSet<ID> { }

public class MainScript : MonoBehaviour
{
    public const float ANIMATION_FRAMERATE = 10f;
    public const int EXPECTED_SELECT_COUNT = 8;

    public enum SpriteSheetIndex { WarriorBlue, ArcherBlue };
    [Header("Assets")]
    public List<SpriteSheet> spriteSheets;

    public enum AnimationClipIndex { WarriorStand, WarriorWalk };
    public List<AnimationClip> animationClips;

    [Header("References")]
    public Camera cameraMain;
    public GameObject selectIconPrefab;
    public SpriteRenderer boxSelector;

    [Header("Pathfinding")]
    public NavigationGrid navigationGrid;

    [Header("Selection")]
    public HashSetID currentSelectIDs;
    public List<GameObject> selectIcons;

    [Header("Box Select")]
    public LayerMask boxSelectLayerMask;
    public Vector2 boxSelectPositionStart;
    public bool isBoxSelect;
    public Collider2D[] boxSelectQuery;
    public HashSetID boxSelectIDs;

    [Header("Camera controls")]
    public float cameraEdgeDragSpace = 20f;
    public float cameraEdgeDragSpeed = 1f;
    public float cameraMoveSpeed = 1f;

    [Header("Entities")]
    public VersionedPool<UnitEntity> units;

#if UNITY_EDITOR
    [Header("EDITOR ONLY")]
    public List<GameObject> unitsDefaultDebug;

    public Bounds2D setNavigationGridBoundsSize;
    public Vector2 setNavigationGridElementSize;
    [ContextMenu("_SetNavigationGridElementSize")]
    void SetNavigationGridElementSize()
    {
        Undo.RecordObject(this, "SetNavigationGridElementSize");
        navigationGrid.SetElementSizeUninitialised(setNavigationGridBoundsSize, setNavigationGridElementSize);
    }

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

    /// <summary>
    /// Fills up lists with enum element length { spriteSheets, animationClips }
    /// </summary>
    [ContextMenu("_DefaultEnumNames")]
    void DefaultEnumNames()
    {
        Undo.RecordObject(this, "DefaultEnumNames");
        DefaultEnumNamesIterate<SpriteSheetIndex, SpriteSheet>(ref spriteSheets, (inS, inName) => { inS.name = inName; return inS; });
        DefaultEnumNamesIterate<AnimationClipIndex, AnimationClip>(ref animationClips, (inS, inName) => { inS.name = inName; return inS; });
    }

    [ContextMenu("_Prepopulate Pools")]
    void PrepopulatePools()
    {
        Undo.RecordObject(this, "PrepopulatePools");
        Pool.Prepopulate(selectIcons, selectIconPrefab, EXPECTED_SELECT_COUNT);
    }
#endif

    void Start()
    {
        Cursor.lockState = CursorLockMode.Confined;

        navigationGrid.Snapshot();

        QualitySettings.vSyncCount = 1;
        boxSelectQuery = new Collider2D[EXPECTED_SELECT_COUNT];

        //TODO THINK
        pathfinding = new List<Vector2Int>();
        pathfindingVisitedFrom = new Dictionary<Vector2Int, Vector2Int>();
        pathfindingScores = new List<Pathfind.ScoreAStar>();

#if UNITY_EDITOR
        //Application.targetFrameRate = 30;

        units.SpawnRange(unitsDefaultDebug,
            (unitGameObject, id) =>
            {
                unitGameObject.GetComponent<IDComponent>().id = id;
                Collider2D collider = unitGameObject.GetComponent<Collider2D>();
                SpriteRenderer spriteRenderer = unitGameObject.GetComponent<SpriteRenderer>();
                Rigidbody2D rigidbody2D = unitGameObject.GetComponent<Rigidbody2D>();
                SpriteSheetIndex chooseSpritesheet;

                if (spriteRenderer.sprite == spriteSheets[0].spriteSheet[0])
                {
                    chooseSpritesheet = SpriteSheetIndex.WarriorBlue;
                }
                else
                {
                    chooseSpritesheet = SpriteSheetIndex.ArcherBlue;
                }

                return new UnitEntity
                {
                    transform = unitGameObject.transform,
                    animation = new AnimationComponent
                    {
                        spriteSheetIndex = chooseSpritesheet,
                        animationClipIndex = AnimationClipIndex.WarriorStand,
                        currentIndex = 0f,
                        spriteRenderer = spriteRenderer,
                    },
                    move = new MoveComponent
                    {
                        rigidbody = rigidbody2D,
                        target = unitGameObject.transform.position,
                        speed = 10f,
                    },
                    currentBounds = new Bounds2D(collider.bounds),
                    navigationNode = NavigationNode.Blocking(),
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

        // Camera controls
        {
            Vector2 moveInput = new Vector2(
                Input.GetAxis(ConstStrings.Horizontal),
                Input.GetAxis(ConstStrings.Vertical));
            if (1f < moveInput.sqrMagnitude)
            {
                moveInput.Normalize();
            }
            moveInput *= cameraMoveSpeed;

            if (moveInput.sqrMagnitude < Mathf.Epsilon * 8f && Input.mousePresent)
            {
                Vector2 mousePosition = Input.mousePosition;
                Vector2 screenDimensions = new Vector2(Screen.width, Screen.height);

                float? dragSpace = null;
                if (mousePosition.x <= cameraEdgeDragSpace)
                {
                    dragSpace = cameraEdgeDragSpace - mousePosition.x;
                }
                else if (mousePosition.y <= cameraEdgeDragSpace)
                {
                    dragSpace = cameraEdgeDragSpace - mousePosition.y;
                }
                else if (screenDimensions.x - cameraEdgeDragSpace <= mousePosition.x)
                {
                    dragSpace = mousePosition.x - (screenDimensions.x - cameraEdgeDragSpace);
                }
                else if (screenDimensions.y - cameraEdgeDragSpace <= mousePosition.y)
                {
                    dragSpace = mousePosition.y - (screenDimensions.y - cameraEdgeDragSpace);
                }

                if (dragSpace.HasValue)
                {
                    // edge dragging
                    moveInput = cameraEdgeDragSpeed * Mathf.Clamp(dragSpace.Value / cameraEdgeDragSpace, 0f, 1f) * (mousePosition - screenDimensions / 2.0f).normalized;
                }
            }

            cameraMain.transform.Translate(moveInput * Time.deltaTime);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            GizmosMode = 1 - GizmosMode;
        }
    }

    public List<Vector2Int> pathfinding;
    public Dictionary<Vector2Int, Vector2Int> pathfindingVisitedFrom;
    public List<Pathfind.ScoreAStar> pathfindingScores;
    public float forceMax = 5f;
    void FixedUpdate()
    {
        Vector2 elementSize = navigationGrid.GetElementSize();
        Vector2 halfElementSize = elementSize * 0.5f;
        
        // movement update
        {
            Span<UnitEntity> unitsSpan = units.elements.AsSpan();
            foreach (ref UnitEntity unit in unitsSpan)
            {
                Vector2 unitPosition = (Vector2)unit.transform.position;
                Vector2? previousUnitBoundsCenter = null;
                Collider2D unitCollider = unit.transform.GetComponent<Collider2D>();

                if (unit.currentBounds.min.x != float.NaN)
                {
                    navigationGrid.AddBounds(unit.currentBounds, -unit.navigationNode, elementSize);
                    previousUnitBoundsCenter = unit.currentBounds.center;
                }

                Vector2 targetVelocity = new Vector2();

                if (Pathfind.FindAStar(
                        navigationGrid, unit.move.rigidbody.mass * unit.move.speed, unitPosition, unit.move.target,
                        pathfinding,
                        pathfindingVisitedFrom, pathfindingScores) &&

                    0 < pathfinding.Count)
                {
                    if (navigationGrid.nodes.TryIndex(pathfinding[0], out NavigationNode node) &&
                        0 == node.blocking)
                    {
                        Vector2 nextPathPosition = (Vector2)navigationGrid.GetElementWorldPosition(navigationGrid.bounds.size, halfElementSize, pathfinding[0]);

                        Vector2 toNextPathPosition = nextPathPosition - unitPosition;
                        float toNextPathPositionMagnitude = toNextPathPosition.magnitude;

                        targetVelocity = toNextPathPosition * unit.move.speed;
                        // normalised only above 1 magnitude
                        if (1f < toNextPathPositionMagnitude)
                        {
                            targetVelocity /= toNextPathPositionMagnitude;
                        }

                        Vector2 force = (targetVelocity - unit.move.rigidbody.velocity) * unit.move.rigidbody.mass;
                        float forceMag = force.magnitude;
                        if (forceMax < forceMag)
                        {
                            force *= forceMax / forceMag;
                        }
                        unit.move.rigidbody.AddForce(force, ForceMode2D.Impulse);

                        pathfinding.Insert(0, navigationGrid.GetIndex(elementSize, unitPosition));
                    }
                }

                unit.currentBounds = new Bounds2D(unitCollider.bounds);
                unit.navigationNode = NavigationNode.FromCollider(unitCollider,
                    previousUnitBoundsCenter.HasValue ?
                        (unit.currentBounds.center - previousUnitBoundsCenter.Value) :
                        targetVelocity * Time.fixedDeltaTime);
                navigationGrid.AddBounds(unit.currentBounds, unit.navigationNode, elementSize);
            }
        }
    }

    public int GizmosMode = 0;

    private void OnDrawGizmos()
    {
        Vector2 halfElementSize = navigationGrid.GetElementSize() / 2.0f;
        Vector2 boundsSize = navigationGrid.bounds.size;

        if (GizmosMode == 0 && pathfinding != null)
        {
            Gizmos.color = Color.red;
            for (int i = 1; i < pathfinding.Count; i++)
            {
                GizmosMore.DrawArrow(
                    navigationGrid.GetElementWorldPosition(boundsSize, halfElementSize, pathfinding[i - 1]),
                    navigationGrid.GetElementWorldPosition(boundsSize, halfElementSize, pathfinding[i]));
            }
        }
        if (GizmosMode == 1 && pathfindingVisitedFrom != null)
        {

            Gizmos.color = Color.green;
            foreach (var kvp in pathfindingVisitedFrom)
            {
                GizmosMore.DrawArrow(
                    navigationGrid.GetElementWorldPosition(boundsSize, halfElementSize, kvp.Value),
                    navigationGrid.GetElementWorldPosition(boundsSize, halfElementSize, kvp.Key));
            }
        }
    }
}

