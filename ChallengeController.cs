using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GameDB;
using GameScene;
using DG.Tweening;
using ChallengeDef;
using diverseshared;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;

public class ChallengeController : MonoBehaviour
{
    //public static readonly string CHALLENGE_SKILL_PRESET_KEY = "ChallengeSkillPreset";

    //public static readonly string CHALLENGE_CUBE_PRESET_KEY = "ChallengeCubePreset";

    public static readonly string CHALLENGE_PLAY_SPEED_KEY = "ChallengePlaySpeed";

    public static readonly int CHALLENGE_BATTLE_HERO_COUNT = 3;

    public static readonly float CHALLENGE_CAMERA_ORTHO_SIZE = 14f;

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
    private ArenaPanel _arenaPanel;

    public ArenaPanel MainPanel { get { return _arenaPanel; } }

    private long _resetTime = 0;
    private Coroutine _corUpdate = null;
    private WaitForSeconds _wait = new WaitForSeconds(1f);

    public ulong PlayerNo { get; set; }

    public ulong TargetPlayerNo { get; set; }

    public int TargetIndex { get; set; }

    public uint TargetDummyTid { get; set; }

    private ChallengeField _field { get; set; }

    public ChallengeField Field => _field;

    private List<uint> _packetHeroTids = new List<uint>();
    private List<uint> _packetTargetHeroTids = new List<uint>();
    public List<uint> PacketHeroTids
    { get { return _packetHeroTids; } }

    public List<uint> PacketTargetHeroTids
    { get { return _packetTargetHeroTids; } }

    // 진입한 영웅리스트(ulong, Stage)
    private List<ulong> _enterHeroes = new List<ulong>();

    public bool GetEnterHeroTids(ref List<uint> tids)
    {
        if (tids == null)
        {
            return false;
        }
        foreach (var playerHeroId in _enterHeroes)
        {
            var playerEntity = _field.GetPlayerHeroEntity(playerHeroId);
            tids.Add(playerEntity.Tid);
        }
        return true;
    }

    // 다음투입 영웅인덱스
    private int _nextHeroIndex = 0;

    // 상대 영웅리스트(tid, Stage)
    private List<ulong> _enterTargetHeroes = new List<ulong>();

    // 다음투입 상대 영웅인덱스
    private int _nextTargetHeroIndex = 0;

    // 현재까지 받아서 진행된 틱
    private int _progressTick = 0;

    public int ProgressTick => _progressTick;

    private float curSec = 0.0f;

    private bool _isTick = false;

    private ChallengeInfo _challengeInfo = null;

    // 배속 모드
    private int _speed = 1;

    public int Speed
    { get { return _speed; } }

    // 스킵 모드
    private bool _isSkip = false;

    public bool IsSkip
    { get { return _isSkip; } }

    // 플레이한 리그
    private int _currentWeek = 0;

    // 현재 스코어
    private long _currentScore = 0;
    public long CurrentScore { get { return _currentScore; } }

    // 타겟 정보
    private PChallengeTargetInfo _targetInfo = null;

    public PChallengeTargetInfo TargetInfo { get { return _targetInfo; } }

    private Dictionary<uint, PChallengeTargetHeroInfo> _targetHeroInfos = null;
    public Dictionary<uint, PChallengeTargetHeroInfo> TargetHeroInfos { get { return _targetHeroInfos; } }

    // 체력값 캐싱
    private Dictionary<uint, BigDecimal> _playerRemainHPInfos = new Dictionary<uint, BigDecimal>();
    private BigDecimal _playerTotalMaxHP;

    private Dictionary<uint, BigDecimal> _targetRemainHPInfos = new Dictionary<uint, BigDecimal>();
    private BigDecimal _targetTotalMaxHP;

    private void Awake()
    {

    }

    private void Update()
    {
        // 아직 세팅전일때는 돌면 안된다
        if (GameController.CoolSecond < 0.00001f)
        {
            return;
        }

        if (_field == null || _challengeInfo == null)
        {
            return;
        }

        if (_isTick == false)
        {
            return;
        }

        if (_isSkip)
        {
            while (_challengeInfo.GetState() != EChallengeState.EndChallenge)
            {
                ProcessTick_Skip();
            }

            // 스킵 후 지저분하지 않도록 정리
            _field.ResetField();
        }
        else
        {
            if (_speed > 1)
            {
                for (int i = 0; i < _speed; i++)
                {
                    ProcessTick();
                }
            }
            else
            {
                ProcessTick();
            }
        }
    }

    private void ProcessTick()
    {
        curSec += Time.deltaTime;
        if (curSec < GameController.CoolSecond)
        {
            return;
        }
        curSec -= GameController.CoolSecond;

        _progressTick = _progressTick + 1;

        // challenge는 조작이 없다

        // Tick
        _challengeInfo.OnTick(_progressTick, GameController.Delta);

        //GameScene_MainUI.i.UI_BottomStageHUD.OnTick();
    }

    private void ProcessTick_Skip()
    {
        _progressTick = _progressTick + 1;

        // challenge는 조작이 없다

        _challengeInfo.OnTick(_progressTick, GameController.Delta);

        //GameScene_MainUI.i.UI_BottomStageHUD.OnTick();
    }

    public void StartChallenge(int targetIndex, PChallengeTargetInfo targetInfo, Dictionary<uint, PChallengeTargetHeroInfo> targetHeroInfos)
    {
        _targetInfo = targetInfo;
        _targetHeroInfos = targetHeroInfos;

        TargetIndex = targetIndex;
        TargetPlayerNo = targetInfo.DummyTid == 0 ? targetInfo.PlayerNo : GamePlayData.i.UserAccount.PlayerNo + targetInfo.DummyTid;
        TargetDummyTid = targetInfo.DummyTid;

        _challengeInfo = new ChallengeInfo(this);

        GameController.i.Idle.SetIdleMode(false);
        PopupManager.i.ShowPopup_Logo_Transition(() =>
        {
            GameController.i.StartChallenge();
            SendReqStartChallenge(TargetIndex, TargetPlayerNo, TargetDummyTid);
        }, 0.2f, null, -1);
    }

    public void RetryChallenge()
    {
        SendReqStartChallenge(TargetIndex, TargetPlayerNo, TargetDummyTid);
    }

    public void ExitChallenge()
    {
        if (_challengeInfo.GetState() != EChallengeState.PlayingChallenge)
        {
            return;
        }

        string title = MultiLanguageManager.i.GetText("ResourceStage_UI_Popup_Name0"); // 알림
        string content = MultiLanguageManager.i.GetText("ResourceStage_UI_Popup_Desc0"); // 전투를 포기하고 정말 나가시겠습니까?

        PopupManager.i.ShowPopup_YesNo(title, content, EscapeAllHeroes, null);
    }

    public void LeaveChallenge()
    {
        _enterHeroes.Clear();
        _enterTargetHeroes.Clear();

        DestroyField();

        if (_field != null)
        {
            PoolController.i.DespawnAll(_field.FieldName);
        }

        GameController.i.LeaveChallenge();
        BattleCameraDirector.i.RevertCameraOrthoSize();
        GameScene.GameScene_MainUI.i.TabToggle(GameScene.GameScene_MainUI.eBottomToggle.Home);
        GameScene.GameScene_MainUI.i.UI_BottomMenu.SetViewArenaPanel(true);
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

        if (entity.GetFaction() == 1)
        {
            unit.UpdateLookDir(F64Vec2.Right);
        }
        else
        {
            unit.UpdateLookDir(F64Vec2.Left);
        }

        entity.SetState(BaseEntityState.Spawn);
    }

    public void OnDieEntity(BaseEntity entity)
    {
        if (entity.IsSummon)
            return;

        _challengeInfo.OnDieEntity(entity);
    }

    public void DestroyField()
    {
        if (_field != null)
        {
            _field.DestroyField();
        }

        _challengeInfo = null;
        _isTick = false;

        if(_gameController)
            _gameController.SetCameraMoveActive(true);
    }

    private async Task PrepareField()
    {
        if (_field != null)
        {
            _field.DestroyField();
        }

        // 필드 생성
        F64Vec2 pos = F64Vec2.FromInt(400, 0);
        _field = await CreateFieldAsync(pos); 
    }

    private async Task<ChallengeField> CreateFieldAsync(F64Vec2 pos)
    {
        var mapPath = "BattleStage/PVP_Arena.prefab";
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
        var field = new ChallengeField(0, this, _fieldMono);
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

        // 플레이어 위치
        field.PlayerSpawnPos = pos;
        F64Vec2 playerPos = pos;
        if (field.GetSpot("PlayerSpot", ref playerPos) == true)
        {
            field.PlayerSpawnPos = playerPos;
        }

        // 타겟 플레이어 위치
        field.TargetSpawnPos = pos;
        F64Vec2 targetPos = pos;
        if (field.GetSpot("TargetSpot", ref targetPos) == true)
        {
            field.TargetSpawnPos = targetPos;
        }

        // 카메라 고정
        var centerObj = _stageObj.transform.Find("Center");
        if (centerObj != null)
        {
            _gameController.SetCameraFollow(centerObj.gameObject, field.CameraBound.Item1, field.CameraBound.Item2);
            _gameController.SetCameraMoveActive(false);

            //22.12.30 배경 카메라 타겟 갱신
            if (BGControllerSettings.Verify())
                BGControllerSettings.i.RefreshCamTransform();
            //--
        }

        return field;
    }

    private void OnCreateField()
    {
        if (_field == null)
        {
            return;
        }

        GlobalMusicManager.i.PlayMusic("BGM_Arena");

        int index = 0;

        // 첫 번째 영웅 투입
        _nextHeroIndex = CHALLENGE_BATTLE_HERO_COUNT;

        foreach (var heroTid in _packetHeroTids)
        {
            var playerModel = SgModel.Instance.GetPlayer(PlayerNo);
            if (playerModel == null)
            {
                DebugManager.LogError("No PlayerModel: " + PlayerNo);
                continue;
            }
            var heroInfo = playerModel.GetHeroModel(heroTid);
            if (heroInfo != null)
            {
                PlaceHeroToField(1, index, playerModel, heroInfo);
            }

            index++;

            if (index == CHALLENGE_BATTLE_HERO_COUNT)
                break;
        }

        index = 0;

        // 상대방의 첫 번째 영웅 투입
        _nextTargetHeroIndex = CHALLENGE_BATTLE_HERO_COUNT;

        foreach (var heroTid in _packetTargetHeroTids)
        {
            var playerModel = SgModel.Instance.GetPlayer(TargetPlayerNo);
            if (playerModel == null)
            {
                DebugManager.LogError("No PlayerModel: " + TargetPlayerNo);
                continue;
            }
            var targetHeroInfo = playerModel.GetHeroModel(heroTid);
            if (targetHeroInfo != null)
            {
                PlaceHeroToField(2, index, playerModel, targetHeroInfo);
            }

            index++;

            if (index == CHALLENGE_BATTLE_HERO_COUNT)
                break;
        }

        //var entityList = GetHeroEntityList(1);
        //if (entityList != null && entityList.Count > 0)
        //{
        //    GameScene_MainUI.i.UI_BottomStageHUD.Init(_field, entityList, true, true);
        //    GameScene_MainUI.i.UI_BottomStageHUD.Set(entityList[0].Tid, true);
        //}

        GameScene_MainUI.i.UI_BottomStageHUD.Release();

        // 필드 데미지로그 활성
        _field.OnStartBattleForLog();

        // 제한 시간
        _challengeInfo.OnCreateField(_field, GameDBContainer.Instance.config.ArenaFightTime);
    }

    // 한명 파병함수(내부)
    private void PlaceHeroToField(int faction, int index, PlayerModel playerModel, HeroInfo heroInfo)
    {
        // 현재 필드상태가 진입가능한지 체크
        if (_field == null)
        {
            return;
        }

        var playerHeroId = _field.GetPlayerHeroId(heroInfo.PlayerId, heroInfo.Tid);

        // 해당필드에 이미 해당 영웅이 있으면 할 것이 없다
        if (_field.IsInField(playerHeroId) == true)
        {
            return;
        }

        // 플레이어 필드 진입
        // Player스폰
        var rotatedPos = BattleHelper.GetSampleRotatePos(F64.FromInt(1), index, CHALLENGE_BATTLE_HERO_COUNT);
        if (faction == 2) rotatedPos.X = -rotatedPos.X;
        var pos = (faction == 1 ? _field.PlayerSpawnPos : _field.TargetSpawnPos) + rotatedPos;
        var playerEntity = _field.CommonSpawnPlayer(playerModel, heroInfo, pos, faction);

        _field.RefreshHeroInfo(heroInfo);

        var heroMono = playerEntity.GetMonoEntity();
        if (heroMono != null)
        {
            heroMono.gameObject.SetActive(false);
            heroMono.gameObject.SetActive(true);

            // 스폰 이펙트 붙인다.
            heroMono.gameObject.AddComponent<BossSpawnEffect>();
        }

        if (faction == 1)
        {
            _enterHeroes.Add(playerEntity.PlayerHeroId);
            _playerRemainHPInfos.Remove(heroInfo.Tid);
        }
        else
        {
            _enterTargetHeroes.Add(playerEntity.PlayerHeroId);
            _targetRemainHPInfos.Remove(heroInfo.Tid);
        }
    }

    public List<BaseEntity> GetHeroEntityList(int faction)
    {
        List<BaseEntity> result = new List<BaseEntity>();

        if (_field != null)
        {
            if (faction == 1)
            {
                foreach (var playerHeroId in _enterHeroes)
                {
                    var entity = _field.GetPlayerHeroEntity(playerHeroId);
                    if (entity != null && !entity.IsDeadInGame)
                    {
                        result.Add(entity);
                    }
                }
            }
            else if (faction == 2)
            {
                foreach (var playerHeroId in _enterTargetHeroes)
                {
                    var entity = _field.GetPlayerHeroEntity(playerHeroId);
                    if (entity != null && !entity.IsDeadInGame)
                    {
                        result.Add(entity);
                    }
                }
            }
        }

        return result;
    }

    public uint GetDummyHeroTid(uint entryTid)
    {
        var dummyHero = GetDummyHeroInfo(entryTid);
        if (dummyHero != null)
        {
            return dummyHero.Herotid;
        }
        return 0;
    }

    public ChallengeDummyHero GetDummyHeroInfo(uint entryTid)
    {
        if (GameDBContainer.Instance.ChallengeDummyHeroList.TryGetValue(entryTid, out ChallengeDummyHero dummyHero))
        {
            return dummyHero;
        }
        return null;
    }

    public bool IsAllDead(int faction)
    {
        if (faction == 1)
        {
            return _challengeInfo.DeathCount >= _enterHeroes.Count;
        }
        else if (faction == 2)
        {
            return _challengeInfo.KillCount >= _enterTargetHeroes.Count;
        }

        return false;
    }

    public float GetHpRatio(int faction)
    {
        var totalCurHP = BigDecimal.Zero;

        if (faction == 1)
        {
            foreach (var playerHeroId in _enterHeroes)
            {
                var heroEntity = _field.GetPlayerHeroEntity(playerHeroId);
                if (heroEntity != null && heroEntity.CurrentHP > 0)
                {
                    totalCurHP += heroEntity.CurrentHP;
                }
            }
            foreach (var remainHPInfo in _playerRemainHPInfos)
            {
                totalCurHP += remainHPInfo.Value;
            }
            return (float)(totalCurHP / _playerTotalMaxHP);
        }
        else if (faction == 2)
        {
            foreach (var playerHeroId in _enterTargetHeroes)
            {
                var heroEntity = _field.GetPlayerHeroEntity(playerHeroId);
                if (heroEntity != null && heroEntity.CurrentHP > 0)
                {
                    totalCurHP += heroEntity.CurrentHP;
                }
            }
            foreach (var remainHPInfo in _targetRemainHPInfos)
            {
                totalCurHP += remainHPInfo.Value;
            }
            return (float)(totalCurHP / _targetTotalMaxHP);
        }

        return 0f;
    }

    public void Timeout()
    {
        // 승부 판별 (시간이 지나면 패배)
        //_challengeInfo.OnFinish(IsAllDead(2));

        if (IsAllDead(2))
        {
            _challengeInfo.OnFinish(true);
        }
        else
        {
            if (_enterTargetHeroes.Count == _packetTargetHeroTids.Count)
            {
                bool isAllDead = true;
                foreach (var playerHeroId in _enterTargetHeroes)
                {
                    var targetEntity = _field.GetPlayerHeroEntity(playerHeroId);
                    if (targetEntity != null)
                    {
                        if (!targetEntity.IsDeadInGame)
                        {
                            isAllDead = false;
                            break;
                        }
                    }
                }
                _challengeInfo.OnFinish(isAllDead);
            }
            else
            {
                _challengeInfo.OnFinish(false);
            }
        }
    }

    public void FinishGame(bool isWin)
    {
        _isSkip = false;
        _speed = 1;
        SendReqFinishChallenge(TargetIndex, TargetPlayerNo, TargetDummyTid, _currentWeek, isWin);
    }

    // 영웅들 필드에서 탈출시킨다
    public void EscapeAllHeroes()
    {
        if (_field == null)
        {
            return;
        }

        if (_challengeInfo.GetState() != EChallengeState.PlayingChallenge)
        {
            return;
        }

        _field.EscapeHeroesField();

        _challengeInfo.OnFinish(false);
    }

    public void SetSpeed(int speed)
    {
        if (_challengeInfo.GetState() != EChallengeState.PlayingChallenge)
            return;

        _speed = speed;
    }

    public void SetNextSpeed()
    {
        if (_challengeInfo.GetState() != EChallengeState.PlayingChallenge)
            return;

        if (_speed == 1)
            _speed = 2;
        else if (_speed == 2)
            _speed = 4;
        else if (_speed == 4)
            _speed = 1;
    }

    public void Skip()
    {
        if (_isSkip)
            return;

        if (_challengeInfo.GetState() != EChallengeState.PlayingChallenge)
            return;

        _isSkip = true;
    }

    #region 연출

    public void OnSpawnEvent()
    {
        StartCoroutine(OnSpawnCoroutine());
    }

    private IEnumerator OnSpawnCoroutine()
    {
        //foreach (var playerHeroId in _enterHeroes)
        //{
        //    var heroEntity = _field.GetPlayerHeroEntity(playerHeroId);
        //    if (heroEntity != null)
        //    {
        //        var heroMono = heroEntity.GetMonoEntity();
        //        heroMono.gameObject.SetActive(false);
        //    }
        //}

        //foreach (var playerHeroId in _enterTargetHeroes)
        //{
        //    var targetEntity = _field.GetPlayerHeroEntity(playerHeroId);
        //    if (targetEntity != null)
        //    {
        //        var targetMono = targetEntity.GetMonoEntity();
        //        targetMono.gameObject.SetActive(false);
        //    }
        //}

        BattleCameraDirector.i.SetCameraOrthoSize(CHALLENGE_CAMERA_ORTHO_SIZE);

        //foreach (var playerHeroId in _enterHeroes)
        //{
        //    var heroEntity = _field.GetPlayerHeroEntity(playerHeroId);
        //    if (heroEntity != null)
        //    {
        //        var heroMono = heroEntity.GetMonoEntity();
        //        heroMono.gameObject.SetActive(true);
        //        //SpawnHero(heroEntity, heroMono);
        //        //heroEntity.SetState(BaseEntityState.Idle);
        //    }
        //}

        //foreach (var playerHeroId in _enterTargetHeroes)
        //{
        //    var targetEntity = _field.GetPlayerHeroEntity(playerHeroId);
        //    if (targetEntity != null)
        //    {
        //        var targetMono = targetEntity.GetMonoEntity();
        //        targetMono.gameObject.SetActive(true);
        //        //SpawnHero(targetEntity, targetMono);
        //        //targetEntity.SetState(BaseEntityState.Idle);
        //    }
        //}

        var popup = PopupManager.i.ShowPopup<Popup_Logo_Transition>(Global.ePopupPrefab.Popup_Logo_Transition);
        if (popup != null)
        {
            popup.CallEnd();
        }

        yield return new WaitForSeconds(1.5f);

        _isTick = true;
        _challengeInfo.OnStartGame();

        yield break;
    }

    private void SpawnHero(BaseEntity heroEntity, BaseEntityMono heroMono)
    {
        var screenSize = GameScene_MainUI.i.UI_DirectionBattle.GetCanvasSize();

        heroMono.enabled = false;
        var animBase = heroMono.GetComponentInChildren<AnimBase>(true);
        //animBase.transform.eulerAngles = Vector3.zero;
        animBase.SetAnimation(BaseEntityState.Move, heroEntity);
        heroEntity.SetState(BaseEntityState.Move);

        var pos = heroMono.transform.position;
        var fromPos = heroEntity.GetFaction() == 1 ? new Vector3(pos.x - (screenSize.x * 0.0075f), pos.y, pos.z) : new Vector3(pos.x + (screenSize.x * 0.0075f), pos.y, pos.z);
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

    public void NextHero(BaseEntity deadEntity)
    {
        if (IsAllDead(1))
        {
            _challengeInfo.OnFinish(false);
            return;
        }

        if (IsAllDead(2))
        {
            _challengeInfo.OnFinish(true);
            return;
        }

        // 다음 영웅 투입
        if (deadEntity.GetFaction() == 1)
        {
            if (_nextHeroIndex < _packetHeroTids.Count)
            {
                var playerModel = SgModel.Instance.GetPlayer(PlayerNo);
                if (playerModel == null)
                {
                    DebugManager.LogError("No PlayerModel: " + PlayerNo);
                    return;
                }

                var heroTid = _packetHeroTids[_nextHeroIndex];
                var heroInfo = playerModel.GetHeroModel(heroTid);
                if (heroInfo != null)
                {
                    PlaceHeroToField(1, _nextHeroIndex, playerModel, heroInfo);
                }

                _nextHeroIndex++;
            }
        }
        else
        {
            if (_nextTargetHeroIndex < _packetTargetHeroTids.Count)
            {
                var playerModel = SgModel.Instance.GetPlayer(TargetPlayerNo);
                if (playerModel == null)
                {
                    DebugManager.LogError("No PlayerModel: " + TargetPlayerNo);
                    return;
                }

                var heroTid = _packetTargetHeroTids[_nextTargetHeroIndex];

                var targetHeroInfo = playerModel.GetHeroModel(heroTid);
                if (targetHeroInfo != null)
                {
                    PlaceHeroToField(2, _nextTargetHeroIndex, playerModel, targetHeroInfo);
                }

                _nextTargetHeroIndex++;
            }
        }

        _challengeInfo.UpdateHeroList(deadEntity);

        //if (deadEntity.GetFaction() == 1)
        //{
        //    GameScene_MainUI.i.UI_BottomStageHUD.Release();

        //    //하단 기타 캐릭터 슬롯용 처리
        //    var entityList = GetHeroEntityList(1);
        //    if (entityList != null && entityList.Count > 0)
        //    {
        //        GameScene_MainUI.i.UI_BottomStageHUD.Init(_field, entityList, true, true);

        //        foreach (var entity in entityList)
        //        {
        //            if (!entity.IsDeadInGame)
        //            {
        //                GameScene_MainUI.i.UI_BottomStageHUD.Set(entity.Tid, true);
        //                break;
        //            }
        //        }
        //    }
        //}
    }

    #endregion 연출

    #region packet

    public async void CheckChallengePacketInfo(bool isReset = false)
    {
        await SendReqGetChallengeInfo(isReset);
    }

    public async Task SendReqGetChallengeInfo(bool isReset = false)
    {
        var (err, ack) = await NetworkHubManager.i.BlockingSendAsync<AckGetChallengeInfo>("ReqGetChallengeInfo");
        if (err != RESPONSE_ERR.None)
        {
            DebugManager.LogError(string.Format("'<color=green>SendReqGetChallengeInfo</color>' error: '<color=red>{0}</color>'", err));
            return;
        }

        _currentScore = ack.CurrentMyScore;

        // 비용처리
        _resourceController.ProcessReward(ack.RewardInfo);

        // 리셋 타임
        if (_corUpdate != null)
        {
            StopCoroutine(_corUpdate);
            _corUpdate = null;
        }

        _resetTime = ack.EndDailyTime;

        long now = SgTime.I.Now;
        if (_resetTime > now)
        {
            _corUpdate = StartCoroutine(CorUpdate());
        }

        if (!isReset)
        {
            ChallengePlayerInfo playerInfo = new ChallengePlayerInfo();
            playerInfo.CurrentMyRank = ack.CurrentMyRank;
            playerInfo.CurrentMyScore = ack.CurrentMyScore;
            playerInfo.WinCount = ack.WinCount;
            playerInfo.LoseCount = ack.LoseCount;
            playerInfo.SuccessiveWin = ack.SuccessiveWin;
            playerInfo.EndDailyTime = ack.EndDailyTime;
            playerInfo.SeasonPlayCount = ack.SeasonPlayCount;
            playerInfo.EndSeasonTime = ack.EndSeasonTime;
            playerInfo.MaxTierTid = ack.MaxTierTid;
            playerInfo.SeasonMaxTierTid = ack.SeasonMaxTierTid;
            playerInfo.InitialRewardedBit = ack.InitialRewardedBit;
            playerInfo.SeasonRewardedBit = ack.SeasonRewardedBit;
            playerInfo.HasBeforeReward = ack.HasBeforeReward;
            playerInfo.RefreshLastScore = ack.RefreshLastScore;

            _arenaPanel.RefreshTime = ack.EndRefreshCoolTime;
            _arenaPanel.Open(ack.Info, playerInfo);
        }
    }

    public async Task SendReqRefreshChallengeMatch()
    {
        var (err, ack) = await NetworkHubManager.i.BlockingSendAsync<AckRefreshChallengeMatch>("ReqRefreshChallengeMatch", new ReqRefreshChallengeMatch
        {
        });

        if (err != RESPONSE_ERR.None)
        {
            DebugManager.LogError(string.Format("'<color=green>SendReqRefreshChallengeMatch</color>' error: '<color=red>{0}</color>'", err));
            return;
        }

        // 비용처리
        _resourceController.ProcessReward(ack.RewardInfo);

        _arenaPanel.RefreshTime = ack.EndRefreshCoolTime;
        _arenaPanel.Refresh(ack.Info);
    }

    public async void SendReqSetDeckChallenge(PChallengeDeckInfo deckInfo)
    {
        var (err, ack) = await NetworkHubManager.i.BlockingSendAsync<AckSetDeckChallenge>("ReqSetDeckChallenge", new ReqSetDeckChallenge
        {
            DeckInfo = deckInfo
        });

        if (err != RESPONSE_ERR.None)
        {
            DebugManager.LogError(string.Format("'<color=green>SendReqSetDeckChallenge</color>' error: '<color=red>{0}</color>'", err));
            return;
        }

        _arenaPanel.CallbackSetDeck(deckInfo);
    }

    public async void SendReqGetChallengeBattleLog()
    {
        var (err, ack) = await NetworkHubManager.i.BlockingSendAsync<AckGetChallengeBattleLog>("ReqGetChallengeBattleLog", new ReqGetChallengeBattleLog
        {
        });

        if (err != RESPONSE_ERR.None)
        {
            DebugManager.LogError(string.Format("'<color=green>SendReqGetChallengeBattleLog</color>' error: '<color=red>{0}</color>'", err));
            return;
        }

        _arenaPanel.CallbackRequestRecord(ack.TargetInfos);
    }

    private async void SendReqStartChallenge(int targetIndex, ulong targetPlayerNo, uint targetDummyTid)
    {
        var (err, ack) = await NetworkHubManager.i.BlockingSendAsync<AckStartChallenge>("ReqStartChallenge", new ReqStartChallenge
        {
            Index = targetIndex,
            TargetNo = targetDummyTid == 0 ? targetPlayerNo : targetDummyTid,
        });

        if (err != RESPONSE_ERR.None)
        {
            var popup = PopupManager.i.ShowPopup<Popup_Logo_Transition>(Global.ePopupPrefab.Popup_Logo_Transition);
            if (popup != null)
            {
                popup.CallEnd();
            }
            LeaveChallenge();
            DebugManager.LogError(string.Format("'<color=green>SendReqStartChallenge</color>' error: '<color=red>{0}</color>'", err));
            return;
        }

        // 필드 준비
        await PrepareField();

        /******************** 대전이력 리셋 ********************/
        _arenaPanel.RecordInfos = null;

        _currentWeek = ack.CurrentWeek;

        /******************** 내 정보 세팅 ********************/

        PlayerNo = ack.MyPlayerInfo.PlayerNo;
        var playerModel = SgModel.Instance.AddPlayer(ack.MyPlayerInfo, Global.EGameModeType.Challenge);

        // 내 영웅 리스트(언락된 영웅들만)
        _packetHeroTids.Clear();
        foreach (var it in ack.MyPlayerInfo.ChallengeDeckInfos)
        {
            if (it.Deck1 > 0) _packetHeroTids.Add(it.Deck1);
            if (it.Deck2 > 0) _packetHeroTids.Add(it.Deck2);
            if (it.Deck3 > 0) _packetHeroTids.Add(it.Deck3);
            if (it.Deck4 > 0) _packetHeroTids.Add(it.Deck4);
            if (it.Deck5 > 0) _packetHeroTids.Add(it.Deck5);
            if (it.Deck6 > 0) _packetHeroTids.Add(it.Deck6);
            break;
        }

        // 스킬 프리셋 세팅
        //var skillPreset = _selectedSkillPresetTid > 0 ? _selectedSkillPresetTid : (uint)PlayerPrefsX.GetInt(CHALLENGE_SKILL_PRESET_KEY, 1);
        //playerModel.RefreshAllSkillPreset(skillPreset);

        // 운명 프리셋 세팅
        //foreach (var heroTid in _packetHeroTids)
        //{
        //    byte cubePreset = 0;
        //    if (_selectedCubePresetOrderIds.TryGetValue(heroTid, out cubePreset) == false)
        //    {
        //        var key = string.Format("{0}_{1}", CHALLENGE_CUBE_PRESET_KEY, heroTid);
        //        cubePreset = (byte)PlayerPrefsX.GetInt(key, 1);
        //    }
        //    playerModel.OnChangeCubePreset(heroTid, cubePreset);
        //}

        // 시너지 효과 갱신. 각자 계산하기
        foreach (var heroTid in _packetHeroTids)
        {
            playerModel.RefreshSynergyBuff(_packetHeroTids, heroTid);
        }

        // 체력값 캐싱
        _playerRemainHPInfos.Clear();
        _playerTotalMaxHP = BigDecimal.Zero;

        foreach (var heroTid in _packetHeroTids)
        {
            var heroInfo = playerModel.GetHeroModel(heroTid);
            var total = Formula.FinalMaxHp(heroInfo.StatInfo);
            _playerRemainHPInfos.Add(heroTid, total);
            _playerTotalMaxHP += total;
        }

        /*******************************************************/

        /******************** 타겟 정보 세팅 ********************/

        PlayerModel targetPlayer = null;

        if (targetDummyTid > 0 && GameDBContainer.Instance.ChallengeDummyPlayerList.TryGetValue(targetDummyTid, out ChallengeDummyPlayer dummyPlayer))
        {
            // 더미인 경우
            TargetPlayerNo = GamePlayData.i.UserAccount.PlayerNo + dummyPlayer.tid;
            targetPlayer = SgModel.Instance.GetPlayer(TargetPlayerNo);
            if (targetPlayer == null)
            {
                targetPlayer = SgModel.Instance.AddDummyPlayer(Field, playerModel, _packetHeroTids, dummyPlayer);
            }

            // 타겟 영웅 리스트
            _packetTargetHeroTids.Clear();

            var heroTid = GetDummyHeroTid(dummyPlayer.Entry1);
            if (heroTid > 0) _packetTargetHeroTids.Add(heroTid);

            heroTid = GetDummyHeroTid(dummyPlayer.Entry2);
            if (heroTid > 0) _packetTargetHeroTids.Add(heroTid);

            heroTid = GetDummyHeroTid(dummyPlayer.Entry3);
            if (heroTid > 0) _packetTargetHeroTids.Add(heroTid);

            heroTid = GetDummyHeroTid(dummyPlayer.Entry4);
            if (heroTid > 0) _packetTargetHeroTids.Add(heroTid);

            heroTid = GetDummyHeroTid(dummyPlayer.Entry5);
            if (heroTid > 0) _packetTargetHeroTids.Add(heroTid);

            heroTid = GetDummyHeroTid(dummyPlayer.Entry6);
            if (heroTid > 0) _packetTargetHeroTids.Add(heroTid);
        }
        else
        {
            PAnotherPlayerInfo targetPlayerInfo = null;
            foreach (var target in ack.TargetPlayerInfos)
            {
                if (target.PlayerNo == targetPlayerNo)
                {
                    targetPlayerInfo = target;
                    break;
                }
            }

            TargetPlayerNo = targetPlayerInfo.PlayerNo;
            targetPlayer = SgModel.Instance.AddPlayer(targetPlayerInfo, Global.EGameModeType.Challenge);

            // 타겟 영웅 리스트
            _packetTargetHeroTids.Clear();
            foreach (var it in targetPlayerInfo.ChallengeDeckInfos)
            {
                if (it.Deck1 > 0) _packetTargetHeroTids.Add(it.Deck1);
                if (it.Deck2 > 0) _packetTargetHeroTids.Add(it.Deck2);
                if (it.Deck3 > 0) _packetTargetHeroTids.Add(it.Deck3);
                if (it.Deck4 > 0) _packetTargetHeroTids.Add(it.Deck4);
                if (it.Deck5 > 0) _packetTargetHeroTids.Add(it.Deck5);
                if (it.Deck6 > 0) _packetTargetHeroTids.Add(it.Deck6);

                break;
            }
        }

        // 타겟 시너지 효과 갱신. 각자 계산하기
        foreach (var heroTid in _packetTargetHeroTids)
        {
            targetPlayer.RefreshSynergyBuff(_packetTargetHeroTids);
        }

        // 체력값 캐싱
        _targetRemainHPInfos.Clear();
        _targetTotalMaxHP = BigDecimal.Zero;

        foreach (var heroTid in _packetTargetHeroTids)
        {
            var heroInfo = targetPlayer.GetHeroModel(heroTid);
            var total = Formula.FinalMaxHp(heroInfo.StatInfo);
            _targetRemainHPInfos.Add(heroTid, total);
            _targetTotalMaxHP += total;
        }

        /*******************************************************/

        // 비용처리
        _resourceController.ProcessReward(ack.RewardInfo);

        // 필드 세팅 완료
        OnCreateField();
    }

    private async void SendReqFinishChallenge(int targetIndex, ulong targetPlayerNo, uint targetDummyTid, int currentWeek, bool isWin)
    {
        PDamageLog damageLog = null;
        if (_field != null)
        {
            damageLog = _field.GetDamageLog(0, SgTime.I.Now);
        }

        var (err, ack) = await NetworkHubManager.i.BlockingSendAsync<AckFinishChallenge>("ReqFinishChallenge", new ReqFinishChallenge
        {
            CurrentWeek = currentWeek,
            Index = targetIndex,
            TargetNo = targetDummyTid == 0 ? targetPlayerNo : targetDummyTid,
            IsWin = isWin,
            DamageLog = damageLog
        });

        if(_field != null)
        {
            _field.OnEndBattleForLog();
        }

        if (err != RESPONSE_ERR.None)
        {
            DebugManager.LogError(string.Format("'<color=green>SendReqFinishChallenge</color>' error: '<color=red>{0}</color>'", err));
            return;
        }

        // 보상 처리
        _resourceController.ProcessReward(ack.RewardInfo);

        GameScene_MainUI.i.UI_ArenaHUD.gameObject.SetActive(false);
        //GameScene_MainUI.i.UI_BottomStageHUD.Release();

        _challengeInfo.OnResult(ack.RewardInfo.RewardInfos, ack.ResultScore - ack.DeltaScore, ack.ResultScore);

        // 유저 정보 제거해준다
        SgModel.Instance.RemovePlayer(PlayerNo);
        SgModel.Instance.RemovePlayer(TargetPlayerNo);
    }

    public async void SendReqChallengeRewardInitialRank(uint rankTid)
    {
        var (err, ack) = await NetworkHubManager.i.BlockingSendAsync<AckChallengeRewardInitialRank>("ReqChallengeRewardInitialRank", new ReqChallengeRewardInitialRank
        {
            RankTid = rankTid
        });

        if (err != RESPONSE_ERR.None)
        {
            DebugManager.LogError(string.Format("'<color=green>SendChallengeRewardInitialRank</color>' error: '<color=red>{0}</color>'", err));
            return;
        }

        _resourceController.ProcessReward(ack.RewardInfo);
        _arenaPanel.CallbackInitialReward(ack.InitialRewardedBit);

        Popup_Reward_A rewardPopup = PopupManager.i.ShowPopup<Popup_Reward_A>(Global.ePopupPrefab.Popup_Reward_A);
        if (rewardPopup)
        {
            rewardPopup.Show(ack.RewardInfo);
        }
    }

    public async void SendReqChallengeRewardSeasonRank(uint rankTid)
    {
        var (err, ack) = await NetworkHubManager.i.BlockingSendAsync<AckChallengeRewardSeasonRank>("ReqChallengeRewardSeasonRank", new ReqChallengeRewardSeasonRank
        {
            RankTid = rankTid
        });

        if (err != RESPONSE_ERR.None)
        {
            DebugManager.LogError(string.Format("'<color=green>SendReqChallengeRewardSeasonRank</color>' error: '<color=red>{0}</color>'", err));
            return;
        }

        _resourceController.ProcessReward(ack.RewardInfo);
        _arenaPanel.CallbackSeasonReward(ack.SeasonRewardedBit);

        Popup_Reward_A rewardPopup = PopupManager.i.ShowPopup<Popup_Reward_A>(Global.ePopupPrefab.Popup_Reward_A);
        if (rewardPopup)
        {
            rewardPopup.Show(ack.RewardInfo);
        }
    }

    #endregion packet

    #region 갱신

    private IEnumerator CorUpdate()
    {
        while (true)
        {
            long now = SgTime.I.Now;

            if (_resetTime < now)
            {
                CheckChallengePacketInfo(true);
                yield break;
            }

            yield return _wait;
        }
    }

    #endregion 갱신

    #region PlayerPrefsX

    public void SavePlaySpeed()
    {
        PlayerPrefsX.SetInt(CHALLENGE_PLAY_SPEED_KEY, _speed);
    }

    public int LoadPlaySpeed()
    {
        if (PlayerPrefsX.HasKey(CHALLENGE_PLAY_SPEED_KEY) == false)
        {
            PlayerPrefsX.SetInt(CHALLENGE_PLAY_SPEED_KEY, 1);
        }
        return PlayerPrefsX.GetInt(CHALLENGE_PLAY_SPEED_KEY, 1);
    }

    #endregion PlayerPrefsX
}