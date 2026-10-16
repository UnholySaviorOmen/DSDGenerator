// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;

namespace DSDGenerator.Core.Tests;

public class DsdPathsTests
{
    [Fact]
    public void Root_ContainsDsdGenerator()
    {
        DsdPaths.Root.Should().Contain("DSDGenerator");
        Path.IsPathRooted(DsdPaths.Root).Should().BeTrue();
    }

    [Fact]
    public void LogsDirectory_UnderRoot()
    {
        DsdPaths.LogsDirectory.Should().Be(
            Path.Combine(DsdPaths.Root, "logs"));
    }

    [Fact]
    public void OutputDirName_IsExpectedConstant()
    {
        DsdPaths.OutputDirName.Should().Be("__DSD_Output");
    }

    [Fact]
    public void DynamicStringDistributorSubPath_IsExpectedConstant()
    {
        DsdPaths.DynamicStringDistributorSubPath
            .Should().Be("SKSE/Plugins/DynamicStringDistributor");
    }

    [Fact]
    public void RuModSuffix_IsExpectedConstant()
    {
        DsdPaths.RuModSuffix.Should().Be(" - RU");
    }

    [Fact]
    public void DsdSuffix_IsExpectedConstant()
    {
        DsdPaths.DsdSuffix.Should().Be(" (DSD)");
    }

    [Fact]
    public void Esp2DsdExeName_IsExpectedConstant()
    {
        DsdPaths.Esp2DsdExeName.Should().Be("esp2dsd.exe");
    }

    [Fact]
    public void Esp2DsdExePath_IsUnderBaseDirectoryAssets()
    {
        var expected = Path.Combine(
            AppContext.BaseDirectory, "Assets", "esp2dsd.exe");

        DsdPaths.Esp2DsdExePath.Should().Be(expected);
    }
}
