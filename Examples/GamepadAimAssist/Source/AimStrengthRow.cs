using System;
using System.Collections;
using System.Reflection;
using FrontierTale.Modding;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using V.Framework;
using V.UI;

namespace GamepadAimAssist
{
    // 复用设置页的布局、字体和滑条皮肤，行随所属游戏设置页显隐。
    public sealed class AimStrengthRow : MonoBehaviour
    {
        private Slider slider;
        private TMP_Text caption, percent;
        private SettingNode owner;
        private Action<int> save;
        private GameLanguage? language; // 语言切换后才重设标签。
        private bool dirty; // 关闭设置时合并写盘。
        private StrengthFocus focus; // 与原生设置行同属手柄焦点名单，滑条保留鼠标拖动。
        private IDictionary pages, views, visibility; // SDK v1 通过现有设置页注册表接入旧玩家包。
        private object gamePage, itemKey;

        internal static AimStrengthRow Mount(ModContext context, SettingNode settings, int strength, Action<int> save)
        {
            var page = (RectTransform)settings.transform.Find("Content/ContentArea/gameSettingNode");
            var template = (RectTransform)page.Find("settingAutoPickRange");
            var row = context.Attach(page, "AimStrength");
            row.sizeDelta = template.sizeDelta;
            var control = row.gameObject.AddComponent<AimStrengthRow>();
            control.save = save;
            var original = template.Find("autoPickRangeSlider").GetComponent<Slider>();
            control.slider = Instantiate(original, row, false);
            control.slider.onValueChanged = new Slider.SliderEvent();
            control.slider.minValue = 0;
            control.slider.maxValue = 20;
            control.slider.wholeNumbers = true;
            control.slider.SetValueWithoutNotify(strength / 5f);
            // 鼠标拖动滑条，手柄导航由整行接收，左右与其他设置统一。
            control.slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var flag = Instantiate(template.Find("autoPickRangeSelect1").GetComponent<Image>(), row, false);
            flag.transform.SetAsFirstSibling();
            flag.raycastTarget = false;
            control.focus = flag.gameObject.AddComponent<StrengthFocus>();
            control.focus.ValueSlider = control.slider;
            control.BindOwner(settings);
            control.caption = CreateText(row, template.Find("autoPickRangeTxt").GetComponent<TMP_Text>(), 15, 370);
            control.percent = CreateText(row, template.Find("autoPickRangeSelectTxt").GetComponent<TMP_Text>(), 1010, 235);
            control.slider.onValueChanged.AddListener(control.OnSlide);
            control.ShowValue(strength);
            return control;
        }

        // 正式包重建导航时会清空临时连接，因此通过设置页自身的 RegisterItem 登记整行。
        internal void BindOwner(SettingNode settings)
        {
            if (owner == settings) return;
            owner = settings;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = typeof(SettingNode);
            pages = (IDictionary)type.GetField("pageItems", flags).GetValue(owner);
            views = (IDictionary)type.GetField("itemViews", flags).GetValue(owner);
            visibility = (IDictionary)type.GetField("configuredItemVisibility", flags).GetValue(owner);
            gamePage = Enum.Parse(type.GetNestedType("SettingPage", BindingFlags.NonPublic), "Gameplay");
            var itemType = type.GetNestedType("SettingItem", BindingFlags.NonPublic);
            int slot = -1; // 扩展项在本实例挑选未占用的键，避开原游戏及其他扩展项。
            while (views.Contains(Enum.ToObject(itemType, slot))) slot--;
            itemKey = Enum.ToObject(itemType, slot);
            var image = focus.GetComponent<Image>();
            type.GetMethod("RegisterItem", flags).Invoke(owner, new object[] { itemKey, (RectTransform)transform, image, image });
            EnsurePageEntry();
        }

        private void EnsurePageEntry()
        {
            // 设备变化时原游戏会重新分配列表，重新加入这一页后由原版生成上下连接。
            var entries = (IList)pages[gamePage];
            if (entries.Contains(itemKey)) return;
            entries.Add(itemKey);
            if (isActiveAndEnabled) owner.ConfigureGamepadNavigation();
        }

        private static TMP_Text CreateText(RectTransform row, TMP_Text style, float left, float width)
        {
            // 只沿用字体和排版，不复制原字段身上的本地化绑定及事件。
            var label = new GameObject(style.name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var rect = label.rectTransform;
            rect.SetParent(row, false);
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(left, 0);
            rect.sizeDelta = new Vector2(width, 0);
            label.font = style.font;
            label.fontSharedMaterial = style.fontSharedMaterial;
            label.fontStyle = style.fontStyle;
            label.color = style.color;
            label.fontSizeMax = style.fontSize;
            label.fontSizeMin = 10;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        internal void Refresh(int strength)
        {
            EnsurePageEntry();
            slider.SetValueWithoutNotify(strength / 5f);
            ShowValue(strength);
            var input = InputManager.Instance;
            var pad = Gamepad.current;
            if (!Application.isFocused || owner.IsRebindingKey || owner.IsRuntimeLeaveConfirmShowing ||
                input == null || input.currDeviceType != 1 || pad == null || EventSystem.current == null ||
                EventSystem.current.currentSelectedGameObject != focus.gameObject) return;
            int delta = (pad.rightTrigger.wasPressedThisFrame ? 1 : 0) - (pad.leftTrigger.wasPressedThisFrame ? 1 : 0);
            if (delta != 0) slider.value += delta;
        }

        private int displayedStrength = -1; // 避免停留设置页时重复拼接百分比。
        private void ShowValue(int strength)
        {
            var current = LocalizationManager.Instance.CurrentLanguage;
            if (language != current)
            {
                // 独立投稿包自带四种译文，游戏本体的语言表无须添加模组词条。
                language = current;
                caption.text = current switch {
                    GameLanguage.SimplifiedChinese => "辅助瞄准强度",
                    GameLanguage.TraditionalChinese => "輔助瞄準強度",
                    GameLanguage.Japanese => "エイムアシスト強度",
                    _ => "Aim assist strength"
                };
            }
            if (displayedStrength == strength) return;
            displayedStrength = strength;
            percent.text = $"◀   {strength}%   ▶";
        }

        private void OnSlide(float tick)
        {
            int value = Mathf.RoundToInt(tick) * 5;
            save(value);
            dirty = true;
            ShowValue(value);
        }

        private void OnDisable()
        {
            if (!dirty) return;
            PlayerPrefs.Save();
            dirty = false;
        }

        private void OnDestroy()
        {
            if (owner == null || itemKey == null) return;
            ((IList)pages[gamePage]).Remove(itemKey);
            views.Remove(itemKey);
            visibility.Remove(itemKey);
            if (owner.isActiveAndEnabled) owner.ConfigureGamepadNavigation();
        }
    }

    public sealed class StrengthFocus : UIHoverButton
    {
        internal Slider ValueSlider; // 方向导航和鼠标拖动共享同一份强度值。

        public override void OnMove(AxisEventData data)
        {
            if (data.moveDir == MoveDirection.Left || data.moveDir == MoveDirection.Right)
            {
                ValueSlider.value += data.moveDir == MoveDirection.Right ? 1 : -1;
                data.Use();
                return;
            }
            base.OnMove(data); // 上下交回宿主，保留底部按钮、侧栏与弹窗的跳转规则。
        }
    }
}
