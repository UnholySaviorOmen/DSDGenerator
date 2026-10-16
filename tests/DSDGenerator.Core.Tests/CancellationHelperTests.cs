// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using FluentAssertions;

namespace DSDGenerator.Core.Tests;

public class CancellationHelperTests
{
    [Fact]
    public void Null_ReturnsFalse()
    {
        CancellationHelper.IsCancellation(null!).Should().BeFalse();
    }

    [Fact]
    public void OperationCanceledException_ReturnsTrue()
    {
        CancellationHelper.IsCancellation(
            new OperationCanceledException()).Should().BeTrue();
    }

    [Fact]
    public void TaskCanceledException_ReturnsTrue()
    {
        CancellationHelper.IsCancellation(
            new TaskCanceledException()).Should().BeTrue();
    }

    [Fact]
    public void OrdinaryException_ReturnsFalse()
    {
        CancellationHelper.IsCancellation(
            new InvalidOperationException("boom")).Should().BeFalse();
    }

    [Fact]
    public void AggregateOfCancellations_ReturnsTrue()
    {
        var agg = new AggregateException(
            new OperationCanceledException(),
            new TaskCanceledException());

        CancellationHelper.IsCancellation(agg).Should().BeTrue();
    }

    [Fact]
    public void AggregateWithMixedInner_ReturnsFalse()
    {
        var agg = new AggregateException(
            new OperationCanceledException(),
            new InvalidOperationException("boom"));

        CancellationHelper.IsCancellation(agg).Should().BeFalse();
    }

    [Fact]
    public void EmptyAggregate_ReturnsFalse()
    {
        var agg = new AggregateException(Array.Empty<Exception>());

        CancellationHelper.IsCancellation(agg).Should().BeFalse();
    }

    [Fact]
    public void NestedAggregateOfCancellations_ReturnsTrue()
    {
        var inner = new AggregateException(
            new OperationCanceledException(),
            new OperationCanceledException());

        var outer = new AggregateException(inner);

        CancellationHelper.IsCancellation(outer).Should().BeTrue();
    }
}
