using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using GameDB;
using diverseshared;

public enum FoodState
{
    None,
    Spawn,
    Idle01,
    Idle02,
    Despawn,
}

public class Food : StateKitCommon<FoodState>
{
    public uint Tid { get; set; }

    public F64Vec2 Pos { get; private set; }

    //프리팹에서 설정한 오프셋 값으로 중점 잡기
    public F64Vec2 CenterPos
    {
        get
        {
            if (_mono == null)
                return Pos;

            return new F64Vec2(Pos.X + (_mono.XOffset * Scale),
                Pos.Y + (_mono.YOffset * Scale));
        }
    }

    public F64 Radius
    {
        get
        {
            if (_mono == null)
                return F64.Ratio10(3);

            return _mono.Radius * Scale;
        }
    }

    public F64 Scale { get; set; }

    public LinkedListNode<Food> LinkedNode { get; set; }

    public bool IsRemoved { get; set; }

    public BaseEntity Owner { get; private set; }

    private GameDB.FoodSheet _foodSheet = null;

    private FoodMono _mono = null;

    private FieldBase _field = null;

    public FieldBase Field => _field;

    private int _index = 0;

    private F64Vec2 _hitPos = F64Vec2.Zero;

    private List<ISectorMember> _candidates = new List<ISectorMember>();

    private int _endStateTick = 0;

    public Food(uint tid, FieldBase field, BaseEntity owner, F64Vec2 hitPos, int index)
    {
        initialState = FoodState.None;

        Tid = tid;
        Owner = owner;

        _field = field;
        _index = index;
        _hitPos = hitPos;

        if (GameDBContainer.Instance.FoodSheetList.TryGetValue(tid, out GameDB.FoodSheet foodSheet) == false)
        {
            IsRemoved = true;
            return;
        }

        _foodSheet = foodSheet;

        Scale = F64.Ratio100(_foodSheet.Scale);
    }

    public void SetMono(FoodMono mono)
    {
        _mono = mono;

        if (_mono.FoodSpawnType == EFoodSpawnType.Distance)
        {
            var dir = F64Vec2.Normalize(_hitPos - Owner.CenterPos);
            Pos = _field.GetClampBound(Owner.CenterPos + dir * (F64.Ratio100(_mono.SpawnRange) + Radius * 2 * _index));
        }
        else if (_mono.FoodSpawnType == EFoodSpawnType.Random)
        {
            Pos = _field.GetClampBound(_field.GetRandomPos(_hitPos, F64.Ratio100(_mono.SpawnRange)));
        }

        //Pos = new F64Vec2(Pos.X - (_mono.XOffset * Scale), Pos.Y - (_mono.YOffset * Scale));
    }

    public string GetPrefabName()
    {
        if (_foodSheet != null)
        {
            return _foodSheet.ItemPrefab;
        }
        return "";
    }

    public void StartAction()
    {
        currentState = FoodState.Spawn;
    }

    public void OnRemoved()
    {
        currentState = FoodState.None;
        _mono.OnRemoved();
    }

    public void OnTick(F64 delta)
    {
        // 상태머신 업데이트
        OnStateTick(delta);

        //if (_mono != null)
        //{
        //    _mono.OnTick(delta);
        //}

        if (Owner == null || Owner.IsDead || Owner.IsRemoved)
        {
            currentState = FoodState.Despawn;
        }
    }

    private void Spawn_Enter()
    {
        _endStateTick = _field.CurrentTick + _mono.SpawnDuration;
        _mono.SetAnimation(_mono.SpawnAniInfo, false);
        _mono.OnShowArea(0);
    }

    private void Spawn_Tick(F64 delta)
    {
        if (_field.CurrentTick >= _endStateTick)
        {
            if (_mono.TargetType01 == EFoodTargetType.None)
            {
                if (_mono.TargetType02 == EFoodTargetType.None)
                {
                    currentState = FoodState.Despawn;
                }
                else
                {
                    currentState = FoodState.Idle02;
                }
            }
            else
            {
                currentState = FoodState.Idle01;
            }
        }
    }

    private void Idle01_Enter()
    {
        _endStateTick = _field.CurrentTick + _mono.Idle01Duration;
        _mono.SetAnimation(_mono.Idle01AniInfo, true);
        _mono.OnShowArea(1);
    }

    private void Idle01_Tick(F64 delta)
    {
        if (CheckTaker())
        {
            IsRemoved = true;
            return;
        }

        if (_field.CurrentTick >= _endStateTick)
        {
            if (CheckExplosion())
            {
                if (_mono.TargetType02 == EFoodTargetType.None)
                {
                    IsRemoved = true;
                }
                else
                {
                    currentState = FoodState.Idle02;
                }
            }
            else
            {
                if (_mono.TargetType02 == EFoodTargetType.None)
                {
                    currentState = FoodState.Despawn;
                }
                else
                {
                    currentState = FoodState.Idle02;
                }
            }
        }
    }

    private void Idle02_Enter()
    {
        _endStateTick = _field.CurrentTick + _mono.Idle02Duration;
        _mono.SetAnimation(_mono.Idle02AniInfo, true);
        _mono.OnShowArea(2);
    }

    private void Idle02_Tick(F64 delta)
    {
        if (CheckTaker())
        {
            IsRemoved = true;
            return;
        }

        if (_field.CurrentTick >= _endStateTick)
        {
            if (CheckExplosion())
            {
                IsRemoved = true;
            }
            else
            {
                currentState = FoodState.Despawn;
            }
        }
    }

    private void Despawn_Enter()
    {
        _endStateTick = _field.CurrentTick + _mono.DespawnDuration;
        _mono.SetAnimation(_mono.DespawnAniInfo, false);
        _mono.OnShowArea(0);
    }

    private void Despawn_Tick(F64 delta)
    {
        if (_field.CurrentTick >= _endStateTick)
        {
            IsRemoved = true;
        }
    }

    private bool CheckTaker()
    {
        if (Owner == null)
        {
            return false;
        }

        _candidates.Clear();
        _field.GetCandidatesOnSector(SectorType.Entity, CenterPos, F64.Two, _candidates);
        if (_candidates.Count == 0)
        {
            return false;
        }

        IEnumerable<ISectorMember> sortList = null;

        sortList = _candidates
        .Where(candidate =>
        {
            var target = (BaseEntity)candidate;
            // 이미 죽었으면 넘어간다
            if (target.IsDeadInGame)
            {
                return false;
            }

            // faction체크
            if (!IsSatisfiedFaction(target))
            {
                return false;
            }

            var distance = F64Vec2.Distance(target.CenterPos, CenterPos);
            if (distance > Radius + target.Radius)
            {
                return false;
            }

            return true;
        })
        .OrderBy(target => F64Vec2.Distance(target.GetCenterPosition(), CenterPos))
        .Take(1);

        if (sortList == null)
            return false;

        foreach (var candidate in sortList)
        {
            var target = (BaseEntity)candidate;

            GameObject rangeSpecPrefab = null;

            if (currentState == FoodState.Idle01)
            {
                if (_mono.AbilityGroupId01 > 0)
                {
                    AbilityLauncher.i.LaunchAbilityGroup(_mono.AbilityGroupId01, Owner, target, _field, CenterPos);
                }
                _mono.OnShowHit(1, target);

                rangeSpecPrefab = _mono.RangeSpec01;
            }
            else if (currentState == FoodState.Idle02)
            {
                if (_mono.AbilityGroupId02 > 0)
                {
                    AbilityLauncher.i.LaunchAbilityGroup(_mono.AbilityGroupId02, Owner, target, _field, CenterPos);
                }
                _mono.OnShowHit(2, target);

                rangeSpecPrefab = _mono.RangeSpec02;
            }

            // rangeSpecPrefab이 있는 경우
            if (rangeSpecPrefab != null)
            {
                RangeSpecMono rangeSpecMono = rangeSpecPrefab.GetComponent<RangeSpecMono>();
                if (rangeSpecMono != null)
                {
                    foreach (var rangeSpec in rangeSpecMono.Specs)
                    {
                        // 현재 pos에서 상대위치
                        var addedPos = rangeSpec.UseRandomPos ?
                            new F64Vec2(
                                F64.Ratio100(_field.RangeRandom(rangeSpec.RandomXRangeMin, rangeSpec.RandomXRangeMax)),
                                F64.Ratio100(_field.RangeRandom(rangeSpec.RandomYRangeMin, rangeSpec.RandomYRangeMax)))

                                : new F64Vec2(F64.Ratio100(rangeSpec.PosX), F64.Ratio100(rangeSpec.PosY));

                        var resPos = _field.GetClampBound(Pos + addedPos);
                        var range = new Range(Owner.Stats, _field, Owner, Owner.Pos, resPos, rangeSpec, false);
                        _field.SpawnRange(range);
                    }
                }
            }

            // 테이커는 한 명 뿐이므로 리턴
            return true;
        }

        return false;
    }

    private bool CheckExplosion()
    {
        if (Owner == null)
        {
            return false;
        }

        GameObject rangeSpecPrefab = null;

        if (currentState == FoodState.Idle01)
        {
            rangeSpecPrefab = _mono.RangeSpec01;
        }
        else if (currentState == FoodState.Idle02)
        {
            rangeSpecPrefab = _mono.RangeSpec02;
        }

        // rangeSpecPrefab이 있는 경우
        if (rangeSpecPrefab != null)
        {
            RangeSpecMono rangeSpecMono = rangeSpecPrefab.GetComponent<RangeSpecMono>();
            if (rangeSpecMono != null)
            {
                foreach (var rangeSpec in rangeSpecMono.Specs)
                {
                    // 현재 pos에서 상대위치
                    var addedPos = rangeSpec.UseRandomPos ?
                    new F64Vec2(
                            F64.Ratio100(_field.RangeRandom(rangeSpec.RandomXRangeMin, rangeSpec.RandomXRangeMax)),
                            F64.Ratio100(_field.RangeRandom(rangeSpec.RandomYRangeMin, rangeSpec.RandomYRangeMax)))

                            : new F64Vec2(F64.Ratio100(rangeSpec.PosX), F64.Ratio100(rangeSpec.PosY));

                    var resPos = _field.GetClampBound(Pos + addedPos);
                    var range = new Range(Owner.Stats, _field, Owner, Owner.Pos, resPos, rangeSpec, false);
                    _field.SpawnRange(range);
                }
            }

            return true;
        }

        return false;
    }

    private bool IsSatisfiedFaction(BaseEntity target)
    {
        if (Owner == null)
            return false;

        EFoodTargetType targetType = EFoodTargetType.None;
        if (currentState == FoodState.Idle01)
        {
            targetType = _mono.TargetType01;
        }
        else if (currentState == FoodState.Idle02)
        {
            targetType = _mono.TargetType02;
        }

        if (targetType == EFoodTargetType.None)
        {
            return false;
        }
        else if (targetType == EFoodTargetType.Self)
        {
            if (target == Owner)
            {
                return true;
            }
        }
        else if (targetType == EFoodTargetType.Ally)
        {
            if (target.Faction == Owner.GetFaction())
            {
                return true;
            }
        }
        else if (targetType == EFoodTargetType.Enemy)
        {
            if (target.Faction != Owner.GetFaction())
            {
                return true;
            }
        }
        else if (targetType == EFoodTargetType.All)
        {
            return true;
        }

        return false;
    }
}