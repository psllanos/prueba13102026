import type { OptimizeResult } from '../../shared/types';

/**
 * Shows WHY a provider was chosen: each factor with its weight and contribution.
 *
 * The API returns a score breakdown precisely so the decision is auditable rather than a bare
 * number (docs/architecture.md § "Every decision must be explainable"). A UI that only showed
 * "score: 0.87" would throw that away.
 */
export function ScoreBreakdown({ result }: { result: OptimizeResult }) {
  return (
    <div className="breakdown">
      <h3>
        Selected: <strong>{result.selectedProvider.name}</strong> — {result.selectedProvider.etaMinutes} min ETA
      </h3>
      <p className="muted small">
        Case {result.caseId} · decided {new Date(result.decidedAt).toLocaleTimeString()}
      </p>

      <table className="table">
        <caption className="sr-only">Score factors for the selected provider</caption>
        <thead>
          <tr>
            <th scope="col">Factor</th>
            <th scope="col">Raw value</th>
            <th scope="col">Weight</th>
            <th scope="col">Contribution</th>
          </tr>
        </thead>
        <tbody>
          {result.breakdown.map((factor) => (
            <tr key={factor.factor}>
              <td data-label="Factor">{factor.factor}</td>
              <td data-label="Raw value">{factor.raw.toFixed(2)}</td>
              <td data-label="Weight">{(factor.weight * 100).toFixed(0)}%</td>
              <td data-label="Contribution">{factor.contribution.toFixed(4)}</td>
            </tr>
          ))}
          <tr className="row total">
            <td data-label="Factor">
              <strong>Total</strong>
            </td>
            <td data-label="Raw value" />
            <td data-label="Weight" />
            <td data-label="Contribution">
              <strong>{result.totalScore.toFixed(4)}</strong>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  );
}
