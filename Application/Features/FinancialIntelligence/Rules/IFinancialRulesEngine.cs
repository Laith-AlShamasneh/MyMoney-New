namespace Application.Features.FinancialIntelligence.Rules;

/// <summary>
/// Evaluates a user's financial context against a set of rules and returns
/// candidates for insight and recommendation creation.
///
/// The interface is intentionally thin so an AI/ML implementation can replace
/// the current rule-based one without touching any other code.
/// </summary>
public interface IFinancialRulesEngine
{
    IReadOnlyList<InsightCandidate>        EvaluateInsights(FinancialRulesContext context);
    IReadOnlyList<RecommendationCandidate> EvaluateRecommendations(FinancialRulesContext context);
}
