using System.ComponentModel.DataAnnotations;
using System.IO;

namespace MissilePredictor.AI.Config;

public class GoogleConfig
{
    public string CredentialsFilePath { get; init; }  = Path.Combine("data", "credentials.json");

    [Required]
    public required string SpreadsheetId { get; init; }

    public string Range { get; init; } = "Sheet1!A2:D";     
}