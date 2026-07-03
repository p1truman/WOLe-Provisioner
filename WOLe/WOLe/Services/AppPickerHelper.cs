using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace WOLe.Provisioner.Services
{
    /// <summary>
    /// Shared utilities for app-picker UI used in ActionSwitchPage and ShutdownActionMappingPage.
    /// </summary>
    internal static class AppPickerHelper
    {
        private const int FileBufferSize = 1024;
        private const int FileTitleBufferSize = 256;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int OFN_EXPLORER = 0x00080000;

        /// <summary>
        /// Opens a file-open picker for executable or shortcut files.
        /// Returns the selected file path, or null if the user cancelled.
        /// </summary>
        internal static async Task<string?> PickExecutableAsync()
        {
            var hwnd = GetOwnerWindowHandle();

            try
            {
                var picker = new FileOpenPicker();
                if (hwnd != IntPtr.Zero)
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                picker.FileTypeFilter.Add(".exe");
                picker.FileTypeFilter.Add(".lnk");

                StorageFile? file = await picker.PickSingleFileAsync();
                return file?.Path;
            }
            catch (COMException)
            {
                // Fallback for environments where WinRT picker intermittently fails with E_FAIL.
                return PickExecutableWithWin32Dialog(hwnd);
            }
        }

        private static IntPtr GetOwnerWindowHandle()
        {
            if (App.MainWindow is not null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                if (hwnd != IntPtr.Zero)
                    return hwnd;
            }

            return GetForegroundWindow();
        }

        private static string? PickExecutableWithWin32Dialog(IntPtr hwndOwner)
        {
            var fileBuffer = Marshal.AllocHGlobal(FileBufferSize * sizeof(char));

            var dialog = new OPENFILENAME
            {
                hwndOwner = hwndOwner,
                lpstrFilter = "Executable and Shortcut Files (*.exe;*.lnk)\0*.exe;*.lnk\0All Files (*.*)\0*.*\0\0",
                lpstrFile = fileBuffer,
                Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST,
                lpstrDefExt = "exe"
            };

            try
            {
                Marshal.WriteInt16(fileBuffer, 0);
                return GetOpenFileName(dialog) ? Marshal.PtrToStringUni(fileBuffer) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(fileBuffer);
            }
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileName([In, Out] OPENFILENAME lpofn);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class OPENFILENAME
        {
            public OPENFILENAME()
            {
                lStructSize = Marshal.SizeOf<OPENFILENAME>();
                nMaxFile = FileBufferSize;
                lpstrFile = IntPtr.Zero;
                lpstrFileTitle = IntPtr.Zero;
                nMaxFileTitle = FileTitleBufferSize;
            }

            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string? lpstrFilter;
            public string? lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public string? lpstrInitialDir;
            public string? lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string? lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string? lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        /// <summary>
        /// Populates a ComboBox with discovered app entries and pre-selects the saved path if present.
        /// </summary>
        internal static void PopulateAppComboBox(
            ComboBox combo,
            IList<AppDiscovery.AppEntry> apps,
            string currentPath)
        {
            combo.Items.Clear();

            foreach (var app in apps)
                combo.Items.Add(new ComboBoxItem { Content = app.Name, Tag = app.Path });

            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                foreach (var item in combo.Items)
                {
                    if (item is ComboBoxItem cbi && cbi.Tag is string path && path == currentPath)
                    {
                        combo.SelectedItem = cbi;
                        return;
                    }
                }
            }

            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        /// <summary>
        /// Sets the ComboBox selection to the given path, inserting a new item if not already present.
        /// </summary>
        internal static void SetComboBoxToCustomPath(ComboBox combo, string path)
        {
            foreach (var item in combo.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag is string tag && tag == path)
                {
                    combo.SelectedItem = cbi;
                    return;
                }
            }

            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var newItem = new ComboBoxItem { Content = name, Tag = path };
            combo.Items.Insert(0, newItem);
            combo.SelectedItem = newItem;
        }
    }
}
