using AwesomeAssertions;
using Soarscore.Domain;
using Xunit;

namespace Soarscore.Domain.Tests;

public class ResultTests
{
    [Fact]
    public void Success_carries_the_value_and_no_failure_detail()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
        result.Code.Should().BeNull();
        result.Message.Should().BeNull();
        result.Defects.Should().BeEmpty();
    }

    [Fact]
    public void Failure_carries_a_stable_code_and_message_and_no_value()
    {
        var result = Result<int>.Failure("person.name.blank", "Name must not be blank.");

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("person.name.blank");
        result.Message.Should().Be("Name must not be blank.");
        result.Defects.Should().BeEmpty();
    }

    [Fact]
    public void Failure_can_carry_more_than_one_defect()
    {
        Defect[] defects =
        [
            new("check.3", "$.bands[0]", "Band 0 has no rows."),
            new("check.7", "$.tasks[2]", "Task 2 references an unbound parameter."),
        ];

        var result = Result<string>.Failure("classDefinition.invalid", "Definition failed validation.", defects);

        result.Defects.Should().BeEquivalentTo(defects);
    }

    [Fact]
    public void Reading_Value_on_a_failure_throws()
    {
        var result = Result<int>.Failure("some.code", "some message");

        FluentActions.Invoking(() => result.Value).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Match_invokes_the_branch_matching_the_outcome()
    {
        var success = Result<int>.Success(7);
        var failure = Result<int>.Failure("some.code", "some message");

        success.Match(v => v * 2, _ => -1).Should().Be(14);
        failure.Match(v => v * 2, _ => -1).Should().Be(-1);
    }
}
