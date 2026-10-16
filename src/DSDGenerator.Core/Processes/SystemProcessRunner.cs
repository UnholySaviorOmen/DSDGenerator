// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Text;
using DSDGenerator.Core.Abstractions;

namespace DSDGenerator.Core.Processes;

/// <summary>
/// Продакшн-реализация IProcessRunner через System.Diagnostics.Process.
///
/// Асинхронно читает stdout/stderr (иначе возможен deadlock при
/// заполнении буфера пайпа).
///
/// Kill всего дерева процессов при таймауте.
/// </summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(
                "File name must be non-empty.", nameof(fileName));

        if (string.IsNullOrWhiteSpace(workingDirectory))
            throw new ArgumentException(
                "Working directory must be non-empty.",
                nameof(workingDirectory));

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };

        process.Start();

        // Асинхронно вычитываем stdout/stderr, чтобы избежать
        // deadlock при переполнении пайпа.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
            when (!ct.IsCancellationRequested)
        {
            // Таймаут, а не отмена пользователем.
            try { process.Kill(entireProcessTree: true); } catch { }

            throw new TimeoutException(
                $"Process '{Path.GetFileName(fileName)}' " +
                $"did not finish within {timeout.TotalMinutes:F1} minute(s).");
        }
        catch (OperationCanceledException)
        {
            // Внешняя отмена.
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new ProcessRunResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout,
            StandardError = stderr,
        };
    }
}
