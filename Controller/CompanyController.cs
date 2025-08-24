using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;

[ApiController]
[Route("companies")]
public class CompaniesController : ControllerBase
{
    private readonly CompanyDb _db;
    public CompaniesController(CompanyDb db) => _db = db;

    private string RequireUserId()
        => Request.Headers["X-User-Id"].FirstOrDefault()
           ?? throw new UnauthorizedAccessException("Missing user id.");


    private static bool IsAdminOrOwner(CompanyRole r) => r is CompanyRole.Owner or CompanyRole.Admin;

    // --- Companies ---

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequest req, CancellationToken ct)
    {
        var uid = RequireUserId();

        if (await _db.Companies.AnyAsync(c => c.Name == req.Name, ct))
            return Conflict("Company name already taken.");

        var company = new Company { Name = req.Name, Plan = req.Plan };
        await _db.Companies.AddAsync(company, ct);
        await _db.SaveChangesAsync(ct);
        
        await _db.CompanyMembers.AddAsync(new CompanyMember
        {
            CompanyId = company.Id, UserId = uid, Role = CompanyRole.Owner
        }, ct);

        await _db.SaveChangesAsync(ct);
        return Created($"/companies/{company.Id}", company);
    }

    [HttpGet]
    public async Task<IActionResult> ListMine(CancellationToken ct)
    {
        var uid = RequireUserId();

        var companyIds = await _db.CompanyMembers
            .Where(m => m.UserId == uid)
            .Select(m => m.CompanyId)
            .ToListAsync(ct);

        var companies = await _db.Companies
            .Where(c => companyIds.Contains(c.Id))
            .ToListAsync(ct);

        return Ok(companies);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var uid = RequireUserId();

        var membership = await _db.CompanyMembers
            .FirstOrDefaultAsync(m => m.CompanyId == id && m.UserId == uid, ct);
        if (membership is null) return Forbid();

        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == id, ct);
        return company is null ? NotFound() : Ok(company);
    }

    // --- Members ---

    [HttpGet("{id:long}/members")]
    public async Task<IActionResult> ListMembers(long id, CancellationToken ct)
    {
        var uid = RequireUserId();
        var me = await _db.CompanyMembers.FirstOrDefaultAsync(m => m.CompanyId == id && m.UserId == uid, ct);
        if (me is null) return Forbid();

        var members = await _db.CompanyMembers.Where(m => m.CompanyId == id).ToListAsync(ct);
        return Ok(members);
    }

    [HttpPost("{id:long}/members")]
    public async Task<IActionResult> AddMember(long id, [FromBody] AddMemberRequest body, CancellationToken ct)
    {
        var uid = RequireUserId();
        var me = await _db.CompanyMembers.FirstOrDefaultAsync(m => m.CompanyId == id && m.UserId == uid, ct);
        if (me is null || !IsAdminOrOwner(me.Role)) return Forbid();

        var exists = await _db.CompanyMembers.AnyAsync(m => m.CompanyId == id && m.UserId == body.UserId, ct);
        if (exists) return Conflict("User already a member.");

        await _db.CompanyMembers.AddAsync(new CompanyMember
        {
            CompanyId = id, UserId = body.UserId, Role = body.Role
        }, ct);

        await _db.SaveChangesAsync(ct);
        return Created($"/companies/{id}/members/{body.UserId}", body);
    }

    [HttpPatch("{id:long}/members/{memberUserId}")]
    public async Task<IActionResult> UpdateMemberRole(long id, string memberUserId, [FromBody] UpdateMemberRoleRequest body, CancellationToken ct)
    {
        var uid = RequireUserId();
        var me = await _db.CompanyMembers.FirstOrDefaultAsync(m => m.CompanyId == id && m.UserId == uid, ct);
        if (me is null || !IsAdminOrOwner(me.Role)) return Forbid();

        var target = await _db.CompanyMembers.FirstOrDefaultAsync(m => m.CompanyId == id && m.UserId == memberUserId, ct);
        if (target is null) return NotFound();

        if (target.Role == CompanyRole.Owner && body.Role != CompanyRole.Owner)
        {
            var owners = await _db.CompanyMembers.CountAsync(m => m.CompanyId == id && m.Role == CompanyRole.Owner, ct);
            if (owners <= 1) return BadRequest("Cannot demote the last Owner.");
        }

        target.Role = body.Role;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:long}/members/{memberUserId}")]
    public async Task<IActionResult> RemoveMember(long id, string memberUserId, CancellationToken ct)
    {
        var uid = RequireUserId();
        var me = await _db.CompanyMembers.FirstOrDefaultAsync(m => m.CompanyId == id && m.UserId == uid, ct);
        if (me is null || !IsAdminOrOwner(me.Role)) return Forbid();

        var target = await _db.CompanyMembers.FirstOrDefaultAsync(m => m.CompanyId == id && m.UserId == memberUserId, ct);
        if (target is null) return NotFound();

        if (target.Role == CompanyRole.Owner)
        {
            var owners = await _db.CompanyMembers.CountAsync(m => m.CompanyId == id && m.Role == CompanyRole.Owner, ct);
            if (owners <= 1) return BadRequest("Cannot remove the last Owner.");
        }

        _db.CompanyMembers.Remove(target);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:long}/me")]
    public async Task<IActionResult> MyMembership(long id, CancellationToken ct)
    {
        var uid = RequireUserId();
        var me = await _db.CompanyMembers.FirstOrDefaultAsync(m => m.CompanyId == id && m.UserId == uid, ct);
        return me is null ? Forbid() : Ok(new { me.UserId, me.Role, me.CompanyId });
    }
}
