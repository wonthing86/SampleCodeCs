using diverseshared;
using GameDB;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentPanel : MonoBehaviour
{
    #region SerializeField

    [SerializeField]
    private CheatController _cheatController;

    [SerializeField]
    private HeroLevelUpBox _heroLevelUpBox;

    [SerializeField]
    private ExtensionsToggleGroup _categoryToggleGroup;

    [SerializeField]
    private ExtensionsToggleGroup _groupToggleGroup;

    [SerializeField]
    private GameObject _groupToggle;

    [SerializeField]
    private ScrollRect _scrollRect;

    [SerializeField]
    private GameObject _iconPrefab;

    [SerializeField]
    private WeaponIcon _equippedIcon;

    [SerializeField]
    private GameObject _equippedTag;

    [SerializeField]
    private GameObject _equippedEmpty;

    [SerializeField]
    private GameObject _masteryInfo;

    [SerializeField]
    private GameObject _masteryExp;

    #endregion SerializeField

    private EquipmentController _equipmentController = null;

    private EEquipmentCategoryType _selectedCategoryType = EEquipmentCategoryType.Armor;
    public EEquipmentCategoryType SelectedCategoryType { get { return _selectedCategoryType; } }

    private uint _selectedGroupTid = 0;

    private uint _selectedHeroTid = 0;

    // 장비 그룹 탭
    private List<ExtensionsToggle> _groupToggles = new List<ExtensionsToggle>();

    // 선택된 장비 그룹 탭 캐싱
    private Dictionary<EEquipmentCategoryType, uint> _selectedGroupTids = new Dictionary<EEquipmentCategoryType, uint>();

    // 장비 그룹 Tid
    private Dictionary<EEquipmentCategoryType, List<uint>> _equipGroupTids = new Dictionary<EEquipmentCategoryType, List<uint>>();

    // 아이콘
    private List<WeaponIcon> _icons = new List<WeaponIcon>();

    // 모든 파츠 자동 장착용
    private Dictionary<uint, uint> _autoAllPartsList = new Dictionary<uint, uint>();
    private List<uint> _allPartsGroupList = new List<uint>();

    private bool _init = false;

    public EEquipmentCategoryType InitCategory = EEquipmentCategoryType.None;

    private Coroutine _coroutine = null;

    private StringBuilder _sb = new StringBuilder();

    private GameDB.EEquipmentRarity _composableRarity = EEquipmentRarity.None;

    private void Awake()
    {
        // 합성 가능 Rarity 기준
        foreach (var it in GameDBContainer.Instance.EquipmentCombineList)
        {
            if (_composableRarity < it.Value.NeedEquipmentRarity)
                _composableRarity = it.Value.NeedEquipmentRarity;
        }
    }

    private void Init()
    {
        if (!_init)
        {
            _selectedGroupTids.Add(EEquipmentCategoryType.Armor, 0);
            _selectedGroupTids.Add(EEquipmentCategoryType.Accessory, 0);

            _categoryToggleGroup.onToggleGroupToggleChangedWithObject.AddListener(CategoryToggleAction);
            _groupToggleGroup.onToggleGroupToggleChangedWithObject.AddListener(GroupToggleAction);

            _init = true;
        }
    }

    public void LoadFromGameDB(EquipmentController equipmentController)
    {
        _equipmentController = equipmentController;

        _equipGroupTids.Clear();

        // 탭 세팅
        foreach (var equipmentGroupDB in GameDBContainer.Instance.EquipmentGroupList.Values)
        {
            // 탭 판별용 정보 세팅
            if (!_equipGroupTids.ContainsKey(equipmentGroupDB.CategoryType))
            {
                _equipGroupTids.Add(equipmentGroupDB.CategoryType, new List<uint>());
            }

            if (_equipGroupTids.TryGetValue(equipmentGroupDB.CategoryType, out List<uint> tidList))
            {
                tidList.Add((uint)equipmentGroupDB.tid);
            }

            GameObject tabButton = Instantiate(_groupToggle, _groupToggleGroup.transform);
            if (tabButton != null)
            {
                tabButton.SetActive(false);
                tabButton.name = "Tab_0" + equipmentGroupDB.tid;

                ExtensionsToggle toggleComponent = tabButton.GetComponent<ExtensionsToggle>();
                if (toggleComponent != null)
                {
                    toggleComponent.UniqueID = equipmentGroupDB.tid.ToString();
                    toggleComponent.IsOn = false;

                    var groupName = MultiLanguageManager.i.GetText(equipmentGroupDB.EquipmentTypeName);

                    GlobalUtil.SetChildText(tabButton.transform, "Toggle_Off/Text", groupName);
                    GlobalUtil.SetChildText(tabButton.transform, "Toggle_On/Text", groupName);

                    _groupToggles.Add(toggleComponent);
                }
            }
        }
    }

    public void Open(uint heroTid = 0)
    {
        // 이미 열려있고 요청한 영웅 아이디가 같으면 리턴
        if (gameObject.activeSelf && _selectedHeroTid != 0 && heroTid == _selectedHeroTid)
        {
            return;
        }

        if (_coroutine != null)
        {
            StopCoroutine(_coroutine);
        }

        Init();

        if (heroTid > 0)
        {
            _selectedHeroTid = heroTid;
        }
        else
        {
            var selectedHeroId = GameController.i.Idle.GetSelectHeroTid();
            _selectedHeroTid = selectedHeroId == 0 ? 1 : selectedHeroId;
        }

        var heroModel = SgModel.Instance.LocalPlayer.GetHeroModel(_selectedHeroTid);
        if (heroModel == null)
            return;

        //_heroLevelUpBox.Set(heroModel);
        _equipmentController.OnSelectHero(_selectedHeroTid);

        if (InitCategory != EEquipmentCategoryType.None)
        {
            _selectedCategoryType = InitCategory;
            InitCategory = EEquipmentCategoryType.None;
        }

        TabCategoryToggle();
    }

    private void SetEquippedIcon(uint equipmentTid)
    {
        if (equipmentTid > 0)
        {
            _equippedTag.SetActive(true);
            _equippedEmpty.SetActive(false);
            _equippedIcon.SetEquipmentEquipInfo(equipmentTid, OnClickIcon);
        }
        else
        {
            _equippedTag.SetActive(false);
            _equippedEmpty.SetActive(true);
        }
    }

    private void SetMasteryInfo()
    {
        if (GameDBContainer.Instance.EquipmentGroupList.TryGetValue(_selectedGroupTid, out var groupDB) == false)
            return;

        GameDB.EquipmentMastery masteryDB = GameDBMapper.Instance.GetEquipmentMastery(_selectedGroupTid);
        if (masteryDB == null)
            return;

        var masteryModel = SgModel.Instance.LocalPlayer.GetEquipmentMasteryModel(_selectedGroupTid);
        if (masteryModel == null)
            return;

        GlobalUtil.SetChildSprite(_masteryInfo.transform, "Icon", masteryDB.Icon);
        GlobalUtil.SetChildText(_masteryInfo.transform, "Name", MultiLanguageManager.i.GetText(groupDB.EquipmentTypeName));

        var masteryLevel = string.Format(MultiLanguageManager.i.GetText("UI_MasteryLevel"), masteryModel.MasteryLevel); // <color=#FE6F01>LV.</color> {0}
        GlobalUtil.SetChildText(_masteryInfo.transform, "Level", masteryLevel);

        // exp slider
        var masteryExpBar = GlobalUtil.GetChildComponent<UISlicedFilledImage>(_masteryExp.transform, "Fill");
        if (masteryExpBar)
        {
            if (masteryModel.MasteryLevel < masteryDB.StatLvMax)
            {
                int curMaxMasteryExp = 0;
                var masteryLv = GameDBMapper.Instance.GetEquipmentMasteryLv(masteryModel.MasteryLevel);
                if (masteryLv != null)
                {
                    curMaxMasteryExp = masteryLv.Exp;
                }

                int nextMaxMasteryExp = 0;
                var nextMasteryLv = GameDBMapper.Instance.GetEquipmentMasteryLv(masteryModel.MasteryLevel + 1);
                if (nextMasteryLv != null)
                {
                    nextMaxMasteryExp = nextMasteryLv.Exp;
                }

                int curMasteryExp = masteryModel.MasteryExp - curMaxMasteryExp;
                int maxMasteryExp = nextMaxMasteryExp - curMaxMasteryExp;
                decimal masteryPer = maxMasteryExp > 0 ? ((decimal)curMasteryExp / maxMasteryExp * 100) : 100;

                masteryExpBar.fillAmount = (maxMasteryExp > 0) ? ((float)curMasteryExp / maxMasteryExp) : 1.0f;
                GlobalUtil.SetChildText(_masteryExp.transform, "Percent", string.Format("{0}%", masteryPer.ToString("N2")));
            }
            else
            {
                masteryExpBar.fillAmount = 1.0f;
                GlobalUtil.SetChildText(_masteryExp.transform, "Percent", MultiLanguageManager.i.GetText("Weapon_UI_Popup_Desc2"));
            }
        }

        // mastery stat coef
        var coef = GameDBMapper.Instance.FindEquipmentCoefSection((int)masteryDB.StatCoef, masteryModel.MasteryLevel);
        var stat = GameDBMapper.Instance.GetEquipmentStatValue(coef, masteryModel.MasteryLevel);

        if (PrintStat.Instance.GetNameAndValue(masteryDB.StatType, stat, false, out string nameStr, out string valueStr))
        {
            var strStat = string.Format("{0}<size=40><color=#FA6A00> +{1}</color></size>", MultiLanguageManager.i.GetText(nameStr), valueStr);
            GlobalUtil.SetChildText(_masteryExp.transform, "Stat", strStat);
        }
    }

    // 영웅 정보 갱신용
    public void RefreshHeroUI(HeroInfo info)
    {
        //if (info.Tid != _selectedHeroTid)
        //    return;

        //_heroLevelUpBox.Refresh();
    }

    // 영웅 장비 착용 갱신용
    public void RefreshHeroEquipmentUI(HeroInfo heroInfo)
    {
        heroInfo.Equipments.TryGetValue(_selectedGroupTid, out var equipTid);
        SetEquippedIcon(equipTid);
        SetMasteryInfo();
        SetEquipmentIconList(false);
    }

    // 장비 갱신용
    public void RefreshEquipmentUI(uint groupId)
    {
        var groupDB = GameDBMapper.Instance.GetEquipmentGroup(groupId);
        if (groupDB == null)
            return;

        if (_selectedGroupTid != groupDB.tid)
            return;

        SetMasteryInfo();
        SetEquipmentIconList(false);
    }

    private void CategoryToggleAction(ExtensionsToggle toggle)
    {
        if (toggle.IsOn)
        {
            GlobalSFXManager.i.PlayCommonButtonSound();
        }

        OnCategoryToggleAction(toggle, false);
    }

    private void OnCategoryToggleAction(ExtensionsToggle toggle, bool isRefresh)
    {
        var type = EnumStringExtension.ParseEnum<EEquipmentCategoryType>(toggle.UniqueID);
        {
            var status = false;
            foreach (var groupID in _equipGroupTids[type])
                status = status || SgModel.i.LocalPlayer.IsHaveNewEquip((int)groupID);
            GlobalUtil.SetChildActive(toggle.transform, "Toggle_Off/NoticeCore", status);
        }
        if (toggle.IsOn)
        {
            if (!isRefresh && _selectedCategoryType == type)
                return;

            _selectedCategoryType = type;

            ResetGroupTab();
        }
    }

    public void TabCategoryToggle()
    {
        //좌측 탭 알림 설정
        {
            switch (_selectedCategoryType)
            {
                case EEquipmentCategoryType.Accessory:
                    {
                        SetNotice(EEquipmentCategoryType.Armor);
                    }
                    break;

                case EEquipmentCategoryType.Armor:
                    {
                        SetNotice(EEquipmentCategoryType.Accessory);
                    }
                    break;
            }
            void SetNotice(EEquipmentCategoryType categoryType)
            {
                var toggle = _categoryToggleGroup.FindToggleWithID(categoryType.ToString());

                var status = false;
                foreach (var groupID in _equipGroupTids[categoryType])
                    status = status || SgModel.i.LocalPlayer.IsHaveNewEquip((int)groupID);
                GlobalUtil.SetChildActive(toggle.transform, "Toggle_Off/NoticeCore", status);
            }
        }

        _categoryToggleGroup.SetAllTogglesOff();

        ExtensionsToggle toggle = _categoryToggleGroup.FindToggleWithID(_selectedCategoryType.ToString());
        if (toggle != null)
        {
            toggle.IsOn = true;

            OnCategoryToggleAction(toggle, true);
        }
    }

    private void GroupToggleAction(ExtensionsToggle toggle)
    {
        if (toggle.IsOn)
        {
            GlobalSFXManager.i.PlayCommonButtonSound();
        }

        OnGroupToggleAction(toggle, false);
    }

    private void OnGroupToggleAction(ExtensionsToggle toggle, bool isRefresh)
    {
        if (toggle.IsOn)
        {
            var groupTid = uint.Parse(toggle.UniqueID);

            if (!isRefresh && _selectedGroupTid == groupTid)
                return;

            _selectedGroupTid = groupTid;
            _selectedGroupTids[_selectedCategoryType] = groupTid;

            GlobalUtil.SetChildActive(toggle.transform, "Toggle_Off/NoticeCore", false);

            var heroInfo = SgModel.Instance.LocalPlayer.GetHeroModel(_selectedHeroTid);
            if (heroInfo != null)
            {
                heroInfo.Equipments.TryGetValue(_selectedGroupTid, out var equipTid);
                SetEquippedIcon(equipTid);
            }

            SetMasteryInfo();
            SetEquipmentIconList(true);
        }
    }

    public void TabGroupToggle()
    {
        _groupToggleGroup.SetAllTogglesOff();

        if (_selectedGroupTid == 0)
        {
            _selectedGroupTid = 1;
        }

        ExtensionsToggle toggle = _groupToggleGroup.FindToggleWithID(_selectedGroupTid.ToString());
        if (toggle != null)
        {
            toggle.IsOn = true;

            OnGroupToggleAction(toggle, true);
        }
    }

    private void ResetGroupTab()
    {
        uint initTid = 0;

        for (int i = 0; i < _groupToggles.Count; i++)
        {
            ExtensionsToggle toggle = _groupToggles[i];
            if (toggle == null)
                continue;

            uint tid = uint.Parse(toggle.UniqueID);

            if (GameDBContainer.Instance.EquipmentGroupList.TryGetValue(tid, out var equipmentGroupDB))
            {
                if (equipmentGroupDB.CategoryType == _selectedCategoryType)
                {
                    if (initTid == 0)
                    {
                        initTid = tid;
                    }
                    toggle.gameObject.SetActive(true);

                    var status = SgModel.i.LocalPlayer.IsHaveNewEquip((int)tid);
                    GlobalUtil.SetChildActive(toggle.transform, "Toggle_Off/NoticeCore", status);
                }
                else
                {
                    toggle.gameObject.SetActive(false);
                }
            }
        }

        if (_selectedGroupTids.TryGetValue(_selectedCategoryType, out uint lastTid))
        {
            if (lastTid == 0)
            {
                _selectedGroupTid = initTid;
            }
            else
            {
                _selectedGroupTid = lastTid;
            }
        }

        TabGroupToggle();
    }

    private void SetEquipmentIconList(bool isResetScroll)
    {
        if (GameDBMapper.Instance.GetEquipmentList(_selectedGroupTid, out List<GameDB.Equipment> equipmentList) == false)
        {
            return;
        }

        int index = 0;

        foreach (var equipmentSheet in equipmentList)
        {
            WeaponIcon weaponIcon = null;

            if (index < _icons.Count)
            {
                weaponIcon = _icons[index];
            }
            else
            {
                GameObject iconObj = Instantiate(_iconPrefab, _scrollRect.content);
                if (iconObj != null)
                {
                    weaponIcon = iconObj.GetComponent<WeaponIcon>();
                    _icons.Add(weaponIcon);
                }
            }

            if (weaponIcon != null)
            {
                weaponIcon.gameObject.SetActive(true);
                weaponIcon.SetEquipmentInfo(equipmentSheet, OnClickIcon);
            }

            index++;
        }

        for (int i = index; i < _icons.Count; i++)
        {
            if (_icons[i] != null)
            {
                _icons[i].gameObject.SetActive(false);
            }
        }

        if (isResetScroll)
        {
            GlobalUtil.ResetScrollView(_scrollRect, true);
        }

        //여기가 신호가 오는 마지막 위치이기 때문에 이 때 알림 갱신 처리를 한다.
        CheckAutoEquipNotice();
        CheckComposeNotice();

        //장비 리스트 출력 처리 완료 후 획득 레드닷 종료 처리
        SgModel.Instance.LocalPlayer.OffNewEquipNotice((int)_selectedGroupTid);
        NoticeManager.i.SetCondition(
            GameDB.ENoticeCondition.Cond_Equip_New_Count,
            SgModel.Instance.LocalPlayer.IsHaveNewEquip(),
            GameDB.ENoticeCategory.Cat_BottomMenu_Equip_Btn);
    }

    public void FixPosition()
    {
        if (_coroutine != null)
        {
            StopCoroutine(_coroutine);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);

        var heroInfo = SgModel.Instance.LocalPlayer.GetHeroModel(_selectedHeroTid);
        if (heroInfo != null)
        {
            heroInfo.Equipments.TryGetValue(_selectedGroupTid, out var equipTid);
            if (GameDBContainer.Instance.EquipmentList.TryGetValue(equipTid, out var equipmentDB))
            {
                if (_selectedGroupTid == equipmentDB.GroupId)
                {
                    float slotHeight = GlobalUtil.GetWidgetSize(_iconPrefab.transform).y;
                    var grid = _scrollRect.content.GetComponent<GridLayoutGroup>();
                    int line = (equipmentDB.OrderId - 1) / grid.constraintCount;
                    float spacing = grid.spacing.y;
                    float padding = grid.padding.top + grid.padding.bottom;

                    var offset = line * (slotHeight + spacing);
                    var contentHeight = _scrollRect.content.rect.size.y - padding;
                    var scrollViewHeight = GlobalUtil.GetWidgetSize(_scrollRect.transform).y - padding;
                    var totalScrollableHeight = contentHeight - scrollViewHeight;

                    var topOffsetLimit = (offset + slotHeight - scrollViewHeight) / totalScrollableHeight;
                    var bottomOffsetLimit = offset / totalScrollableHeight;

                    if (_scrollRect.verticalNormalizedPosition > 1f - topOffsetLimit)
                    {
                        _coroutine = StartCoroutine(UpdateOffset(1f - topOffsetLimit, 0.2f));
                    }
                    else if (_scrollRect.verticalNormalizedPosition < 1f - bottomOffsetLimit)
                    {
                        _coroutine = StartCoroutine(UpdateOffset(1f - bottomOffsetLimit, 0.2f));
                    }
                }
            }
        }
    }

    protected IEnumerator UpdateOffset(float offset, float duration)
    {
        float start = _scrollRect.verticalNormalizedPosition;

        float time = 0f;
        while (time < duration)
        {
            _scrollRect.verticalNormalizedPosition = Mathf.Lerp(start, offset, time / duration);
            time += Time.deltaTime;

            yield return null;
        }

        _scrollRect.verticalNormalizedPosition = offset;

        yield break;
    }

    public void OnClickIcon(uint groupId, int orderId)
    {
        GlobalSFXManager.i.PlayCommonButtonSound();

        Popup_WeaponDetail popup = PopupManager.i.ShowPopup<Popup_WeaponDetail>(Global.ePopupPrefab.Popup_WeaponDetail);
        if (popup)
        {
            popup.ShowEquipment(groupId, orderId, _selectedHeroTid);
        }
    }

    public void OnClickCompose()
    {
        GlobalSFXManager.i.PlayCommonButtonSound();

        Popup_EquipmentCompose popup = PopupManager.i.ShowPopup<Popup_EquipmentCompose>(Global.ePopupPrefab.Popup_EquipmentCompose);
        if (popup)
        {
            popup.Show(_selectedCategoryType, _selectedGroupTid);
        }
    }

    private bool IsComposeAvailable(out EEquipmentRarity rarity)
    {
        rarity = EEquipmentRarity.None;
        int count = 0;
        int combineValue = GameDBContainer.Instance.config.EquipmentCombineValue;

        var player = SgModel.Instance.LocalPlayer;

        if (GameDBMapper.Instance.GetEquipmentList(_selectedGroupTid, out List<GameDB.Equipment> equipmentList))
        {
            for (int i = 0; i < equipmentList.Count; i++)
            {
                GameDB.Equipment equipmentTable = equipmentList[i];
                if (equipmentTable.Rarity > _composableRarity)
                {
                    return false;
                }

                if (equipmentTable.Rarity != rarity)
                {
                    rarity = equipmentTable.Rarity;
                    count = 0;

                    // 합성석이 필요한데 부족하면
                    if (GameDBContainer.Instance.EquipmentGroupList.TryGetValue(_selectedGroupTid, out var equipmentGroup))
                    {
                        var combineInfo = GameDBMapper.Instance.GetEquipmentCombineInfo(equipmentGroup.EquipmentType, rarity);
                        if (combineInfo == null)
                        {
                            return false;
                        }

                        if (combineInfo.CostResourceTid > 0 && combineInfo.CostResourceValue > 0)
                        {
                            var itemCount = GameController.i.Resource.GetItemCount((uint)combineInfo.CostResourceTid);
                            if (itemCount < (ulong)combineInfo.CostResourceValue)
                            {
                                return false;
                            }
                        }
                    }
                }

                EquipmentInfo equipmentInfo = player.GetEquipmentModel(equipmentTable.tid);
                if (equipmentInfo.Count > 0)
                {
                    count += equipmentInfo.Count;
                    if (count >= combineValue)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public void CheckComposeNotice()
    {
        var notice = transform.Find("MainPanel/BG/Left/LeftBtnTab/Btn_01/NoticeCore");
        var result = IsComposeAvailable(out var rarity);
        notice.gameObject.SetActive(result);
    }

    /// <summary>
    /// 자동 장착
    /// </summary>
    public void OnClickAutoEquip()
    {
        GlobalSFXManager.i.PlayCommonButtonSound();

        var searchedEquipmentDB = FindAutoEquipData(_selectedGroupTid, out var equipTid);
        if (searchedEquipmentDB != null && searchedEquipmentDB.tid != equipTid)
        {
            _equipmentController.SendReqHeroEquipmentMounting(_selectedHeroTid, searchedEquipmentDB.tid, searchedEquipmentDB.GroupId, true);
            return;
        }

        // 못 찾았거나 현재 장착된 장비와 같더라도 일단 갱신
        FixPosition();
    }

    public void OnClickAutoEquipAllParts()
    {
        GlobalSFXManager.i.PlayCommonButtonSound();

        _autoAllPartsList.Clear();
        _allPartsGroupList.Clear();

        foreach (var groupId in _equipGroupTids[_selectedCategoryType])
        {
            var searchedEquipmentDB = FindAutoEquipData(groupId, out var equipTid);
            var result = (searchedEquipmentDB != null && searchedEquipmentDB.tid != equipTid);
            if (result)
            {
                _autoAllPartsList.Add(searchedEquipmentDB.GroupId, searchedEquipmentDB.tid);
            }
        }

        if (_autoAllPartsList.Count > 0)
        {
            // 모든 파츠 자동 장착
            foreach (var it in _autoAllPartsList)
            {
                _equipmentController.SendReqHeroEquipmentMounting(_selectedHeroTid, it.Value, it.Key, true, true);
            }
            return;
        }

        // 못 찾았거나 현재 장착된 장비와 같더라도 일단 갱신
        FixPosition();
    }

    public void CallbackAutoEquipAllParts(uint groupId)
    {
        _autoAllPartsList.Remove(groupId);
        _allPartsGroupList.Add(groupId);

        if (_autoAllPartsList.Count == 0)
        {
            _sb.Clear();

            for (int i = 0; i < _allPartsGroupList.Count; i++)
            {
                if (GameDBContainer.Instance.EquipmentGroupList.TryGetValue(_allPartsGroupList[i], out var groupDB))
                {
                    if (i > 0)
                    {
                        _sb.Append("/");
                    }
                    _sb.Append(MultiLanguageManager.i.GetText(groupDB.EquipmentTypeName));
                }
            }

            var strParts = string.Format("<color=#00ffff>{0}</color>", _sb.ToString());
            var strToast = string.Format(MultiLanguageManager.i.GetText("Common_Toast_Msg1"), strParts); // {0} 장착 완료
            PopupManager.i.ShowPopup_Toast(strToast);
        }
    }

    private GameDB.Equipment FindAutoEquipData(uint groupTid, out uint equipTid)
    {
        equipTid = 0;

        if (GameDBContainer.Instance.HeroList.TryGetValue(_selectedHeroTid, out GameDB.Hero heroDB) == false)
        {
            return null;
        }

        var player = SgModel.Instance.LocalPlayer;
        HeroInfo heroInfo = player.GetHeroModel(_selectedHeroTid);
        if (heroInfo == null || heroInfo.Islock)
        {
            return null;
        }

        heroInfo.Equipments.TryGetValue(groupTid, out equipTid);

        if (GameDBMapper.Instance.GetEquipmentList(groupTid, out List<GameDB.Equipment> equipmentList) == false)
        {
            return null;
        }

        GameDB.Equipment searchedEquipmentDB = null;
        EquipmentInfo searchedEquipmentInfo = null;

        for (int i = equipmentList.Count - 1; i >= 0; i--) // orderId 높은 순으로
        {
            EquipmentInfo equipmentInfo = player.GetEquipmentModel(equipmentList[i].tid);
            if (equipmentInfo != null && equipmentInfo.Count >= 0)
            {
                var ownerTid = player.GetEquipmentOwner(equipmentInfo.Tid);
                if (ownerTid == 0 || ownerTid == _selectedHeroTid) // 장비 중인 영웅이 없거나 해당 영웅이 이미 장비 중이면 후보에 올리고 다음 장비로 넘어간다.
                {
                    if (searchedEquipmentDB == null)
                    {
                        searchedEquipmentDB = equipmentList[i];
                        searchedEquipmentInfo = equipmentInfo;
                    }
                    else // 같은 등급 내 다른 장비가 있으면 업그레이드 레벨로 비교
                    {
                        if (searchedEquipmentDB.Rarity == equipmentList[i].Rarity)
                        {
                            if (searchedEquipmentInfo.Enchant <= equipmentInfo.Enchant)
                            {
                                searchedEquipmentDB = equipmentList[i];
                                searchedEquipmentInfo = equipmentInfo;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }

        // 이미 장비 중인 것과 비교
        if (searchedEquipmentDB != null)
        {
            if (GameDBContainer.Instance.EquipmentList.TryGetValue(equipTid, out var curEquipmentDB) && curEquipmentDB.Rarity == searchedEquipmentDB.Rarity)
            {
                EquipmentInfo curEquipmentInfo = player.GetEquipmentModel(equipTid);
                if (curEquipmentInfo != null && searchedEquipmentInfo != null && curEquipmentInfo.Enchant == searchedEquipmentInfo.Enchant)
                {
                    return curEquipmentDB;
                }
            }
        }

        return searchedEquipmentDB;
    }

    private void CheckAutoEquipNotice()
    {
        var notice = transform.Find("MainPanel/BG/Left/LeftBtnTab/Btn_02/NoticeCore");
        var searchedEquipmentDB = FindAutoEquipData(_selectedGroupTid, out var equipTid);
        var result = (searchedEquipmentDB != null && searchedEquipmentDB.tid != equipTid);
        notice.gameObject.SetActive(result);

        var noticeAllParts = transform.Find("MainPanel/BG/Left/LeftBtnTab/Btn_03/NoticeCore");
        foreach (var groupId in _equipGroupTids[_selectedCategoryType])
        {
            searchedEquipmentDB = FindAutoEquipData(groupId, out equipTid);
            result = (searchedEquipmentDB != null && searchedEquipmentDB.tid != equipTid);
            if (result)
            {
                noticeAllParts.gameObject.SetActive(true);
                return;
            }
        }
        noticeAllParts.gameObject.SetActive(false);
    }

    public void OnClickMastery()
    {
        GlobalSFXManager.i.PlayCommonButtonSound();

        Popup_WeaponMastery masteryPopup = PopupManager.i.ShowPopup<Popup_WeaponMastery>(Global.ePopupPrefab.Popup_WeaponMastery);
        if (masteryPopup)
        {
            masteryPopup.ShowEquipment(_selectedCategoryType);
        }
    }

    public void OnBackButton()
    {
        //GameScene.GameScene_MainUI.i.UI_BottomMenu.SetAllTogglesOff();

        //어딘선가 백버튼 통합 처리가 필요해(히스토리)
        if (GameScene.GameScene_MainUI.i.UI_BottomMenu.UsedBottomBar_Shotcut)
        {
            GameScene.GameScene_MainUI.i.UI_BottomMenu.UsedBottomBar_Shotcut = false;
            GameScene.GameScene_MainUI.i.UI_BottomMenu.SetAllTogglesOff();
        }
        else
        {
            GameScene.GameScene_MainUI.i.TabToggle(GameScene.GameScene_MainUI.eBottomToggle.Home);
        }
        //--
    }
}