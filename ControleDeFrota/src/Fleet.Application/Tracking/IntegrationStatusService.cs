using Fleet.Application.Assistant;
using Fleet.Application.Common;
using Fleet.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tracking;

public enum IntegrationState
{
    Active,
    Available,
    NotConfigured,
    Planned,
}

public sealed record IntegrationStatus(string Key, string Name, string Description, IntegrationState State, string Detail, string? Link);

/// <summary>
/// What the product talks to (spec §19, ADR-051): one place to see each integration point, whether it is in use and how
/// it is configured. Every integration is behind a port (IFileStorage, IAssistantLanguageModel, the tracking ingestion
/// API, future notification channels as automation actions), so a new provider is an adapter, not a module rewrite.
/// </summary>
public sealed class IntegrationStatusService(IFleetDbContext db, IClock clock, IAssistantLanguageModel assistantModel)
{
    public async Task<IReadOnlyList<IntegrationStatus>> GetAsync(CancellationToken ct)
    {
        var providers = await db.TrackingProviders.CountAsync(p => p.IsActive, ct);
        var devices = await db.TrackingDevices.CountAsync(d => d.IsActive, ct);
        var since = clock.UtcNow.AddMinutes(-TrackingRules.OnlineMinutes);
        var online = await db.TrackingDeviceLastPositions.CountAsync(p => p.RecordedAt >= since, ct);

        return
        [
            new("tracking", "Rastreamento (GPS)", "Rastreadores enviam posições para a API de recebimento, autenticados pela chave de cada aparelho.",
                devices > 0 ? IntegrationState.Active : IntegrationState.Available,
                devices > 0 ? $"{providers} provedor(es), {devices} rastreador(es) ativo(s), {online} transmitindo agora." : "Nenhum rastreador cadastrado.",
                "/configuracoes/rastreadores"),
            new("assistant-ai", "Assistente com IA (Claude)", "Redige as respostas do assistente a partir dos números calculados pelo sistema.",
                assistantModel.IsConfigured ? IntegrationState.Active : IntegrationState.NotConfigured,
                assistantModel.IsConfigured ? "Ligado." : "Desligado: o assistente responde com textos montados pelo sistema. Ligar exige configuração no servidor.",
                null),
            new("notifications", "Notificações", "Avisos das regras de automação.",
                IntegrationState.Active, "Somente dentro do aplicativo (sino). E-mail e WhatsApp: previstos como novas ações das regras.", "/configuracoes/automacoes"),
            new("files", "Armazenamento de arquivos", "Fotos, documentos e comprovantes anexados.",
                IntegrationState.Active, "Disco do servidor. Pode ser trocado por armazenamento em nuvem sem mudar os módulos.", null),
            new("exports", "Exportação", "Relatórios em CSV, Excel e PDF gerados no navegador.", IntegrationState.Active, "Disponível nos relatórios.", "/relatorios"),
            new("erp", "ERP, cartão combustível, oficinas e consultas externas", "Integrações com sistemas de terceiros.",
                IntegrationState.Planned, "Não implementadas: entram como adaptadores quando houver um fornecedor definido.", null),
        ];
    }
}
