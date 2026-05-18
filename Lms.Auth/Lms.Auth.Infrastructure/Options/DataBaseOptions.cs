namespace Lms.Auth.Infrastructure.Options;

public class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";
    public string AuthDb { get; set; } = null!;
}
