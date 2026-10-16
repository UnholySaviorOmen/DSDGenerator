// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Models;

/// <summary>
/// Стабильные причины skip на шаге DetectPluginPairStep.
/// Константы, чтобы тесты и логи ссылались на одну строку.
/// </summary>
public static class PluginSkipReasons
{
    /// <summary>В оригинальном моде нет ни одного .esp/.esm/.esl в корне.</summary>
    public const string NoPluginsInOriginal = "No plugins in original mod";

    /// <summary>В RU-моде нет ни одного .esp/.esm/.esl в корне.</summary>
    public const string NoPluginsInRu = "No plugins in RU mod";

    /// <summary>Плагин есть в оригинале, но нет одноимённого в RU.</summary>
    public const string NoMatchingTranslatedPlugin = "No matching translated plugin";
}
