// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Progress;
using FluentAssertions;

namespace DSDGenerator.Core.Tests;

public class StepProgressTests
{
    [Fact]
    public void Constructor_WithoutDetail_DetailIsNull()
    {
        var p = new StepProgress(1, 5, "ScanMods");

        p.StepIndex.Should().Be(1);
        p.TotalSteps.Should().Be(5);
        p.StepName.Should().Be("ScanMods");
        p.Detail.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithDetail_DetailIsSet()
    {
        var p = new StepProgress(4, 5, "GenerateDsd", "Processing: SkyUI_SE.esp");

        p.Detail.Should().Be("Processing: SkyUI_SE.esp");
    }

    [Fact]
    public void Equality_SameValues_Equal()
    {
        var a = new StepProgress(1, 5, "X", "detail");
        var b = new StepProgress(1, 5, "X", "detail");

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentDetail_NotEqual()
    {
        var a = new StepProgress(1, 5, "X", "a");
        var b = new StepProgress(1, 5, "X", "b");

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_ModifiesDetail()
    {
        var original = new StepProgress(1, 5, "X");
        var modified = original with { Detail = "hello" };

        original.Detail.Should().BeNull();
        modified.Detail.Should().Be("hello");
    }
}
