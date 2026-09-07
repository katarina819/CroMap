// Models/AuthDtos.cs
public class ForgotPasswordDto
{
    public string Email { get; set; }
   
}

public class ResetPasswordDto
{
    /// <summary>E-mail za koji je kod zatražen — kod se provjerava samo za tog korisnika.</summary>
    public string Email { get; set; }
    public string Code { get; set; }
    public string NewPassword { get; set; }
}