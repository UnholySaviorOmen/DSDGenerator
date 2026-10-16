// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Progress;

/// <summary>
/// Прогресс одного шага pipeline.
///
/// StepIndex — 1-based, для отображения «шаг 3 из 5».
/// TotalSteps — общее число шагов в pipeline.
/// StepName — короткое стабильное имя шага, для логов и UI.
/// Detail — опциональная деталь шага (например,
///   «Processing: MyMod.esp»). Null для шагов без деталей.
/// </summary>
public readonly record struct StepProgress(
    int StepIndex,
    int TotalSteps,
    string StepName,
    string? Detail = null);
