using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

// On macOS the GameSir G7 Pro is a plain HID gamepad with an Android style report, so Unity
// makes it a Joystick and Gamepad.current skips it. This layout makes it a Gamepad.
// Report: 15 buttons, hat, X Y (left stick), Z Rz (right stick), brake and gas (triggers).
[StructLayout(LayoutKind.Explicit, Size = 9)]
struct GameSirG7ProReport : IInputStateTypeInfo
{
    public FourCC format => new FourCC('H', 'I', 'D');

    [InputControl(name = "buttonSouth", layout = "Button", bit = 0)]
    [InputControl(name = "buttonEast", layout = "Button", bit = 1)]
    [InputControl(name = "buttonWest", layout = "Button", bit = 3)]
    [InputControl(name = "buttonNorth", layout = "Button", bit = 4)]
    [InputControl(name = "leftShoulder", layout = "Button", bit = 6)]
    [InputControl(name = "rightShoulder", layout = "Button", bit = 7)]
    [InputControl(name = "select", layout = "Button", bit = 10)]
    [InputControl(name = "start", layout = "Button", bit = 11)]
    [InputControl(name = "leftStickPress", layout = "Button", bit = 13)]
    [InputControl(name = "rightStickPress", layout = "Button", bit = 14)]
    [FieldOffset(0)] public ushort buttons;

    [InputControl(name = "dpad", format = "BIT", layout = "Dpad", sizeInBits = 4, defaultState = 8)]
    [InputControl(name = "dpad/up", format = "BIT", layout = "DiscreteButton", parameters = "minValue=7,maxValue=1,nullValue=8,wrapAtValue=7", bit = 0, sizeInBits = 4)]
    [InputControl(name = "dpad/right", format = "BIT", layout = "DiscreteButton", parameters = "minValue=1,maxValue=3", bit = 0, sizeInBits = 4)]
    [InputControl(name = "dpad/down", format = "BIT", layout = "DiscreteButton", parameters = "minValue=3,maxValue=5", bit = 0, sizeInBits = 4)]
    [InputControl(name = "dpad/left", format = "BIT", layout = "DiscreteButton", parameters = "minValue=5,maxValue=7", bit = 0, sizeInBits = 4)]
    [FieldOffset(2)] public byte hat;

    [InputControl(name = "leftStick", layout = "Stick", format = "VC2B")]
    [InputControl(name = "leftStick/x", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
    [InputControl(name = "leftStick/left", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
    [InputControl(name = "leftStick/right", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1")]
    [InputControl(name = "leftStick/y", offset = 1, format = "BYTE", parameters = "invert,normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
    [InputControl(name = "leftStick/up", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
    [InputControl(name = "leftStick/down", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1,invert=false")]
    [FieldOffset(3)] public byte leftX;
    [FieldOffset(4)] public byte leftY;

    [InputControl(name = "rightStick", layout = "Stick", format = "VC2B")]
    [InputControl(name = "rightStick/x", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
    [InputControl(name = "rightStick/left", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
    [InputControl(name = "rightStick/right", offset = 0, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1")]
    [InputControl(name = "rightStick/y", offset = 1, format = "BYTE", parameters = "invert,normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
    [InputControl(name = "rightStick/up", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0,clampMax=0.5,invert")]
    [InputControl(name = "rightStick/down", offset = 1, format = "BYTE", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,clamp=1,clampMin=0.5,clampMax=1,invert=false")]
    [FieldOffset(5)] public byte rightX;
    [FieldOffset(6)] public byte rightY;

    [InputControl(name = "leftTrigger", format = "BYTE")]
    [FieldOffset(7)] public byte leftTrigger;
    [InputControl(name = "rightTrigger", format = "BYTE")]
    [FieldOffset(8)] public byte rightTrigger;
}

#if UNITY_EDITOR
[InitializeOnLoad]
#endif
[InputControlLayout(stateType = typeof(GameSirG7ProReport))]
public class GameSirG7Pro : Gamepad
{
    static GameSirG7Pro()
    {
        InputSystem.RegisterLayout<GameSirG7Pro>(matches: new InputDeviceMatcher()
            .WithInterface("HID")
            .WithCapability("vendorId", 0x3537)
            .WithCapability("productId", 0x1022));
    }

    // Runs the static constructor in player builds.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init() { }
}
