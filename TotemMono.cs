using UnityEngine;
using UnityEngine.Rendering;
using GAP_ParticleSystemController;
using Sirenix.OdinInspector;
using Spine.Unity;
using System.Collections.Generic;
using GameDB;
using diverseshared;

public enum ETotemAbilityType
{
    None = 0,
    Instant = 1,
    Buff = 2,
}

public enum ETotemTargetType
{
    None = 0,
    Self = 1,
    Ally = 2,
    Enemy = 3,
    All = 4,
}

public class TotemMono : MonoBehaviour
{
    [System.Serializable]
    public struct TotemStateAniInfo
    {
        public string AnimName;
        public GameObject EffectPrefab;
        public ESoundKeyType SFX;
    }

#pragma warning disable 0649

    [TitleGroup("Default", "", TitleAlignments.Left)]
    [PropertyOrder(1)]
    [SerializeField]
    private Transform _unit;

    [TitleGroup("Default", "", TitleAlignments.Left)]
    [PropertyOrder(2)]
    [SerializeField]
    private SkeletonAnimation _anim;

    [TitleGroup("Default", "", TitleAlignments.Left)]
    [PropertyOrder(3)]
    [SerializeField]
    private int _radius = 100;

    [Title("Info", "", TitleAlignments.Left)]
    public string Description;

    [TitleGroup("Spawn", "", TitleAlignments.Left)]
    [PropertyOrder(1)]
    public TotemStateAniInfo SpawnAniInfo;

    [PropertyOrder(1)]
    [SerializeField]
    public int SpawnOffsetX;

    [PropertyOrder(1)]
    [SerializeField]
    public int SpawnOffsetY;

    [TitleGroup("Spawn", "", TitleAlignments.Left)]
    [PropertyOrder(2)]
    [Range(0, 1000)]
    public int SpawnDuration;

    [TitleGroup("Spawn", "", TitleAlignments.Left)]
    [PropertyOrder(3)]
    [ShowInInspector]
    public float SpawnDurationSecond
    { get { return SpawnDuration * 0.025f; } }

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(2)]
    public TotemStateAniInfo IdleAniInfo;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(3)]
    [Range(0, 1000)]
    public int IdleDuration;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(4)]
    [ShowInInspector]
    public float IdleDurationSecond
    { get { return IdleDuration * 0.025f; } }

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(1)]
    public ETotemTargetType TargetType;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(5)]
    public ETotemAbilityType AbilityType;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [Range(1, 2000)]
    [ShowIf("AbilityType", ETotemAbilityType.Instant)]
    [PropertyOrder(6)]
    public int Interval = 1;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(7)]
    public int AbilityGroupId;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(8)]
    public GameObject EnterPrefab;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(9)]
    public ESoundKeyType EnterSFX;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(10)]
    public ETotemTargetType TargetType2;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(11)]
    public ETotemAbilityType AbilityType2;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [Range(1, 2000)]
    [ShowIf("AbilityType2", ETotemAbilityType.Instant)]
    [PropertyOrder(12)]
    public int Interval2 = 1;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(13)]
    public int AbilityGroupId2;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(14)]
    public GameObject EnterPrefab2;

    [TitleGroup("Idle", "", TitleAlignments.Left)]
    [PropertyOrder(15)]
    public ESoundKeyType EnterSFX2;

    [TitleGroup("Despawn", "", TitleAlignments.Left)]
    [PropertyOrder(1)]
    public TotemStateAniInfo DespawnAniInfo;

    [TitleGroup("Despawn", "", TitleAlignments.Left)]
    [PropertyOrder(2)]
    [Range(0, 1000)]
    public int DespawnDuration;

    [TitleGroup("Despawn", "", TitleAlignments.Left)]
    [PropertyOrder(3)]
    [ShowInInspector]
    public float DespawnDurationSecond
    { get { return DespawnDuration * 0.025f; } }

    private readonly string _sortingLayerName = "BattleEntity";

    private Dictionary<TotemState, GameObject> _effectMonoDic = new Dictionary<TotemState, GameObject>();

#pragma warning restore

    public F64 Radius
    {
        get
        {
            return F64.Ratio100((int)_radius);
        }
    }

    private Totem _totem = null;

#if UNITY_EDITOR

    public void OnDrawGizmos()
    {
        //-2023/01/30 타원 비율 1:0.37
        var sizeY = Radius * F64.Ratio100(37);

        DrawEllipse(transform.position, transform.forward, transform.up, Radius.Float, sizeY.Float, 64);
    }

    private void DrawEllipse(Vector3 pos, Vector3 forward, Vector3 up, float radiusX, float radiusY, int segments)
    {
        float angle = 0f;
        Quaternion rot = Quaternion.LookRotation(forward, up);
        Vector3 lastPoint = Vector3.zero;
        Vector3 thisPoint = Vector3.zero;

        for (int i = 0; i < segments + 1; i++)
        {
            thisPoint.x = Mathf.Sin(Mathf.Deg2Rad * angle) * radiusX;
            thisPoint.y = Mathf.Cos(Mathf.Deg2Rad * angle) * radiusY;

            if (i > 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(rot * lastPoint + pos, rot * thisPoint + pos);
            }

            lastPoint = thisPoint;
            angle += 360f / segments;
        }
    }

#endif

    public void Init(Totem totem)
    {
        _totem = totem;
        _totem.SetMono(this);

        var simulPos = new Vector3((float)_totem.Pos.X.Float, (float)_totem.Pos.Y.Float, 0.0f);
        transform.position = simulPos;

        // 스케일 세팅
        transform.localScale = Vector3.one * _totem.Scale.Float;

        SetSortingGroup(transform, 0);
        SetLayer(transform, 2);
        SetLayer_Renderer(transform);        
    }

    public void OnRemoved()
    {
        foreach (var effect in _effectMonoDic)
        {
            PoolController.i.Despawn(effect.Value);
        }

        _effectMonoDic.Clear();

        DeSpawnMonoObject(gameObject);
    }

    public void SetAnimation(TotemStateAniInfo aniInfo, TotemState state)
    {
        if (string.IsNullOrEmpty(aniInfo.AnimName))
            return;

        _anim.state.SetAnimation(0, aniInfo.AnimName, state == TotemState.Idle);

        if (aniInfo.EffectPrefab != null)
        {
            var simulPos = new Vector3(_totem.Pos.X.Float, _totem.Pos.Y.Float, 0.0f);

            var hitFx = PoolController.i.Spawn(_totem.Field.FieldName, aniInfo.EffectPrefab, simulPos, Quaternion.identity, SpawnRoot.i.Get((SpawnRoot.EMode)_totem.Field.SpawnRoot, SpawnRoot.EType.Root_FX));
            if (hitFx != null)
            {
                int sortingOrder = 1;
                float duration = 1.0f;

                if (state == TotemState.Idle)
                {
                    sortingOrder = -2;
                    duration = IdleDurationSecond;
                }
                else
                {
                    sortingOrder = 1;

                    var ps = hitFx.GetComponent<ParticleSystemController>();
                    if (ps != null)
                    {
                        duration = ps.duration;
                    }
                    else
                    {
                        var psChild = hitFx.transform.GetChild(0).GetComponent<ParticleSystem>();

                        if (psChild != null)
                        {
                            duration = psChild.main.duration;
                        }
                    }
                }

                SetLayer(hitFx.transform, sortingOrder);
                SetLayer_Renderer(hitFx.transform);
                SetSortingGroup(hitFx.transform, sortingOrder);

                _effectMonoDic.Add(state, hitFx);

                //PoolController.i.Despawn(hitFx, duration);
            }
        }

        if (aniInfo.SFX != GameDB.ESoundKeyType.None && _totem.Owner != null)
        {
            var ownerMono = _totem.Owner.GetMonoEntity();
            if (ownerMono != null)
            {
                ownerMono.PlaySFX(aniInfo.SFX);
            }
        }
    }

    public void OnShowEnter(BaseEntity target)
    {
        if (target == null)
            return;

        var targetMono = target.GetMonoEntity();
        if (targetMono == null)
            return;

        GameObject prefab = null;
        ESoundKeyType soundKey = ESoundKeyType.None;

        if (TargetType == ETotemTargetType.All && AbilityGroupId2 > 0 && _totem.Owner.Faction != target.Faction)
        {
            prefab = EnterPrefab2;
            soundKey = EnterSFX2;
        }
        else
        {
            prefab = EnterPrefab;
            soundKey = EnterSFX;
        }

        if (prefab != null)
        {
            var simulPos = new Vector3(target.Pos.X.Float, target.Pos.Y.Float, 0.0f);

            var hitFx = PoolController.i.Spawn(target.GetField().FieldName, prefab, simulPos, Quaternion.identity);
            if (hitFx != null)
            {
                var ps = hitFx.GetComponent<ParticleSystemController>();
                SetLayer(hitFx.transform, 1);
                SetLayer_Renderer(hitFx.transform);

                float duration = 1.0f;

                if (ps != null)
                {
                    duration = ps.duration;
                }
                else
                {
                    var psChild = hitFx.transform.GetChild(0).GetComponent<ParticleSystem>();

                    if (psChild != null)
                    {
                        duration = psChild.main.duration;
                    }
                }

                hitFx.transform.SetParent(targetMono.transform);

                PoolController.i.Despawn(hitFx, duration);
            }
        }

        if (soundKey != GameDB.ESoundKeyType.None)
        {
            targetMono.PlaySFX(soundKey);
        }
    }

    private void SetLayer(Transform target, int sortingOrder)
    {
        target.SetLayer(LayerMask.NameToLayer(_sortingLayerName));
        target.SetOrderInLayer(SortingLayer.NameToID(_sortingLayerName), sortingOrder);
    }

    private void SetLayer_Renderer(Transform target)
    {
        foreach (var r in target.GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerName = _sortingLayerName;
        }
    }

    private void SetSortingGroup(Transform target, int sortingOrder)
    {
        var sg = GetComponent<SortingGroup>();
        if (sg == null)
        {
            sg = gameObject.AddComponent<SortingGroup>();
        }

        sg.sortingLayerName = _sortingLayerName;
        sg.sortingOrder = sortingOrder; //캐릭터가 0, 캐릭터 앞 3이므로 캐릭터 뒤는 -2
    }

    public void RemoveEffect(TotemState state)
    {
        if (_effectMonoDic.TryGetValue(state, out GameObject effect) == false)
        {
            return;
        }

        PoolController.i.Despawn(effect);
        _effectMonoDic.Remove(state);
    }
    private void DeSpawnMonoObject(GameObject obj, float duration = 0)
    {
        if (obj == null)
        {
            return;
        }

        if (_totem.Field.IsTestField)
        {
            Destroy(obj, duration);
            return;
        }

        PoolController.i.Despawn(obj, duration);
    }
}