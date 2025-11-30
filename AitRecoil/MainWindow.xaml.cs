using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;

namespace AitRecoil
{
    public partial class MainWindow : Window
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        const uint INPUT_MOUSE = 0;
        const uint MOUSEEVENTF_MOVE = 0x0001;
        const int VK_LBUTTON = 0x01;

        private bool recoilActive = false;
        private Thread recoilThread;
        private PresetManager presetManager = new PresetManager();
        private List<RecoilPreset> allPresets = new List<RecoilPreset>();
        private bool waitingForShortcut = false;

        private WindowInteropHelper helper;
        private HwndSource source;

        // Low-level keyboard hook
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc;
        private static IntPtr _hookID = IntPtr.Zero;
        private Dictionary<Key, string> hotkeyPresetMap = new Dictionary<Key, string>();

        // Sub-pixel accumulation for precise recoil
        private double accumulatedX = 0;
        private double accumulatedY = 0;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public MainWindow()
        {
            InitializeComponent();

            presetManager.Load();
            RefreshPresetCombo();

            sliderVertical.Value = 0;
            sliderHorizontal.Value = 0;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RegisterAllPresetHotkeysHook();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UnregisterAllHotkeysHook();
            recoilActive = false;
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private void RegisterAllPresetHotkeysHook()
        {
            // Always unhook first
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }

            hotkeyPresetMap.Clear();
            foreach (var preset in presetManager.Presets)
            {
                if (!string.IsNullOrEmpty(preset.ShortcutKey))
                {
                    if (Enum.TryParse(preset.ShortcutKey, out Key key))
                        hotkeyPresetMap[key] = preset.Name;
                }
            }

            // Keep the delegate alive
            _proc ??= HookCallback;
            _hookID = SetHook(_proc);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
                {
                    var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    Key key = KeyInterop.KeyFromVirtualKey((int)kb.vkCode);

                    if (hotkeyPresetMap.TryGetValue(key, out string presetName))
                    {
                        var preset = presetManager.Presets.Find(p => p.Name == presetName);
                        if (preset != null)
                        {
                            Dispatcher.BeginInvoke(() =>
                            {
                                sliderVertical.Value = preset.Vertical;
                                sliderHorizontal.Value = preset.Horizontal;
                                txtPresetName.Text = preset.Name;
                                txtShortcutKey.Text = preset.ShortcutKey;
                            });
                        }
                    }
                }
            }
            catch
            {
                // Swallow exceptions to prevent crash
            }

            // Always call next hook even if _hookID is zero, safer
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void UnregisterAllHotkeysHook()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private void RefreshPresetCombo()
        {
            comboPresets.Items.Clear();
            foreach (var p in presetManager.Presets)
                comboPresets.Items.Add(p.Name);
        }

        private void btnSavePreset_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtPresetName.Text))
            {
                var preset = new RecoilPreset
                {
                    Name = txtPresetName.Text.Trim(),
                    Vertical = (float)sliderVertical.Value,
                    Horizontal = (float)sliderHorizontal.Value,
                    ShortcutKey = txtShortcutKey.Text
                };

                presetManager.AddOrUpdatePreset(preset);
                RefreshPresetCombo();
                RegisterAllPresetHotkeysHook(); // re-register after changes
            }
        }

        private void comboPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboPresets.SelectedItem != null)
            {
                var preset = presetManager.Presets.Find(p => p.Name == comboPresets.SelectedItem.ToString());
                if (preset != null)
                {
                    sliderVertical.Value = preset.Vertical;
                    sliderHorizontal.Value = preset.Horizontal;
                    txtPresetName.Text = preset.Name;
                    txtShortcutKey.Text = preset.ShortcutKey;
                }
            }
        }

        private void btnDeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (comboPresets.SelectedItem != null)
            {
                presetManager.DeletePreset(comboPresets.SelectedItem.ToString());
                RefreshPresetCombo();
                txtPresetName.Clear();
                txtShortcutKey.Clear();
                RegisterAllPresetHotkeysHook();
            }
        }

        private void btnSetShortcut_Click(object sender, RoutedEventArgs e)
        {
            txtShortcutKey.Text = "Press a key...";
            waitingForShortcut = true;
        }

        private void btnDeleteShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtPresetName.Text))
            {
                var preset = presetManager.Presets.Find(p => p.Name == txtPresetName.Text.Trim());
                if (preset != null)
                {
                    preset.ShortcutKey = "";
                    txtShortcutKey.Text = "";
                    presetManager.AddOrUpdatePreset(preset);
                    RegisterAllPresetHotkeysHook();

                    MessageBox.Show(
                        $"Shortcut cleared for preset '{preset.Name}'.",
                        "Keybind Deleted",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }

        private void btnDeleteAllShortcuts_Click(object sender, RoutedEventArgs e)
        {
            if (presetManager.Presets.Count == 0)
            {
                MessageBox.Show(
                    "There are no presets to clear.",
                    "No Presets",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "This will clear the shortcut key for ALL presets. Continue?",
                "Clear All Shortcuts",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            foreach (var preset in presetManager.Presets)
            {
                preset.ShortcutKey = string.Empty;
            }

            presetManager.Save();
            txtShortcutKey.Text = string.Empty;
            RegisterAllPresetHotkeysHook();

            MessageBox.Show(
                "All preset shortcuts have been cleared.",
                "Shortcuts Cleared",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!waitingForShortcut)
                return;

            Key key = e.Key;

            // Check if the key is already assigned to another preset
            if (hotkeyPresetMap.TryGetValue(key, out string existingPreset))
            {
                MessageBox.Show(
                    $"Key '{key}' is already assigned to preset '{existingPreset}'. Please choose another key.",
                    "Shortcut Already Assigned",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtShortcutKey.Text = "";
                waitingForShortcut = true; // allow user to press another key
                return;
            }

            // Assign the key to the current preset
            txtShortcutKey.Text = key.ToString();
            waitingForShortcut = false;

            if (!string.IsNullOrWhiteSpace(txtPresetName.Text))
            {
                RecoilPreset recoilPreset = presetManager.Presets.Find(p => p.Name == txtPresetName.Text.Trim());
                if (recoilPreset != null)
                {
                    recoilPreset.ShortcutKey = key.ToString();
                    presetManager.AddOrUpdatePreset(recoilPreset);
                    RegisterAllPresetHotkeysHook();

                    MessageBox.Show(
                        $"Shortcut '{recoilPreset.ShortcutKey}' assigned to preset '{recoilPreset.Name}'.",
                        "Shortcut Saved",
                        MessageBoxButton.OK,
                        MessageBoxImage.Asterisk);
                }
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!recoilActive)
            {
                recoilActive = true;
                recoilThread = new Thread(AutoRecoilLoop) { IsBackground = true };
                recoilThread.Start();

                btnStart.Content = "Running...";
                btnStart.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                btnStop.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E"));
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            recoilActive = false;
            btnStart.Content = "Start";
            btnStart.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E"));
            btnStop.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF7043"));
        }

        private void AutoRecoilLoop()
        {
            while (recoilActive)
            {
                bool leftPressed = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
                bool rightPressed = (GetAsyncKeyState(0x02) & 0x8000) != 0;

                if (leftPressed && rightPressed)
                {
                    double vertical = 0;
                    double horizontal = 0;

                    // Read current slider values on the UI thread
                    Dispatcher.Invoke(() =>
                    {
                        vertical = sliderVertical.Value;
                        horizontal = sliderHorizontal.Value;
                    });

                    // Accumulate fractional movement
                    accumulatedX += horizontal;
                    accumulatedY += vertical;

                    int moveX = 0;
                    int moveY = 0;

                    // Only move when magnitude >= 1 in either direction
                    if (accumulatedX >= 1.0)
                        moveX = (int)Math.Floor(accumulatedX);
                    else if (accumulatedX <= -1.0)
                        moveX = (int)Math.Ceiling(accumulatedX);

                    if (accumulatedY >= 1.0)
                        moveY = (int)Math.Floor(accumulatedY);
                    else if (accumulatedY <= -1.0)
                        moveY = (int)Math.Ceiling(accumulatedY);

                    // Remove the part we've actually used
                    accumulatedX -= moveX;
                    accumulatedY -= moveY;

                    // Only send input if there is real movement
                    if (moveX != 0 || moveY != 0)
                    {
                        MoveCursorRelative(moveX, moveY);
                    }
                }
                else
                {
                    // When not firing, reset so we don't "dump" stored movement later
                    accumulatedX = 0;
                    accumulatedY = 0;
                }

                Thread.Sleep(5);
            }
        }

        private void MoveCursorRelative(int dx, int dy)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dx = dx;
            inputs[0].mi.dy = dy;
            inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE;
            inputs[0].mi.mouseData = 0;
            inputs[0].mi.time = 0;
            inputs[0].mi.dwExtraInfo = IntPtr.Zero;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }

    public class RecoilPreset
    {
        public string Name { get; set; }
        public float Vertical { get; set; }
        public float Horizontal { get; set; }
        public string ShortcutKey { get; set; } = "";
    }
}
