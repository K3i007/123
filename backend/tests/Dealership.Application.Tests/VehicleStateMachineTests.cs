using Dealership.Domain;
using Xunit;

namespace Dealership.Application.Tests;
public sealed class VehicleStateMachineTests
{
    [Theory]
    [InlineData(VehicleStatus.Draft, VehicleStatus.InReview)]
    [InlineData(VehicleStatus.InReview, VehicleStatus.Photography)]
    [InlineData(VehicleStatus.Photography, VehicleStatus.Inspection)]
    [InlineData(VehicleStatus.Inspection, VehicleStatus.Approved)]
    [InlineData(VehicleStatus.Approved, VehicleStatus.Published)]
    public void Allows_required_lifecycle_transition(VehicleStatus from, VehicleStatus to) => Assert.True(VehicleStateMachine.CanTransition(from, to));
    [Fact] public void Rejects_skipping_a_lifecycle_step() => Assert.False(VehicleStateMachine.CanTransition(VehicleStatus.Draft, VehicleStatus.Published));
    [Fact] public void Publishing_requires_data_and_price() => Assert.Contains(PublicationRequirements.Evaluate(new Vehicle()), x => !x.IsSatisfied);
}
