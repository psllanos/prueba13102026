# assistance-case-lifecycle Specification

## Purpose

Defines how an assistance case and its provider decision are recorded for traceability. Answers:
"what happened, and can we prove it later?"

## Requirements

### Requirement: Case Persistence

The system MUST persist exactly one assistance case for every optimization that selects a provider,
recording the case identifier, the service type, the customer location, and the creation timestamp.

#### Scenario: A successful optimization creates a case

- GIVEN a valid request that selects a provider
- WHEN the optimization completes
- THEN exactly one case is persisted
- AND it carries a unique identifier and the requested service type

#### Scenario: A rejected request creates no case

- GIVEN a request whose customer location cannot be resolved
- WHEN the request is rejected
- THEN no case is persisted

### Requirement: Decision Record

The system MUST persist the selected provider and its score breakdown together with the case, so
the decision is reproducible later.

#### Scenario: The decision is stored with the case

- GIVEN a successful optimization
- WHEN the case is persisted
- THEN the selected provider reference is stored
- AND the stored score breakdown matches the one returned in the response

### Requirement: Atomic Persistence

The system MUST NOT leave a persisted case without its decision record.

#### Scenario: A persistence failure leaves nothing behind

- GIVEN a persistence failure while writing the decision record
- WHEN the transaction ends
- THEN no case exists for that request

### Requirement: No Assignment Without a Provider

The system MUST NOT persist an assignment for a request for which no provider was selected.

#### Scenario: An empty candidate set persists no assignment

- GIVEN an optimization that selects no provider
- WHEN it completes
- THEN no assignment is persisted
