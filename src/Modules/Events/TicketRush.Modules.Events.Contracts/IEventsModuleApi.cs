namespace TicketRush.Modules.Events.Contracts;

/// <summary>
/// API pública do módulo Events. Outros módulos só enxergam este contrato,
/// nunca as tabelas ou classes internas de Events.
/// </summary>
public interface IEventsModuleApi
{
    Task<EventSnapshot?> GetEventAsync(Guid eventId, CancellationToken cancellationToken);
}

public sealed record EventSnapshot(
    Guid Id,
    string Title,
    string Venue,
    string City,
    DateTime StartsAt,
    IReadOnlyList<SectionSnapshot> Sections)
{
    public SectionSnapshot? FindSection(string seatId) =>
        SeatCode.TryParse(seatId, out var sectionCode, out var row, out var number)
            && SeatCode.Format(sectionCode, row, number) == seatId // rejeita variações como "VIP-A01"
            ? Sections.FirstOrDefault(s => s.Code == sectionCode && row < s.Rows && number >= 1 && number <= s.SeatsPerRow)
            : null;
}

public sealed record SectionSnapshot(string Code, string Name, decimal Price, int Rows, int SeatsPerRow);

/// <summary>
/// Os assentos não são uma tabela: são derivados da seção. Formato: {SEÇÃO}-{FILEIRA}{NÚMERO}, ex.: VIP-A12.
/// </summary>
public static class SeatCode
{
    public const int MaxRows = 26;

    public static string Format(string sectionCode, int rowIndex, int number) =>
        $"{sectionCode}-{(char)('A' + rowIndex)}{number}";

    public static bool TryParse(string seatId, out string sectionCode, out int rowIndex, out int number)
    {
        sectionCode = string.Empty;
        rowIndex = -1;
        number = 0;

        var dash = seatId.LastIndexOf('-');
        if (dash <= 0 || seatId.Length < dash + 3)
        {
            return false;
        }

        var row = seatId[dash + 1];
        if (row is < 'A' or > 'Z' || !int.TryParse(seatId.AsSpan(dash + 2), out number))
        {
            return false;
        }

        sectionCode = seatId[..dash];
        rowIndex = row - 'A';
        return true;
    }
}
