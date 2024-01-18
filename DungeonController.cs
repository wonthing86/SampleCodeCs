using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lean.Pool;
using DG.Tweening;
using GameDB;
using diverseshared;

public class DungeonController : MonoBehaviour
{
    public interface IDungeonInfo
    {
        void OnCreateField(DungeonField field, GameDB.GrowthStage stageSheet);

        void OnStartGame();

        void OnTick(int progressTick, F64 delta);

        void OnInputCommand(ulong playerHeroId, F64Vec2 dir, bool isStop, ref uint skillTid);

        void OnHitEntity(BaseEntity entity, uint casterEntityId, BigDecimal damage);

        void OnDieEntity(BaseEntity entity);

        void OnStealGold();

        void OnLeaveGame();

        void OnResult(List<PDeltaInfo> rewardInfos);
    }

    public static readonly string DUNGEON_ENTERED_HEROES_KEY = "DungeonEnteredHeroes";

    //public static readonly string DUNGEON_SKILL_PRESET_KEY = "DungeonSkillPreset";

    //public static readonly string DUNGEON_CUBE_PRESET_KEY = "DungeonCubePreset";

    [SerializeField]
    private GameController _gameController;

    [SerializeField]
    private HeroController _heroController;

    [SerializeField]
    private ResourceController _resourceController;

    [SerializeField]
    private Transform _mapGroundSlot;

    [SerializeField]
    private FieldMono _fieldMono;

    [SerializeField]
    private DungeonSelectPanel _selectPanel;

    private long _resetTime = 0;
    private Coroutine _corUpdate = null;
    private WaitForSeconds _wait = new WaitForSeconds(1f);

    private ulong _playerNo = 0;
    public ulong PlayerNo => _playerNo;

    private DungeonField _field { get; set; }

    public DungeonField Field => _field;

    private IDungeonInfo _curDungeonInfo = null;

    private PGrowthInfo _growthPacketInfo = null;

    public PGrowthInfo GrowthPacketInfo => _growthPacketInfo;

    // 연출 중
    private bool _isPlayingEvent = false;

    // 진입한 영웅리스트(tid, Stage)
    private List<ulong> _enterHeroes = new List<ulong>();

    public bool GetEnterHeroTids(ref List<uint> tids)
    {
        if(tids == null)
        {
            return false;
        }
        foreach(var playerHeroId in _enterHeroes)
        {
            var playerEntity = _field.GetPlayerHeroEntity(playerHeroId);
            tids.Add(playerEntity.Tid);
        }
        return true;
    }
    private ulong SelectedPlayerHeroId = 0;

    public BaseEntity SelectedHeroEntity
    {
        get
        {
            if (_enterHeroes.Count == 0)
            {
                return null;
            }
            if (_field == null)
            {
                return null;
            }

            var playerEntity = _field.GetPlayerHeroEntity(SelectedPlayerHeroId);
            if (playerEntity == null)
            {
                return null;
            }

            //_field.SelectedHeroId = res;

            return playerEntity;
        }
    }

    // 재시작용 백업 데이터
    private uint _lastStageTid = 0;

    private List<uint> _lastHeroTids = new List<uint>();

    public List<uint> LastHeroTids
    { get { return _lastHeroTids; } }

    // 현재까지 받아서 진행된 틱
    private int _progressTick = 0;

    public int ProgressTick => _progressTick;

    private float curSec = 0.0f;

    private bool _isTick = false;

    public bool IsTick
    { get { return _isTick; } set { _isTick = value; } }

    //------------------------------------------------------------------------
    // Input
    //------------------------------------------------------------------------
    private EMoveInputMode _moveInput = EMoveInputMode.NONE;

    private F64Vec2 _moveDir;
    private uint _reserveSkillIndex = 0;

    private void Update()
    {
        // 아직 세팅전일때는 돌면 안된다
        if (GameController.CoolSecond < 0.00001f)
        {
            return;
        }

        if (_isTick == false)
        {
            return;
        }

#if UNITY_EDITOR
        curSec += Time.deltaTime;
#else
        curSec += SgDelta.I.GetDeltaSecond(SgDelta.DeltaContent.Etc);
#endif
        if (curSec < GameController.CoolSecond)
        {
            return;
        }
        curSec -= GameController.CoolSecond;

        // idle과 다르게 안전하게 한번만 호출한다
        ProcessTick();
    }

    private void ProcessTick()
    {
        _progressTick = _progressTick + 1;

        if (SelectedPlayerHeroId > 0)
        {
            _curDungeonInfo.OnInputCommand(
                SelectedPlayerHeroId,
                _moveDir,
                _moveInput == EMoveInputMode.STOP,
                ref _reserveSkillIndex);
        }

        if (_curDungeonInfo != null)
        {
            _curDungeonInfo.OnTick(_progressTick, GameController.Delta);
        }

        GameScene.GameScene_MainUI.i.UI_BottomStageHUD.OnTick();
    }

    private void RefreshCamera()
    {
        if (SelectedPlayerHeroId == 0)
            return;

        var entity = _field.GetPlayerHeroEntity(SelectedPlayerHeroId);
        if (entity == null)
            return;

        var mono = entity.GetMonoEntity();
        if (mono == null)
            return;

        var bound = _field.CameraBound;
        _gameController.SetCameraFollow(mono.gameObject, bound.Item1, bound.Item2);

        //22.12.30 배경 카메라 타겟 갱신
        if (BGControllerSettings.Verify())
            BGControllerSettings.i.RefreshCamTransform();
        //--
    }

    #region IdleController에서 온 조작 캐릭터 변경

    /// <summary>
    /// return = 0:false, 1:true
    /// </summary>
    public int IsSelectedHero(ulong playerHeroId)
    {
        return (SelectedPlayerHeroId == playerHeroId) ? 1 : 0;
    }

    /// <summary>
    /// 조작 캐릭터 변경
    /// </summary>
    /// <param name="playerHeroId"></param>
    public void OnChangeSelectedHero(ulong playerHeroId)
    {
        if (_enterHeroes.Count == 0)
        {
            return;
        }

        if (playerHeroId == 0)
        {
            SelectedPlayerHeroId = _enterHeroes[0];
            _field.SelectedPlayerHeroId = SelectedPlayerHeroId;
            return;
        }

        SelectedPlayerHeroId = playerHeroId;
        _field.SelectedPlayerHeroId = playerHeroId;
    }

    public void OnChangeSelectedHeroIndex(int index)
    {
        if (_enterHeroes.Count == 0)
        {
            return;
        }
        SelectedPlayerHeroId = _enterHeroes[index];
        _field.SelectedPlayerHeroId = SelectedPlayerHeroId;
    }

    //--

    public void OnSelectHero(ulong playerHeroId)
    {
        //변경 처리하지 않으면 기본 값으로 첫 번째 리스트 캐릭터가 선택 된다.
        OnChangeSelectedHero(playerHeroId);

        RefreshCamera();

        RefreshSkillPanel();
    }

    public void OnSelectNextHero()
    {
        if (_field == null)
        {
            return;
        }

        if (SelectedHeroEntity != null && SelectedHeroEntity.IsDeadInGame)
        {
            BaseEntity entity = null;

            _field.EachPlayers((BaseEntity player) =>
            {
                if (entity != null)
                {
                    return;
                }
                if (player.IsDeadInGame)
                {
                    return;
                }
                entity = player;
            });

            if (entity != null)
            {
                OnSelectHero(entity.PlayerHeroId);
            }
        }
    }

    #endregion IdleController에서 온 조작 캐릭터 변경

    #region 하단 기타 캐릭터용 슬롯 표시

    public List<BaseEntity> GetHeroesByTid(List<uint> heroesTid)
    {
        List<BaseEntity> result = new List<BaseEntity>();
        if (_field == null)
        {
            return null;
        }

        foreach (var iter in heroesTid)
        {
            var playerHeroId = _field.GetPlayerHeroId(_playerNo, iter);
            result.Add(_field.GetPlayerHeroEntity(playerHeroId));
        }

        return result;
    }

    #endregion 하단 기타 캐릭터용 슬롯 표시

    public void RefreshSkillPanel()
    {
        if(SelectedHeroEntity == null)
        {
            return;
        }

        GameScene.GameScene_MainUI.i.UI_BottomStageHUD.Set(SelectedHeroEntity.Tid, true);
    }

    public async void StartDungeon(uint stageTid, List<uint> heroTids, bool isRetry = false)
    {
        await SendReqStartGrowthDungeon(stageTid, heroTids, isRetry);
    }

    private async void OpenDungeon(uint stageTid, List<uint> heroTids, bool isRetry)
    {
        if (heroTids.Count == 0)
        {
            return;
        }

        if (GameDBContainer.Instance.GrowthStageList.TryGetValue(stageTid, out GameDB.GrowthStage stageSheet) == false)
        {
            return;
        }

        //카메라 거리 조절
        BattleCameraDirector.i.SetCameraOrthographicSize(stageSheet.CamDistance);

        if (!isRetry)
        {
            RefreshLastInfo(stageSheet, heroTids);
        }

        if (stageSheet.Package == EStagePackage.GoldStage)
        {
            GlobalMusicManager.i.PlayMusic("BGM_Dungeon_UnderMoon");
            GoldDungeonInfo dungeonInfo = new GoldDungeonInfo(this);
            _curDungeonInfo = dungeonInfo;
        }
        else if (stageSheet.Package == EStagePackage.ExpStage)
        {
            GlobalMusicManager.i.PlayMusic("BGM_Dungeon_Labs");
            ExpDungeonInfo dungeonInfo = new ExpDungeonInfo(this);
            _curDungeonInfo = dungeonInfo;
        }
        else if (stageSheet.Package == EStagePackage.WeaponUpgradeStage ||
                 stageSheet.Package == EStagePackage.ArmorUpgradeStage ||
                 stageSheet.Package == EStagePackage.AccessoryUpgradeStage)
        {
            GlobalMusicManager.i.PlayMusic("BGM_Dungeon_Trash");
            HeroUpgradeDungeonInfo dungeonInfo = new HeroUpgradeDungeonInfo(this);
            _curDungeonInfo = dungeonInfo;
        }

        // 필드생성
        await PrepareField(stageSheet, heroTids);
    }

    public void RetryDungeon()
    {
        StartDungeon(_lastStageTid, _lastHeroTids, true);
    }

    public void ExitDungeon()
    {
        if (_isPlayingEvent)
        {
            return;
        }

        string title = MultiLanguageManager.i.GetText("ResourceStage_UI_Popup_Name0"); // 알림
        string content = MultiLanguageManager.i.GetText("ResourceStage_UI_Popup_Desc0"); // 전투를 포기하고 정말 나가시겠습니까?

        PopupManager.i.ShowPopup_YesNo(title, content, EscapeAllHeroes, null);
    }

    public void LeaveDungeon()
    {
        //카메라 거리 원복
        BattleCameraDirector.i.RecoveryCameraOrthographicSize();

        DestroyField();

        if (_field != null)
        {
            PoolController.i.DespawnAll(_field.FieldName);
        }

        GameController.i.LeaveDungeon();
        GameScene.GameScene_MainUI.i.TabToggle(GameScene.GameScene_MainUI.eBottomToggle.Home);

        if (GameDBContainer.Instance.GrowthStageList.TryGetValue(_lastStageTid, out var stageDB))
        {
            GameScene.GameScene_MainUI.i.UI_BottomMenu.SetViewDungeonSelectPanel(true, GameDB.EStageType.Growth, stageDB.GroupId);
        }
    }

    public PGrowthCategory GetGrowthCategoryInfo(uint categoryTid)
    {
        if (_growthPacketInfo == null)
        {
            return null;
        }

        foreach (var info in _growthPacketInfo.GrowthInfos)
        {
            if (info.CategoryTid == categoryTid)
            {
                return info;
            }
        }
        return null;
    }

    #region 필드

    public void DestroyField()
    {
        if (_field != null)
        {
            _field.DestroyField();
        }
        _isTick = false;
    }

    private async Task PrepareField(GameDB.GrowthStage stageDB, List<uint> heroTids)
    {
        DestroyField();

        // 필드 생성
        F64Vec2 pos = F64Vec2.FromInt(200, 0);
        _field = await CreateFieldAsync(stageDB, heroTids, pos);

        OnCreateField(stageDB, heroTids);
    }

    private async Task<DungeonField> CreateFieldAsync(GameDB.GrowthStage stageDB, List<uint> heroTids, F64Vec2 pos)
    {
        var mapPath = "BattleStage/" + stageDB.MapPrefab;
        GameObject prefab = await AssetFiles.LoadAssetAsync<GameObject>(mapPath);
        if (prefab == null)
        {
            return null;
        }

        var _stageObj = Instantiate(prefab, new Vector3(pos.X.Float, pos.Y.Float), Quaternion.identity, _mapGroundSlot);
        if (_stageObj == null)
        {
            return null;
        }
        _stageObj.GetComponent<BGController>()?.SyncMapPos();
        _stageObj.GetComponent<BGController>()?.SetCameraTarget(BGController.ECameraTarget.Main);

        // 필드생성
        var field = new DungeonField(stageDB.tid, stageDB.Package, this, _fieldMono);
        field.SetStageMono(_stageObj.transform, mapPath);

        // 바운드(섹터맵) 생성
        var obstacles = _stageObj.transform.Find("Obstacles");
        // 바운드를 제일 먼저 설정해야한다(섹터맵 설정을 위해서)
        for (var i = 0; i < obstacles.childCount; i++)
        {
            var child = obstacles.GetChild(i);
            var bound = child.GetComponent<SimBound>();
            if (bound != null)
            {
                bound.Init(pos);
                field.AddBound(bound.Min, bound.Max);
            }
        }

        // 카메라바운드
        var cameraBoundObj = _stageObj.transform.Find("CameraBound");
        if (cameraBoundObj != null)
        {
            var cameraBound = cameraBoundObj.GetComponent<SimBound>();
            if (cameraBound != null)
            {
                cameraBound.Init(pos);
                field.SetCameraBound(cameraBound.Min, cameraBound.Max);
            }
        }

        // 장애물
        for (var i = 0; i < obstacles.childCount; i++)
        {
            var child = obstacles.GetChild(i);
            var obstacle = child.GetComponent<SimObstacle>();
            if (obstacle != null)
            {
                field.AddObstacle(
                    BattleHelper.ToFp2(child.position.x, child.position.y),
                    BattleHelper.Truncate(obstacle.Radius));
            }
        }

        // Spot 지정
        var spotObj = _stageObj.transform.Find("Spots");
        if (spotObj != null)
        {
            var spots = spotObj.GetComponent<SimSpot>();
            if (spots != null)
            {
                spots.Init(pos);
                foreach (var spot in spots.Specs)
                {
                    field.AddSpot(spot.Name, spots.Pos(spot.X, spot.Y));
                }
            }
        }

        // 플레이어 위치만 세팅(생성은 필요시)
        //var player = _stageObj.transform.Find("Player");
        //var playerPos = BattleHelper.ToFp2(player.position.x, player.position.y);
        //field.PlayerSpawnPos = playerPos;
        field.PlayerSpawnPos = pos;
        if (string.IsNullOrEmpty(stageDB.CharacterSpotName) == false)
        {
            F64Vec2 playerPos = pos;
            if (field.GetSpot(stageDB.CharacterSpotName, ref playerPos) == true)
            {
                field.PlayerSpawnPos = playerPos;
            }
        }

        // 스포너
        if (stageDB.Monster1 > 0 && stageDB.MonsterNum1 > 0)
        {
            if (GameDBContainer.Instance.MonsterList.TryGetValue(stageDB.Monster1, out Monster monsterDB) == true)
            {
                var entityId = field.GenerateEntityId();
                var spawner = new DungeonSpawner(stageDB.MonsterNum1, false, stageDB.Monster1, entityId, field, 2, monsterDB.UnitPrefab);
                field.AddSpawner(spawner);
            }
        }
        if (stageDB.Monster2 > 0 && stageDB.MonsterNum2 > 0)
        {
            if (GameDBContainer.Instance.MonsterList.TryGetValue(stageDB.Monster2, out Monster monsterDB) == true)
            {
                var entityId = field.GenerateEntityId();
                var spawner = new DungeonSpawner(stageDB.MonsterNum2, false, stageDB.Monster2, entityId, field, 2, monsterDB.UnitPrefab);
                field.AddSpawner(spawner);
            }
        }
        if (stageDB.Monster3 > 0 && stageDB.MonsterNum3 > 0)
        {
            if (GameDBContainer.Instance.MonsterList.TryGetValue(stageDB.Monster3, out Monster monsterDB) == true)
            {
                var entityId = field.GenerateEntityId();
                var spawner = new DungeonSpawner(stageDB.MonsterNum3, false, stageDB.Monster3, entityId, field, 2, monsterDB.UnitPrefab);
                field.AddSpawner(spawner);
            }
        }

        // 보스
        if (stageDB.BossMonster > 0)
        {
            if (GameDBContainer.Instance.MonsterList.TryGetValue(stageDB.BossMonster, out Monster monsterDB) == true)
            {
                var entityId = field.GenerateEntityId();
                var spawner = new DungeonSpawner(1, true, stageDB.BossMonster, entityId, field, 2, monsterDB.UnitPrefab);
                field.AddSpawner(spawner);

                // 스포너 위치를 Spot위치로 재조정해본다
                if (string.IsNullOrEmpty(stageDB.BossSpotName) == false)
                {
                    F64Vec2 bossPos = F64Vec2.Zero;
                    if (field.GetSpot(stageDB.BossSpotName, ref bossPos) == true)
                    {
                        spawner.ResetIsSpawned(bossPos);
                    }
                }
            }
        }

        if (stageDB.Package == EStagePackage.WeaponUpgradeStage ||
            stageDB.Package == EStagePackage.ArmorUpgradeStage ||
            stageDB.Package == EStagePackage.AccessoryUpgradeStage) // 승급 재료 던전은 보스를 즉시 스폰해준다. (UI 연결 위한...)
        {
            field.EachSpawners((DungeonSpawner spawner) =>
            {
                if (spawner.IsBoss)
                {
                    field.SpawnMonster(spawner._desiredPos, spawner);
                }
                else
                {
                    spawner.IsActive = true;
                }
            });
        }
        else
        {
            field.EachSpawners((DungeonSpawner spawner) =>
            {
                spawner.IsActive = true;
            });
        }

        return field;
    }

    private void OnCreateField(GameDB.GrowthStage stageDB, List<uint> heroTids)
    {
        if (_field == null)
        {
            return;
        }

        PlaceHeroes(heroTids);

        //캐릭터 컨트롤 선택 0번으로 초기화
        OnChangeSelectedHeroIndex(0);

        GameScene.GameScene_MainUI.i.UI_BottomStageHUD.Init(_field, GetHeroesByTid(heroTids), true);

        RefreshCamera();
        RefreshSkillPanel();

        _curDungeonInfo.OnCreateField(_field, stageDB);
    }

    // 파병 리스트 함수
    private void PlaceHeroes(List<uint> heroTids)
    {
        // 현재 필드상태가 진입가능한지 체크
        if (_field == null)
        {
            return;
        }

        _enterHeroes.Clear();

        PlayerModel playerModel = SgModel.Instance.GetPlayer(PlayerNo);
        if (playerModel != null)
        {
            int index = 0;
            foreach (var heroTid in heroTids)
            {
                var heroInfo = playerModel.GetHeroModel(heroTid);
                if (heroInfo == null)
                {
                    continue;
                }
                var rotatedPos = BattleHelper.GetSampleRotatePos(F64.FromInt(1), (int)index, 5);
                var pos = _field.PlayerSpawnPos + rotatedPos;
                var playerEntity = _field.CommonSpawnPlayer(playerModel, heroInfo, pos);
                _field.RefreshHeroInfo(heroInfo);

                index++;

                if(playerEntity == null)
                {
                    DebugManager.LogError("playerEntity is null");
                    continue;
                }

                _enterHeroes.Add(playerEntity.PlayerHeroId);
            }
        }
    }

    // 영웅들 필드에서 탈출시킨다
    public void EscapeAllHeroes()
    {
        if (_isPlayingEvent)
        {
            return;
        }
        _field.EscapeHeroesField();
        _enterHeroes.Clear();

        GameScene.GameScene_MainUI.i.UI_BottomStageHUD.Release();

        LeaveDungeon();
    }

    public void FinishGame()
    {
        _field.EscapeHeroesField();
        _enterHeroes.Clear();

        // 필드 비활성화
        _curDungeonInfo.OnLeaveGame();
    }

    public void OnEntitySpawned(BaseEntity entity)
    {
        var field = entity.GetField();
        if (field == null)
        {
            DebugManager.LogError("No Field");
            return;
        }

        if (GameDB.GameDBContainer.Instance.HeroList.TryGetValue(entity.Tid, out GameDB.Hero heroTable) == false)
        {
            DebugManager.LogError("No hero Tid:" + entity.Tid);
            return;
        }

        //BaseEntityMono prefab = AssetFiles.LoadAsset<BaseEntityMono>(heroTable.EntityPrefab);
        //if (prefab == null)
        //{
        //    return;
        //}

        var unit = PoolController.i.Spawn<BaseEntityMono>(field.FieldName, heroTable.EntityPrefab, SpawnRoot.i.Get((SpawnRoot.EMode)field.SpawnRoot, SpawnRoot.EType.Root_Unit));
        if (unit == null)
        {
            DebugManager.LogError("Failed to Instantiate Entity, EntityId:" + entity.Id);
            return;
        }

        unit.Init(field, entity);
        unit.UpdateLookDir(F64Vec2.Right);
        entity.SetState(BaseEntityState.Spawn);
    }

    public void RefreshHeroInfo(HeroInfo heroInfo)
    {
        if (_field == null)
        {
            return;
        }

        _field.RefreshHeroInfo(heroInfo);
    }

    public void OnMonsterSpawned(BaseEntity monster, SpawnerBase spawner)
    {
        var field = monster.GetField();
        if (field == null)
        {
            DebugManager.LogError("No Field");
            return;
        }

        //var prefab = AssetFiles.LoadAsset<BaseEntityMono>(spawner.ResourceName);
        //if (prefab == null)
        //{
        //    DebugManager.LogError("Failed to Instantiate raidMonster, PefabName:" + spawner.ResourceName);
        //    return;
        //}

        var unit = PoolController.i.Spawn<BaseEntityMono>(field.FieldName, spawner.ResourceName, SpawnRoot.i.Get((SpawnRoot.EMode)field.SpawnRoot, SpawnRoot.EType.Root_Unit));
        if (unit == null)
        {
            DebugManager.LogError("Failed to Instantiate raidMonster, EntityId:" + monster.Id);
            return;
        }

        monster.RaidEntityType = unit.RaidEntityType;

        unit.Init(field, monster);
        monster.SetState(BaseEntityState.Spawn);
        if (monster.IsBoss)
        {
            StartCoroutine(NoShowSpineSetupPose(unit));
        }
    }

    /// <summary>
    /// 생성시 SetupPose가 1프레임 보이는 현상 때문에 1프레임 정도 생성 위치를 옮겼다가 오도록 함
    /// </summary>
    private IEnumerator NoShowSpineSetupPose(BaseEntityMono baseEntityMono)
    {
        Vector3 orgPos = baseEntityMono.transform.position;
        baseEntityMono.transform.position = Vector3.one * 1000f;
        yield return new WaitForEndOfFrame();
        baseEntityMono.transform.position = orgPos;
    }

    public void OnHitEntity(BaseEntity entity, uint casterEntityId, BigDecimal damage)
    {
        _curDungeonInfo.OnHitEntity(entity, casterEntityId, damage);
    }

    public void OnDieEntity(BaseEntity entity)
    {
        if (entity.IsSummon)
            return;

        _curDungeonInfo.OnDieEntity(entity);

        if (!entity.IsMonster)
        {
            OnSelectNextHero();
        }
    }

    public void OnStealGold()
    {
        _curDungeonInfo.OnStealGold();
    }

    #endregion 필드

    #region 입력

    public void OnInputMove(Vector2 dir, bool isStop)
    {
        if (isStop)
        {
            _moveInput = EMoveInputMode.STOP;
        }
        else
        {
            _moveInput = EMoveInputMode.MOVE;
            _moveDir = BattleHelper.ToFp2(dir.x, dir.y);
            _moveDir = F64Vec2.Normalize(_moveDir);
        }
    }

    public void InputUseSkill(uint heroTid, uint skillTid)
    {
        _reserveSkillIndex = skillTid;
    }

    #endregion 입력

    #region 던전 이벤트

    public async void OnResult(uint growthStageTid, bool isWin, double value1, double value2)
    {
        // 시너지 효과를 제거해준다
        SgModel.Instance.GetPlayer(_playerNo).ClearSynergyBuff(_lastHeroTids);

        Invoke("FinishGame", 1f);

        GameScene.GameScene_MainUI.i.UI_BottomStageHUD.Release();

        await SendReqFinishGrowthDungeon(growthStageTid, isWin, value1, value2, _lastHeroTids);
    }

    public void OnSpawnEvent()
    {
        if (_enterHeroes.Count == 0)
        {
            // 연출필요없다
            _curDungeonInfo.OnStartGame();
            return;
        }

        // 연출시작
        StartCoroutine(OnSpawnCoroutine());
    }

    private IEnumerator OnSpawnCoroutine()
    {
        _isPlayingEvent = true;

        foreach (var playerHeroId in _enterHeroes)
        {
            var heroEntity = _field.GetPlayerHeroEntity(playerHeroId);
            var heroMono = heroEntity.GetMonoEntity();
            heroMono.gameObject.SetActive(false);
        }

        _gameController.SetCameraMoveActive(false);

        foreach (var playerHeroId in _enterHeroes)
        {
            var heroEntity = _field.GetPlayerHeroEntity(playerHeroId);
            var heroMono = heroEntity.GetMonoEntity();
            heroMono.gameObject.SetActive(true);
            //heroMono.OnSpawnEntity();
            SpawnHero(heroEntity, heroMono);
        }

        var popup = PopupManager.i.ShowPopup<Popup_Logo_Transition>(Global.ePopupPrefab.Popup_Logo_Transition);
        if (popup != null)
        {
            popup.CallEnd();
        }

        yield return new WaitForSeconds(1.5f);

        _gameController.SetCameraMoveActive(true);

        _curDungeonInfo.OnStartGame();

        _isPlayingEvent = false;

        yield break;
    }

    private void SpawnHero(BaseEntity heroEntity, BaseEntityMono heroMono)
    {
        var screenSize = GameScene.GameScene_MainUI.i.UI_DirectionBattle.GetCanvasSize();

        heroMono.enabled = false;
        var animBase = heroMono.GetComponentInChildren<AnimBase>(true);
        //animBase.transform.eulerAngles = Vector3.zero;
        animBase.SetAnimation(BaseEntityState.Move, heroEntity);
        heroEntity.SetState(BaseEntityState.Move);

        var pos = heroMono.transform.position;
        var fromPos = new Vector3(pos.x - (screenSize.x * 0.0075f), pos.y, pos.z);
        heroEntity.GetMonoEntity().transform
            .DOMove(pos, 1.5f, false)
            .From(fromPos, true)
            .SetRelative(false)
            .SetAutoKill(true)
            .SetEase(Ease.Linear)
            //.SetSpeedBased()
            .OnStepComplete(() => { heroMono.enabled = true; heroEntity.SetState(BaseEntityState.Idle); })
            .PlayForward();
    }

    #endregion 던전 이벤트

    #region 패킷

    public async void CheckGrowthPacketInfo()
    {
        await SendReqGetGrowthDungeon();
    }

    public async Task SendReqGetGrowthDungeon()
    {
        var (err, ack) = await NetworkHubManager.i.BlockingSendAsync<AckGetGrowthDungeon>("ReqGetGrowthDungeon");
        if (err != RESPONSE_ERR.None)
        {
            DebugManager.LogError(string.Format("'<color=green>SendReqGetGrowthDungeon</color>' error: '<color=red>{0}</color>'", err));
            return;
        }

        // 1. 현재 진행월 EndTimeStamp
        // 2. 클리어 업적 보상진행도(0부터 시작해 OrderId별로 증가)
        // 3. 각 카테고리(던전)별 정보( category Tid, 진행도, 히든정보 오픈 Bit )
        _growthPacketInfo = ack.GrowthInfo;

        // 1. 월별 리셋될때 초기화되는  재화 던전 업적 열쇠(아이템 tid : 17001)
        // 2. 일일 초기화시간 지날때, 각 카테고리(던전별)로 지급되는 입장권 3종류
        _resourceController.ProcessReward(ack.RewardInfo);

        // 리셋 타임
        if (_corUpdate != null)
        {
            StopCoroutine(_corUpdate);
            _corUpdate = null;
        }

        _resetTime = ack.GrowthInfo.GrowthDailyReceivedTime;

        long now = SgTime.I.Now;
        if (_resetTime > now)
        {
            _corUpdate = StartCoroutine(CorUpdate());
        }

        _selectPanel.Refresh();

        // 디버깅용으로 필요한 것 출력해봄
        //{
        //    // 던전(카테고리)별 정보
        //    foreach (var categoryInfo in ack.GrowthInfo.GrowthInfos)
        //    {
        //        if (GameDBContainer.Instance.GrowthCategoryList.TryGetValue(categoryInfo.CategoryTid, out GrowthCategory categorySheet) == false)
        //        {
        //            DebugManager.LogError("No GrowthCategory, tid:" + categoryInfo.CategoryTid);
        //            continue;
        //        }

        //        // 현재 도전하려는 진행중인 던전별 입장권 종류/갯수
        //        // ProgressOrderId는 클리어한 OrderId를 의미한다(0인경우는 아무것도 미클리어)
        //        var challengeDungeonOrder = categoryInfo.ProgressOrderId + 1;
        //        var stageDB = GameDBMapper.Instance.GetGrowthStage(categorySheet.GroupId, challengeDungeonOrder);
        //        if (stageDB == null)
        //        {
        //            DebugManager.LogError("No GrowthStageDB in orderId : " + challengeDungeonOrder);
        //            continue;
        //        }

        //        // 던전별 challengeDungeonOrder * 10 이 각 던전별 받은 클리어 업적 갯수와 동일하다
        //        /*
        //        var clearAchievement = challengeDungeonOrder * 10;
        //        DebugManager.Log("던전 클리어 업적: " + clearAchievement);

        //        var itemCount = _resourceController.GetItemCount((uint)stageDB.NeedResourceTid);
        //        DebugManager.Log(string.Format("{0} type, Enter Key Count : {1}", categorySheet.PackageType.ToString(), itemCount));
        //        */
        //    }
        //}
    }

    public async Task<bool> SendReqStartGrowthDungeon(uint growthStageTid, List<uint> heroTids, bool isRetry)
    {
        var infos = new List<PDungeonHeroInfo>();
        foreach (var tid in heroTids)
        {
            double totalPower = 0.0;
            var heroModel = SgModel.i.LocalPlayer.GetHeroModel(tid);
            if (heroModel != null)
            {
                totalPower = heroModel.TotalPower;
            }

            infos.Add(new PDungeonHeroInfo
            {
                HeroTid = tid,
                BattlePoint = totalPower
            });
        }

        var (err, ack) = await NetworkHubManager.i.BlockingSendAsync<AckStartGrowthDungeon>("ReqStartGrowthDungeon", new ReqStartGrowthDungeon
        {
            GrowthDungeonTid = growthStageTid,
            DungeonHeroInfos = infos
        });

        if (err != RESPONSE_ERR.None)
        {
            var popup = PopupManager.i.ShowPopup<Popup_Logo_Transition>(Global.ePopupPrefab.Popup_Logo_Transition);
            if (popup != null)
            {
                popup.CallEnd();
            }
            LeaveDungeon();
            DebugManager.LogError(string.Format("'<color=green>SendReqStartGrowthDungeon</color>' error: '<color=red>{0}</color>'", err));
            return false;
        }

        if (ack == null)
        {
            var popup = PopupManager.i.ShowPopup<Popup_Logo_Transition>(Global.ePopupPrefab.Popup_Logo_Transition);
            if (popup != null)
            {
                popup.CallEnd();
            }
            LeaveDungeon();
            DebugManager.LogError(string.Format("'<color=green>SendReqStartGrowthDungeon</color>' error: '<color=red>{0}</color>'", "ack is null"));
            return false;
        }

        // 1. 현재 진행월 EndTimeStamp
        // 2. 클리어 업적 보상진행도(0부터 시작해 OrderId별로 증가)
        // 3. 각 카테고리(던전)별 정보( category Tid, 진행도, 히든정보 오픈 Bit )
        _growthPacketInfo = ack.GrowthInfo;

        // 1. 월별 리셋될때 초기화되는  재화 던전 업적 열쇠(아이템 tid : 17001)
        // 2. 일일 초기화시간 지날때, 각 카테고리(던전별)로 지급되는 입장권 3종류
        // 3. 소모한 티켓관련
        _resourceController.ProcessReward(ack.RewardInfo);

        _playerNo = ack.MyPlayerInfo.PlayerNo;
        PlayerModel playerModel = SgModel.Instance.AddPlayer(ack.MyPlayerInfo, Global.EGameModeType.Dungeon);

        // 스킬 프리셋 세팅
        //var skillPreset = (uint)PlayerPrefsX.GetInt(DUNGEON_SKILL_PRESET_KEY, 1);
        //playerModel.RefreshAllSkillPreset(skillPreset);

        // 운명 프리셋 세팅
        //for (int i = 0; i < heroTids.Count; i++)
        //{
        //    var key = string.Format("{0}_{1}", DUNGEON_CUBE_PRESET_KEY, heroTids[i]);
        //    var cubePreset = (byte)PlayerPrefsX.GetInt(key, 1);
        //    playerModel.OnChangeCubePreset(heroTids[i], cubePreset);
        //}

        // 시너지 효과 갱신
        playerModel.RefreshSynergyBuff(heroTids);

        OpenDungeon(growthStageTid, heroTids, isRetry);

        return true;
    }

    public async Task<bool> SendReqFinishGrowthDungeon(uint growthStageTid, bool isWin, double value1, double value2, List<uint> heroTids, bool isSweep = false)
    {
        DebugManager.Log("ReqFinishGrowthDungeon tid:" + growthStageTid + ", isWin:" + isWin.ToString() + ", value1:" + value1 + ", value2:" + value2);

        if (GameDBContainer.Instance.GrowthStageList.TryGetValue(growthStageTid, out GameDB.GrowthStage stageSheet) == false)
        {
            return false;
        }

        if (isSweep)
        {
            RefreshLastInfo(stageSheet, heroTids);
        }

        var infos = new List<PDungeonHeroInfo>();
        foreach (var tid in heroTids)
        {
            double totalPower = 0.0;
            var heroModel = SgModel.i.LocalPlayer.GetHeroModel(tid);
            if (heroModel != null)
            {
                totalPower = heroModel.TotalPower;
            }

            infos.Add(new PDungeonHeroInfo
            {
                HeroTid = tid,
                BattlePoint = totalPower
            });
        }

        var (err, ack) = await NetworkHubManager.i.BlockingSendAsync<AckFinishGrowthDungeon>("ReqFinishGrowthDungeon", new ReqFinishGrowthDungeon
        {
            GrowthDungeonTid = growthStageTid,
            IsWin = isWin,
            Value1 = value1,
            Value2 = value2,
            DungeonHeroInfos = infos
        });

        if (err != RESPONSE_ERR.None)
        {
            if (!isSweep)
                LeaveDungeon();
            DebugManager.LogError(string.Format("'<color=green>SendReqFinishGrowthDungeon</color>' error: '<color=red>{0}</color>'", err));
            return false;
        }

        if (ack == null)
        {
            if (!isSweep)
                LeaveDungeon();
            DebugManager.LogError(string.Format("'<color=green>SendReqFinishGrowthDungeon</color>' error: '<color=red>{0}</color>'", "ack is null"));
            return false;
        }

        // 1. 현재 진행월 EndTimeStamp
        // 2. 클리어 업적 보상진행도(0부터 시작해 OrderId별로 증가)
        // 3. 각 카테고리(던전)별 정보( category Tid, 진행도, 히든정보 오픈 Bit )
        _growthPacketInfo = ack.GrowthInfo;

        // 1. 월별 리셋될때 초기화되는  재화 던전 업적 열쇠(아이템 tid : 17001)
        // 2. 일일 초기화시간 지날때, 각 카테고리(던전별)로 지급되는 입장권 3종류
        // 3. 받은 보상
        _resourceController.ProcessReward(ack.RewardInfo);

        if (isSweep)
        {
            if (stageSheet.Package == EStagePackage.GoldStage || stageSheet.Package == EStagePackage.ExpStage)
            {
                Popup_DungeonResult popup = PopupManager.i.ShowPopup<Popup_DungeonResult>(Global.ePopupPrefab.Popup_DungeonResult);
                if (popup)
                {
                    popup.Show(growthStageTid, 0, 0, 0, true, ack.RewardInfo.RewardInfos, heroTids, true);
                }
            }
            else
            if (stageSheet.Package == EStagePackage.WeaponUpgradeStage ||
                stageSheet.Package == EStagePackage.ArmorUpgradeStage ||
                stageSheet.Package == EStagePackage.AccessoryUpgradeStage)
            {
                Popup_DungeonResult popup = PopupManager.i.ShowPopup<Popup_DungeonResult>(Global.ePopupPrefab.Popup_DungeonResult);
                if (popup)
                {
                    popup.Show(growthStageTid, 0, 0, 0, true, ack.RewardInfo.RewardInfos, heroTids, true); // UR 등급을 기록해야 승리 2023.3.8
                }
            }
        }
        else
        {
            _curDungeonInfo.OnResult(ack.RewardInfo.RewardInfos);
        }

        if (isSweep)
            _selectPanel.Refresh();

        foreach (var rewardInfo in ack.RewardInfo.RewardInfos)
        {
            DebugManager.Log("   reward Type:" + rewardInfo.DeltaType.ToString() + ", tid:" + rewardInfo.Target + ", delta:" + rewardInfo.Delta);
        }

        AnalyticsManager.i.ClearGrowthStage(growthStageTid);

        return true;
    }

    #endregion 패킷

    #region 갱신

    private IEnumerator CorUpdate()
    {
        while (true)
        {
            long now = SgTime.I.Now;

            if (_resetTime < now)
            {
                CheckGrowthPacketInfo();
                yield break;
            }

            yield return _wait;
        }
    }

    #endregion 갱신

    #region PlayerPrefsX

    public void SaveEnteredHeroes(int groupId, int slotIndex, uint heroTid)
    {
        string strKey = string.Format("{0}_{1}_{2}", DUNGEON_ENTERED_HEROES_KEY, groupId, slotIndex);
        PlayerPrefsX.SetInt(strKey, (int)heroTid);
    }

    public uint LoadEnteredHeroes(int groupId, int slotIndex)
    {
        string strKey = string.Format("{0}_{1}_{2}", DUNGEON_ENTERED_HEROES_KEY, groupId, slotIndex);
        if (PlayerPrefsX.HasKey(strKey) == false)
        {
            PlayerPrefsX.SetInt(strKey, 0);
        }
        return (uint)PlayerPrefsX.GetInt(strKey, 0);
    }

    #endregion PlayerPrefsX

    private void RefreshLastInfo(GameDB.GrowthStage stageSheet, List<uint> heroTids)
    {
        _lastStageTid = stageSheet.tid;
        _lastHeroTids.Clear();

        for (int i = 0; i < heroTids.Count; i++)
        {
            _lastHeroTids.Add(heroTids[i]);
        }

        for (int i = 0; i < 3; i++)
        {
            if (i < heroTids.Count)
            {
                SaveEnteredHeroes(stageSheet.GroupId, i, heroTids[i]);
            }
            else
            {
                SaveEnteredHeroes(stageSheet.GroupId, i, 0);
            }
        }
    }
}