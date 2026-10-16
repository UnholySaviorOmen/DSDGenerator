// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Models;

/// <summary>
/// Папка мода в MO2/mods/.
///
/// Name — имя папки (например, "SkyUI" или "SkyUI - RU").
///   Регистр сохраняется как в ФС.
/// FullPath — полный путь к папке.
/// </summary>
public sealed record ModFolder
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
}
