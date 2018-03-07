using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RhinoTetris
{
    internal static class KeyBoard
    {
        private const int
            WH_KEYBOARD_LL = 13,
            WM_KEYDOWN = 0x0100,
            WM_KEYUP = 0x0101;

        private static readonly LowLevelKeyboardProc Proc = HookCallback;
        private static IntPtr _hookId = IntPtr.Zero;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        public static event EventHandler<KeyEventArgs> OnKeyDown, OnKeyUp;

        public static void Start()
        {
            _hookId = SetHook(Proc);
        }

        public static void Stop()
        {
            UnhookWindowsHookEx(_hookId);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,

            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                var ea = new KeyEventArgs((Keys)vkCode);

                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    OnKeyDown?.Invoke(null, ea);
                }
                if (wParam == (IntPtr)WM_KEYUP)
                {
                    OnKeyUp?.Invoke(null, ea);
                }
                if (ea.SuppressKeyPress)
                    return (IntPtr)1;
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,

                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    }
}