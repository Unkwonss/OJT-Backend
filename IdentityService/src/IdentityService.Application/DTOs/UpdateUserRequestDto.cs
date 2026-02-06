namespace IdentityService.Application.DTOs;

public class UpdateUserRequestDto
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Status { get; set; }
}
