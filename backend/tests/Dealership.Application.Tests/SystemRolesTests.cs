using Dealership.Domain;
using Xunit;
namespace Dealership.Application.Tests;
public sealed class SystemRolesTests { [Fact] public void Has_the_seven_required_roles() => Assert.Equal(7, SystemRoles.All.Length); }
