// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Models;

/// <summary>
/// Пара плагинов оригинал + перевод с одинаковым именем файла.
///
/// PluginFileName — имя файла с расширением (например, "SkyUI_SE.esp"),
///   совпадает в обоих модах.
/// Original — оригинальный плагин.
/// Translated — переведённый плагин.
/// SourceMod — родительская пара модов (для контекста и логов).
/// </summary>
public sealed record PluginPair
{
    public required string PluginFileName { get; init; }
    public required PluginFile Original { get; init; }
    public required PluginFile Translated { get; init; }
    public required ModPair SourceMod { get; init; }
}
