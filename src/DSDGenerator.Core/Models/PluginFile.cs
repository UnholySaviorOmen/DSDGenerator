// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Models;

/// <summary>
/// Один файл плагина (.esp/.esm/.esl), найденный в корне мода.
///
/// FileName — имя файла с расширением, как в ФС (например, "SkyUI_SE.esp").
///   Регистр сохраняется как есть.
/// FullPath — полный путь к файлу.
/// </summary>
public sealed record PluginFile
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
}
