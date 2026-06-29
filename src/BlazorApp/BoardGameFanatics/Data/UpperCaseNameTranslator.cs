namespace BoardGameFanatics.Data;

/// <summary>
/// Maps C# PascalCase enum member names to UPPERCASE PostgreSQL enum labels.
/// e.g. Pending → PENDING, Player → PLAYER
/// </summary>
internal sealed class UpperCaseNameTranslator : Npgsql.INpgsqlNameTranslator
{
    public static readonly UpperCaseNameTranslator Instance = new();
    public string TranslateTypeName(string clrName) => clrName;
    public string TranslateMemberName(string clrName) => clrName.ToUpperInvariant();
}
