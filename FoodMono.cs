using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;
using Lean.Pool;
using Spine.Unity;
using GameDB;
using diverseshared;
using GAP_ParticleSystemController;

public enum EFoodSpawnType
{
    Distance,
    Random,
}

public enum EFoodTargetType
{
    None = 0,
    Self = 1,
    Ally = 2,
    Enemy = 3,
    All = 4,
}

public class FoodMono : MonoBehaviour
{
    [System.Serializable]
    public struct FoodStateAniInfo
    {
        public string AnimName;
        public GameObject EffectPrefab;
        public ESoundKeyType SFX;
    }

#pragma warning disable 0649

    [SerializeField]
    private Transform _unit;

    [SerializeField]
    private SkeletonAnimation _anim;

    [SerializeField]
    private int _radius = 50;

    [SerializeField]
    private int _xOffset = 0;

    [SerializeField]
    private int _yOffset = 0;

    [Title("Info", "", TitleAlignments.Left)]
    public string Description;

    [TitleGroup("Spawn", "", TitleAlignments.Left)]
    [PropertyOrder(1)]
    public FoodStateAniInfo SpawnAniInfo;

    [TitleGroup("Spawn", "", TitleAlignments.Left)]
    [PropertyOrder(2)]
    [Range(0, 1000)]
    public int SpawnDuration;

    [TitleGroup("Spawn", "", TitleAlignments.Left)]
    [PropertyOrder(3)]
    [ShowInInspector]
    public float SpawnDurationSecond { get { return SpawnDuration * 0.025f; } }

    [TitleGroup("Idle 01", "", TitleAlignments.Left)]
    [PropertyOrder(1)]
    public EFoodTargetType TargetType01;

    [TitleGroup("Idle 01", "", TitleAlignments.Left)]
    [PropertyOrder(2)]
    [HideIf("TargetType01", EFoodTargetType.None)]
    public FoodStateAniInfo Idle01AniInfo;

    [TitleGroup("Idle 01", "", TitleAlignments.Left)]
    [PropertyOrder(3)]
    [HideIf("TargetType01", EFoodTargetType.None)]
    [Range(0, 1000)]
    public int Idle01Duration;

    [TitleGroup("Idle 01", "", TitleAlignments.Left)]
    [PropertyOrder(4)]
    [ShowInInspector]
    public float Idle01DurationSecond { get { return Idle01Duration * 0.025f; } }

    [TitleGroup("Idle 01", "", TitleAlignments.Left)]
    [PropertyOrder(5)]
    [HideIf("TargetType01", EFoodTargetType.None)]
    public int AbilityGroupId01;

    [TitleGroup("Idle 01", "", TitleAlignments.Left)]
    [PropertyOrder(6)]
    [HideIf("TargetType01", EFoodTargetType.None)]
    public GameObject RangeSpec01;

    [TitleGroup("Idle 01", "", TitleAlignments.Left)]
    [PropertyOrder(7)]
    [HideIf("TargetType01", EFoodTargetType.None)]
    public GameObject HitPrefab01;

    [TitleGroup("Idle 01", "", TitleAlignments.Left)]
    [PropertyOrder(8)]
    [HideIf("TargetType01", EFoodTargetType.None)]
    public ESoundKeyType HitSFX01;

    [TitleGroup("Idle 01", "", TitleAlignments.Left)]
    [PropertyOrder(9)]
    [HideIf("TargetType01", EFoodTargetType.None)]
    public GameObject AreaPrefab01;

    [TitleGroup("Idle 02", "", TitleAlignments.Left)]
    [PropertyOrder(1)]
    public EFoodTargetType TargetType02;

    [TitleGroup("Idle 02", "", TitleAlignments.Left)]
    [PropertyOrder(2)]
    [HideIf("TargetType02", EFoodTargetType.None)]
    public FoodStateAniInfo Idle02AniInfo;

    [TitleGroup("Idle 02", "", TitleAlignments.Left)]
    [PropertyOrder(3)]
    [HideIf("TargetType02", EFoodTargetType.None)]
    [Range(0, 1000)]
    public int Idle02Duration;

    [TitleGroup("Idle 02", "", TitleAlignments.Left)]
    [PropertyOrder(4)]
    [ShowInInspector]
    public float Idle02DurationSecond { get { return Idle02Duration * 0.025f; } }

    [TitleGroup("Idle 02", "", TitleAlignments.Left)]
    [PropertyOrder(5)]
    [HideIf("TargetType02", EFoodTargetType.None)]
    public int AbilityGroupId02;

    [TitleGroup("Idle 02", "", TitleAlignments.Left)]
    [PropertyOrder(6)]
    [HideIf("TargetType02", EFoodTargetType.None)]
    public GameObject RangeSpec02;

    [TitleGroup("Idle 02", "", TitleAlignments.Left)]
    [PropertyOrder(7)]
    [HideIf("TargetType02", EFoodTargetType.None)]
    public GameObject HitPrefab02;

    [TitleGroup("Idle 02", "", TitleAlignments.Left)]
    [PropertyOrder(8)]
    [HideIf("TargetType02", EFoodTargetType.None)]
    public ESoundKeyType HitSFX02;

    [TitleGroup("Idle 02", "", TitleAlignments.Left)]
    [PropertyOrder(9)]
    [HideIf("TargetType02", EFoodTargetType.None)]
    public GameObject AreaPrefab02;

    [TitleGroup("Despawn", "", TitleAlignments.Left)]
    [PropertyOrder(1)]
    public FoodStateAniInfo DespawnAniInfo;

    [TitleGroup("Despawn", "", TitleAlignments.Left)]
    [PropertyOrder(2)]
    [Range(0, 1000)]
    public int DespawnDuration;

    [TitleGroup("Despawn", "", TitleAlignments.Left)]
    [PropertyOrder(3)]
    [ShowInInspector]
    public float DespawnDurationSecond { get { return DespawnDuration * 0.025f; } }

    [TitleGroup("Spawn Type", "", TitleAlignments.Left)]
    [PropertyOrder(1)]
    public EFoodSpawnType FoodSpawnType;

    [TitleGroup("Spawn Type", "", TitleAlignments.Left)]
    [PropertyOrder(2)]
    [Range(-1000, 1000)]
    public int SpawnRange;

    [TitleGroup("Spawn Type", "", TitleAlignments.Left)]
    [PropertyOrder(3)]
    [ShowInInspector]
    public float SpawnRangeFloat { get { return F64.Ratio100(SpawnRange).Float; } }

    private readonly string _sortingLayerName = "BattleEntity";

#pragma warning restore

    public F64 Radius
    {
        get
        {
            return F64.Ratio100((int)_radius);
        }
    }

    public F64 XOffset
    {
        get
        {
            return F64.Ratio100((int)_xOffset);
        }
    }

    public F64 YOffset
    {
        get
        {
            return F64.Ratio100((int)_yOffset);
        }
    }

    private Food _food = null;

    private GameObject _areaFx = null;

#if UNITY_EDITOR

    public void OnDrawGizmos()
    {
        // 중점
        Vector3 centerPos = new Vector3(transform.position.x + (XOffset.Float * transform.localScale.x),
            transform.position.y + (YOffset.Float * transform.localScale.y));

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(centerPos, Radius.Float * transform.localScale.x);
    }

#endif

    public void Init(Food food)
    {
        _food = food;
        _food.SetMono(this);

        var simulPos = new Vector3((float)_food.Pos.X.Float, (float)_food.Pos.Y.Float, 0.0f);
        transform.position = simulPos;

        // 스케일 세팅
        transform.localScale = Vector3.one * _food.Scale.Float;
    }

    public void OnRemoved()
    {
        if (_areaFx != null)
        {
            PoolController.i.Despawn(_areaFx);
        }

        if (gameObject != null)
        {
            PoolController.i.Despawn(gameObject);
        }
    }

    public void SetAnimation(FoodStateAniInfo aniInfo, bool isLoop)
    {
        if (string.IsNullOrEmpty(aniInfo.AnimName))
            return;

        _anim.state.SetAnimation(0, aniInfo.AnimName, isLoop);

        if (aniInfo.EffectPrefab != null)
        {
            var simulPos = new Vector3(_food.Pos.X.Float, _food.Pos.Y.Float, 0.0f);

            var hitFx = PoolController.i.Spawn(_food.Field.FieldName, aniInfo.EffectPrefab, simulPos, Quaternion.identity, SpawnRoot.i.Get((SpawnRoot.EMode)_food.Field.SpawnRoot, SpawnRoot.EType.Root_FX));
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
                PoolController.i.Despawn(hitFx, duration);
            }
        }

        if (aniInfo.SFX != GameDB.ESoundKeyType.None && _food.Owner != null)
        {
            var ownerMono = _food.Owner.GetMonoEntity();
            if (ownerMono != null)
            {
                ownerMono.PlaySFX(aniInfo.SFX);
            }
        }
    }

    public void OnShowHit(int phase, BaseEntity target)
    {
        var targetMono = target.GetMonoEntity();
        if (targetMono == null)
            return;

        GameObject hitPrefab = null;
        ESoundKeyType hitSFX = ESoundKeyType.None;

        if (phase == 1)
        {
            hitPrefab = HitPrefab01;
            hitSFX = HitSFX01;
        }
        else if (phase == 2)
        {
            hitPrefab = HitPrefab02;
            hitSFX = HitSFX02;
        }
        else
        {
            return;
        }

        if (hitPrefab != null)
        {
            var simulPos = new Vector3(target.Pos.X.Float, target.Pos.Y.Float, 0.0f);

            var hitFx = PoolController.i.Spawn(_food.Field.FieldName, hitPrefab, simulPos, Quaternion.identity);
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

                LeanPool.Despawn(hitFx, duration);
            }
        }

        if (hitSFX != GameDB.ESoundKeyType.None)
        {
            targetMono.PlaySFX(hitSFX);
        }
    }

    public void OnShowArea(int phase)
    {
        if (_areaFx != null)
        {
            PoolController.i.Despawn(_areaFx);
            _areaFx = null;
        }

        GameObject areaPrefab = null;

        if (phase == 1)
        {
            areaPrefab = AreaPrefab01;
        }
        else if (phase == 2)
        {
            areaPrefab = AreaPrefab02;
        }
        else
        {
            return;
        }

        if (areaPrefab != null)
        {
            var simulPos = new Vector3(_food.Pos.X.Float, _food.Pos.Y.Float, 0.0f);

            _areaFx = PoolController.i.Spawn(_food.Field.FieldName, areaPrefab, simulPos, Quaternion.identity);
            if (_areaFx != null)
            {
                SetLayer(_areaFx.transform, 1);
                SetLayer_Renderer(_areaFx.transform);
            }
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
}
