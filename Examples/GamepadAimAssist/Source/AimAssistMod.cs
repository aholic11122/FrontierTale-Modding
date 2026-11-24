using System;
using System.Collections.Generic;
using System.Reflection;
using FrontierTale.Modding;
using UnityEngine;
using UnityEngine.InputSystem;
using V;
using V.Framework;
using V.UI;

namespace GamepadAimAssist
{
    public sealed class AimAssistMod : GameMod
    {
        internal const string Preference = "frontiertale.gamepad-aim-assist.strength"; // 本机设置独立于存档及模组安装目录保存。
        private InputManager boundInput; // 在原版 Action.started 修正，角色在后续 performed 才读取本发落点。
        private readonly List<MapMonster> candidates = new List<MapMonster>(); // 复用游戏注册表快照。
        private Action<InputManager, Rect, Vector2> setAim; // 本版 SDK 仅公开读取准星，绑定游戏自己的视口写入方法。
        private ModContext context; // 宿主负责回收设置节点。
        private UICanvas settingsCanvas; // 仅在开始或系统界面搜索设置实例。
        private AimStrengthRow settingsRow;
        private float nextSettingsSearch;
        private float nextSearch; // 相邻目标每秒最多重新比较十次。
        private MapMonster target;
        private int strength;

        public override void Load(ModContext context)
        {
            this.context = context;
            var method = typeof(InputManager).GetMethod("SetGamepadRemoteAimPosition",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Rect), typeof(Vector2) }, null)
                ?? throw new MissingMethodException(typeof(InputManager).FullName, "SetGamepadRemoteAimPosition(Rect, Vector2)");
            setAim = (Action<InputManager, Rect, Vector2>)method.CreateDelegate(typeof(Action<InputManager, Rect, Vector2>));
            strength = Mathf.Clamp(PlayerPrefs.GetInt(Preference, 50), 0, 100);
        }

        public override void OnFrame()
        {
            var input = InputManager.Instance;
            if (boundInput != input)
            {
                UnbindInput();
                boundInput = input;
                if (input != null) input.AddInputActionEvent("Action", OnAttackStarted, null, null);
            }
            if (!UIManager.IsActive()) return;
            var ui = UIManager.Instance;
            var screen = ui.GetTopUICanvas();
            if (screen is StartUI || screen is GeneralUI)
                UpdateSettings(screen, ui);

            var pad = Gamepad.current;
            UpdateAim(pad, false, screen);
        }

        // 同帧轻扣 RT 也先更新共享准星，主攻击仍由游戏自己的输入回调执行。
        private void OnAttackStarted(InputAction.CallbackContext action)
        {
            if (action.control.device is Gamepad pad && UIManager.IsActive())
                UpdateAim(pad, true, UIManager.Instance.GetTopUICanvas());
        }

        private void UpdateAim(Gamepad pad, bool firstPress, UICanvas screen)
        {
            var input = boundInput;
            if (strength == 0 || !(screen is MainUI) || input == null || input.currDeviceType != 1 ||
                !input.interactable || pad == null || !Application.isFocused || Time.timeScale <= 0 ||
                !CharacterCore.IsActive() || !UserDataManager.IsActive())
            {
                target = null;
                return;
            }

            var stick = pad.rightStick.ReadValue();
            bool attacking = firstPress || pad.rightTrigger.isPressed;
            var weapon = UserDataManager.Instance.currentWeaponConfig;
            var player = CharacterCore.Instance.c_ctrl;
            if ((!attacking && stick.sqrMagnitude < 0.01f) || weapon == null || !weapon.UsesRemoteCrosshair || player == null || player.IsDeadOrDying() ||
                EnemyManager.instance == null || RegionManager.instance == null ||
                !UIManager.Instance.CanDisplayMainGameplayPresentation() || !player.GetWeaponCrossshairModule().IsCurrentCrosshairVisible)
            {
                target = null;
                return;
            }

            var camera = RegionManager.instance.mainCamera;
            if (camera == null) return;
            Vector2 cursor = InputManager.GameplayPointerPosition;
            float shortSide = Mathf.Min(camera.pixelWidth, camera.pixelHeight);
            Vector2 origin = camera.WorldToScreenPoint(player.BodyNode);
            if (firstPress || Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 0.1f;
                target = FindTarget(camera, cursor, origin, shortSide, player, stick, attacking);
            }
            if (!TryProject(target, player, camera, out var point)) return;
            if (float.IsPositiveInfinity(TargetScore(cursor, origin, point, stick, shortSide, strength, attacking))) return;
            Vector2 corrected = CorrectPointer(cursor, point, strength, attacking, firstPress, Time.unscaledDeltaTime);
            if (corrected == cursor) return;
            if (!BattleCore.HasMonsterAttackLineOfSight(player.BodyNode, target.BodyNode)) return;
            setAim(input, camera.pixelRect, corrected);
        }

        // 开火边沿按强度直接修正本发。持续操作使用指数收敛，使不同帧率的跟随速度一致。
        internal static Vector2 CorrectPointer(Vector2 cursor, Vector2 point, int strength, bool attacking, bool firstPress, float deltaTime)
        {
            float weight = strength / 100f;
            if (firstPress || attacking && strength == 100) return Vector2.Lerp(cursor, point, weight);
            float speed = attacking ? 16f : 8f;
            return Vector2.Lerp(cursor, point, 1f - Mathf.Exp(-speed * weight * deltaTime));
        }

        // 首选准星附近，其次在高强度且松开右杆开火时寻找角色身旁的近敌。两档分数互不重叠。
        internal static float TargetScore(Vector2 cursor, Vector2 origin, Vector2 point, Vector2 stick, float shortSide, int strength, bool attacking)
        {
            if (strength <= 0 || shortSide <= 0) return float.PositiveInfinity;
            float weight = strength / 100f;
            Vector2 offset = point - cursor;
            float radius = shortSide * (0.05f + (attacking ? 0.19f : 0.09f) * weight);
            bool steering = stick.sqrMagnitude >= 0.01f;
            if ((!attacking && !steering) || (steering && Vector2.Dot(stick, offset.normalized) < -0.2f))
                return float.PositiveInfinity;
            if (offset.sqrMagnitude <= radius * radius) return offset.sqrMagnitude / (radius * radius);
            float nearby = shortSide * 0.18f * Mathf.Clamp01((weight - 0.5f) * 2);
            float distance = (point - origin).sqrMagnitude;
            if (attacking && !steering && nearby > 0 && distance <= nearby * nearby)
                return 2f + distance / (nearby * nearby);
            return float.PositiveInfinity;
        }

        private MapMonster FindTarget(Camera camera, Vector2 cursor, Vector2 origin, float shortSide, CharacterCtrl player, Vector2 stick, bool attacking)
        {
            EnemyManager.instance.CopyRegisteredMonstersTo(candidates);
            float bestScore = float.PositiveInfinity;
            MapMonster best = null;
            foreach (var monster in candidates)
            {
                if (!TryProject(monster, player, camera, out var point)) continue;
                float score = TargetScore(cursor, origin, point, stick, shortSide, strength, attacking);
                if (monster == target) score *= 0.9f; // 轻微偏好已有目标，避免两只怪重叠时抖动。
                if (score >= bestScore || !BattleCore.HasMonsterAttackLineOfSight(player.BodyNode, monster.BodyNode)) continue;
                bestScore = score;
                best = monster;
            }
            return best;
        }

        // 复用原版怪物存活、夜晚显形、终局敌我和相机视口判定。
        private static bool TryProject(MapMonster monster, CharacterCtrl player, Camera camera, out Vector2 point)
        {
            point = default;
            if (monster == null || !monster.IsAlive || !monster.CanShowOccludedSilhouette()) return false;
            if (monster is MapFinalMonster final && (!final.IsCombatEnabled || !final.IsEnemy(player))) return false;
            Vector3 projected = camera.WorldToScreenPoint(monster.BodyNode);
            point = projected;
            return projected.z >= camera.nearClipPlane && projected.z <= camera.farClipPlane && camera.pixelRect.Contains(point);
        }

        private void UpdateSettings(UICanvas screen, UIManager ui)
        {
            if (screen != settingsCanvas || Time.unscaledTime >= nextSettingsSearch)
            {
                settingsCanvas = screen;
                nextSettingsSearch = Time.unscaledTime + 0.35f;
                var settings = screen.GetComponentInChildren<SettingNode>();
                settingsRow = settings == null ? null : settings.GetComponentInChildren<AimStrengthRow>(true);
                if (settings != null && settingsRow == null)
                    settingsRow = AimStrengthRow.Mount(context, settings, strength, SaveStrength);
                else if (settingsRow != null)
                    settingsRow.BindOwner(settings);
            }
            if (settingsRow != null && settingsRow.isActiveAndEnabled && ui.GetActiveInputTopCanvas() == null)
                settingsRow.Refresh(strength);
        }

        private void SaveStrength(int value)
        {
            strength = value;
            PlayerPrefs.SetInt(Preference, value);
        }

        public override void Unload()
        {
            UnbindInput();
            candidates.Clear();
            target = null;
            setAim = null;
        }

        private void UnbindInput()
        {
            if (boundInput != null) boundInput.RemoveInputActionEvent("Action", OnAttackStarted, null, null);
            boundInput = null;
        }
    }
}
