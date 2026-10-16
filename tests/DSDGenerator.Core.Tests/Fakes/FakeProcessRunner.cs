// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Abstractions;

namespace DSDGenerator.Core.Tests.Fakes;

/// <summary>
/// Fake IProcessRunner для тестов Esp2DsdRunner.
///
/// Поведение задаётся делегатом OnRun, который получает
/// (fileName, arguments, workingDirectory) и возвращает
/// ProcessRunResult. Внутри делегата тест может создать
/// нужный выходной файл в workingDirectory.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    public Func<string, IReadOnlyList<string>, string, ProcessRunResult>?
        OnRun
    { get; set; }

    public ProcessRunResult DefaultResult { get; set; } = new()
    {
        ExitCode = 0,
        StandardOutput = "",
        StandardError = "",
    };

    public int CallCount { get; private set; }
    public string? LastFileName { get; private set; }
    public IReadOnlyList<string>? LastArguments { get; private set; }
    public string? LastWorkingDirectory { get; private set; }

    public Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        CallCount++;
        LastFileName = fileName;
        LastArguments = arguments;
        LastWorkingDirectory = workingDirectory;

        var result = OnRun is null
            ? DefaultResult
            : OnRun(fileName, arguments, workingDirectory);

        return Task.FromResult(result);
    }
}
