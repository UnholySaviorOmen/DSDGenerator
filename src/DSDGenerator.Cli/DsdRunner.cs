// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core;
using DSDGenerator.Core.Models;
using DSDGenerator.Core.Progress;
using Microsoft.Extensions.Logging;

namespace DSDGenerator.Cli;

/// <summary>
/// CLI-обёртка над DsdPipeline:
///   - запускает pipeline с прогресс-репортом в stderr;
///   - печатает итоговый отчёт в stdout;
///   - возвращает exit code.
/// </summary>
internal sealed class DsdRunner
{
    private const int MaxFailedShown = 5;

    private readonly DsdPipeline _pipeline;
    private readonly ILogger<DsdRunner> _logger;

    public DsdRunner(
        DsdPipeline pipeline,
        ILogger<DsdRunner> logger)
    {
        _pipeline = pipeline;
        _logger = logger;
    }

    public async Task<int> RunAsync(
        string instancePath,
        CancellationToken ct)
    {
        var progress = new Progress<StepProgress>(OnProgress);

        DsdSummary summary;
        try
        {
            summary = await _pipeline.ExecuteAsync(
                instancePath, ct, progress);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline failed");
            Console.Error.WriteLine();
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }

        PrintSummary(summary);
        return 0;
    }

    private static void OnProgress(StepProgress p)
    {
        var line = $"Step {p.StepIndex} of {p.TotalSteps}: {p.StepName}";
        if (!string.IsNullOrEmpty(p.Detail))
            line += $" — {p.Detail}";

        // Прогресс — в stderr, чтобы не мешать итоговому отчёту в stdout.
        Console.Error.WriteLine(line);
    }

    private static void PrintSummary(DsdSummary summary)
    {
        Console.WriteLine();
        Console.WriteLine("=== DSDGenerator ===");
        Console.WriteLine($"Instance:          {summary.InstancePath}");
        Console.WriteLine($"Output:            {summary.OutputPath}");
        Console.WriteLine();
        Console.WriteLine($"Mods scanned:      {summary.ModsScanned}");
        Console.WriteLine($"Mod pairs:         {summary.ModPairsFound}");
        Console.WriteLine($"Orphan originals:  {summary.OrphanOriginals}");
        Console.WriteLine($"Orphan RU mods:    {summary.OrphanRuMods}");
        Console.WriteLine();
        Console.WriteLine($"Plugin pairs:      {summary.PluginPairsFound}");
        Console.WriteLine();
        Console.WriteLine($"Generated:         {summary.Generated}");
        Console.WriteLine($"Skipped:           {summary.Skipped}");
        Console.WriteLine($"Failed:            {summary.Failed}");

        var failed = summary.Results
            .Where(r => r.Kind == DsdResultKind.Failed)
            .Take(MaxFailedShown)
            .ToList();

        if (failed.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Failed plugins:");
            foreach (var f in failed)
            {
                Console.WriteLine(
                    $"  {f.SourceModName} / {f.PluginFileName}: {f.Message}");
            }

            if (summary.Failed > failed.Count)
            {
                Console.WriteLine(
                    $"  ... and {summary.Failed - failed.Count} more");
            }
        }
    }
}
