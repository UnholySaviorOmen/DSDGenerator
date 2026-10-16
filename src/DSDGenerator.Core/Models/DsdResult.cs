// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Models;

/// <summary>
/// Результат обработки одного плагина.
///
/// Kind — что произошло (Generated / Skipped / Failed).
/// PluginFileName — имя плагина (для отчёта).
/// SourceModName — имя оригинального мода (для отчёта).
/// Message — опциональное сообщение (причина skip или текст ошибки).
/// OutputPath — путь к сгенерированному JSON (только при Generated).
/// </summary>
public sealed record DsdResult
{
    public required DsdResultKind Kind { get; init; }
    public required string PluginFileName { get; init; }
    public required string SourceModName { get; init; }
    public string? Message { get; init; }
    public string? OutputPath { get; init; }

    public static DsdResult Generated(
        string pluginFileName,
        string sourceModName,
        string outputPath) => new()
        {
            Kind = DsdResultKind.Generated,
            PluginFileName = pluginFileName,
            SourceModName = sourceModName,
            OutputPath = outputPath,
        };

    public static DsdResult Skipped(
        string pluginFileName,
        string sourceModName,
        string reason) => new()
        {
            Kind = DsdResultKind.Skipped,
            PluginFileName = pluginFileName,
            SourceModName = sourceModName,
            Message = reason,
        };

    public static DsdResult Failed(
        string pluginFileName,
        string sourceModName,
        string reason) => new()
        {
            Kind = DsdResultKind.Failed,
            PluginFileName = pluginFileName,
            SourceModName = sourceModName,
            Message = reason,
        };
}

public enum DsdResultKind
{
    Generated,
    Skipped,
    Failed,
}
