# provider-scoring Specification

## Purpose

Defines how a candidate set becomes exactly one optimal provider. Answers: "of these candidates,
which one, and why?"

## Requirements

### Requirement: Composite Score

The system MUST rank candidates by a composite score derived from estimated time of arrival,
distance, availability, and rating, using weights supplied by configuration.

#### Scenario: The best overall candidate wins

- GIVEN two available candidates
- AND one of them has a better ETA, distance, and rating
- WHEN the optimal provider is selected
- THEN the candidate with the higher composite score is selected

#### Scenario: A better rating does not override a much worse ETA

- GIVEN a near candidate with a lower rating
- AND a far candidate with a higher rating
- AND ETA is weighted more heavily than rating
- WHEN the optimal provider is selected
- THEN the near candidate is selected

### Requirement: Score Breakdown

The system MUST report the per-factor contribution for the selected provider, so the decision can
be audited.

#### Scenario: The breakdown accounts for the total

- GIVEN a selection made over three candidates
- WHEN the response is produced
- THEN it contains one contribution per scoring factor
- AND the contributions sum to the reported total score

### Requirement: Deterministic Tie-Breaking

The system MUST break equal composite scores deterministically.

#### Scenario: Identical scores produce the same winner

- GIVEN two candidates with identical composite scores
- WHEN the optimal provider is selected twice
- THEN the same provider is selected both times

### Requirement: Single Selection

The system MUST select exactly one provider for a non-empty candidate set.

#### Scenario: Exactly one provider is returned

- GIVEN five eligible candidates
- WHEN the optimization runs
- THEN exactly one provider is selected

### Requirement: Empty Candidate Set

The system MUST report that no provider is available when the candidate set is empty, and MUST NOT
select a provider.

#### Scenario: No candidates are available

- GIVEN an empty candidate set
- WHEN the optimization runs
- THEN no provider is selected
- AND the outcome reports that no provider is available

### Requirement: Weight Configuration Validation

The system MUST reject a scoring weight configuration that is missing, negative, or all-zero.

#### Scenario: All-zero weights are rejected

- GIVEN a configuration where every scoring weight is zero
- WHEN the service starts
- THEN startup fails with a configuration error
