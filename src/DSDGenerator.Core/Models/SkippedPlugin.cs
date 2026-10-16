// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Models;

/// <summary>
/// Причина, по которой плагин не попал в обработку.
///
/// Это НЕ результат прогона (не DsdResult). Это состояние
/// на входе шага DetectPluginPairStep: плагин не был даже
/// предложен к генерации.
///
/// PluginFileName — имя плагина (или пустая строка, если плагинов
///   в моде нет вообще; тогда заполняется SourceModName и Reason).
/// SourceModName — имя оригинального мода.
/// Reason — человекочитаемая причина.
/// </summary>
public sealed record SkippedPlugin
{
    public required string PluginFileName { get; init; }
    public required string SourceModName { get; init; }
    public required string Reason { get; init; }
}
