using CryptoHelper;
using Demizon.Contracts.Members;
using Demizon.Core.Services.Member;
using Demizon.Mvc.Extensions;
using Demizon.Mvc.Mapping;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demizon.Mvc.Controllers.Api;

[ApiController]
[Route("api/members")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MembersController(IMemberService memberService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<MemberProfileDto>> GetMyProfile()
    {
        var memberId = User.GetMemberId();
        var member = await memberService.GetOneAsync(memberId);
        return Ok(member.ToProfileDto());
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
    {
        var memberId = User.GetMemberId();
        var member = await memberService.GetOneAsync(memberId);

        member.Name = request.Name.Trim();
        member.Surname = request.Surname.Trim();
        member.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        await memberService.UpdateAsync(memberId, member);
        return Ok();
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var memberId = User.GetMemberId();
        var member = await memberService.GetOneAsync(memberId);

        if (!Crypto.VerifyHashedPassword(member.PasswordHash, request.CurrentPassword))
            return BadRequest(new { error = "Nesprávné aktuální heslo." });

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 4)
            return BadRequest(new { error = "Nové heslo musí mít alespoň 4 znaky." });

        member.PasswordHash = Crypto.HashPassword(request.NewPassword);
        await memberService.UpdateAsync(memberId, member);
        return Ok();
    }
}
