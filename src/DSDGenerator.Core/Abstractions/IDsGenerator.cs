// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Models;

namespace DSDGenerator.Core.Abstractions;

/// <summary>
/// Обёртка над внешним esp2dsd.exe.
///
/// Абстракция нужна, чтобы DsdPipeline тестировался без реального
/// exe (в тестах подставляется fake, возвращающий заготовленный JSON).
///
/// Контракт:
///   - вход: пара плагинов (оригинал + перевод);
///   - выход: строковое содержимое JSON-файла перевода.
///
/// Реализация (Esp2DsdRunner) запускает esp2dsd.exe в temp-папке,
/// ждёт файл в Output/, читает, минимально валидирует (непустой,
/// парсится как JSON-массив), возвращает содержимое.
///
/// Бросает исключение при любой ошибке. Retry не делается: esp2dsd
/// либо работает, либо нет, повторный прогон на тех же входных
/// данных даст тот же результат.
/// </summary>
public interface IDsGenerator
{
    /// <summary>
    /// Сгенерировать JSON перевода для пары плагинов.
    /// </summary>
    /// <param name="pair">Пара оригинал + перевод.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Содержимое JSON-файла (UTF-8, как строка).</returns>
    /// <exception cref="FileNotFoundException">
    /// Если esp2dsd.exe не найден по ожидаемому пути.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Если esp2dsd.exe завершился с ненулевым кодом, не создал
    /// выходной файл или файл не парсится как JSON-массив.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Если ct отменён.
    /// </exception>
    Task<string> GenerateAsync(
        PluginPair pair,
        CancellationToken ct);
}
