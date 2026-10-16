// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Cli;
using DSDGenerator.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ---------------------------------------------------------------------
//  DSDGenerator — CLI-утилита для генерации DSD-переводов.
//
//  Читает установленный инстанс MO2, находит пары
//  <ModName> / <ModName> - RU, прогоняет плагины через esp2dsd.exe
//  и раскладывает готовые JSON в __DSD_Output/.
//
//  Использование:
//    DSDGenerator.exe <instancePath>
//
//  Exit codes:
//    0   — pipeline завершён (даже если есть Failed-плагины).
//    1   — pipeline упал (плохой инстанс, нет MO2/, и т.п.).
//    130 — отмена (Ctrl+C).
//
//  Логи: %LOCALAPPDATA%\DSDGenerator\logs\dsdgenerator-YYYY-MM-DD.log
// ---------------------------------------------------------------------

const string Version = "0.1.0";

Console.WriteLine($"DSDGenerator v{Version}");
Console.WriteLine();

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: DSDGenerator.exe <instancePath>");
    return 1;
}

var instancePath = args[0];

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;   // не убивать процесс мгновенно
    cts.Cancel();
    Console.Error.WriteLine();
    Console.Error.WriteLine("Cancellation requested...");
};

using var services = BuildServices();
var runner = services.GetRequiredService<DsdRunner>();

try
{
    return await runner.RunAsync(instancePath, cts.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return 130;
}

static ServiceProvider BuildServices()
{
    var services = new ServiceCollection();

    services.AddLogging(builder =>
    {
        builder.AddSimpleConsole(opts =>
        {
            opts.SingleLine = true;
            opts.TimestampFormat = "HH:mm:ss ";
        });

        builder.AddFileLogger();

        builder.SetMinimumLevel(LogLevel.Warning);
    });

    services.AddDsdGenerator();

    return services.BuildServiceProvider();
}
