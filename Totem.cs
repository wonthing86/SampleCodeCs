using diverseshared;
using GameDB;
using System.Collections.Generic;
using System.Linq;

public enum TotemState
{
    None,
    Spawn,
    Idle,
    Despawn,
}

public class Totem : StateKitCommon<TotemState>
{
    public uint OwnerSkillTid { get; set; }

    public uint Tid { get; set; }

    public F64Vec2 Pos { get; private set; }

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

    public LinkedListNode<Totem> LinkedNode { get; set; }

    public bool IsRemoved { get; set; }

    public BaseEntity Owner { get; private set; }

    public BaseEntity MainTarget { get; private set; }

    private GameDB.TotemSheet _totemSheet = null;

    private TotemMono _mono = null;

    private FieldBase _field = null;

    public FieldBase Field => _field;

    private int _faction = 0;

    private F64Vec2 _hitPos = F64Vec2.Zero;

    private List<ISectorMember> _candidates = new List<ISectorMember>();

    private LinkedList<BaseEntity> _targets = new LinkedList<BaseEntity>();

    private LinkedList<BurstMono> _fxObjects = new LinkedList<BurstMono>();

    private List<(EBuffStat, BigDecimal, string)> _buffs = new List<(EBuffStat, BigDecimal, string)>();

    private List<(EBuffStat, BigDecimal, string)> _buffs2 = new List<(EBuffStat, BigDecimal, string)>();

    private int _endStateTick = 0;

    // 타원(Ellipse)에서 쓰는 변수
    private F64 _xRadius;

    private F64 _yRadius;
    private F64Vec2 _dot1;
    private F64Vec2 _dot2;

    private int _skillLevel = 1;

    private int _nextInvokeTick;
    private int _nextInvokeTick2;

    public Totem(uint invokedSkillId, uint tid, FieldBase field, BaseEntity owner, BaseEntity mainTarget, F64Vec2 hitPos, int skillLevel)
    {
        initialState = TotemState.None;

        OwnerSkillTid = invokedSkillId;
        Tid = tid;
        Owner = owner;
        MainTarget = mainTarget;

        _field = field;
        _faction = owner.GetFaction();
        _hitPos = hitPos;
        _skillLevel = skillLevel;

        if (GameDBContainer.Instance.TotemSheetList.TryGetValue(tid, out GameDB.TotemSheet totemSheet) == false)
        {
            IsRemoved = true;
            return;
        }

        _totemSheet = totemSheet;

        Scale = F64.Ratio100(_totemSheet.Scale);
    }

    public void SetMono(TotemMono mono)
    {
        _mono = mono;

        Pos = _field.GetClampBound(MainTarget.Pos
            + new F64Vec2(F64.Ratio100(_mono.SpawnOffsetX), F64.Ratio100(_mono.SpawnOffsetY)));

        // 버프 정보
        _buffs.Clear();
        if (_mono.AbilityType == ETotemAbilityType.Buff)
        {
            GameDBMapper.Instance.EachGroupAbilities(_mono.AbilityGroupId, (AbilitySheet data) =>
            {
                if (data.BuffStatAbilityType != EBuffStat.None)
                {
                    var buffValue =
                        FormulaHelper.GetLevelValue(
                            data.BuffBasePer,
                            data.BuffLvRange,
                            data.BuffLvPerDelta,
                            _skillLevel);
                    _buffs.Add((data.BuffStatAbilityType, buffValue, data.EffectPrefab));
                }
            });
        }

        _buffs2.Clear();
        if (_mono.AbilityType2 == ETotemAbilityType.Buff)
        {
            GameDBMapper.Instance.EachGroupAbilities(_mono.AbilityGroupId2, (AbilitySheet data) =>
            {
                if (data.BuffStatAbilityType != EBuffStat.None)
                {
                    var buffValue =
                        FormulaHelper.GetLevelValue(
                            data.BuffBasePer,
                            data.BuffLvRange,
                            data.BuffLvPerDelta,
                            _skillLevel);
                    _buffs2.Add((data.BuffStatAbilityType, buffValue, data.EffectPrefab));
                }
            });
        }

        // 타원이라고 가정하자.
        var sizeX = Radius * 2;
        // (임시로 가로사이즈의 반으로 해보자)
        var sizeY = Radius;
        // 가로반지름, 세로반지름
        _xRadius = sizeX / F64.FromInt(2);
        _yRadius = sizeY / F64.FromInt(2);
        // 타원의 정의에 해당하는 두 점(점과의 충돌처리 판정시 사용)
        var dist = F64.SqrtFast(_xRadius * _xRadius - _yRadius * _yRadius);
        _dot1 = Pos;
        _dot1.X -= dist;
        _dot2 = Pos;
        _dot2.X += dist;
    }

    public uint GetTid()
    {
        if (_totemSheet != null)
        {
            return _totemSheet.tid;
        }
        return 0;
    }

    public string GetPrefabName()
    {
        if (_totemSheet != null)
        {
            return _totemSheet.ItemPrefab;
        }
        return "";
    }

    public void StartAction()
    {
        currentState = TotemState.Spawn;
    }

    public void OnRemoved()
    {
        currentState = TotemState.None;
        _mono.OnRemoved();
    }

    public void OnTick(F64 delta)
    {
        // 상태머신 업데이트
        OnStateTick(delta);
    }

    private void Spawn_Enter()
    {
        _endStateTick = _field.CurrentTick + _mono.SpawnDuration;
        _mono.SetAnimation(_mono.SpawnAniInfo, TotemState.Spawn);
    }

    private void Spawn_Tick(F64 delta)
    {
        if (_field.CurrentTick >= _endStateTick)
        {
            currentState = TotemState.Idle;
        }
    }

    private void Spawn_Exit()
    {
        if (_mono == null)
        {
            return;
        }

        _mono.RemoveEffect(TotemState.Spawn);
    }

    private void Idle_Enter()
    {
        _endStateTick = _field.CurrentTick + _mono.IdleDuration;
        _mono.SetAnimation(_mono.IdleAniInfo, TotemState.Idle);
    }

    private void Idle_Tick(F64 delta)
    {
        BuffAction();
        IntervalInstant();

        if (_field.CurrentTick >= _endStateTick)
        {
            currentState = TotemState.Despawn;
        }
    }

    private void BuffAction()
    {
        if (_mono.AbilityType == ETotemAbilityType.Buff)
        {
            ApplyBuff(_mono.TargetType, _buffs);
        }

        if (_mono.AbilityType2 == ETotemAbilityType.Buff)
        {
            ApplyBuff(_mono.TargetType2, _buffs2);
        }
    }

    private void ApplyBuff(ETotemTargetType targetType, List<(EBuffStat, BigDecimal, string)> buffs)
    {
        // 나갔는지 판단
        var targetNode = _targets.First;
        while (targetNode != null)
        {
            var next = targetNode.Next;
            var target = targetNode.Value;
            if (target.IsDeadInGame || !IsInEllipse(target.CenterPos))
            {
                //
                foreach (var buff in buffs)
                {
                    target.Stats.PutAdditveBuffStat(buff.Item1, -buff.Item2);

                    var targetMono = target.GetMonoEntity();

                    // detach effect
                    var fxNode = _fxObjects.First;
                    while (fxNode != null)
                    {
                        var fxNext = fxNode.Next;
                        if (fxNode.Value.Caster == targetMono)
                        {
                            PoolController.i.Despawn(fxNode.Value);
                            _fxObjects.Remove(fxNode);
                        }
                        fxNode = fxNext;
                    }
                }
                //
                _targets.Remove(targetNode);
            }
            targetNode = next;
        }

        // 들어왔는지 판단
        _candidates.Clear();
        _field.GetCandidatesOnSector(SectorType.Entity, Pos, Radius, _candidates);
        if (_candidates.Count == 0)
        {
            return;
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
            if (!IsSatisfiedFaction(targetType, target))
            {
                return false;
            }

            if (!IsInEllipse(target.CenterPos))
            {
                return false;
            }

            return true;
        })
        .OrderBy(target => F64Vec2.Distance(target.GetCenterPosition(), Pos));

        // 타겟 리스트 갱신
        if (sortList != null)
        {
            foreach (var candidate in sortList)
            {
                var target = (BaseEntity)candidate;

                if (_targets.Contains(target))
                {
                    return;
                }

                _targets.AddLast(target);
                _mono.OnShowEnter(target);

                int offset = 0;

                foreach (var buff in buffs)
                {
                    target.Stats.PutAdditveBuffStat(buff.Item1, buff.Item2);

                    var targetMono = target.GetMonoEntity();

                    // attach effect
                    if (buff.Item3 != null)
                    {
                        var fxObject = PoolController.i.Spawn(Owner.GetField().FieldName, buff.Item3, targetMono.transform);
                        var burstMono = fxObject.GetComponentInChildren<BurstMono>(true);
                        if (burstMono == null)
                        {
                            burstMono = fxObject.AddComponent<BurstMono>();
                        }
                        burstMono.Caster = targetMono;

                        _fxObjects.AddLast(burstMono);
                    }

                    // 버프 표기 (임시)
                    if (PrintStat.Instance.GetNameAndValue(buff.Item1, buff.Item2, true, out string nameStr, out string valueStr))
                    {
                        var nameLocale = MultiLanguageManager.i.GetText(nameStr);
                        var resText = string.Format("{0} {1}", nameLocale, valueStr);
                        targetMono?.CreateBuffFont(resText, buff.Item2 >= BigDecimal.Zero ? false : true, offset);
                    }

                    offset++;
                }
            }
        }
    }

    private void IntervalInstant()
    {
        if (_mono.AbilityType == ETotemAbilityType.Instant)
        {
            if (_field.CurrentTick >= _nextInvokeTick)
            {
                _nextInvokeTick = _field.CurrentTick + _mono.Interval;
                ApplyInstant();
            }
        }

        if (_mono.AbilityType2 == ETotemAbilityType.Instant)
        {
            if (_field.CurrentTick >= _nextInvokeTick2)
            {
                _nextInvokeTick2 = _field.CurrentTick + _mono.Interval2;
                ApplyInstant2();
            }
        }
    }

    private void Idle_Exit()
    {
        if (_mono == null)
        {
            return;
        }

        _mono.RemoveEffect(TotemState.Idle);
    }

    private void Despawn_Enter()
    {
        ClearTargets();

        _endStateTick = _field.CurrentTick + _mono.DespawnDuration;
        _mono.SetAnimation(_mono.DespawnAniInfo, TotemState.Despawn);
    }

    private void Despawn_Tick(F64 delta)
    {
        if (_field.CurrentTick >= _endStateTick)
        {
            IsRemoved = true;
            _mono.RemoveEffect(TotemState.Despawn);
        }
    }

    private void Despawn_Exit()
    {
        if (_mono == null)
        {
            return;
        }
    }

    private void ClearTargets()
    {
        var targetNode = _targets.First;
        while (targetNode != null)
        {
            var next = targetNode.Next;
            var target = targetNode.Value;

            if (IsSatisfiedFaction(_mono.TargetType, target))
            {
                foreach (var buff in _buffs)
                {
                    target.Stats.PutAdditveBuffStat(buff.Item1, -buff.Item2);
                }
            }

            if (IsSatisfiedFaction(_mono.TargetType2, target))
            {
                foreach (var buff in _buffs2)
                {
                    target.Stats.PutAdditveBuffStat(buff.Item1, -buff.Item2);
                }
            }

            targetNode = next;
        }

        var fxNode = _fxObjects.First;
        while (fxNode != null)
        {
            var next = fxNode.Next;
            PoolController.i.Despawn(fxNode.Value);

            fxNode = next;
        }

        _buffs.Clear();
        _buffs2.Clear();
        _targets.Clear();
        _fxObjects.Clear();
    }

    private bool IsSatisfiedFaction(ETotemTargetType type, BaseEntity target)
    {
        if (type == ETotemTargetType.Self)
        {
            if (Owner != null && target == Owner)
            {
                return true;
            }
        }
        else if (type == ETotemTargetType.Ally)
        {
            if (target.Faction == _faction)
            {
                return true;
            }
        }
        else if (type == ETotemTargetType.Enemy)
        {
            if (target.Faction != _faction)
            {
                return true;
            }
        }
        else if (type == ETotemTargetType.All)
        {
            return true;
        }

        return false;
    }

    private bool IsInEllipse(F64Vec2 targetPos)
    {
        F64 dist = F64.Zero;

        dist += F64Vec2.Distance(targetPos, _dot1);
        dist += F64Vec2.Distance(targetPos, _dot2);

        // 두 점으로부터의 거리 합이 2x보다 작으면 타원안에 포함된다
        if (dist <= (_xRadius * F64.FromInt(2)))
        {
            return true;
        }

        return false;
    }

    private void CheckTargets()
    {
        // 나갔는지 판단
        var targetNode = _targets.First;
        while (targetNode != null)
        {
            var next = targetNode.Next;
            var target = targetNode.Value;
            if (target.IsDeadInGame || !IsInEllipse(target.CenterPos))
            {
                EndEffect(target);
                _targets.Remove(targetNode);
            }
            targetNode = next;
        }

        // 들어왔는지 판단
        _candidates.Clear();
        _field.GetCandidatesOnSector(SectorType.Entity, Pos, Radius, _candidates);
        if (_candidates.Count == 0)
        {
            return;
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

            //// faction체크
            //if (!IsSatisfiedFaction(_mono.TargetType, target))
            //{
            //    return false;
            //}

            if (!IsInEllipse(target.CenterPos))
            {
                return false;
            }

            return true;
        })
        .OrderBy(target => F64Vec2.Distance(target.GetCenterPosition(), Pos));

        // 타겟 리스트 갱신
        if (sortList != null)
        {
            foreach (var candidate in sortList)
            {
                var target = (BaseEntity)candidate;

                if (_targets.Contains(target))
                {
                    return;
                }
                _targets.AddLast(target);
                _mono.OnShowEnter(target);

                StartEffect(target);
            }
        }
    }

    private void StartEffect(BaseEntity target)
    {
        int offset = 0;

        List<(EBuffStat, BigDecimal, string)> buffs = null;

        if (_mono.TargetType == ETotemTargetType.All && _mono.AbilityGroupId2 > 0 && _faction != target.Faction)
        {
            buffs = _buffs2;
        }
        else
        {
            buffs = _buffs;
        }

        foreach (var buff in buffs)
        {
            target.Stats.PutAdditveBuffStat(buff.Item1, buff.Item2);

            var targetMono = target.GetMonoEntity();

            // attach effect
            if (buff.Item3 != null)
            {
                var fxObject = PoolController.i.Spawn(Owner.GetField().FieldName, buff.Item3, targetMono.transform);
                var burstMono = fxObject.GetComponentInChildren<BurstMono>(true);
                if (burstMono == null)
                {
                    burstMono = fxObject.AddComponent<BurstMono>();
                }
                burstMono.Caster = targetMono;

                _fxObjects.AddLast(burstMono);
            }

            // 버프 표기 (임시)
            if (PrintStat.Instance.GetNameAndValue(buff.Item1, buff.Item2, true, out string nameStr, out string valueStr))
            {
                var nameLocale = MultiLanguageManager.i.GetText(nameStr);
                var resText = string.Format("{0} {1}", nameLocale, valueStr);
                targetMono?.CreateBuffFont(resText, buff.Item2 >= BigDecimal.Zero ? false : true, offset);
            }

            offset++;
        }
    }

    private void EndEffect(BaseEntity target)
    {
        //int offset = 0;

        List<(EBuffStat, BigDecimal, string)> buffs = null;

        if (_mono.TargetType == ETotemTargetType.All && _mono.AbilityGroupId2 > 0 && _faction != target.Faction)
        {
            buffs = _buffs2;
        }
        else
        {
            buffs = _buffs;
        }

        foreach (var buff in buffs)
        {
            target.Stats.PutAdditveBuffStat(buff.Item1, -buff.Item2);

            var targetMono = target.GetMonoEntity();

            // detach effect
            var fxNode = _fxObjects.First;
            while (fxNode != null)
            {
                var next = fxNode.Next;
                if (fxNode.Value.Caster == targetMono)
                {
                    PoolController.i.Despawn(fxNode.Value);
                    _fxObjects.Remove(fxNode);
                }
                fxNode = next;
            }

            //var res = (((decimal)buff.Item2) / 100);
            //var resText = string.Format("{0} 타입 버프 값 {1}% 제거.", buff.Item2.ToString(), res.ToString());
            //ALog.Log(resText);
            //if (PrintStat.Instance.GetNameAndValue(buff.Item1, buff.Item2, true, out string nameStr, out string valueStr))
            //{
            //    var nameLocale = MultiLanguageManager.i.GetText(nameStr);
            //    var resText = string.Format("{0} {1}", nameLocale, valueStr);
            //    targetMono?.CreateBuffFont(resText, buff.Item2 < BigDecimal.Zero ? false : true, offset);
            //}
            //offset++;
        }
    }

    public void ApplyInstant()
    {
        // 들어왔는지 판단
        _candidates.Clear();
        _field.GetCandidatesOnSector(SectorType.Entity, Pos, Radius, _candidates);
        if (_candidates.Count == 0)
        {
            return;
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
            if (!IsSatisfiedFaction(_mono.TargetType, target))
            {
                return false;
            }

            if (!IsInEllipse(target.CenterPos))
            {
                return false;
            }

            return true;
        })
        .OrderBy(target => F64Vec2.Distance(target.GetCenterPosition(), Pos));

        foreach (var candidate in sortList)
        {
            var target = (BaseEntity)candidate;

            if (_mono.AbilityGroupId > 0)
            {
                AbilityLauncher.i.LaunchAbilityGroup(_mono.AbilityGroupId, Owner, target, _field, Pos, OwnerSkillTid, _skillLevel);
            }

            // Hit Effect
            _mono.OnShowEnter(target);
        }
    }

    public void ApplyInstant2()
    {
        // 들어왔는지 판단
        _candidates.Clear();
        _field.GetCandidatesOnSector(SectorType.Entity, Pos, Radius, _candidates);
        if (_candidates.Count == 0)
        {
            return;
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
            if (!IsSatisfiedFaction(_mono.TargetType2, target))
            {
                return false;
            }

            if (!IsInEllipse(target.CenterPos))
            {
                return false;
            }

            return true;
        })
        .OrderBy(target => F64Vec2.Distance(target.GetCenterPosition(), Pos));

        foreach (var candidate in sortList)
        {
            var target = (BaseEntity)candidate;

            if (_mono.AbilityGroupId2 > 0)
            {
                AbilityLauncher.i.LaunchAbilityGroup(_mono.AbilityGroupId2, Owner, target, _field, Pos, OwnerSkillTid, _skillLevel);
            }

            // Hit Effect
            _mono.OnShowEnter(target);
        }
    }
}