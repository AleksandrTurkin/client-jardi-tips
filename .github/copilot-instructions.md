# Copilot Instructions

## Project Overview

- Framework: .NET 10 Blazor WebAssembly.
- UI library: MudBlazor.
- The client is an independent application and an independent Git repository.
- Keep the client buildable and deployable without the Web API solution.
- Treat the Web API strictly as an external HTTP service.

## Independence and Boundaries

- Never add project references to Web API, Application, Domain, Infrastructure, or other server-side projects.
- Do not reuse or copy server implementation types such as handlers, entities, repositories, `Result<T>`, or server error classes.
- Define client-owned request and response records as wire contracts inside their owning feature.
- API coupling must be limited to configured URLs, HTTP methods, headers, status codes, and JSON contracts.
- Keep transport concerns out of Blazor components.
- Prefer small changes in the layer that owns the behavior.

## Client Structure

Use the existing folders according to these responsibilities:

- `Application/Abstractions`: client-facing contracts such as `IApiClient`, token access, browser services, and persistence interfaces.
- `Application/Coordination`: cross-service workflows and Rx.NET coordinators when reactive composition is justified.
- `Features/<Feature>`: feature-owned routes, wire contracts, typed services, immutable state records, stores, and components.
- `Infrastructure/Api`: generic HTTP implementation, API configuration, JSON handling, authentication headers, and Problem Details handling.
- `Infrastructure/Browser`: isolated browser integrations.
- `Infrastructure/IndexedDb`: Dexie modules and typed C# repository implementations.
- `Shared/Components`: reusable presentation components without feature business logic.
- `Shared/Styling`: shared styling primitives and responsive design rules.

## HTTP

- Use one configured `HttpClient` for the Web API.
- Store the API base URL in public Blazor configuration under `Api:BaseUrl`.
- Never place credentials, access tokens, refresh tokens, or other secrets in WebAssembly configuration.
- Place generic HTTP, JSON, bearer authentication, cancellation, and error handling behind `IApiClient`.
- Require and propagate `CancellationToken` for every asynchronous API operation.
- Support successful JSON responses and `204 No Content` responses.
- Parse RFC Problem Details and Validation Problem Details into client-owned error contracts.
- Use thin typed feature services to own routes and domain-oriented API operations.
- Define feature request and response records locally; do not reference server DTO assemblies.
- Do not create generic CRUD repositories for behavior-oriented API endpoints.
- Do not expose `HttpClient` or `HttpResponseMessage` directly to components or feature stores.
- Never retry non-idempotent mutations automatically.
- Do not add retry or resilience policies unless their idempotency behavior is explicitly designed and verified.

## Authentication

- Attach bearer tokens in generic API infrastructure rather than in individual feature services or components.
- Access the current token through a client-owned abstraction so token persistence can change independently.
- Keep login, refresh, logout, and token lifecycle coordination in the authentication feature.
- Do not prescribe browser token persistence from generic HTTP infrastructure.
- Treat `401 Unauthorized` and `403 Forbidden` as structured API failures and preserve their Problem Details.

## Reactivity and State

- Store rendered UI state in small feature-level stores.
- Represent state with immutable records.
- Expose read-only `IObservable<TState>` streams; never expose mutable subjects.
- Keep subjects private to their owning store or coordinator.
- Use Rx.NET only for multi-source events, throttling, buffering, sequencing, cancellation, and synchronization triggers.
- Use normal `async`/`await` for individual HTTP and IndexedDB operations.
- Dispose component subscriptions when components are disposed.
- Marshal observable callbacks that update component state through `InvokeAsync`.
- Avoid unnecessary component rerenders and publish state only when meaningful state changes occur.

## Offline-First Behavior

- Use Dexie.js over IndexedDB for dynamic local data.
- Access Dexie only through isolated JavaScript modules and typed C# repository interfaces.
- Do not call Dexie or raw IndexedDB APIs directly from components or feature stores.
- Read IndexedDB first and refresh from the API in the background.
- Store entity data, pagination state, synchronization cursors, and pending mutations in IndexedDB.
- Use an outbox with idempotency keys for offline mutations.
- Treat server change cursors as authoritative.
- Treat real-time notifications only as hints to refresh authoritative data.
- Cache only the application shell and static assets in the service worker.
- Do not use the service worker as the dynamic application-data cache.

## Coordination Flow

Follow this dependency direction:

`Browser events and user actions -> Rx.NET coordinators -> feature services -> IndexedDB`

`API results -> coordinators/services -> feature stores -> Blazor components`

- Keep components focused on rendering state and forwarding user intent.
- Coordinators may synchronize API, browser events, local persistence, and stores.
- Services and repositories must not depend on Blazor components.

## UX and Performance

- Prefer immediate cached rendering and optimistic interactions.
- Use skeletons rather than blocking spinners when no cached content exists.
- Represent offline, pending-sync, syncing, and failed-sync states without blocking navigation.
- Design mobile-first and support device safe areas.
- Make interactive touch targets at least 44 by 44 CSS pixels.
- Animate primarily `transform` and `opacity`.
- Respect `prefers-reduced-motion`.
- Avoid unnecessary component rerenders.
- Virtualize long collections where appropriate.
- Keep network and persistence work asynchronous and cancellation-aware.

## Code Style

- Enable nullable reference types and preserve nullable correctness.
- Prefer records for immutable state and wire contracts.
- Prefer dependency injection and small interfaces over static service access.
- Use existing libraries before adding dependencies.
- Do not add comments unless they explain non-obvious behavior or match the surrounding style.
- Keep feature-specific behavior out of shared and generic infrastructure.
- Do not introduce Rx.NET, IndexedDB, or offline synchronization when a simple local `async` operation is sufficient.

## Validation

- Build `JardiTips.Client/JardiTips.Client.csproj` directly after changes.
- Verify the client remains independent and has no references to server projects.
- Test HTTP behavior against public API contracts rather than server implementation types.
- Verify cancellation, JSON serialization, no-content responses, Problem Details, and authentication headers when changing API infrastructure.
- Verify offline and synchronization changes with online, offline, interrupted, and resumed scenarios.
