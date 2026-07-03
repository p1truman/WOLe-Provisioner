using System;
using System.Collections.Generic;
using System.IO;

namespace WOLe.Provisioner.Services
{
    public static class AppDiscovery
    {
        public record AppEntry(string Name, string Path);

        /// <summary>
        /// Enumerates .lnk shortcut files from the current user and common Start Menu Programs folders.
        /// Returns a sorted list of discovered applications.
        /// </summary>
        public static List<AppEntry> GetInstalledApps()
        {
            var apps = new List<AppEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var searchRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
            };

            foreach (var root in searchRoots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    continue;

                try
                {
                    foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(lnk);
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        if (seen.Add(name))
                            apps.Add(new AppEntry(name, lnk));
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }

            apps.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return apps;
        }
    }
}
