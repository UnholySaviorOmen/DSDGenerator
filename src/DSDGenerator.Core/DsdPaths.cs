// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core;

/// <summary>
/// Единый источник правды для путей DSDGenerator.
///
/// Корень user-data: %LOCALAPPDATA%\DSDGenerator\.
/// (Отдельный от ModsyncManager — это отдельный продукт.)
///
/// Пока используется только LogsDirectory (будет в блоке 10),
/// но структура заводится сразу, чтобы пути не разъезжались
/// по коду.
/// </summary>
public static class DsdPaths
{
    /// <summary>
    /// Корень DSDGenerator: %LOCALAPPDATA%\DSDGenerator\.
    /// На не-Windows — %TEMP%\DSDGenerator\ (fallback для dev/CI).
    /// </summary>
    public static string Root { get; } = ComputeRoot();

    /// <summary>
    /// %LOCALAPPDATA%\DSDGenerator\logs\.
    /// </summary>
    public static string LogsDirectory => Path.Combine(Root, "logs");

    /// <summary>
    /// Имя корневой папки с выходными DSD-файлами внутри инстанса.
    /// Симметрично __ModsyncManager_Output/.
    /// </summary>
    public const string OutputDirName = "__DSD_Output";

    /// <summary>
    /// Относительный путь внутри мода-RU, куда кладётся JSON.
    /// Внутри выходной папки DSDGenerator создаёт структуру:
    ///   &lt;ModName&gt; - RU (DSD)/SKSE/Plugins/DynamicStringDistributor/&lt;plugin&gt;/&lt;plugin&gt;.json
    /// </summary>
    public const string DynamicStringDistributorSubPath =
        "SKSE/Plugins/DynamicStringDistributor";

    /// <summary>
    /// Суффикс имени RU-мода. Например, оригинал "SkyUI" → пара "SkyUI - RU".
    /// </summary>
    public const string RuModSuffix = " - RU";

    /// <summary>
    /// Суффикс имени выходной папки. Например, "SkyUI - RU (DSD)".
    /// </summary>
    public const string DsdSuffix = " (DSD)";

    /// <summary>
    /// Имя внешнего exe-конвертера. Лежит в AppContext.BaseDirectory/Assets/.
    /// В .gitignore — пользователь кладёт сам.
    /// </summary>
    public const string Esp2DsdExeName = "esp2dsd.exe";

    /// <summary>
    /// Полный путь к esp2dsd.exe (AppContext.BaseDirectory/Assets/esp2dsd.exe).
    /// </summary>
    public static string Esp2DsdExePath =>
        Path.Combine(AppContext.BaseDirectory, "Assets", Esp2DsdExeName);

    private static string ComputeRoot()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrEmpty(localAppData))
        {
            // Не-Windows: LocalApplicationData может быть пустым.
            localAppData = Path.GetTempPath();
        }

        return Path.Combine(localAppData, "DSDGenerator");
    }
}
