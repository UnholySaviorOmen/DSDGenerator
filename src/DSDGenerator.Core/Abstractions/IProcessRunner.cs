// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Abstractions;

/// <summary>
/// Тонкая обёртка над запуском внешнего процесса.
///
/// Абстракция нужна для тестируемости: Esp2DsdRunner не должен
/// сам дёргать Process.Start — иначе тесты будут запускать
/// реальные exe. В тестах подставляется FakeProcessRunner,
/// который эмулирует нужное поведение (exit code, создание
/// файлов, таймаут).
///
/// Контракт:
///   - StartAsync запускает процесс, ждёт завершения;
///   - возвращает exit code и захваченные stdout/stderr;
///   - бросает TimeoutException при превышении таймаута;
///   - бросает OperationCanceledException при отмене ct.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Запустить процесс и дождаться его завершения.
    /// </summary>
    /// <param name="fileName">Путь к exe.</param>
    /// <param name="arguments">Аргументы командной строки.</param>
    /// <param name="workingDirectory">Рабочая директория.</param>
    /// <param name="timeout">Максимальное время работы.</param>
    /// <param name="ct">Токен отмены.</param>
    Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken ct);
}

/// <summary>
/// Результат запуска процесса.
/// </summary>
public sealed record ProcessRunResult
{
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
}
