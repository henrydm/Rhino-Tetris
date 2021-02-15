using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RhinoTetris
{
    /// <summary>
    /// Hook trough pinvoke to capture the keys and discard them through Win API, due to Rhino it's not capable to "don't process a key"
    /// </summary>
    internal static class KeyBoard
    {
        /// <summary>
        /// Constants for Windows meassaging keyboard events
        /// </summary>
        private const int
            WH_KEYBOARD_LL = 13,
            WM_KEYDOWN = 0x0100,
            WM_KEYUP = 0x0101;

        /// <summary>
        /// Get the current presed key
        /// </summary>
        public static Keys PressedKey;

        /// <summary>
        /// Process Id which is currently monitoring for keystrokes.
        /// </summary>
        private static IntPtr _hookId = IntPtr.Zero;

        /// <summary>
        /// Delegate (callback) for the keyboard events.
        /// </summary>
        private static readonly LowLevelKeyboardProc Proc = HookCallback;

        /// <summary>
        /// Delegate signature for the keyboard callback.
        /// </summary>
        /// <param name="nCode">Win msg code.</param>
        /// <param name="wParam">Event param type.</param>
        /// <param name="lParam">Aditional param. (pressed or released key code in this case)</param>
        /// <returns></returns>
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Events to manage externally
        /// </summary>
        public static event EventHandler<KeyEventArgs> OnKeyDown, OnKeyUp;

        /// <summary>
        /// Start monitoring for key strokes.
        /// </summary>
        public static void Start()
        {
            if (_hookId != IntPtr.Zero) return;
            PressedKey = Keys.None;
            _hookId = SetHook(Proc);
        }

        /// <summary>
        /// Stop monitoring for key strokes.
        /// </summary>
        public static void Stop()
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        /// <summary>
        /// Raised when Windows messaging pipeline current step is a keystroke action.
        /// </summary>
        /// <param name="nCode">Win msg code.</param>
        /// <param name="wParam">Event param type.</param>
        /// <param name="lParam">Aditional param. (pressed or released key code in this case)</param>
        /// <returns></returns>
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                var ea = new KeyEventArgs((Keys)vkCode);

                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    PressedKey = (Keys)vkCode;
                    OnKeyDown?.Invoke(null, ea);
                }
                if (wParam == (IntPtr)WM_KEYUP)
                {
                    PressedKey = Keys.None;
                    OnKeyUp?.Invoke(null, ea);
                }
                if (ea.SuppressKeyPress)
                    return (IntPtr)1;
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Set the callback for a key pres/release in Windows messaging pipeline.
        /// </summary>
        /// <param name="proc">Callback function to execute for the event.</param>
        /// <returns>Pointer to the hook procedure if the link succes, otherwise null.</returns>
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            {
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    }
}