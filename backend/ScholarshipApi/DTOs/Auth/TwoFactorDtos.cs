namespace ScholarshipApi.DTOs.Auth;

public static class TwoFactorMethods
{
    public const string Totp = "totp";
    public const string Email = "email";
}

public class TwoFactorChallengeResponse
{
    public bool TwoFactorRequired { get; set; } = true;
    public string ChallengeToken { get; set; } = null!;
    public string Method { get; set; } = null!;
    // Set for the email method so the sign-in page can say where the code went
    public string? MaskedEmail { get; set; }
}

public class TwoFactorResendRequest
{
    public string ChallengeToken { get; set; } = null!;
}

// Exactly one of Auth / Challenge is set
public class LoginResult
{
    public AuthResponse? Auth { get; init; }
    public TwoFactorChallengeResponse? Challenge { get; init; }
    public object Body => (object?)Auth ?? Challenge!;
}

public class TwoFactorLoginRequest
{
    public string ChallengeToken { get; set; } = null!;
    public string Code { get; set; } = null!;
}

public class TwoFactorStatusResponse
{
    public bool Enabled { get; set; }
    public string? Method { get; set; }
    public int RecoveryCodesRemaining { get; set; }
}

public class TwoFactorSetupResponse
{
    public string Secret { get; set; } = null!;
    public string OtpauthUri { get; set; } = null!;
}

public class TwoFactorEnableRequest
{
    public string Code { get; set; } = null!;
}

public class TwoFactorEnableResponse
{
    public string[] RecoveryCodes { get; set; } = [];
}

// Disabling 2FA and regenerating recovery codes both re-confirm the password
public class TwoFactorPasswordRequest
{
    public string Password { get; set; } = null!;
}
