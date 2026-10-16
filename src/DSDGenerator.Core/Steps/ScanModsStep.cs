// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace DSDGenerator.Core.Steps;

/// <summary>
/// Сканирует MO2/mods/ и возвращает список папок модов.
///
/// Контрактные проверки (падаем с понятной ошибкой):
///   - InstancePath непустой;
///   - InstancePath существует;
///   - InstancePath/MO2 существует;
///   - InstancePath/MO2/mods существует.
///
/// Файлы в mods/ игнорируются (MO2 хранит только папки, но
/// подстраховываемся — вдруг кто-то положил readme.txt).
///
/// Результат отсортирован по Name (Ordinal) — детерминизм для
/// последующих шагов и тестов.
///
/// Ничего не пишет, ничего не удаляет.
/// </summary>
public sealed class ScanModsStep
{
    private const string Mo2DirName = "MO2";
    private const string ModsDirName = "mods";

    private readonly ILogger<ScanModsStep> _logger;

    public ScanModsStep(ILogger<ScanModsStep> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<ModFolder>> ExecuteAsync(
        string instancePath,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(instancePath))
            throw new ArgumentException(
                "Instance path must be non-empty.", nameof(instancePath));

        var fullInstance = Path.GetFullPath(instancePath);

        if (!Directory.Exists(fullInstance))
            throw new DirectoryNotFoundException(
                $"Instance directory not found: {fullInstance}");

        var mo2Path = Path.Combine(fullInstance, Mo2DirName);
        if (!Directory.Exists(mo2Path))
            throw new DirectoryNotFoundException(
                $"MO2/ not found in instance: {mo2Path}. " +
                $"This does not look like a MO2 instance.");

        var modsPath = Path.Combine(mo2Path, ModsDirName);
        if (!Directory.Exists(modsPath))
            throw new DirectoryNotFoundException(
                $"MO2/mods/ not found: {modsPath}.");

        _logger.LogInformation(
            "Scanning mods in {Path}", modsPath);

        var result = new List<ModFolder>();

        foreach (var dir in Directory.EnumerateDirectories(modsPath))
        {
            ct.ThrowIfCancellationRequested();

            var name = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(name))
                continue;

            result.Add(new ModFolder
            {
                Name = name,
                FullPath = dir,
            });
        }

        result.Sort(static (a, b) =>
            string.CompareOrdinal(a.Name, b.Name));

        _logger.LogInformation(
            "ScanMods complete: {Count} folder(s)", result.Count);

        return Task.FromResult<IReadOnlyList<ModFolder>>(result);
    }
}
