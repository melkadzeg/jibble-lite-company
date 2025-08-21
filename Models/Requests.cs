public record CreateCompanyRequest(string Name, string Plan);
public record AddMemberRequest(string UserId, CompanyRole Role);
public record UpdateMemberRoleRequest(CompanyRole Role);