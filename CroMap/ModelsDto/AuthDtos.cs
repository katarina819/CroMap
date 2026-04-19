// Models/AuthDtos.cs
public class ForgotPasswordDto
{
    public string Email { get; set; }
}

public class ResetPasswordDto
{
    public string Code { get; set; }
    public string NewPassword { get; set; }
}