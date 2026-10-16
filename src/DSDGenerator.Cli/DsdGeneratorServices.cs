// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core;
using DSDGenerator.Core.Abstractions;
using DSDGenerator.Core.Processes;
using DSDGenerator.Core.Steps;
using Microsoft.Extensions.DependencyInjection;

namespace DSDGenerator.Cli;

/// <summary>
/// DI-extension для CLI: регистрирует шаги, pipeline и инфраструктуру.
/// </summary>
internal static class DsdGeneratorServices
{
    public static IServiceCollection AddDsdGenerator(
        this IServiceCollection services)
    {
        // Инфраструктура.
        services.AddSingleton<IProcessRunner, SystemProcessRunner>();
        services.AddSingleton<IDsGenerator, Esp2DsdRunner>();

        // Шаги.
        services.AddSingleton<ScanModsStep>();
        services.AddSingleton<FindRuPairStep>();
        services.AddSingleton<DetectPluginPairStep>();
        services.AddSingleton<WriteDsdStep>();

        // Pipeline.
        services.AddSingleton<DsdPipeline>();

        // CLI-раннер.
        services.AddSingleton<DsdRunner>();

        return services;
    }
}
