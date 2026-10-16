// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using DSDGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace DSDGenerator.Core.Steps;

/// <summary>
/// Пишет JSON-перевод в __DSD_Output/.
///
/// Целевой путь:
///   &lt;instancePath&gt;/__DSD_Output/&lt;OriginalModName&gt; - RU (DSD)/
///     SKSE/Plugins/DynamicStringDistributor/
///       &lt;PluginFileName&gt;/&lt;PluginStem&gt;.json
///
/// Пример для SkyUI_SE.esp из мода SkyUI:
///   .../__DSD_Output/SkyUI - RU (DSD)/SKSE/Plugins/DynamicStringDistributor/
///     SkyUI_SE.esp/SkyUI_SE.json
///
/// Кодировка файла: UTF-8 без BOM (совпадает с тем, что пишет esp2dsd).
/// Содержимое пишется как есть — без парсинга и переформатирования.
///
/// Очистка __DSD_Output/ — ответственность DsdPipeline, не этого шага.
/// </summary>
public sealed class WriteDsdStep
{
    private readonly ILogger<WriteDsdStep> _logger;

    public WriteDsdStep(ILogger<WriteDsdStep> logger)
    {
        _logger = logger;
    }

    public Task<string> ExecuteAsync(
        string instancePath,
        PluginPair pair,
        string jsonContent,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(instancePath))
            throw new ArgumentException(
                "Instance path must be non-empty.", nameof(instancePath));

        if (pair is null)
            throw new ArgumentNullException(nameof(pair));

        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new ArgumentException(
                "JSON content must be non-empty.", nameof(jsonContent));

        var fullInstance = Path.GetFullPath(instancePath);

        if (!Directory.Exists(fullInstance))
            throw new DirectoryNotFoundException(
                $"Instance directory not found: {fullInstance}");

        var outputDir = Path.Combine(fullInstance, DsdPaths.OutputDirName);

        var outputModName =
            pair.SourceMod.OriginalName +
            DsdPaths.RuModSuffix +
            DsdPaths.DsdSuffix;

        var pluginStem = Path.GetFileNameWithoutExtension(
            pair.PluginFileName);

        var destinationPath = Path.Combine(
            outputDir,
            outputModName,
            DsdPaths.DynamicStringDistributorSubPath
                .Replace('/', Path.DirectorySeparatorChar),
            pair.PluginFileName,
            pluginStem + ".json");

        var destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        // UTF-8 без BOM. new UTF8Encoding(false) — явно, чтобы
        // не зависеть от глобальных настроек.
        File.WriteAllText(
            destinationPath,
            jsonContent,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        _logger.LogDebug(
            "Wrote DSD: {Path}", destinationPath);

        return Task.FromResult(destinationPath);
    }
}
