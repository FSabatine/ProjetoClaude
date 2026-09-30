namespace Fleet.Domain.Documents;

public sealed record DocumentTypeDefinition(string Name, DocumentOwnerType OwnerType, bool HasExpiration, int AlertDaysBefore);

/// <summary>
/// Starting catalog created for every company (ADR-021). Companies edit, deactivate and add their own afterwards.
/// The CNH is NOT here: its validity lives in the driver record (Phase 1) — a single source for the same date.
/// </summary>
public static class DocumentTypeDefaults
{
    public static readonly IReadOnlyList<DocumentTypeDefinition> All =
    [
        new("CRLV / Licenciamento anual", DocumentOwnerType.Vehicle, true, 30),
        new("Seguro", DocumentOwnerType.Vehicle, true, 30),
        new("Aferição do tacógrafo (Inmetro)", DocumentOwnerType.Vehicle, true, 30),
        new("Inspeção veicular", DocumentOwnerType.Vehicle, true, 30),
        new("Outros documentos do veículo", DocumentOwnerType.Vehicle, false, 30),

        new("Exame toxicológico", DocumentOwnerType.Driver, true, 45),
        new("Exame médico (ASO)", DocumentOwnerType.Driver, true, 30),
        new("Curso MOPP", DocumentOwnerType.Driver, true, 60),
        new("Certificado de treinamento", DocumentOwnerType.Driver, true, 30),
        new("Outras qualificações", DocumentOwnerType.Driver, false, 30),

        new("CRLV / Licenciamento anual", DocumentOwnerType.Implement, true, 30),
        new("Inspeção (CIV/CIPP)", DocumentOwnerType.Implement, true, 30),
        new("Outros documentos do implemento", DocumentOwnerType.Implement, false, 30),

        new("RNTRC (ANTT)", DocumentOwnerType.Company, true, 60),
        new("Licença ambiental", DocumentOwnerType.Company, true, 60),
        new("Alvará de funcionamento", DocumentOwnerType.Company, true, 30),
    ];
}
