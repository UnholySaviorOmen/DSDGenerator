// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace DSDGenerator.Core.Steps;

/// <summary>
/// Для каждого ModPair находит пары плагинов с одинаковым именем файла.
///
/// Сканируется ТОЛЬКО корень папки мода (не рекурсивно). Плагины
/// внутри подпапок (например, mods/Foo/Data/Bar.esp) игнорируются —
/// это соответствует конвенции MO2 и поведению esp2dsd.
///
/// Расширения: .esp, .esm, .esl (OrdinalIgnoreCase — Windows ФС).
///
/// Логика для одной пары модов:
///   1. Найти все плагины в корне оригинала.
///      Если 0 → skip всего мода с причиной NoPluginsInOriginal.
///   2. Найти все плагины в корне RU.
///      Если 0 → skip всех плагинов оригинала с причиной NoPluginsInRu.
///   3. Для каждого плагина оригинала ищем одноимённый в RU:
///      - есть → PluginPair.
///      - нет → SkippedPlugin с причиной NoMatchingTranslatedPlugin.
///
/// Плагины, найденные только в RU (без пары в оригинале), игнорируются
/// молча — переводить нечего.
///
/// Ничего не пишет, ничего не удаляет.
/// </summary>
public sealed class DetectPluginPairStep
{
    private static readonly string[] PluginExtensions =
        { ".esp", ".esm", ".esl" };

    private readonly ILogger<DetectPluginPairStep> _logger;

    public DetectPluginPairStep(ILogger<DetectPluginPairStep> logger)
    {
        _logger = logger;
    }

    public Task<DetectPluginPairResult> ExecuteAsync(
        IReadOnlyList<ModPair> modPairs,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (modPairs is null)
            throw new ArgumentNullException(nameof(modPairs));

        _logger.LogInformation(
            "DetectPluginPair: scanning {Count} mod pair(s)",
            modPairs.Count);

        var pairs = new List<PluginPair>();
        var skipped = new List<SkippedPlugin>();

        foreach (var modPair in modPairs)
        {
            ct.ThrowIfCancellationRequested();

            ProcessOneModPair(modPair, pairs, skipped);
        }

        _logger.LogInformation(
            "DetectPluginPair complete: {Pairs} plugin pair(s), " +
            "{Skipped} skipped",
            pairs.Count, skipped.Count);

        return Task.FromResult(new DetectPluginPairResult
        {
            Pairs = pairs,
            Skipped = skipped,
        });
    }

    private void ProcessOneModPair(
        ModPair modPair,
        List<PluginPair> pairs,
        List<SkippedPlugin> skipped)
    {
        var originals = ScanPlugins(modPair.OriginalPath);
        var translated = ScanPlugins(modPair.RuPath);

        if (originals.Count == 0)
        {
            _logger.LogDebug(
                "Mod '{Mod}': no plugins in original — skipping",
                modPair.OriginalName);

            skipped.Add(new SkippedPlugin
            {
                PluginFileName = "",
                SourceModName = modPair.OriginalName,
                Reason = PluginSkipReasons.NoPluginsInOriginal,
            });
            return;
        }

        if (translated.Count == 0)
        {
            _logger.LogDebug(
                "Mod '{Mod}': no plugins in RU — skipping all {Count}",
                modPair.OriginalName, originals.Count);

            foreach (var plugin in originals)
            {
                skipped.Add(new SkippedPlugin
                {
                    PluginFileName = plugin.FileName,
                    SourceModName = modPair.OriginalName,
                    Reason = PluginSkipReasons.NoPluginsInRu,
                });
            }
            return;
        }

        // Индекс RU по имени файла.
        var ruByName = new Dictionary<string, PluginFile>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var f in translated)
            ruByName[f.FileName] = f;

        foreach (var original in originals)
        {
            if (ruByName.TryGetValue(original.FileName, out var ruFile))
            {
                pairs.Add(new PluginPair
                {
                    PluginFileName = original.FileName,
                    Original = original,
                    Translated = ruFile,
                    SourceMod = modPair,
                });
            }
            else
            {
                _logger.LogDebug(
                    "Mod '{Mod}': no translated plugin for '{Plugin}' — skipping",
                    modPair.OriginalName, original.FileName);

                skipped.Add(new SkippedPlugin
                {
                    PluginFileName = original.FileName,
                    SourceModName = modPair.OriginalName,
                    Reason = PluginSkipReasons.NoMatchingTranslatedPlugin,
                });
            }
        }
    }

    /// <summary>
    /// Находит все плагины в КОРНЕ указанной папки (не рекурсивно),
    /// сортирует по имени (Ordinal).
    /// </summary>
    private static List<PluginFile> ScanPlugins(string modPath)
    {
        var result = new List<PluginFile>();

        if (!Directory.Exists(modPath))
        {
            // Мод мог быть удалён между ScanMods и этим шагом.
            // Это не наша вина — возвращаем пустой список,
            // skip-логика выше отработает.
            return result;
        }

        foreach (var path in Directory.EnumerateFiles(
            modPath, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(path);
            if (!HasPluginExtension(name))
                continue;

            result.Add(new PluginFile
            {
                FileName = name,
                FullPath = path,
            });
        }

        result.Sort(static (a, b) =>
            string.CompareOrdinal(a.FileName, b.FileName));

        return result;
    }

    private static bool HasPluginExtension(string fileName)
    {
        foreach (var ext in PluginExtensions)
        {
            if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
