using System.Security.Claims;
using Demizon.Mvc.Extensions;

namespace Demizon.Tests.Unit;

/// <summary>
/// API čte member id z <see cref="ClaimTypes.PrimarySid"/>. Kdyby se claim
/// přejmenoval v <c>TokenService</c> a tady ne, každý autentizovaný endpoint
/// by padl až za běhu.
/// </summary>
public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetMemberId_cte_PrimarySid()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.PrimarySid, "17")
        ]));

        Assert.Equal(17, user.GetMemberId());
    }

    [Fact]
    public void GetMemberId_bez_claimu_vyhodi()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Throws<InvalidOperationException>(() => user.GetMemberId());
    }
}
