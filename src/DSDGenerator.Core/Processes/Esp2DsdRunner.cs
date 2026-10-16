// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using DSDGenerator.Core.Abstractions;
using DSDGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace DSDGenerator.Core.Processes;

/// <summary>
/// Реализация IDsGenerator поверх внешнего esp2dsd.exe.
///
/// esp2dsd.exe — сборка PyInstaller (onedir). Рядом с exe лежат:
///   - string_records.json — читается относительно CWD (см. converter.py);
///   - _internal/ — Python runtime и .pyd/.dll.
///
/// esp2dsd:
///   - принимает на вход два пути (оригинал + перевод);
///   - пишет JSON-массив в CWD/Output/&lt;stem&gt;_output&lt;suffix&gt;.json;
///   - возвращает exit code 0 при успехе.
///
/// Мы не запускаем exe напрямую из Assets/: CWD процесса должен
/// указывать на папку, где лежит string_records.json, а вывод —
/// в отдельный временный Output/. Поэтому:
///   1. Создаём уникальную temp-папку.
///   2. Копируем в неё ВСЁ содержимое папки exe (exe + string_records.json
///      + _internal/).
///   3. Запускаем temp/esp2dsd.exe с CWD = temp.
///   4. Читаем temp/Output/&lt;file&gt;.json.
///   5. Удаляем temp целиком.
///
/// Валидация содержимого JSON не делается — только структура
/// верхнего уровня. За корректность самого содержимого
/// отвечает esp2dsd.
///
/// Путь к exe передаётся через конструктор: продакшн использует
/// DsdPaths.Esp2DsdExePath, тесты — путь в своей temp-папке.
/// </summary>
public sealed class Esp2DsdRunner : IDsGenerator
{
    private const string OutputSubdir = "Output";
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(5);

    private readonly IProcessRunner _processRunner;
    private readonly string _exePath;
    private readonly ILogger<Esp2DsdRunner> _logger;

    /// <summary>
    /// Продакшн-конструктор: использует DsdPaths.Esp2DsdExePath.
    /// </summary>
    public Esp2DsdRunner(
        IProcessRunner processRunner,
        ILogger<Esp2DsdRunner> logger)
        : this(processRunner, DsdPaths.Esp2DsdExePath, logger)
    {
    }

    /// <summary>
    /// Конструктор с явным путём к exe. Для тестов.
    /// </summary>
    public Esp2DsdRunner(
        IProcessRunner processRunner,
        string exePath,
        ILogger<Esp2DsdRunner> logger)
    {
        _processRunner = processRunner;
        _exePath = exePath;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(
        PluginPair pair,
        CancellationToken ct)
    {
        if (pair is null)
            throw new ArgumentNullException(nameof(pair));

        ct.ThrowIfCancellationRequested();

        if (!File.Exists(_exePath))
        {
            throw new FileNotFoundException(
                $"esp2dsd.exe not found at expected location: {_exePath}. " +
                $"Place esp2dsd.exe into the Assets/ folder next to " +
                $"DSDGenerator.exe.",
                _exePath);
        }

        if (!File.Exists(pair.Original.FullPath))
            throw new FileNotFoundException(
                $"Original plugin not found: {pair.Original.FullPath}",
                pair.Original.FullPath);

        if (!File.Exists(pair.Translated.FullPath))
            throw new FileNotFoundException(
                $"Translated plugin not found: {pair.Translated.FullPath}",
                pair.Translated.FullPath);

        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "dsdgen-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempDir);

        try
        {
            // Копируем ВСЮ папку с esp2dsd.exe (PyInstaller onedir:
            // exe + _internal/ + string_records.json) в temp.
            // Причина: esp2dsd читает string_records.json относительно
            // CWD, а CWD у нас = temp. Плюс exe ищет _internal/ рядом
            // с собой — значит, exe тоже должен быть в temp.
            var exeDir = Path.GetDirectoryName(_exePath)
                ?? throw new InvalidOperationException(
                    $"Cannot determine directory of esp2dsd.exe: {_exePath}");

            CopyDirectoryRecursive(exeDir, tempDir);

            var tempExePath = Path.Combine(
                tempDir, Path.GetFileName(_exePath));

            _logger.LogDebug(
                "Running esp2dsd for '{Plugin}' (temp: {Temp})",
                pair.PluginFileName, tempDir);

            var run = await _processRunner.RunAsync(
                fileName: tempExePath,
                arguments: new[]
                {
                    pair.Original.FullPath,
                    pair.Translated.FullPath,
                },
                workingDirectory: tempDir,
                timeout: ProcessTimeout,
                ct);

            if (!string.IsNullOrWhiteSpace(run.StandardOutput))
            {
                _logger.LogTrace(
                    "esp2dsd stdout for '{Plugin}':\n{Stdout}",
                    pair.PluginFileName, run.StandardOutput);
            }

            if (!string.IsNullOrWhiteSpace(run.StandardError))
            {
                _logger.LogWarning(
                    "esp2dsd stderr for '{Plugin}':\n{Stderr}",
                    pair.PluginFileName, run.StandardError);
            }

            if (run.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"esp2dsd.exe failed for '{pair.PluginFileName}' " +
                    $"with exit code {run.ExitCode}." +
                    (string.IsNullOrWhiteSpace(run.StandardError)
                        ? ""
                        : $" stderr: {Truncate(run.StandardError, 500)}"));
            }

            var outputFileName = BuildOutputFileName(pair.Original.FileName);
            var outputPath = Path.Combine(
                tempDir, OutputSubdir, outputFileName);

            if (!File.Exists(outputPath))
            {
                throw new InvalidOperationException(
                    $"esp2dsd.exe did not produce expected output " +
                    $"file for '{pair.PluginFileName}': {outputPath}");
            }

            var content = await File.ReadAllTextAsync(outputPath, ct);
            ValidateJsonArray(content, pair.PluginFileName);

            return content;
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    /// <summary>
    /// Строит имя выходного файла esp2dsd:
    /// &lt;stem&gt;_output&lt;suffix&gt;.json.
    /// Пример: SkyUI_SE.esp → SkyUI_SE_output.esp.json.
    /// </summary>
    internal static string BuildOutputFileName(string pluginFileName)
    {
        var stem = Path.GetFileNameWithoutExtension(pluginFileName);
        var suffix = Path.GetExtension(pluginFileName);

        return $"{stem}_output{suffix}.json";
    }

    private static void ValidateJsonArray(string content, string pluginName)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                $"esp2dsd.exe produced an empty output for " +
                $"'{pluginName}'.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"esp2dsd.exe produced malformed JSON for " +
                $"'{pluginName}': {ex.Message}", ex);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"esp2dsd.exe produced a non-array JSON " +
                    $"({doc.RootElement.ValueKind}) for " +
                    $"'{pluginName}'.");
            }

            if (doc.RootElement.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"esp2dsd.exe produced an empty JSON array for " +
                    $"'{pluginName}'. Nothing to translate.");
            }
        }
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to delete temp directory: {Path}", path);
        }
    }

    /// <summary>
    /// Рекурсивно копирует всё содержимое sourceDir в destDir.
    /// destDir должна существовать (создаётся вызывающим).
    /// Файлы копируются с overwrite: false — temp пустой, коллизий
    /// быть не должно.
    /// </summary>
    private static void CopyDirectoryRecursive(
        string sourceDir, string destDir)
    {
        foreach (var dir in Directory.EnumerateDirectories(
            sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(destDir, relative));
        }

        foreach (var file in Directory.EnumerateFiles(
            sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(destDir, relative);

            var destParent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destParent)
                && !Directory.Exists(destParent))
            {
                Directory.CreateDirectory(destParent);
            }

            File.Copy(file, dest, overwrite: false);
        }
    }
}
