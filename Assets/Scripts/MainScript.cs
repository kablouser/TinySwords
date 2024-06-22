using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum SpriteSheetIndex { WarriorBlue, ArcherBlue };
public enum AnimationClipIndex { WarriorStand, WarriorWalk };
public enum UnitType { Warrior, Archer };
public enum LeftClickCommand { None, Move, Attack };

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
public struct AttackComponent
{
    public float attackEndTime;
    public bool isAttackMoving;
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
    public NavigationNode navigationChangeInBounds;
    public AttackComponent attack;
    public DefendComponent defend;
}

[Serializable]
public class HashSetID : SerializableCollections.HashSet<ID> { }

public class MainScript : MonoBehaviour
{
    public const float ANIMATION_FRAMERATE = 10f;
    public const int EXPECTED_SELECT_COUNT = 8;

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

    [Header("Selection")]
    public HashSetID currentSelectIDs;
    public List<GameObject> selectIcons;
    public LayerMask selectionMask;
    public float doubleSelectTimeThreshold = 0.4f;
    public float currentSelectEndTime;
    // valid id if last select has only 1 result, used for detecting double select
    public ID lastSelectIfSingle;

    public HashSetID[] controlGroups;

    [Header("Box Select")]
    public Vector2 boxSelectPositionStart;
    public bool isBoxSelect;
    public List<Collider2D> collider2DCache;
    public HashSetID boxSelectIDs;

    [Header("Left Click Command")]
    public LeftClickCommand leftClickCommand;

    [Header("Camera controls")]
    public float cameraEdgeDragSpace = 20f;
    public float cameraEdgeDragSpeed = 1f;
    public float cameraMoveSpeed = 1f;

    public float cameraZoomSpeed = 1f;
    public float cameraAssetsPPU = 64.0f;
    public Vector2Int cameraAssetsPPURange;

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
        //TODO THINK
        pathfinding = new List<Vector2Int>();
        pathfindingVisitedFrom = new Dictionary<Vector2Int, Vector2Int>();
        pathfindingScores = new List<Pathfind.ScoreAStar>();

        controlGroups = new HashSetID[22];
        for(int i = 0; i < controlGroups.Length; ++i)
        {
            controlGroups[i] = new HashSetID();
        }

        collider2DCache = new List<Collider2D>(EXPECTED_SELECT_COUNT);

        // not just transparency, all sprites are sorted lowerest y first, then lowest x
        mainCamera.transparencySortMode = TransparencySortMode.CustomAxis;
        mainCamera.transparencySortAxis = new Vector3(0.2f, 1.0f, 0.0f);

        Cursor.lockState = CursorLockMode.Confined;
        QualitySettings.vSyncCount = 1;

        navigationGrid.Snapshot(navigationLayerMask);

#if UNITY_EDITOR
        Application.targetFrameRate = 60;

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
                    type = chooseSpritesheet == SpriteSheetIndex.WarriorBlue ? UnitType.Warrior : UnitType.Archer,
                    team = 0,
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
                    navigationChangeInBounds = NavigationNode.Blocking(),
                    attack = new AttackComponent
                    {
                        attackEndTime = 0f,
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

        // todo detect keyboard keys, then change left click commands accordingly

        // Requires mouse world position
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

            // Box selecting update
            // todo change for box select left click command targets
            if (leftClickCommand == LeftClickCommand.None)
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
            }
            else
            {
                isBoxSelect = false;

                // todo detect left click, then execute command
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

            // Left-click
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
                HashSetID controlGroup = controlGroups[downControlGroup.Value];

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
                cameraAssetsPPU = Mathf.Clamp(
                    cameraAssetsPPU + mouseScrollY * cameraZoomSpeed * Time.deltaTime,
                    cameraAssetsPPURange.x,
                    cameraAssetsPPURange.y);

                mainPixelPerfectCamera.assetsPPU = Mathf.RoundToInt(cameraAssetsPPU);
            }
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
                    navigationGrid.AddBounds(unit.currentBounds, -unit.navigationChangeInBounds, elementSize);
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
                unit.navigationChangeInBounds = NavigationNode.FromCollider(unitCollider,
                    previousUnitBoundsCenter.HasValue ?
                        (unit.currentBounds.center - previousUnitBoundsCenter.Value) :
                        targetVelocity * Time.fixedDeltaTime);
                navigationGrid.AddBounds(unit.currentBounds, unit.navigationChangeInBounds, elementSize);
            }
        }
    }
}

