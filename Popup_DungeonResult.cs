using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using GameDB;
using Lean.Pool;
using Spine.Unity;
using DG.Tweening;
using diverseshared;

public class Popup_DungeonResult : PopupBase
{
	#region SerializeField

	[SerializeField]
	private FullStretchScale[] _fullStretchScaleArray;

	[SerializeField]
	private SkeletonGraphic _completeAnimation;

    [SerializeField]
    private SkeletonGraphic _failAnimation;

	[SerializeField]
	private SkeletonGraphic _resultAnimation;

	[SerializeField]
	private SkeletonGraphic _arenaAnimation;

	[SerializeField]
	private RectTransform _portraitPanel;

	[SerializeField]
	private Image _portrait01;

	[SerializeField]
	private Image _portrait02;

	[SerializeField]
	private Image _portrait03;

	[SerializeField]
	private RectTransform _sliderPanel;

	[SerializeField]
	private HeroExpSliderDirection _slider01;

	[SerializeField]
	private HeroExpSliderDirection _slider02;

	[SerializeField]
	private HeroExpSliderDirection _slider03;

	[SerializeField]
	private GameObject _lightEffect;

	[SerializeField]
	private CanvasGroup _btnGroup;

	[SerializeField]
	private Button _btnConfirmPanel;

	[SerializeField]
	private Button _btnConfirm;

	[SerializeField]
	private Button _btnRetry;

	[SerializeField]
	private Button _btnRetry2;

	[SerializeField]
	private Text _clearTimeText;

	[SerializeField]
	private Image _rankIcon;

	[SerializeField]
	private GameObject _scoreInfo;

	[SerializeField]
	private GameObject _gradeInfo;

	[SerializeField]
	private GameObject _arenaInfo;

	[SerializeField]
	private GameObject _itemPanel;

	[SerializeField]
	private GameObject _itemSlot;

	[SerializeField]
	private GameObject _arenaItemPanel;

	[SerializeField]
	private GameObject _arenaItemSlot;

	[SerializeField]
	private Text _timerText;

	[SerializeField]
	private GameObject _blockPanel;

	[SerializeField]
	private float _offset = 0f;

	#endregion SerializeField

	private enum EClearType
	{
		ClearTime = 0,
		DamageRank = 1,
		Score = 2,
		Defeat = 3,
	}

	private enum EResultType
	{
		Complete = 0,
		Fail = 1,
		Result = 2,
		Win = 3,
		Lose = 4,
	}

	private EClearType _clearType = EClearType.ClearTime;
	private EResultType _resultType = EResultType.Complete;
	private Global.EGameModeType _modeType = Global.EGameModeType.Dungeon;
	private bool _isSweep = false;

	private int _clearTime = 0;
	private int _clearRank = 0;
	//private long _clearScore = 0;
	//private int _clearScoreRank = 0;
	//private bool _isBestScore = false;
	private List<PDeltaInfo> _rewardInfos = new List<PDeltaInfo>();

	private List<uint> _heroTids = new List<uint>();
	private float _heroSliderValue01 = 0f;
	private float _heroSliderValue02 = 0f;
	private float _heroSliderValue03 = 0f;

	private List<ItemSlotUI> _slots = new List<ItemSlotUI>();
	private List<ItemSlotUI> _arenaSlots = new List<ItemSlotUI>();

	private Coroutine _openCoroutine = null;
	private Coroutine _closeCoroutine = null;
	private int _remainSec = 0;
	private WaitForSeconds _wait_100ms = new WaitForSeconds(0.1f);
	private WaitForSeconds _wait_300ms = new WaitForSeconds(0.3f);
	private WaitForSeconds _wait_600ms = new WaitForSeconds(0.6f);
	private WaitForSeconds _wait_1000ms = new WaitForSeconds(1f);
	private WaitForSeconds _wait_1500ms = new WaitForSeconds(1.5f);

	private bool _isTimerCounting = false;
	private float _accumDelta = 0f;
	private readonly float _timerCool = 1.5f;

	private int _processTickPerSecond = 0;
	private readonly int _rewardViewCount = 3;

	private bool _isDirecting = false;

	private long _lastScore = 0;
	private long _curScore = 0;

	protected override void Awake()
	{
		base.Awake();

		_processTickPerSecond = GameDBContainer.Instance.config.ProcessTickPerSecond;

		if (_btnConfirm)
		{
			_btnConfirm.
				OnClickAsObservable().
				ThrottleFirst(TimeSpan.FromSeconds(0.5)).
				Where(_w => ButtonExcuteCondition.Invoke()).
				Subscribe(_x =>
				{
					GlobalSFXManager.i.PlayCommonButtonSound();

					HideView();
					ReturnToIdle();
				});
		}

		if (_btnConfirmPanel)
		{
			_btnConfirmPanel.
				OnClickAsObservable().
				ThrottleFirst(TimeSpan.FromSeconds(0.5)).
				Where(_w => ButtonExcuteCondition.Invoke()).
				Subscribe(_x =>
				{
					GlobalSFXManager.i.PlayCommonButtonSound();

					HideView();
					ReturnToIdle();
				});
		}

		if (_btnRetry)
		{
			_btnRetry.
				OnClickAsObservable().
				ThrottleFirst(TimeSpan.FromSeconds(0.5)).
				Where(_w => ButtonExcuteCondition.Invoke()).
				Subscribe(_x =>
				{
					GlobalSFXManager.i.PlayCommonButtonSound();

					HideView();
					Retry();
				});
		}

		if (_btnRetry2)
		{
			_btnRetry2.
				OnClickAsObservable().
				ThrottleFirst(TimeSpan.FromSeconds(0.5)).
				Where(_w => ButtonExcuteCondition.Invoke()).
				Subscribe(_x =>
				{
					GlobalSFXManager.i.PlayCommonButtonSound();

					HideView();
					Retry();
				});
		}
	}

    private void Update()
    {
		if (_isTimerCounting)
		{
			_accumDelta += Time.deltaTime;
			if (_accumDelta >= _timerCool)
			{
				_accumDelta = 0f;
				_isTimerCounting = false;
				_clearTimeText.text = GetTimerText(_clearTime);

				return;
			}

			var lerpTime = Mathf.Lerp(0f, _clearTime, _accumDelta / _timerCool);
			_clearTimeText.text = GetTimerText((int)lerpTime);
		}
	}

    public void Show(uint stageTid, int startTick, int endTick, int currentTick, bool isComplete, List<PDeltaInfo> rewardInfos, List<uint> heroTids, bool isSweep = false)
	{
		ShowView();

		Init();

		_clearType = EClearType.ClearTime;
		_modeType = Global.EGameModeType.Dungeon;
		_clearTime = currentTick - startTick;
		_resultType = isComplete ? EResultType.Complete : EResultType.Fail;
		_isSweep = isSweep;

		_heroTids.Clear();
		foreach (var heroTid in heroTids)
		{
			_heroTids.Add(heroTid);
		}

		CheckOffset();
		CheckReward(rewardInfos);
		CheckTicket(stageTid);

		StartOpenDirection();
	}

	public void Show(uint stageTid, int rank, BigDecimal damage, bool isComplete, List<PDeltaInfo> rewardInfos, List<uint> heroTids, bool isSweep = false)
	{
		ShowView();

		Init();

		_clearType = EClearType.DamageRank;
		_modeType = Global.EGameModeType.Dungeon;
		_clearRank = rank;
		_resultType = EResultType.Result;
		_isSweep = isSweep;

		_heroTids.Clear();
		foreach (var heroTid in heroTids)
		{
			_heroTids.Add(heroTid);
		}

		CheckOffset();
		CheckReward(rewardInfos);
		CheckTicket(stageTid);

		StartOpenDirection();
	}

	public void ShowTactical(uint stageTid, int startTick, int endTick, int currentTick,
		/*long score, int scoreRank, bool isBestScore,*/
		bool isComplete, List<PDeltaInfo> rewardInfos, List<uint> heroTids, bool isSweep = false)
	{
		ShowView();

		Init();

		_clearType = EClearType.Score;
		_modeType = Global.EGameModeType.Tactical;
		_clearTime = currentTick - startTick;
		//_clearScore = score;
		//_clearScoreRank = scoreRank;
		//_isBestScore = isBestScore;
		_resultType = isComplete ? EResultType.Complete : EResultType.Fail;
		_isSweep = isSweep;

		_heroTids.Clear();
		foreach (var heroTid in heroTids)
		{
			_heroTids.Add(heroTid);
		}

		CheckOffset();
		CheckReward(rewardInfos);
		CheckTicket(stageTid);

		StartOpenDirection();
	}

	public void ShowChallenge(int startTick, int endTick, int currentTick, bool isComplete, List<PDeltaInfo> rewardInfos, List<uint> heroTids, long lastScore, long curScore)
	{
		ShowView();

		Init();

		_clearType = EClearType.ClearTime;
		_modeType = Global.EGameModeType.Challenge;
		_clearTime = currentTick - startTick;
		_resultType = isComplete ? EResultType.Win : EResultType.Lose;
		_isSweep = false;

		_heroTids.Clear();
		foreach (var heroTid in heroTids)
		{
			_heroTids.Add(heroTid);
		}

		_lastScore = lastScore;
		_curScore = curScore;

		CheckOffset();
		CheckReward(rewardInfos);
		CheckTicket(0);

		StartOpenDirection();
	}

	public void ShowRaid(int startTick, int endTick, int currentTick, bool isComplete, List<PDeltaInfo> rewardInfos, uint heroTid)
	{
		ShowView();

		Init();

		_clearType = EClearType.ClearTime;
		_modeType = Global.EGameModeType.Raid;
		_clearTime = currentTick - startTick;
		_resultType = isComplete ? EResultType.Complete : EResultType.Fail;
		_isSweep = false;

		_heroTids.Clear();
		_heroTids.Add(heroTid);

		CheckOffset();
		CheckReward(rewardInfos);
		CheckTicket(0);

		StartOpenDirection();
	}

	public void ShowUpgradeBattle(int startTick, int endTick, int currentTick, bool isComplete, uint heroTid)
	{
		ShowView();

		Init();

		_clearType = EClearType.Defeat;
		_modeType = Global.EGameModeType.UpgradeBattle;
		_clearTime = currentTick - startTick;
		_resultType = isComplete ? EResultType.Complete : EResultType.Fail;
		_isSweep = false;

		_heroTids.Clear();
		_heroTids.Add(heroTid);

		CheckOffset();
		CheckReward(null);
		CheckTicket(0);

		StartOpenDirection();
	}

	public override void HideView()
	{
		if (_openCoroutine != null)
		{
			StopCoroutine(_openCoroutine);
			_openCoroutine = null;
		}

		if (_closeCoroutine != null)
		{
			StopCoroutine(_closeCoroutine);
			_closeCoroutine = null;
		}

		base.HideView();
	}

    private void Init()
    {
		foreach (var iter in _fullStretchScaleArray)
		{
			iter.enabled = true;
			iter.Apply();
		}

		_completeAnimation.GetComponent<CanvasGroup>().alpha = 0f;
		_failAnimation.GetComponent<CanvasGroup>().alpha = 0f;
		_resultAnimation.GetComponent<CanvasGroup>().alpha = 0f;
		_arenaAnimation.GetComponent<CanvasGroup>().alpha = 0f;

		_portrait01.gameObject.SetActive(false);
		_portrait02.gameObject.SetActive(false);
		_portrait03.gameObject.SetActive(false);

		_slider01.gameObject.SetActive(false);
		_slider02.gameObject.SetActive(false);
		_slider03.gameObject.SetActive(false);
		_sliderPanel.gameObject.SetActive(false);

		_lightEffect.SetActive(false);
		_btnGroup.alpha = 0f;
		_btnGroup.gameObject.SetActive(false);
		_btnConfirmPanel.gameObject.SetActive(false);
		_clearTimeText.gameObject.SetActive(false);
		_rankIcon.gameObject.SetActive(false);
		_scoreInfo.SetActive(false);
		_gradeInfo.SetActive(false);
		_arenaInfo.SetActive(false);
		_timerText.text = "";

		for (int i = 0; i < _slots.Count; i++)
		{
			if (_slots[i] != null)
			{
				_slots[i].gameObject.SetActive(false);
			}
		}

		for (int i = 0; i < _arenaSlots.Count; i++)
		{
			if (_arenaSlots[i] != null)
			{
				_arenaSlots[i].gameObject.SetActive(false);
			}
		}

		_itemPanel.SetActive(false);
		_arenaItemPanel.SetActive(false);
	}

	private void CheckOffset()
	{
		if (_heroTids.Count == 2)
		{
			_portraitPanel.anchoredPosition = new Vector2(_offset, _portraitPanel.anchoredPosition.y);
			_sliderPanel.anchoredPosition = new Vector2(-_offset / 2, _sliderPanel.anchoredPosition.y);
		}
		else
		{
			_portraitPanel.anchoredPosition = new Vector2(0f, _portraitPanel.anchoredPosition.y);
			_sliderPanel.anchoredPosition = new Vector2(0f, _sliderPanel.anchoredPosition.y);
		}
	}

	private void CheckReward(List<PDeltaInfo> rewardInfos)
	{
		_rewardInfos.Clear();

		if (rewardInfos == null)
			return;

		bool isExp = false;

		foreach (var info in rewardInfos)
		{
			if (info == null)
			{
				continue;
			}

			// exp 하드코딩 - 하나만 표시하기 위해 거름
			if (info.DeltaType == EDeltaType.HeroExp)
			{
				if (!isExp)
				{
					_rewardInfos.Add(info);
				}
				isExp = true;

				int heroOrder = 0;
				foreach (var heroTid in _heroTids)
				{
					heroOrder++;
					if (heroTid == (uint)info.Target)
					{
						break;
					}
				}

				switch (heroOrder)
				{
					case 1:
						_slider01.gameObject.SetActive(true);
						_slider01.Init((uint)info.Target, info.Delta);
						break;

					case 2:
						_slider02.gameObject.SetActive(true);
						_slider02.Init((uint)info.Target, info.Delta);
						break;

					case 3:
						_slider03.gameObject.SetActive(true);
						_slider03.Init((uint)info.Target, info.Delta);
						break;
				}
			}
			else
			{
				_rewardInfos.Add(info);
			}
		}
	}

    private async void CheckTicket(uint stageTid)
	{
		ERewardCategoryType needResourceType = ERewardCategoryType.None;
		uint needResourceTid = 0;

		if (_modeType == Global.EGameModeType.Dungeon)
		{
			_btnRetry2.gameObject.SetActive(false);

			if (GameDBContainer.Instance.GrowthStageList.TryGetValue(stageTid, out GameDB.GrowthStage stageDB) == false)
			{
				_btnRetry.gameObject.SetActive(false);
				return;
			}

			needResourceType = stageDB.NeedResourceType;
			needResourceTid = (uint)stageDB.NeedResourceTid;
		}
		else if (_modeType == Global.EGameModeType.Tactical)
		{
			_btnRetry2.gameObject.SetActive(false);

			if (GameDBContainer.Instance.TacticalStageList.TryGetValue(stageTid, out GameDB.TacticalStage stageDB) == false)
			{
				_btnRetry.gameObject.SetActive(false);
				return;
			}

			needResourceType = stageDB.NeedResourceType;
			needResourceTid = (uint)stageDB.NeedResourceTid;
		}
		else if (_modeType == Global.EGameModeType.Challenge || _modeType == Global.EGameModeType.Raid)
		{
			_btnRetry.gameObject.SetActive(false);
			_btnRetry2.gameObject.SetActive(false);
			return;
		}
		else if (_modeType == Global.EGameModeType.UpgradeBattle)
		{
			_btnRetry.gameObject.SetActive(false);
			_btnRetry2.gameObject.SetActive(_resultType == EResultType.Fail);
			return;
		}

		_btnRetry.gameObject.SetActive(_resultType == EResultType.Fail);

		if (_resultType == EResultType.Fail)
		{
			if (GameDBContainer.Instance.ItemList.TryGetValue(needResourceTid, out GameDB.Item itemDB))
			{
				if (GlobalUtil.GetResourceIconValue(needResourceType, needResourceTid, out string icon, out double value))
				{
					var sprite = await AssetFiles.LoadAssetAsync<Sprite>(icon);
					GlobalUtil.SetChildImage(_btnRetry.transform, "Icon", sprite);

					ulong itemCount = (ulong)value;
					GlobalUtil.SetChildText(_btnRetry.transform, "Text", GlobalUtil.GetMarkStr(itemCount, Global.EMarkPointType.Two, true));

					GlobalUtil.SetBtnDimmed(_btnRetry, itemCount <= 0);
					GlobalUtil.SetChildTextColor(_btnRetry.transform, "RetryText", itemCount <= 0 ? Color.gray : Color.white);
				}
			}
		}
	}

	private string GetTimerText(int clearTime)
	{
		if (clearTime > 0 && _processTickPerSecond > 0)
		{
			var totalSecond = clearTime / _processTickPerSecond;
			var remainTick = clearTime % _processTickPerSecond;
			var minute = totalSecond / 60;
			var second = totalSecond % 60;
			var millisecond = remainTick * GameController.CoolSecond;

			return string.Format("{0}:{1:D2}.{2}", minute, second, (int)millisecond);
		}

		return "0:00.00";
	}

	private void StartOpenDirection()
	{
		_isDirecting = true;

		if (_openCoroutine != null)
		{
			StopCoroutine(_openCoroutine);
		}

		_openCoroutine = StartCoroutine(OnOpenCoroutine());
	}

	IEnumerator OnOpenCoroutine()
	{
		_blockPanel.SetActive(true);

		// 연출 시작
		if (_resultType == EResultType.Complete)
		{
			_completeAnimation.AnimationState.SetAnimation(0, "animation", false);
			GlobalSFXManager.i.PlayUISound(ESoundKeyType.SFX_UI_Dungeon_Clear);
		}
		else if (_resultType == EResultType.Fail)
		{
			_failAnimation.AnimationState.SetAnimation(0, "animation", false);
			GlobalSFXManager.i.PlayUISound(ESoundKeyType.SFX_UI_Dungeon_Fail);
		}
		else if (_resultType == EResultType.Result)
		{
			_resultAnimation.AnimationState.SetAnimation(0, "animation", false);
			GlobalSFXManager.i.PlayUISound(ESoundKeyType.SFX_UI_Dungeon_Clear);
		}
		else if (_resultType == EResultType.Win)
		{
			_arenaAnimation.AnimationState.SetAnimation(0, "Win_Spwan", false);
			GlobalSFXManager.i.PlayUISound(ESoundKeyType.SFX_UI_Dungeon_Clear);
		}
		else if (_resultType == EResultType.Lose)
		{
			_arenaAnimation.AnimationState.SetAnimation(0, "Lose_Spwan", false);
			GlobalSFXManager.i.PlayUISound(ESoundKeyType.SFX_UI_Dungeon_Fail);
		}

		// 애니메이션 초기화 문제로 잠시 대기
		yield return _wait_100ms;

		if (_resultType == EResultType.Complete)
		{
			_completeAnimation.GetComponent<CanvasGroup>().alpha = 1f;
		}
		else if (_resultType == EResultType.Fail)
		{
			_failAnimation.GetComponent<CanvasGroup>().alpha = 1f;
		}
		else if (_resultType == EResultType.Result)
		{
			_resultAnimation.GetComponent<CanvasGroup>().alpha = 1f;
		}
		else if (_resultType == EResultType.Win || _resultType == EResultType.Lose)
		{
			_arenaAnimation.GetComponent<CanvasGroup>().alpha = 1f;
		}

		if (_modeType == Global.EGameModeType.Challenge)
		{
			_arenaItemPanel.SetActive(true);
			GlobalUtil.SetChildText(_arenaItemPanel.transform, "Title", "");
			GlobalUtil.SetChildText(_arenaItemPanel.transform, "Desc", "");
		}
		else
		{
			_itemPanel.SetActive(true);
		}

		yield return _wait_600ms;

		if (_heroTids != null)
		{
			if (_heroTids.Count >= 1)
			{
				if (GameDBContainer.Instance.HeroList.TryGetValue(_heroTids[0], out var heroSheet))
				{
					_portrait01.gameObject.SetActive(true);
					_portrait01.sprite = AssetFiles.LoadAsset<Sprite>(heroSheet.FullSizePng);
					PlayTween(_portrait01.transform);

					yield return _wait_100ms;
				}
			}

			if (_heroTids.Count >= 2)
			{
				if (GameDBContainer.Instance.HeroList.TryGetValue(_heroTids[1], out var heroSheet))
				{
					_portrait02.gameObject.SetActive(true);
					_portrait02.sprite = AssetFiles.LoadAsset<Sprite>(heroSheet.FullSizePng);
					PlayTween(_portrait02.transform);
				}
			}

			if (_heroTids.Count >= 3)
			{
				if (GameDBContainer.Instance.HeroList.TryGetValue(_heroTids[2], out var heroSheet))
				{
					_portrait03.gameObject.SetActive(true);
					_portrait03.sprite = AssetFiles.LoadAsset<Sprite>(heroSheet.FullSizePng);
					PlayTween(_portrait03.transform);
				}
			}

			if (_heroTids.Count >= 2)
			{
				yield return _wait_300ms;
			}
		}

		if (_resultType == EResultType.Complete || _resultType == EResultType.Result || _resultType == EResultType.Win)
		{
			_lightEffect.SetActive(true);
			PlayTween(_lightEffect.transform);
		}

		if (_slider01.gameObject.activeSelf || _slider02.gameObject.activeSelf || _slider03.gameObject.activeSelf)
		{
			_sliderPanel.gameObject.SetActive(true);
			PlayTween(_sliderPanel);
		}

		yield return _wait_100ms;

		////////////////////////////// 보상 세팅 //////////////////////////////
		for (int i = 0; i < _rewardInfos.Count; i++)
		{
			ItemSlotUI itemSlotUI = null;

			if (_modeType == Global.EGameModeType.Challenge)
			{
				if (_resultType == EResultType.Win)
				{
					GlobalUtil.SetChildText(_arenaItemPanel.transform, "Title", MultiLanguageManager.i.GetText("ArenaStage_UI_Result_Desc1")); // 승리 보상
				}
				else if (_resultType == EResultType.Lose)
				{
					GlobalUtil.SetChildText(_arenaItemPanel.transform, "Title", MultiLanguageManager.i.GetText("ArenaStage_UI_Result_Desc2")); // 패배 보상
				}

				if (i < _arenaSlots.Count)
				{
					itemSlotUI = _arenaSlots[i];
				}
				else
				{
					GameObject obj = LeanPool.Spawn(_arenaItemSlot, _arenaItemSlot.transform.parent);
					if (obj != null)
					{
						itemSlotUI = obj.GetComponent<ItemSlotUI>();
						_arenaSlots.Add(itemSlotUI);
					}
				}
			}
			else
			{
				if (i < _slots.Count)
				{
					itemSlotUI = _slots[i];
				}
				else
				{
					GameObject obj = LeanPool.Spawn(_itemSlot, _itemSlot.transform.parent);
					if (obj != null)
					{
						itemSlotUI = obj.GetComponent<ItemSlotUI>();
						_slots.Add(itemSlotUI);
					}
				}
			}

			if (itemSlotUI != null)
			{
				itemSlotUI.gameObject.SetActive(true);
				itemSlotUI.Set(_rewardInfos[i].DeltaType, (uint)_rewardInfos[i].Target, _rewardInfos[i].Delta);

				PlayTween(itemSlotUI.transform);
			}

			if (i < _rewardViewCount)
			{
				yield return _wait_100ms;
			}
		}
		////////////////////////////////////////////////////////////

		if (_clearType == EClearType.ClearTime || _clearType == EClearType.Score)
		{
			if (_modeType == Global.EGameModeType.Challenge)
			{
				_arenaInfo.SetActive(true);

				var challengeRank = GameDBMapper.Instance.GetChallengeTier(_curScore);
				if (challengeRank != null)
				{
					GlobalUtil.SetChildSprite(_arenaInfo.transform, "TierIcon", challengeRank.IconfPrefab);
				}

				var strFormat = _curScore > _lastScore ? "{0}<color=#1ddb16>(▲{1})</color>" : "{0}<color=#ff0000>(▼{1})</color>";
				GlobalUtil.SetChildText(_arenaInfo.transform, "ScoreText", string.Format(strFormat, _curScore, _curScore - _lastScore));

				PlayTween(_arenaInfo.transform);
			}
			else
			{
				if (_resultType == EResultType.Complete)
				{
					if (_clearTime > 0)
					{
						_clearTimeText.gameObject.SetActive(true);
						_clearTimeText.text = GetTimerText(0);
						_accumDelta = 0f;
						_isTimerCounting = true;
					}

					//if (_clearType == EClearType.Score && _clearScore > 0)
					//{
					//	_scoreInfo.SetActive(true);

					//	if (_isBestScore)
					//	{
					//		GlobalUtil.SetChildActive(_scoreInfo.transform, "BG", true);

					//		if (_clearScoreRank > 0)
					//		{
					//			GlobalUtil.SetChildText(_scoreInfo.transform, "BG/RankText", string.Format(MultiLanguageManager.i.GetText("Rank_UI_Desc8"), _clearScoreRank));
					//		}
					//		else
					//		{
					//			GlobalUtil.SetChildText(_scoreInfo.transform, "BG/RankText", string.Format(MultiLanguageManager.i.GetText("Rank_UI_Desc8"), "-"));
					//		}
					//	}
					//	else
					//	{
					//		GlobalUtil.SetChildActive(_scoreInfo.transform, "BG", false);
					//	}

					//	var clearScoreStr = string.Format("<size=40><color=#FFE63C>{0}</color></size>", _clearScore.ToString("N0"));
					//	GlobalUtil.SetChildText(_scoreInfo.transform, "ScoreText", string.Format(MultiLanguageManager.i.GetText("Result_UI_Desc1"), clearScoreStr));

					//	GlobalUtil.SetChildActive(_scoreInfo.transform, "ScoreText/Best", _isBestScore);

					//	PlayTween(_scoreInfo.transform);
					//}
				}
			}
		}
		else if (_clearType == EClearType.DamageRank)
		{
			if (_clearRank > 0)
			{
				_rankIcon.gameObject.SetActive(true);

				GlobalUtil.GetDungeonRankIcon(_clearRank, out string spriteName);
				GlobalUtil.SetSprite(_rankIcon, spriteName, true);

				PlayTween(_rankIcon.transform);
			}
			else
			{
				_rankIcon.gameObject.SetActive(false);
			}
		}
		else if (_clearType == EClearType.Defeat)
		{
			if (_resultType == EResultType.Complete)
			{
				if (_heroTids != null && _heroTids.Count > 0)
				{
					var heroInfo = SgModel.Instance.LocalPlayer.GetHeroModel(_heroTids[0]);
					if (heroInfo != null)
					{
						_gradeInfo.SetActive(true);

						GlobalUtil.SetChildActive(_gradeInfo.transform, "Complete", true);
						GlobalUtil.SetChildActive(_gradeInfo.transform, "Fail", false);

						var beforeGrade = GlobalUtil.GetChildComponent<HeroGradeSlot>(_gradeInfo.transform, "Complete/BeforeGrade");
						if (beforeGrade != null)
						{
							beforeGrade.SetSlot(heroInfo.CurGrade - 1);
						}

						var nextGrade = GlobalUtil.GetChildComponent<HeroGradeSlot>(_gradeInfo.transform, "Complete/NextGrade");
						if (nextGrade != null)
						{
							nextGrade.SetSlot(heroInfo.CurGrade);
						}

						var beforeGradeDef = GameDBMapper.Instance.FindGradeSheet(heroInfo.CurGrade - 1);
						if (beforeGradeDef != null)
						{
							GlobalUtil.SetChildText(_gradeInfo.transform, "Complete/LevelText", beforeGradeDef.MaxLevel.ToString());
						}

						var nextGradeDef = GameDBMapper.Instance.FindGradeSheet(heroInfo.CurGrade);
						if (nextGradeDef != null)
						{
							GlobalUtil.SetChildText(_gradeInfo.transform, "Complete/NextLevelText", nextGradeDef.MaxLevel.ToString());
						}

						PlayTween(_gradeInfo.transform);
					}
				}
			}
			else if (_resultType == EResultType.Fail)
			{
				if (_heroTids != null && _heroTids.Count > 0)
				{
					var heroInfo = SgModel.Instance.LocalPlayer.GetHeroModel(_heroTids[0]);
					if (heroInfo != null)
					{
						_gradeInfo.SetActive(true);

						GlobalUtil.SetChildActive(_gradeInfo.transform, "Complete", false);
						GlobalUtil.SetChildActive(_gradeInfo.transform, "Fail", true);

						var curGrade = GlobalUtil.GetChildComponent<HeroGradeSlot>(_gradeInfo.transform, "Fail/CurGrade");
						if (curGrade != null)
						{
							curGrade.SetSlot(heroInfo.CurGrade);
						}

						var gradeDef = GameDBMapper.Instance.FindGradeSheet(heroInfo.CurGrade);
						if (gradeDef != null)
						{
							string strCurLevel = string.Format("최대 레벨 {0}", gradeDef.MaxLevel.ToString());
							GlobalUtil.SetChildText(_gradeInfo.transform, "Fail/LevelText", strCurLevel);
						}

						PlayTween(_gradeInfo.transform);
					}
				}
			}
		}

		_slider01.Show();
		_slider02.Show();
		_slider03.Show();

		yield return _wait_1500ms;

		_blockPanel.SetActive(false);

		if (_modeType != Global.EGameModeType.Challenge)
		{
			_btnGroup.gameObject.SetActive(true);
			PlayTween(_btnGroup.transform);
			StartCloseTimer();
		}
		else
		{
			_btnConfirmPanel.gameObject.SetActive(true);

			var descText = GlobalUtil.GetChildComponent<Text>(_arenaItemPanel.transform, "Desc");
			if (descText != null)
			{
				descText.text = MultiLanguageManager.i.GetText("ArenaStage_UI_Result_Desc3"); // 화면을 터치하면 닫힙니다.
				PlayTween(descText.transform);
			}

			// 승급/강등 연출
			ShowPromotion();
		}

		_isDirecting = false;

		yield break;
	}

	private void StartCloseTimer()
	{
		PlayTween(_timerText.transform);

		if (_closeCoroutine != null)
		{
			StopCoroutine(_closeCoroutine);
		}

		_remainSec = 10;
		_closeCoroutine = StartCoroutine(OnCloseCoroutine());
	}

	IEnumerator OnCloseCoroutine()
	{
		while (_remainSec > 0)
		{
			string timerLocale = MultiLanguageManager.i.GetText("Result_UI_Msg0");
			_timerText.text = string.Format(timerLocale, _remainSec);
			_remainSec--;

			yield return _wait_1000ms;
		}

		HideView();
		ReturnToIdle();

		yield break;
	}

	private void PlayTween(Transform tr)
	{
		DOTweenAnimation[] tweens = tr.GetComponents<DOTweenAnimation>();
		if (tweens == null)
			return;

		foreach (var tween in tweens)
		{
			tween.DORewind();
			tween.DOPlayForward();
		}
	}

	private void ReturnToIdle()
	{
		if (_isSweep)
			return;

		// 방치형 모드로 돌아간다
		switch (_modeType)
		{
			case Global.EGameModeType.Dungeon:
				GameController.i.Dungeon.LeaveDungeon();
				break;

			case Global.EGameModeType.Tactical:
				GameController.i.Tactical.LeaveTactical();
				break;

			case Global.EGameModeType.Challenge:
				GameController.i.Challenge.LeaveChallenge();
				break;

			case Global.EGameModeType.Raid:
				GameController.i.Raid.LeaveRaid();
				break;

			case Global.EGameModeType.UpgradeBattle:
				GameController.i.UpgradeBattle.LeaveUpgradeBattle();
				break;
		}
	}

	private void Retry()
	{
		PopupManager.i.ShowPopup_Logo_Transition(() =>
		{
			switch (_modeType)
			{
				case Global.EGameModeType.Dungeon:
					GameController.i.Dungeon.RetryDungeon();
					break;

				case Global.EGameModeType.Tactical:
					GameController.i.Tactical.RetryTactical();
					break;

				case Global.EGameModeType.Challenge:
					GameController.i.Challenge.RetryChallenge();
					break;

				case Global.EGameModeType.UpgradeBattle:
					GameController.i.UpgradeBattle.RetryUpgradeBattle();
					break;
			}
			base.OnClickOkButton();
		}, 0.2f, () =>
		{
		});
	}

	private void ShowPromotion()
	{
		var prevRank = GameDBMapper.Instance.GetChallengeTier(_lastScore);
		var curRank = GameDBMapper.Instance.GetChallengeTier(_curScore);
		if (prevRank != null && curRank != null)
		{
			if (prevRank.tid != curRank.tid)
			{
				Popup_ChallengePromotion promotion = PopupManager.i.ShowPopup<Popup_ChallengePromotion>(Global.ePopupPrefab.Popup_ChallengePromotion);
				if (promotion)
				{
					promotion.Show(_lastScore, _curScore);
				}
			}
		}
	}

	#region 상속 오버라이드
	public override void BackButtonMsg()
	{
		if (_isDirecting)
			return;

		HideView();
		ReturnToIdle();
	}

	#endregion 상속 오버라이드
}
