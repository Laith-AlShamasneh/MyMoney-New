namespace Domain.Entities.Identity;

public class RefreshToken
{
    public long      TokenId          { get; set; }
    public long      UserId           { get; set; }

    // SHA-256 hash of the raw token sent to the client — the raw token is never stored
    public string    Token            { get; set; } = null!;
    public DateTime  ExpiresOnUtc     { get; set; }
    public DateTime  CreatedOnUtc     { get; set; }
    public string?   CreatedByIp      { get; set; }
    public DateTime? RevokedOnUtc     { get; set; }
    public string?   RevokedByIp      { get; set; }
    public string?   ReasonRevoked    { get; set; }

    // SHA-256 hash of the replacement token (populated on rotation)
    public string?   ReplacedByToken  { get; set; }

    public bool IsActive  => RevokedOnUtc is null && DateTime.UtcNow < ExpiresOnUtc;
    public bool IsRevoked => RevokedOnUtc is not null;
    public bool IsExpired => DateTime.UtcNow >= ExpiresOnUtc;
}
