using System.Runtime.InteropServices;
using UnityEngine;

public static class GamepadRumble
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("xinput1_4.dll")]
    private static extern int XInputSetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("xinput1_3.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState13(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }
#endif

    public static void SetVibration(float leftMotor, float rightMotor)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        var vib = new XINPUT_VIBRATION
        {
            wLeftMotorSpeed = (ushort)(Mathf.Clamp01(leftMotor) * 65535),
            wRightMotorSpeed = (ushort)(Mathf.Clamp01(rightMotor) * 65535)
        };

        try
        {
            if (XInputSetState(0, ref vib) != 0)
                XInputSetState13(0, ref vib);
        }
        catch (System.DllNotFoundException) { }
        catch (System.EntryPointNotFoundException) { }
#endif
    }

    public static void Stop()
    {
        SetVibration(0, 0);
    }
}
