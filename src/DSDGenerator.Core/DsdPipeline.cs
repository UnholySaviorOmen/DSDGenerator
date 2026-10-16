// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Abstractions;
using DSDGenerator.Core.Models;
using DSDGenerator.Core.Progress;
using DSDGenerator.Core.Steps;
using Microsoft.Extensions.Logging;

namespace DSDGenerator.Core;

/// <summary>
/// Оркестратор: склеивает шаги в единый прогон.
///
///   ScanMods        → список папок модов
///   FindRuPair      → список ModPair + orphans
///   DetectPluginPair → список PluginPair + skipped
///   GenerateDsd     → JSON-строки для каждого PluginPair
///   WriteDsd        → файлы в __DSD_Output/
///
/// Отмена — через ct, пробрасывается во все шаги.
/// Ошибки на шагах 1–3 останавливают прогон.
/// Ошибки на шаге 4 (Generate) → Failed для конкретного плагина,
/// прогон продолжается.
///
/// Очистка __DSD_Output/ выполняется в начале (до генерации),
/// чтобы прогон давал полный снимок, а не накапливал старое.
/// </summary>
public sealed class DsdPipeline
{
    private static readonly string[] StepNames =
    {
        "ScanMods",
        "FindRuPair",
        "DetectPluginPair",
        "GenerateDsd",
    };

    private readonly ScanModsStep _scanMods;
    private readonly FindRuPairStep _findRuPair;
    private readonly DetectPluginPairStep _detectPluginPair;
    private readonly IDsGenerator _generator;
    private readonly WriteDsdStep _writeDsd;
    private readonly ILogger<DsdPipeline> _logger;

    public DsdPipeline(
        ScanModsStep scanMods,
        FindRuPairStep findRuPair,
        DetectPluginPairStep detectPluginPair,
        IDsGenerator generator,
        WriteDsdStep writeDsd,
        ILogger<DsdPipeline> logger)
    {
        _scanMods = scanMods;
        _findRuPair = findRuPair;
        _detectPluginPair = detectPluginPair;
        _generator = generator;
        _writeDsd = writeDsd;
        _logger = logger;
    }

    public async Task<DsdSummary> ExecuteAsync(
        string instancePath,
        CancellationToken ct,
        IProgress<StepProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(instancePath))
            throw new ArgumentException(
                "Instance path must be non-empty.", nameof(instancePath));

        var fullInstance = Path.GetFullPath(instancePath);
        int totalSteps = StepNames.Length;

        _logger.LogInformation(
            "=== DSDGenerator started ===");
        _logger.LogInformation(
            "Instance: {Path}", fullInstance);

        // --- Шаг 1: ScanMods ---
        progress?.Report(new StepProgress(1, totalSteps, StepNames[0]));
        var mods = await _scanMods.ExecuteAsync(fullInstance, ct);
        ct.ThrowIfCancellationRequested();

        // --- Шаг 2: FindRuPair ---
        progress?.Report(new StepProgress(2, totalSteps, StepNames[1]));
        var pairsResult = await _findRuPair.ExecuteAsync(mods, ct);
        ct.ThrowIfCancellationRequested();

        // --- Шаг 3: DetectPluginPair ---
        progress?.Report(new StepProgress(3, totalSteps, StepNames[2]));
        var pluginPairs = await _detectPluginPair.ExecuteAsync(
            pairsResult.Pairs, ct);
        ct.ThrowIfCancellationRequested();

        // --- Очистка __DSD_Output/ ---
        var outputDir = Path.Combine(fullInstance, DsdPaths.OutputDirName);
        CleanOutputDir(outputDir);

        // --- Шаг 4: GenerateDsd (+ WriteDsd) ---
        var results = new List<DsdResult>();

        // Сначала складываем скипы из детектора — это тоже результаты.
        foreach (var skip in pluginPairs.Skipped)
        {
            results.Add(DsdResult.Skipped(
                skip.PluginFileName,
                skip.SourceModName,
                skip.Reason));
        }

        int totalPlugins = pluginPairs.Pairs.Count;
        int processed = 0;

        progress?.Report(new StepProgress(
            4, totalSteps, StepNames[3],
            totalPlugins == 0
                ? "no plugins to process"
                : $"Processing: 0 / {totalPlugins}"));

        foreach (var pair in pluginPairs.Pairs)
        {
            ct.ThrowIfCancellationRequested();

            processed++;

            progress?.Report(new StepProgress(
                4, totalSteps, StepNames[3],
                $"Processing: {processed} / {totalPlugins} — {pair.PluginFileName}"));

            var result = await ProcessOnePluginAsync(
                fullInstance, pair, ct);

            results.Add(result);
        }

        // --- Итоговый summary ---
        int generated = results.Count(r => r.Kind == DsdResultKind.Generated);
        int skipped = results.Count(r => r.Kind == DsdResultKind.Skipped);
        int failed = results.Count(r => r.Kind == DsdResultKind.Failed);

        _logger.LogInformation(
            "=== DSDGenerator finished: {Generated} generated, " +
            "{Skipped} skipped, {Failed} failed ===",
            generated, skipped, failed);

        return new DsdSummary
        {
            InstancePath = fullInstance,
            OutputPath = outputDir,
            ModsScanned = mods.Count,
            ModPairsFound = pairsResult.Pairs.Count,
            OrphanOriginals = pairsResult.OrphanOriginals.Count,
            OrphanRuMods = pairsResult.OrphanRuMods.Count,
            PluginPairsFound = pluginPairs.Pairs.Count,
            Generated = generated,
            Skipped = skipped,
            Failed = failed,
            Results = results,
        };
    }

    // ------------------------------------------------------------------
    //  Обработка одного плагина
    // ------------------------------------------------------------------

    private async Task<DsdResult> ProcessOnePluginAsync(
        string instancePath,
        PluginPair pair,
        CancellationToken ct)
    {
        string json;
        try
        {
            json = await _generator.GenerateAsync(pair, ct);
        }
        catch (Exception ex) when (CancellationHelper.IsCancellation(ex))
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "esp2dsd failed for '{Plugin}' (mod '{Mod}')",
                pair.PluginFileName, pair.SourceMod.OriginalName);

            return DsdResult.Failed(
                pair.PluginFileName,
                pair.SourceMod.OriginalName,
                ex.Message);
        }

        ct.ThrowIfCancellationRequested();

        string outputPath;
        try
        {
            outputPath = await _writeDsd.ExecuteAsync(
                instancePath, pair, json, ct);
        }
        catch (Exception ex) when (CancellationHelper.IsCancellation(ex))
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "WriteDsd failed for '{Plugin}' (mod '{Mod}')",
                pair.PluginFileName, pair.SourceMod.OriginalName);

            return DsdResult.Failed(
                pair.PluginFileName,
                pair.SourceMod.OriginalName,
                ex.Message);
        }

        _logger.LogDebug(
            "Generated DSD for '{Plugin}' → {Path}",
            pair.PluginFileName, outputPath);

        return DsdResult.Generated(
            pair.PluginFileName,
            pair.SourceMod.OriginalName,
            outputPath);
    }

    // ------------------------------------------------------------------
    //  Очистка __DSD_Output/
    // ------------------------------------------------------------------

    private void CleanOutputDir(string outputDir)
    {
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            return;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(outputDir))
        {
            try
            {
                if (Directory.Exists(entry))
                    Directory.Delete(entry, recursive: true);
                else
                    File.Delete(entry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to clean '{Path}' in __DSD_Output/", entry);
            }
        }

        _logger.LogDebug("Cleaned __DSD_Output/: {Path}", outputDir);
    }
}
