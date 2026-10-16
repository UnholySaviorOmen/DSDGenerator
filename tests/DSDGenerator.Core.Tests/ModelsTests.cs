// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Models;
using FluentAssertions;

namespace DSDGenerator.Core.Tests;

public class ModelsTests
{
    // ------------------------------------------------------------------
    //  ModPair
    // ------------------------------------------------------------------

    [Fact]
    public void ModPair_EqualityByValue()
    {
        var a = new ModPair
        {
            OriginalName = "SkyUI",
            RuName = "SkyUI - RU",
            OriginalPath = @"C:\mods\SkyUI",
            RuPath = @"C:\mods\SkyUI - RU",
        };
        var b = new ModPair
        {
            OriginalName = "SkyUI",
            RuName = "SkyUI - RU",
            OriginalPath = @"C:\mods\SkyUI",
            RuPath = @"C:\mods\SkyUI - RU",
        };

        a.Should().Be(b);
    }

    // ------------------------------------------------------------------
    //  PluginFile
    // ------------------------------------------------------------------

    [Fact]
    public void PluginFile_StoresValues()
    {
        var f = new PluginFile
        {
            FileName = "SkyUI_SE.esp",
            FullPath = @"C:\mods\SkyUI\SkyUI_SE.esp",
        };

        f.FileName.Should().Be("SkyUI_SE.esp");
        f.FullPath.Should().Be(@"C:\mods\SkyUI\SkyUI_SE.esp");
    }

    // ------------------------------------------------------------------
    //  PluginPair
    // ------------------------------------------------------------------

    [Fact]
    public void PluginPair_StoresAllFields()
    {
        var modPair = new ModPair
        {
            OriginalName = "SkyUI",
            RuName = "SkyUI - RU",
            OriginalPath = @"C:\mods\SkyUI",
            RuPath = @"C:\mods\SkyUI - RU",
        };
        var original = new PluginFile
        {
            FileName = "SkyUI_SE.esp",
            FullPath = @"C:\mods\SkyUI\SkyUI_SE.esp",
        };
        var translated = new PluginFile
        {
            FileName = "SkyUI_SE.esp",
            FullPath = @"C:\mods\SkyUI - RU\SkyUI_SE.esp",
        };

        var pair = new PluginPair
        {
            PluginFileName = "SkyUI_SE.esp",
            Original = original,
            Translated = translated,
            SourceMod = modPair,
        };

        pair.PluginFileName.Should().Be("SkyUI_SE.esp");
        pair.Original.Should().Be(original);
        pair.Translated.Should().Be(translated);
        pair.SourceMod.Should().Be(modPair);
    }

    // ------------------------------------------------------------------
    //  DsdResult
    // ------------------------------------------------------------------

    [Fact]
    public void DsdResult_Generated_SetsKindAndOutput()
    {
        var r = DsdResult.Generated(
            pluginFileName: "SkyUI_SE.esp",
            sourceModName: "SkyUI",
            outputPath: @"C:\out\SkyUI_SE.esp\SkyUI_SE.json");

        r.Kind.Should().Be(DsdResultKind.Generated);
        r.PluginFileName.Should().Be("SkyUI_SE.esp");
        r.SourceModName.Should().Be("SkyUI");
        r.OutputPath.Should().Be(@"C:\out\SkyUI_SE.esp\SkyUI_SE.json");
        r.Message.Should().BeNull();
    }

    [Fact]
    public void DsdResult_Skipped_SetsMessage()
    {
        var r = DsdResult.Skipped(
            pluginFileName: "Broken.esp",
            sourceModName: "Broken",
            reason: "no matching translated plugin");

        r.Kind.Should().Be(DsdResultKind.Skipped);
        r.Message.Should().Be("no matching translated plugin");
        r.OutputPath.Should().BeNull();
    }

    [Fact]
    public void DsdResult_Failed_SetsMessage()
    {
        var r = DsdResult.Failed(
            pluginFileName: "Broken.esp",
            sourceModName: "Broken",
            reason: "esp2dsd exited with code 2");

        r.Kind.Should().Be(DsdResultKind.Failed);
        r.Message.Should().Be("esp2dsd exited with code 2");
        r.OutputPath.Should().BeNull();
    }

    // ------------------------------------------------------------------
    //  DsdSummary
    // ------------------------------------------------------------------

    [Fact]
    public void DsdSummary_StoresAllCounters()
    {
        var summary = new DsdSummary
        {
            InstancePath = @"C:\TestInstance",
            OutputPath = @"C:\TestInstance\__DSD_Output",
            ModsScanned = 82,
            ModPairsFound = 45,
            OrphanOriginals = 5,
            OrphanRuMods = 3,
            PluginPairsFound = 98,
            Generated = 90,
            Skipped = 8,
            Failed = 0,
            Results = Array.Empty<DsdResult>(),
        };

        summary.ModsScanned.Should().Be(82);
        summary.ModPairsFound.Should().Be(45);
        summary.OrphanOriginals.Should().Be(5);
        summary.OrphanRuMods.Should().Be(3);
        summary.PluginPairsFound.Should().Be(98);
        summary.Generated.Should().Be(90);
        summary.Skipped.Should().Be(8);
        summary.Failed.Should().Be(0);
        summary.Results.Should().BeEmpty();
    }
}
