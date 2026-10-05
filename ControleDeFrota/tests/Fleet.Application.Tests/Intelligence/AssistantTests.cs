using System.Text.Json;
using Fleet.Application.Assistant;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using FluentAssertions;
using FluentValidation;

namespace Fleet.Application.Tests.Intelligence;

/// <summary>Stand-in for Claude: calls the tools it is told to, records what it received, returns a scripted text.</summary>
public sealed class FakeLanguageModel(Func<IReadOnlyList<string>, string> answer, params (string Tool, object Input)[] calls) : IAssistantLanguageModel
{
    public List<string> ToolResults { get; } = [];
    public string? UserMessage { get; private set; }
    public string? SystemPrompt { get; private set; }
    public bool Throw { get; init; }

    public bool IsConfigured => true;

    public async Task<string?> AnswerAsync(string systemPrompt, string userMessage, IReadOnlyList<AssistantToolDefinition> tools,
        Func<string, JsonElement, CancellationToken, Task<string>> executeTool, CancellationToken ct)
    {
        if (Throw) throw new HttpRequestException("provider down");
        SystemPrompt = systemPrompt;
        UserMessage = userMessage;
        foreach (var (tool, input) in calls) ToolResults.Add(await executeTool(tool, JsonSerializer.SerializeToElement(input), ct));
        return answer(ToolResults);
    }
}

public class AssistantTests : AnalyticsTestBase
{
    private async Task<List<Guid>> FourVehiclesWithCostsAsync()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 4; i++) ids.Add((await Scenario.VehicleAsync(T, i)).Id);
        await ExpenseAsync(ids[0], 3_000m, T.Clock.Today.AddDays(-3));
        foreach (var id in ids.Skip(1)) await ExpenseAsync(id, 1_000m, T.Clock.Today.AddDays(-3));
        return ids;
    }

    [Fact]
    public async Task Calculated_MostExpensiveVehicle_UsesTheSystemNumbersAndExplains()
    {
        await ArrangeAsync();
        var ids = await FourVehiclesWithCostsAsync();

        var response = await Services.Assistant(T).AskAsync(new AssistantRequest { Question = "Qual veículo está gerando mais custo?" }, default);

        response.Mode.Should().Be(AssistantMode.Calculated);
        response.Answer.Should().StartWith("ABC1D23.").And.Contain("R$ 3.000,00").And.Contain("acima da média da frota");
        response.Reason.Should().Contain("outras despesas");
        response.Evidence.Should().Contain(e => e.Contains("Média da frota: R$ 1.500,00"));
        response.SuggestedAction.Should().NotBeNullOrWhiteSpace();
        response.FocusVehicleId.Should().Be(ids[0]);
        response.Sources.Should().Contain(s => s.Link.StartsWith("/relatorios"));
        response.InsufficientData.Should().BeFalse();
    }

    [Fact]
    public async Task Calculated_CostQuestionWithoutCostPermission_SaysSoAndRevealsNoMoney()
    {
        await ArrangeAsync();
        await FourVehiclesWithCostsAsync();

        As(SystemRoles.Operations);
        var response = await Services.Assistant(T).AskAsync(new AssistantRequest { Question = "Qual veículo está gerando mais custo?" }, default);

        response.InsufficientData.Should().BeTrue();
        response.Answer.Should().Contain("permissão");
        string.Join(" ", response.Evidence.Append(response.Answer)).Should().NotContain("R$");
    }

    [Fact]
    public async Task Calculated_FromVehiclePage_AnalyzesThatVehicleWithoutAPlate()
    {
        await ArrangeAsync();
        var ids = await FourVehiclesWithCostsAsync();

        var response = await Services.Assistant(T).AskAsync(
            new AssistantRequest { Question = "Analise este veículo", VehicleId = ids[0], Page = "vehicle" }, default);

        response.ToolsUsed.Should().Equal("get_vehicle_analysis");
        response.Answer.Should().StartWith("ABC1D23: custo total de R$ 3.000,00");
        response.FocusVehicleId.Should().Be(ids[0]);
    }

    [Fact]
    public async Task Calculated_PlateOfAnotherCompany_IsNotFound()
    {
        await ArrangeAsync();
        await FourVehiclesWithCostsAsync();
        var other = await T.AddCompanyAsync("11444777000161", "Outra");
        T.SignInAs(other, SystemRoles.Administrator);

        var response = await Services.Assistant(T).AskAsync(new AssistantRequest { Question = "Analise o ABC-1D23" }, default);

        response.InsufficientData.Should().BeTrue();
        response.Answer.Should().Contain("não encontrado");
        response.Evidence.Should().BeEmpty();
    }

    [Fact]
    public async Task Calculated_VehicleIdOfAnotherCompanyInTheContext_IsNotFoundNotAnError()
    {
        await ArrangeAsync();
        var ids = await FourVehiclesWithCostsAsync();
        var other = await T.AddCompanyAsync("11444777000161", "Outra");
        T.SignInAs(other, SystemRoles.Administrator);

        var response = await Services.Assistant(T).AskAsync(
            new AssistantRequest { Question = "Analise este veículo", VehicleId = ids[0], Page = "vehicle" }, default);

        response.InsufficientData.Should().BeTrue();
        response.Answer.Should().Contain("não encontrado");
    }

    [Fact]
    public async Task Calculated_UnknownQuestion_DoesNotGuess()
    {
        await ArrangeAsync();
        var response = await Services.Assistant(T).AskAsync(new AssistantRequest { Question = "Qual a previsão do tempo amanhã?" }, default);

        response.InsufficientData.Should().BeTrue();
        response.ToolsUsed.Should().BeEmpty();
        response.Suggestions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Ask_EmptyQuestion_Throws()
    {
        await ArrangeAsync();
        var act = () => Services.Assistant(T).AskAsync(new AssistantRequest { Question = " " }, default);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Ai_ExplainsToolFacts_AndSourcesComeFromTheTools()
    {
        await ArrangeAsync();
        await FourVehiclesWithCostsAsync();
        var model = new FakeLanguageModel(_ =>
            "Resposta: ABC1D23 é o veículo mais caro, com R$ 3.000,00.\nMotivo: outras despesas.\nEvidências:\n- Média da frota R$ 1.500,00\nSugestão: veja o financeiro.",
            ("get_fleet_ranking", new { metric = "total_cost" }));

        var response = await Services.Assistant(T, model).AskAsync(new AssistantRequest { Question = "Qual veículo é o mais caro?" }, default);

        response.Mode.Should().Be(AssistantMode.AiExplained);
        response.Answer.Should().Contain("ABC1D23");
        response.Evidence.Should().ContainSingle().Which.Should().Contain("1.500,00");
        response.Notice.Should().BeNull();
        response.Sources.Should().NotBeEmpty();
        model.SystemPrompt.Should().Contain("Use SOMENTE os dados devolvidos pelas ferramentas");
    }

    [Fact]
    public async Task Ai_InventedNumber_IsFlaggedToTheUser()
    {
        await ArrangeAsync();
        await FourVehiclesWithCostsAsync();
        var model = new FakeLanguageModel(_ => "Resposta: ABC1D23 custou R$ 99.999,00, 47% acima da média.",
            ("get_fleet_ranking", new { metric = "total_cost" }));

        var response = await Services.Assistant(T, model).AskAsync(new AssistantRequest { Question = "Qual veículo é o mais caro?" }, default);

        response.Notice.Should().Contain("não aparecem nos dados").And.Contain("99.999,00");
    }

    [Fact]
    public async Task Ai_ToolResultsSentToTheProvider_RespectTheUsersPermissions()
    {
        await ArrangeAsync();
        await FourVehiclesWithCostsAsync();
        var model = new FakeLanguageModel(_ => "Resposta: não tenho acesso a custos.",
            ("get_fleet_ranking", new { metric = "total_cost" }), ("get_fleet_costs", new { period = "this_month" }));

        As(SystemRoles.Operations);
        var response = await Services.Assistant(T, model).AskAsync(new AssistantRequest { Question = "Qual veículo é o mais caro?" }, default);

        model.ToolResults.Should().HaveCount(2).And.OnlyContain(r => r.Contains("unavailable"));
        string.Join("", model.ToolResults).Should().NotContain("3000").And.NotContain("1500");
        response.InsufficientData.Should().BeTrue();
    }

    [Fact]
    public async Task Ai_ProviderFailure_FallsBackToCalculatedAnswer()
    {
        await ArrangeAsync();
        await FourVehiclesWithCostsAsync();
        var model = new FakeLanguageModel(_ => "") { Throw = true };

        var response = await Services.Assistant(T, model).AskAsync(new AssistantRequest { Question = "Qual veículo está gerando mais custo?" }, default);

        response.Mode.Should().Be(AssistantMode.Calculated);
        response.Answer.Should().StartWith("ABC1D23.");
        response.Notice.Should().Contain("montada pelo sistema");
    }

    [Fact]
    public async Task Ai_FromVehiclePage_UserMessageTellsTheModelToUseCurrentVehicle()
    {
        await ArrangeAsync();
        var ids = await FourVehiclesWithCostsAsync();
        var model = new FakeLanguageModel(_ => "Resposta: ok", ("get_vehicle_analysis", new { plate = "current" }));

        var response = await Services.Assistant(T, model).AskAsync(
            new AssistantRequest { Question = "Por que o custo aumentou?", VehicleId = ids[0], Page = "vehicle" }, default);

        model.UserMessage.Should().Contain("plate='current'").And.EndWith("Por que o custo aumentou?");
        model.ToolResults.Single().Should().Contain("ABC1D23");
        response.FocusVehicleId.Should().Be(ids[0]);
    }
}

public class AssistantRouterAndGroundingTests
{
    [Theory]
    [InlineData("Qual veículo está mais caro?", AssistantIntent.RankingCost)]
    [InlineData("Quais veículos estão consumindo mais combustível?", AssistantIntent.RankingConsumption)]
    [InlineData("Qual foi o gasto com combustível este mês?", AssistantIntent.RankingFuelCost)]
    [InlineData("Existe algum consumo fora do padrão?", AssistantIntent.Fuel)]
    [InlineData("Quais veículos estão acima da média?", AssistantIntent.RankingAboveAverage)]
    [InlineData("Onde estamos gastando mais dinheiro?", AssistantIntent.FleetCosts)]
    [InlineData("Quanto gastamos este mês?", AssistantIntent.FleetCosts)]
    [InlineData("Qual veículo merece atenção?", AssistantIntent.Attention)]
    [InlineData("Quais veículos tiveram mais manutenções?", AssistantIntent.RankingWorkOrders)]
    [InlineData("Quais problemas estão se repetindo na manutenção?", AssistantIntent.Maintenance)]
    [InlineData("Qual veículo apresenta maior custo de manutenção?", AssistantIntent.RankingMaintenanceCost)]
    [InlineData("Quais pneus estão próximos de substituição?", AssistantIntent.Tires)]
    [InlineData("Quais veículos possuem maior custo com pneus?", AssistantIntent.RankingTireCost)]
    [InlineData("Estamos dentro do orçamento?", AssistantIntent.Budget)]
    [InlineData("Qual categoria aumentou mais?", AssistantIntent.CategoryIncrease)]
    [InlineData("Qual é o custo por km da frota?", AssistantIntent.RankingCostPerKm)]
    [InlineData("Como está a frota? Algum resumo?", AssistantIntent.Insights)]
    public void Route_SpecQuestions_MapToTheRightCalculation(string question, AssistantIntent expected) =>
        AssistantRouter.Route(question, new AssistantContext())!.Intent.Should().Be(expected);

    [Fact]
    public void Route_PlateInTheQuestion_AnalyzesThatVehicle()
    {
        var route = AssistantRouter.Route("Por que o custo do ABC-1D23 aumentou?", new AssistantContext())!;
        route.Intent.Should().Be(AssistantIntent.VehicleAnalysis);
        route.Calls.Single().Input.GetProperty("plate").GetString().Should().Be("ABC-1D23");
    }

    [Fact]
    public void Grounding_AcceptsRoundedNumbersFromTheFacts_AndIgnoresDatesAndSmallCounts()
    {
        const string facts = """{"value":18420.5,"vsFleetAveragePercent":30.7,"average":14060.25}""";
        GroundingCheck.UnsupportedNumbers("ABC-1D23: R$ 18.420,50 (31% acima; média R$ 14.060) em 05/10/2026, 3 veículos.", facts)
            .Should().BeEmpty();
    }

    [Fact]
    public void Grounding_FlagsNumbersAbsentFromTheFacts()
    {
        GroundingCheck.UnsupportedNumbers("Custo de R$ 50.000,00 e 12% acima.", """{"value":18420.5}""")
            .Should().BeEquivalentTo("50.000,00", "12");
    }
}
