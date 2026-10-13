# provider-discovery Specification

## Purpose

Defines which assistance providers are eligible candidates for a customer request, before any
ranking occurs. Answers: "who could serve this request?"

## Requirements

### Requirement: Service Type Eligibility

The system MUST return only providers that offer the requested assistance service type.

#### Scenario: Only matching service types are returned

- GIVEN providers offering tow-truck, locksmith, and battery
- WHEN a request asks for locksmith
- THEN the result contains only providers offering locksmith
- AND no tow-truck or battery provider appears

#### Scenario: No provider offers the requested service type

- GIVEN no provider offers battery
- WHEN a request asks for battery
- THEN the result is an empty candidate set
- AND the outcome reports that no provider is available

### Requirement: Geographic Eligibility

The system MUST return only providers located within the configured search radius of the customer
location.

#### Scenario: Provider inside the radius

- GIVEN a customer at a known location and a search radius of 10 km
- WHEN a provider is 4 km away
- THEN that provider is a candidate

#### Scenario: Provider outside the radius

- GIVEN a customer at a known location and a search radius of 10 km
- WHEN a provider is 25 km away
- THEN that provider is not a candidate

### Requirement: Availability Filtering

The system MUST exclude providers that are marked unavailable.

#### Scenario: Unavailable provider is excluded

- GIVEN two otherwise eligible providers
- AND one of them is marked unavailable
- WHEN candidates are computed
- THEN only the available provider is returned

### Requirement: Customer Location Validation

The system MUST reject a request whose customer location cannot be resolved, and MUST NOT return
any candidate for it.

#### Scenario: Unresolvable location is rejected

- GIVEN a request whose customer location cannot be resolved
- WHEN candidates are computed
- THEN the request is rejected
- AND no candidate set is returned

### Requirement: Deterministic Candidate Ordering

The system SHOULD return candidates in a stable order, so identical repeated requests yield
identical candidate lists.

#### Scenario: Repeated request yields identical order

- GIVEN the same eligible provider set
- WHEN the same request is submitted twice
- THEN both responses list the candidates in the same order
