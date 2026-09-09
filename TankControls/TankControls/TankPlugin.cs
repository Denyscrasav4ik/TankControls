using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace TankControls;

[BepInPlugin("denyscrasav4ik.thedumbfactory.tankcontrols", "Tank Controls", "1.0.0")]
public class TankPlugin : BaseUnityPlugin
{
    private void Awake() => new Harmony("denyscrasav4ik.thedumbfactory.tankcontrols").PatchAll();
}

[HarmonyPatch]
public static class TankControls
{
    private static float accumulatedYRotation;
    private static bool isRotationInitialized = false;

    [HarmonyPatch(typeof(PlayerMovement), "MouseMove")]
    [HarmonyPrefix]
    internal static bool MouseMove_Prefix(PlayerMovement __instance)
    {
        if (!Singleton<CoreGameManager>.Instance.GetCamera(__instance.pm.playerNumber).Controllable)
            return false;

        if (!isRotationInitialized)
        {
            accumulatedYRotation = __instance.transform.eulerAngles.y;
            isRotationInitialized = true;
        }

        Singleton<InputManager>.Instance.GetAnalogInput(__instance.movementAnalogData, out Vector2 moveAbs, out _);
        bool isMoving = Mathf.Abs(moveAbs.y) > 0.05f;

        if (isMoving)
        {
            accumulatedYRotation = __instance.transform.eulerAngles.y;
            return false;
        }

        int num = __instance.pm.reversed ? -1 : 1;
        Vector2 absVector, deltaVector;

        if (!Singleton<PlayerFileManager>.Instance.authenticMode)
            Singleton<InputManager>.Instance.GetAnalogInput(__instance.cameraAnalogData, out absVector, out deltaVector, 0.1f);
        else
            Singleton<InputManager>.Instance.GetAnalogInput(__instance.movementAnalogData, out absVector, out deltaVector, 0.1f);
        deltaVector = Vector2.zero;

        float turnAmount = (deltaVector.x * num * Singleton<PlayerFileManager>.Instance.mouseCameraSensitivity)
                         + (absVector.x * Time.deltaTime * Singleton<PlayerFileManager>.Instance.controllerCameraSensitivity * num);

        if (Mathf.Abs(turnAmount) > 0.001f && __instance.pm.ec.PlayerTimeScale > 0f)
        {
            accumulatedYRotation += turnAmount * Time.timeScale;

            float snappedY = Mathf.Round(accumulatedYRotation / 45f) * 45f;

            Vector3 currentAngles = __instance.transform.eulerAngles;
            __instance.transform.rotation = Quaternion.Euler(currentAngles.x, snappedY, currentAngles.z);
        }

        return false;
    }

    [HarmonyPatch(typeof(PlayerMovement), "PlayerMove")]
    [HarmonyPrefix]
    internal static bool PlayerMove_Prefix(PlayerMovement __instance)
    {
        Vector3 pos = __instance.transform.position;
        pos.y = __instance.height;
        __instance.transform.position = pos;

        bool running = Singleton<PlayerFileManager>.Instance.authenticMode
            ? Singleton<CoreGameManager>.Instance.authenticScreen.LeverOn
            : Singleton<InputManager>.Instance.GetDigitalInput("Run", onDown: false);

        float speed = (__instance.stamina > 0f && running) ? __instance.runSpeed : __instance.walkSpeed;

        Vector3 moveDirection = Vector3.zero;

        if (!__instance.Entity.InteractionDisabled)
        {
            Singleton<InputManager>.Instance.GetAnalogInput(__instance.movementAnalogData, out Vector2 moveAbs, out _);
            moveDirection = __instance.transform.forward * moveAbs.y;
        }

        float sensitivity = 1f;
        if (Singleton<PlayerFileManager>.Instance.analogMovement && speed == __instance.walkSpeed)
            sensitivity = Mathf.Clamp(moveDirection.magnitude, 0f, 1f);

        Vector3 finalVelocity = moveDirection.normalized * speed * sensitivity * __instance.pm.PlayerTimeScale;
        __instance.Entity.UpdateInternalMovement(finalVelocity);
        __instance.StaminaUpdate((moveDirection.normalized * speed * sensitivity).magnitude);

        return false;
    }
}
