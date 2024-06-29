using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum SpriteSheetIndex
{
    BarrelBlue, BarrelPurple, BarrelRed, BarrelYellow,
    TNTBlue, TNTPurple, TNTRed, TNTYellow,
    TorchBlue, TorchPurple, TorchRed, TorchYellow,

    ArcherBlue, ArcherPurple, ArcherRed, ArcherYellow,
    PawnBlue, PawnPurple, PawnRed, PawnYellow,
    WarriorBlue, WarriorPurple, WarriorRed, WarriorYellow,
};

public enum AnimationClipIndex
{
    BarrelSit, BarrelUp, BarrelDown, BarrelWalk, BarrelExplode,
    TNTStand, TNTWalk, TNTThrow,
    TorchStand, TorchWalk, TorchHitRight, TorchHitDown, TorchHitUp,

    ArcherStand, ArcherWalk, ArcherShootUp, ArcherShootUpRight, ArcherShootRight, ArcherShootDownRight, ArcherShootDown,
    PawnStand, PawnWalk, PawnHammer, PawnAxe, PawnCarryStand, PawnCarryWalk,
    WarriorStand, WarriorWalk, WarriorSwordRight0, WarriorSwordRight1, WarriorSwordDown0, WarriorSwordDown1, WarriorSwordUp0, WarriorSwordUp1,
};

public enum UnitType
{
    Invalid,

    Barrel,
    TNT,
    Torch,

    Archer,
    Pawn,
    Warrior,
};

// Commands that are given by mouse clicks
public enum MouseCommandType { None, Select, AttackMove, Build };

public enum CardinalDirection { North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest };

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
    [TextArea(0, 3)]
    public string name;
#endif

    public int startIndex;
    public int endIndex;
    public bool isRepeat;
    // special event at this frame
    public int eventIndex;

    public readonly int GetFrameCount => endIndex - startIndex + 1;
    public readonly bool IsValid => startIndex <= endIndex;
}

[Serializable]
public struct AnimationComponent
{
    public SpriteSheetIndex spriteSheetIndex;
    public AnimationClipIndex animationClipIndex;
    public int currentIndex;
    public SpriteRenderer spriteRenderer;
}

[Serializable]
public struct MoveComponent
{
    public Rigidbody2D rigidbody;
    public bool isFreeze; // if true, move to round clamp position instead
    public bool isTargetValid;
    public Vector2 target;
    public float speed;
}

[Serializable]
public struct AttackComponent
{
    public bool isAttackMoving;
    public ID target;
    public int combo; // chooses animation clip
}

[Serializable]
public struct DefendComponent
{
    public int
        health,
        maxHealth;
}

[Serializable]
public struct UnitEntity
{
    public UnitType type;
    public int team;
    public Transform transform;
    public AnimationComponent animation;
    public MoveComponent move;
    public Bounds2D currentBounds;
    // this unit's effect on the navigation grid within it's currentBounds
    public NavigationNode navigationChangeInBounds;
    public AttackComponent attack;
    public DefendComponent defend;
}

[Serializable]
public struct AnimationEvent
{
    public ID id;
    public AnimationClipIndex clip;
}

public class MainScript : MonoBehaviour
{
    public const float ANIMATION_FRAMERATE = 10f;
    public const int EXPECTED_SELECT_COUNT = 8;
    public const float FORCE_MAX = 5f;

    [Header("Assets")]
    public List<SpriteSheet> spriteSheets;
    public List<AnimationClip> animationClips;

    [Header("References")]
    public Camera mainCamera;
    public PixelPerfectCamera mainPixelPerfectCamera;
    public GameObject selectIconPrefab;
    public SpriteRenderer boxSelector;

    [Header("Pathfinding")]
    public NavigationGrid navigationGrid;
    public LayerMask navigationLayerMask;

    [Header("Animation")]
    // time before next animation frame update
    public float animationAccumulateFrames;

    [Header("Combat Caching")]
    // dupes should be okay
    public List<ID> despawnUnitsQueue;

    [Header("Selection")]
    public List<GameObject> selectIcons;
    public LayerMask selectionMask;
    public float doubleSelectTimeThreshold = 0.4f;
    public float currentSelectEndTime;
    // valid id if last select has only 1 result, used for detecting double select
    public ID lastSelectIfSingle;

    public HashSet<ID> currentSelectIDs;
    public HashSet<ID>[] controlGroups;

    [Header("Box Select")]
    public Vector2 boxSelectPositionStart;
    public bool isBoxSelect;
    public List<Collider2D> collider2DCache;
    public HashSet<ID> boxSelectIDs;

    [Header("Command")]
    public MouseCommandType currentMouseCommand;

    [Header("Camera controls")]
    public float cameraEdgeDragSpace = 20f;
    public float cameraEdgeDragSpeed = 1f;
    public float cameraMoveSpeed = 1f;

    public float cameraZoomSpeed = 1f;
    public float cameraAssetsPPU = 64.0f;
    public Vector2Int cameraAssetsPPURange;

    [Header("Entities")]
    public VersionedPool<UnitEntity> units;

    [Header("Pathfinding Cache")]
    public List<Vector2Int> pathfinding;
    public Dictionary<Vector2Int, Vector2Int> pathfindingVisitedFrom;
    public List<Pathfind.ScoreAStar> pathfindingScores;

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

    void DefaultEnumNamesIterate<TEnum, TStruct>(ref List<TStruct> outList, TStruct defaultStruct, Func<TStruct, string, TStruct> setName) where TStruct : struct
    {
        string[] names = Enum.GetNames(typeof(TEnum));

        if (outList == null)
        {
            outList = new List<TStruct>(names.Length);
        }

        while (outList.Count < names.Length)
        {
            outList.Add(defaultStruct);
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
        DefaultEnumNamesIterate<SpriteSheetIndex, SpriteSheet>(ref spriteSheets, default, (inS, inName) => { inS.name = inName; return inS; });
        DefaultEnumNamesIterate<AnimationClipIndex, AnimationClip>(ref animationClips,
            new AnimationClip
            {
                // -1 means invalid
                eventIndex = -1,
            },
            (inS, inName) => { inS.name = inName; return inS; });

        // clear all events
        /*        foreach (ref var x in animationClips.AsSpan())
                {
                    x.eventIndex = -1;
                }*/
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
        currentSelectIDs = new HashSet<ID>();
        boxSelectIDs = new HashSet<ID>();

        //TODO THINK
        pathfinding = new List<Vector2Int>();
        pathfindingVisitedFrom = new Dictionary<Vector2Int, Vector2Int>();
        pathfindingScores = new List<Pathfind.ScoreAStar>();

        controlGroups = new HashSet<ID>[22];
        for (int i = 0; i < controlGroups.Length; ++i)
        {
            controlGroups[i] = new HashSet<ID>();
        }

        collider2DCache = new List<Collider2D>(EXPECTED_SELECT_COUNT);

        // not just transparency, all sprites are sorted lowerest y first, then lowest x
        mainCamera.transparencySortMode = TransparencySortMode.CustomAxis;
        mainCamera.transparencySortAxis = new Vector3(0.2f, 1.0f, 0.0f);

        Cursor.lockState = CursorLockMode.Confined;
        QualitySettings.vSyncCount = 1;

        navigationGrid.Snapshot(navigationLayerMask);

#if UNITY_EDITOR
        //Application.targetFrameRate = 60;
        // stress test at lowest FPS possible
        //Application.targetFrameRate = Mathf.FloorToInt(1f / Time.maximumDeltaTime);

        if (!units.Validate())
        {
            Debug.LogError("units wrong data, please clear");
            units.Clear();
        }
        units.SpawnRange(unitsDefaultDebug,
            (unitGameObject, id) =>
            {
                unitGameObject.GetComponent<IDComponent>().id = id;
                Collider2D collider = unitGameObject.GetComponent<Collider2D>();
                SpriteRenderer spriteRenderer = unitGameObject.GetComponent<SpriteRenderer>();
                Rigidbody2D rigidbody2D = unitGameObject.GetComponent<Rigidbody2D>();
                SpriteSheetIndex chooseSpritesheet = SpriteSheetIndex.WarriorBlue;
                AnimationClipIndex chooseAnimationClip = AnimationClipIndex.WarriorStand;

                return new UnitEntity
                {
                    type = UnitType.Warrior,
                    team = 0,
                    transform = unitGameObject.transform,
                    animation = new AnimationComponent
                    {
                        spriteSheetIndex = chooseSpritesheet,
                        animationClipIndex = chooseAnimationClip,
                        currentIndex = 0,
                        spriteRenderer = spriteRenderer,
                    },
                    move = new MoveComponent
                    {
                        rigidbody = rigidbody2D,
                        isFreeze = false,
                        isTargetValid = false,
                        speed = 10f,
                    },
                    currentBounds = new Bounds2D(collider.bounds),
                    navigationChangeInBounds = NavigationNode.Blocking(),
                    attack = new AttackComponent
                    {
                        isAttackMoving = false,
                    },
                    defend = new DefendComponent
                    {
                        health = 10,
                        maxHealth = 10,
                    },
                };
            });
#endif
    }

    //static readonly ProfilerMarker s_PreparePerfMarker = new ProfilerMarker("MainScript");

    void Update()
    {
        // Animation update
        animationAccumulateFrames += Time.deltaTime * ANIMATION_FRAMERATE;
        if (1f < animationAccumulateFrames)
        {
            Span<UnitEntity> unitsSpan = units.elements.AsSpan();

            {
                int animateFrames = Mathf.FloorToInt(animationAccumulateFrames);
                animationAccumulateFrames -= animateFrames;

                Span<AnimationClip> animationClipsSpan = animationClips.AsSpan();
                foreach (int unitIndex in units)
                {
                    ref UnitEntity unit = ref unitsSpan[unitIndex];
                    ref AnimationComponent animationComponent = ref unit.animation;

                    List<Sprite> spriteSheet = spriteSheets[(int)animationComponent.spriteSheetIndex].spriteSheet;
                    ref AnimationClip clip = ref animationClipsSpan[(int)animationComponent.animationClipIndex];

                    // prevent infinite while loop
                    if (!clip.IsValid)
                        continue;

                    int frameCount = clip.GetFrameCount;
                    int lastIndex = animationComponent.currentIndex;
                    int lastIndexInClip = clip.startIndex + lastIndex;
                    animationComponent.currentIndex += animateFrames;
                    if (clip.isRepeat)
                    {
                        animationComponent.currentIndex -= animationComponent.currentIndex / frameCount * frameCount;
#if UNITY_EDITOR
                        // events in repeated
                        if (0 <= clip.eventIndex)
                        {
                            Debug.LogError("Not implemented");
                        }
#endif
                    }

                    animationComponent.currentIndex = Mathf.Clamp(animationComponent.currentIndex, 0, frameCount);
                    int currentIndexInClip = clip.startIndex + animationComponent.currentIndex;
                    animationComponent.spriteRenderer.sprite = spriteSheet[currentIndexInClip];

                    if (!clip.isRepeat)
                    {
                        // Animation special events (on hit / spawn projectiles)
                        if (lastIndexInClip < clip.eventIndex && clip.eventIndex <= currentIndexInClip)
                        {
                            if (!units.IsValidID(unit.attack.target))
                                continue;

                            ref UnitEntity target = ref unitsSpan[unit.attack.target.index];
                            switch (GetUnitType(animationComponent.animationClipIndex))
                            {
                                case UnitType.Warrior:
                                    ref DefendComponent defend = ref target.defend;
                                    //defend.health--;
                                    //Debug.Log(defend.health);
                                    if (defend.health <= 0)
                                    {
                                        //death
                                        despawnUnitsQueue.Add(unit.attack.target);
                                    }
                                    break;

                                default:
                                    break;
                            }
                        }

                        // Animation end transitions (from non-looping animation back to stand)
                        // make sure not to use animationClipIndex anymore to animate during this frame
                        if (lastIndexInClip < clip.endIndex && clip.endIndex <= currentIndexInClip)
                        {
                            switch (GetUnitType(animationComponent.animationClipIndex))
                            {
                                case UnitType.Warrior:
                                    animationComponent.animationClipIndex = AnimationClipIndex.WarriorStand;
                                    animationComponent.currentIndex = 0;
                                    break;

                                default:
                                    break;
                            }
                        }
                    }
                }
            }
        }

        // Despawn command queue (after animation special events which might do damage)
        foreach (ID despawnID in despawnUnitsQueue)
        {
            if (units.TryDespawn(despawnID, out UnitEntity despawnedUnit))
            {
                Destroy(despawnedUnit.transform.gameObject);
            }
        }
        despawnUnitsQueue.Clear();

        // Mouse clicks
        // [Requires mouse world position]
        {
            Vector2? mouseWorldPosition = null;
            Vector2 GetMouseWorldPosition(ref Vector2? mouseWorldPosition)
            {
                if (!mouseWorldPosition.HasValue)
                {
                    mouseWorldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                }
                return mouseWorldPosition.Value;
            }

            // try enter new command
            if (currentMouseCommand == MouseCommandType.None)
            {
                if (Input.GetMouseButtonDown(0))
                    currentMouseCommand = MouseCommandType.Select;
                else if (Input.GetMouseButtonDown(1))
                    AttackOrMove(GetMouseWorldPosition(ref mouseWorldPosition), false);
                else if (Input.GetKeyDown(KeyCode.A))
                    currentMouseCommand = MouseCommandType.AttackMove;
            }
            // cancel currentCommand?
            else if (Input.GetKeyDown(KeyCode.Escape) ||
                    Input.GetMouseButtonDown(1))
            {
                if (currentMouseCommand == MouseCommandType.Select)
                {
                    // cleanup end select (box select or double select)
                    boxSelector.enabled = false;
                    boxSelectIDs.Clear();
                    isBoxSelect = false;
                }

                currentMouseCommand = MouseCommandType.None;
            }

            switch (currentMouseCommand)
            {
                default: break;

                // Box selecting update
                case MouseCommandType.Select:
                    {
                        bool isCurrentlyBoxSelecting = Input.GetMouseButton(0);

                        if (isCurrentlyBoxSelecting && !isBoxSelect)
                        {
                            //start box select
                            boxSelectPositionStart = GetMouseWorldPosition(ref mouseWorldPosition);
                            boxSelector.transform.position = boxSelectPositionStart;
                            boxSelector.enabled = true;
                        }

                        // query. we want to query at end too
                        if (isBoxSelect || isCurrentlyBoxSelecting)
                        {
                            if (isCurrentlyBoxSelecting)
                            {
                                boxSelectIDs.Clear();
                            }
                            Vector3 boxSelectPositionCurrent = GetMouseWorldPosition(ref mouseWorldPosition);
                            int queryCount = Physics2D.OverlapArea(
                                boxSelectPositionStart,
                                boxSelectPositionCurrent,
                                new ContactFilter2D()
                                {
                                    layerMask = selectionMask
                                },
                                collider2DCache);

                            for (int i = 0; i < queryCount; ++i)
                            {
                                IDComponent idComp = collider2DCache[i].GetComponentInParent<IDComponent>();
                                if (idComp)
                                {
                                    if (units.IsValidID(idComp.id))
                                    {
                                        boxSelectIDs.Add(idComp.id);
                                    }
                                }
                            }

                            // refs no longer used, help GC out
                            collider2DCache.Clear();

                            if (isCurrentlyBoxSelecting)
                            {
                                boxSelector.size = new Vector2(
                                    boxSelectPositionCurrent.x - boxSelectPositionStart.x,
                                    -boxSelectPositionCurrent.y + boxSelectPositionStart.y);
                            }
                        }

                        if (!isCurrentlyBoxSelecting && isBoxSelect)
                        {
                            // end select (box select or double select)
                            currentMouseCommand = MouseCommandType.None;
                            boxSelector.enabled = false;
                            bool isDoubleSelect = false;

                            if (Time.time <= currentSelectEndTime + doubleSelectTimeThreshold)
                            {
                                // double select
                                isDoubleSelect = true;
                                ID firstBoxSelectID = boxSelectIDs.FirstOrDefault();

                                if (lastSelectIfSingle == firstBoxSelectID /* select previous must match */ &&
                                    units.IsValidID(firstBoxSelectID) /* if present remove, if not present add */)
                                {
                                    UnitType screenSelectUnitType = units.elements[firstBoxSelectID.index].type;

                                    int queryCount = Physics2D.OverlapArea(
                                        mainCamera.ScreenToWorldPoint(new Vector3()),
                                        mainCamera.ScreenToWorldPoint(new Vector3(mainCamera.pixelWidth, mainCamera.pixelHeight)),
                                        new ContactFilter2D()
                                        {
                                            layerMask = selectionMask
                                        },
                                        collider2DCache);

                                    for (int i = 0; i < queryCount; ++i)
                                    {
                                        IDComponent idComp = collider2DCache[i].GetComponentInParent<IDComponent>();
                                        if (idComp &&
                                            units.IsValidID(idComp.id) &&
                                            units.elements[idComp.id.index].type == screenSelectUnitType)
                                        {
                                            boxSelectIDs.Add(idComp.id);
                                        }
                                    }

                                    // refs no longer used, help GC out
                                    collider2DCache.Clear();
                                }

                            }
                            currentSelectEndTime = Time.time;

                            lastSelectIfSingle = boxSelectIDs.Count == 1 ?
                                boxSelectIDs.FirstOrDefault() :
                                new ID();

                            // union selections?
                            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                            {
                                if (1 < boxSelectIDs.Count || isDoubleSelect /* if isDoubleSelect don't toggle selection */)
                                {
                                    // append cur selected with box selected
                                    currentSelectIDs.UnionWith(boxSelectIDs);
                                }
                                else if (1 == boxSelectIDs.Count)
                                {
                                    // if only selected 1 unit, toggle selection on/off
                                    ID firstBoxSelectID = boxSelectIDs.FirstOrDefault();
                                    // if present remove, if not present add
                                    if (!currentSelectIDs.Add(firstBoxSelectID))
                                    {
                                        currentSelectIDs.Remove(firstBoxSelectID);
                                    }
                                }
                            }
                            // replace cur selected with box selected, swap containers
                            else
                            {
                                (currentSelectIDs, boxSelectIDs) = (boxSelectIDs, currentSelectIDs);
                            }

                            boxSelectIDs.Clear();
                        }

                        isBoxSelect = isCurrentlyBoxSelecting;
                        break;
                    }

                // Move command
                case MouseCommandType.AttackMove:
                    if (Input.GetMouseButtonDown(0))
                    {
                        AttackOrMove(GetMouseWorldPosition(ref mouseWorldPosition), true);
                        currentMouseCommand = MouseCommandType.None;
                    }
                    break;
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

            // Control groups
            {
                int? downControlGroup = null;

                for (KeyCode alphanumeric = KeyCode.Alpha0; alphanumeric < KeyCode.Alpha9; alphanumeric++)
                {
                    if (Input.GetKeyDown(alphanumeric))
                    {
                        downControlGroup = alphanumeric - KeyCode.Alpha0;
                        break;
                    }
                }

                if (!downControlGroup.HasValue)
                {
                    for (KeyCode keypadKey = KeyCode.Keypad0; keypadKey < KeyCode.Keypad9; keypadKey++)
                    {
                        if (Input.GetKeyDown(keypadKey))
                        {
                            downControlGroup = keypadKey - KeyCode.Keypad0;
                            break;
                        }
                    }
                }

                if (!downControlGroup.HasValue)
                {
                    for (KeyCode fnKey = KeyCode.F1; fnKey < KeyCode.F12; fnKey++)
                    {
                        if (Input.GetKeyDown(fnKey))
                        {
                            downControlGroup = fnKey - KeyCode.F1 + 10 /*after alphanumerics*/;
                            break;
                        }
                    }
                }

                if (downControlGroup.HasValue)
                {
                    HashSet<ID> controlGroup = controlGroups[downControlGroup.Value];

                    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    {
                        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                        {
                            // if shift is not held, control group is replaced
                            controlGroup.Clear();
                        }

                        controlGroup.UnionWith(currentSelectIDs);
                    }
                    else
                    {
                        // select control group
                        currentSelectIDs.Clear();
                        currentSelectIDs.UnionWith(controlGroup);
                    }
                }
            }

            // Camera controls
            {
                // translate controls
                Vector2 moveInput = new Vector2(
                    Input.GetAxis(ConstStrings.Horizontal),
                    Input.GetAxis(ConstStrings.Vertical));
                if (1f < moveInput.sqrMagnitude)
                {
                    moveInput.Normalize();
                }
                moveInput *= cameraMoveSpeed;

                // screen drag
                if (moveInput.sqrMagnitude < Mathf.Epsilon * 8f && Input.mousePresent)
                {
                    Vector2 mousePosition = Input.mousePosition;
                    Vector2 screenDimensions = new Vector2(mainCamera.pixelWidth, mainCamera.pixelHeight);

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

                // apply translate
                moveInput *= Time.deltaTime;
                if (0f < moveInput.sqrMagnitude)
                {
                    Transform cameraTransform = mainCamera.transform;
                    Vector2 cameraPosition = cameraTransform.position;
                    cameraTransform.position = new Vector3(
                        Mathf.Clamp(cameraPosition.x + moveInput.x, navigationGrid.bounds.min.x, navigationGrid.bounds.max.x),
                        Mathf.Clamp(cameraPosition.y + moveInput.y, navigationGrid.bounds.min.y, navigationGrid.bounds.max.y), 0f);
                }

                // zoom
                float mouseScrollY = Input.mouseScrollDelta.y;
                if (Mathf.Epsilon * 8f < Mathf.Abs(mouseScrollY))
                {
                    // low ppu is zoomed in, high ppu is zoomed out
                    // zoomed in has slower change than zoomed out
                    cameraAssetsPPU = Mathf.Clamp(
                        cameraAssetsPPU + mouseScrollY * cameraZoomSpeed * Time.deltaTime * (1f + 10f * (cameraAssetsPPU - cameraAssetsPPURange.x) / (cameraAssetsPPURange.y - cameraAssetsPPURange.x)),
                        cameraAssetsPPURange.x,
                        cameraAssetsPPURange.y);

                    mainPixelPerfectCamera.assetsPPU = Mathf.RoundToInt(cameraAssetsPPU);
                }
            }
        }
    }

    void FixedUpdate()
    {
        Span<UnitEntity> unitsSpan = units.elements.AsSpan();
        Vector2 elementSize = navigationGrid.GetElementSize();
        Vector2 halfElementSize = elementSize * 0.5f;

        // Combat desired movement update
        foreach (int unitIndex in units)
        {
            ref UnitEntity unit = ref unitsSpan[unitIndex];

            switch (unit.animation.animationClipIndex)
            {
                case AnimationClipIndex.WarriorStand:
                case AnimationClipIndex.WarriorWalk:
                    Vector3? unitPosition = null;

                    if (unit.attack.isAttackMoving)
                    {
                        unitPosition = unit.transform.position;

                        int overlaps = Physics2D.OverlapCircle(unitPosition.Value, 10f /*TODO set to vision range*/,
                            new ContactFilter2D
                            {
                                layerMask = selectionMask,
                            },
                            collider2DCache);

                        float minDist = float.MaxValue;
                        for (int i = 0; i < overlaps; ++i)
                        {
                            IDComponent idComp = collider2DCache[i].GetComponentInParent<IDComponent>();
                            if (idComp)
                            {
                                if (idComp.id.index == unitIndex)
                                    continue;

                                if (!units.IsValidID(idComp.id))
                                    continue;

                                float dist = (idComp.transform.position - unitPosition.Value).sqrMagnitude;
                                if (dist < minDist)
                                {
                                    unit.attack.target = idComp.id;
                                    minDist = dist;
                                }
                            }
                        }
                        collider2DCache.Clear();
                    }

                    // start attack?
                    if (units.IsValidID(unit.attack.target))
                    {
                        Vector3 targetPosition = unitsSpan[unit.attack.target.index].transform.position;
                        if (unitPosition == null)
                            unitPosition = unit.transform.position;
                        Vector3 diff = targetPosition - unitPosition.Value;

                        // manhattan distance < 1.1f
                        if (Mathf.Abs(diff.x) < 1.1f && Mathf.Abs(diff.y) < 1.1f)
                        {
                            // stop moving
                            unit.move.isFreeze = true;
                            // start animation
                            unit.animation.animationClipIndex = GetWarriorAttackClip(diff, unit.attack.combo, out bool? doFlipX);
                            unit.attack.combo++;
                            unit.animation.currentIndex = 0;

                            if (doFlipX.HasValue)
                            {
                                unit.animation.spriteRenderer.flipX = doFlipX.Value;
                            }
                        }
                        else
                        {
                            unit.move.isFreeze = false;
                            unit.move.isTargetValid = true;
                            unit.move.target = targetPosition;
                        }
                    }
                    else
                    {
                        unit.move.isFreeze = false;
                    }
                    break;

                default:
                    unit.move.isFreeze = true;
                    break;
            }
        }

        // Movement update
        foreach (int unitIndex in units)
        {
            ref UnitEntity unit = ref unitsSpan[unitIndex];

            Vector2 unitPosition = (Vector2)unit.transform.position;
            Collider2D unitCollider = unit.transform.GetComponent<Collider2D>();

            if (unit.currentBounds.min.x != float.NaN)
            {
                navigationGrid.AddBounds(unit.currentBounds, -unit.navigationChangeInBounds, elementSize);
            }

            pathfinding.Clear();
            if (unit.move.isFreeze || !unit.move.isTargetValid)
            {
                pathfinding.Add(navigationGrid.RoundClampWorldPositionToIndex(elementSize, unitPosition));
            }
            else
            {
                Pathfind.FindAStar(
                    navigationGrid, unit.move.rigidbody.mass * unit.move.speed, unitPosition, unit.move.target,
                    pathfinding,
                    pathfindingVisitedFrom, pathfindingScores);
            }

            if (0 < pathfinding.Count &&
                navigationGrid.nodes.TryIndex(pathfinding[0], out NavigationNode node) &&
                0 == node.blocking)
            {
                Vector2 nextPathPosition = (Vector2)navigationGrid.GetElementWorldPosition(navigationGrid.bounds.size, halfElementSize, pathfinding[0]);

                Vector2 toNextPathPosition = nextPathPosition - unitPosition;
                float toNextPathPositionMagnitude = toNextPathPosition.magnitude;

                Vector2 targetVelocity = toNextPathPosition * unit.move.speed;
                // normalised only above 1 magnitude
                if (1f < toNextPathPositionMagnitude)
                {
                    targetVelocity /= toNextPathPositionMagnitude;
                }

                Vector2 force = (targetVelocity - unit.move.rigidbody.velocity) * unit.move.rigidbody.mass;
                float forceMag = force.magnitude;
                if (FORCE_MAX < forceMag)
                {
                    force *= FORCE_MAX / forceMag;
                }
                unit.move.rigidbody.AddForce(force, ForceMode2D.Impulse);

                // debug vis only
                //pathfinding.Insert(0, navigationGrid.GetIndex(elementSize, unitPosition));
            }

            bool isPreviousMoving = unit.navigationChangeInBounds.blocking <= 0;
            bool isCurrentMoving;

            unit.currentBounds = new Bounds2D(unitCollider.bounds);
            unit.navigationChangeInBounds = NavigationNode.FromCollider(unitCollider,
                unit.move.rigidbody.velocity * Time.fixedDeltaTime);

            isCurrentMoving = unit.navigationChangeInBounds.blocking <= 0;

            navigationGrid.AddBounds(unit.currentBounds, unit.navigationChangeInBounds, elementSize);

            // change walk animation
            if (isPreviousMoving != isCurrentMoving)
            {
                switch (GetUnitType(unit.animation.animationClipIndex))
                {
                    case UnitType.Warrior:
                        unit.animation.animationClipIndex = isCurrentMoving ? AnimationClipIndex.WarriorWalk : AnimationClipIndex.WarriorStand;
                        break;

                    default:
                        break;
                }
            }

            // flip x sprite?
            if (0 < Mathf.Abs(unit.navigationChangeInBounds.scaledMomentum.x))
            {
                unit.animation.spriteRenderer.flipX = unit.navigationChangeInBounds.scaledMomentum.x <= -1;
            }
        }
    }

    public static UnitType GetUnitType(AnimationClipIndex clip)
    {
        switch (clip)
        {
            case AnimationClipIndex.BarrelSit:
            case AnimationClipIndex.BarrelUp:
            case AnimationClipIndex.BarrelDown:
            case AnimationClipIndex.BarrelWalk:
            case AnimationClipIndex.BarrelExplode:
                return UnitType.Barrel;

            case AnimationClipIndex.TNTStand:
            case AnimationClipIndex.TNTWalk:
            case AnimationClipIndex.TNTThrow:
                return UnitType.TNT;

            case AnimationClipIndex.TorchStand:
            case AnimationClipIndex.TorchWalk:
            case AnimationClipIndex.TorchHitRight:
            case AnimationClipIndex.TorchHitDown:
            case AnimationClipIndex.TorchHitUp:
                return UnitType.Torch;

            case AnimationClipIndex.ArcherStand:
            case AnimationClipIndex.ArcherWalk:
            case AnimationClipIndex.ArcherShootUp:
            case AnimationClipIndex.ArcherShootUpRight:
            case AnimationClipIndex.ArcherShootRight:
            case AnimationClipIndex.ArcherShootDownRight:
            case AnimationClipIndex.ArcherShootDown:
                return UnitType.Archer;

            case AnimationClipIndex.PawnStand:
            case AnimationClipIndex.PawnWalk:
            case AnimationClipIndex.PawnHammer:
            case AnimationClipIndex.PawnAxe:
            case AnimationClipIndex.PawnCarryStand:
            case AnimationClipIndex.PawnCarryWalk:
                return UnitType.Pawn;

            case AnimationClipIndex.WarriorStand:
            case AnimationClipIndex.WarriorWalk:
            case AnimationClipIndex.WarriorSwordRight0:
            case AnimationClipIndex.WarriorSwordRight1:
            case AnimationClipIndex.WarriorSwordDown0:
            case AnimationClipIndex.WarriorSwordDown1:
            case AnimationClipIndex.WarriorSwordUp0:
            case AnimationClipIndex.WarriorSwordUp1:
                return UnitType.Warrior;

            default:
                return UnitType.Invalid;
        }
    }

    //public enum CardinalDirection { North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest };
    // doFlipX == null means do nothing, else do flipX = doFlipX.Value
    public static AnimationClipIndex GetWarriorAttackClip(in Vector3 targetVector, int combo, out bool? doFlipX)
    {
        switch (GetCardinalDirection(targetVector))
        {
            case CardinalDirection.North:
                doFlipX = null;
                return combo % 2 == 0 ? AnimationClipIndex.WarriorSwordUp0 : AnimationClipIndex.WarriorSwordUp1;
            case CardinalDirection.NorthEast:
            case CardinalDirection.East:
            case CardinalDirection.SouthEast:
                doFlipX = false;
                return combo % 2 == 0 ? AnimationClipIndex.WarriorSwordRight0 : AnimationClipIndex.WarriorSwordRight1;
            case CardinalDirection.South:
                doFlipX = null;
                return combo % 2 == 0 ? AnimationClipIndex.WarriorSwordDown0 : AnimationClipIndex.WarriorSwordDown1;
            case CardinalDirection.SouthWest:
            case CardinalDirection.West:
            case CardinalDirection.NorthWest:
                doFlipX = true;
                return combo % 2 == 0 ? AnimationClipIndex.WarriorSwordRight0 : AnimationClipIndex.WarriorSwordRight1;

            default:
                doFlipX = null;
                return AnimationClipIndex.WarriorStand;
        }
    }

    public static CardinalDirection GetCardinalDirection(in Vector3 vector)
    {
        float tan = vector.y / vector.x;
        if (!float.IsFinite(tan))
        {
            return 0f <= vector.y ? CardinalDirection.North : CardinalDirection.South;
        }
        else if (0f < vector.x)
        {
            if (2.41421356237f < tan)
                return CardinalDirection.North;
            else if (0.41421356237f < tan)
                return CardinalDirection.NorthEast;
            else if (-0.41421356237f < tan)
                return CardinalDirection.East;
            else if (-2.41421356237f < tan)
                return CardinalDirection.SouthEast;
            else
                return CardinalDirection.South;
        }
        else
        {
            if (2.41421356237f < tan)
                return CardinalDirection.South;
            else if (0.41421356237f < tan)
                return CardinalDirection.SouthWest;
            else if (-0.41421356237f < tan)
                return CardinalDirection.West;
            else if (-2.41421356237f < tan)
                return CardinalDirection.NorthWest;
            else
                return CardinalDirection.North;
        }
    }

    public void AttackOrMove(in Vector2 mouseWorldPosition, bool isAttackMoving)
    {
        // attack target unit
        int queryCount = Physics2D.OverlapPoint(
            mouseWorldPosition,
            new ContactFilter2D()
            {
                layerMask = selectionMask
            },
            collider2DCache);
        ID targetID = new ID();
        for (int i = 0; i < queryCount; i++)
        {
            IDComponent idComp = collider2DCache[i].GetComponentInParent<IDComponent>();
            if (idComp &&
                units.IsValidID(idComp.id))
            {
                targetID = idComp.id;
                break;
            }
        }
        collider2DCache.Clear();

        Span<UnitEntity> unitsSpan = units.elements.AsSpan();
        foreach (ID id in currentSelectIDs)
        {
            if (units.IsValidID(id))
            {
                ref UnitEntity unit = ref unitsSpan[id.index];

                if (unit.attack.target != targetID &&
                    id != targetID) // no self harm!
                {
                    unit.attack.target = targetID;

                    // attack cancel?
                    switch (GetUnitType(unit.animation.animationClipIndex))
                    {
                        case UnitType.Warrior:
                            unit.animation.animationClipIndex = AnimationClipIndex.WarriorWalk;
                            break;

                        default:
                            break;
                    }
                }

                if (targetID.type == IDType.Invalid)
                {
                    // direct move
                    unit.move.isTargetValid = true;
                    unit.move.target = mouseWorldPosition;
                    unit.attack.isAttackMoving = isAttackMoving;
                }
            }
        }
    }
}
