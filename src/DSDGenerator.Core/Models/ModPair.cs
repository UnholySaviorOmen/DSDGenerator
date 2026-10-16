// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Models;

/// <summary>
/// Пара <ModName> / <ModName> - RU.
///
/// OriginalName — имя папки оригинала (например, "SkyUI").
/// RuName — имя папки перевода (например, "SkyUI - RU").
/// OriginalPath — полный путь к папке оригинала.
/// RuPath — полный путь к папке перевода.
/// </summary>
public sealed record ModPair
{
    public required string OriginalName { get; init; }
    public required string RuName { get; init; }
    public required string OriginalPath { get; init; }
    public required string RuPath { get; init; }
}
