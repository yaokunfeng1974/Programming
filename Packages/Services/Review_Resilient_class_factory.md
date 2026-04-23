# Detailed Review of ResilientHttpClientFactory and ResilientHttpClient

The current implementation has several design considerations that need addressing to ensure production readiness and avoid inefficiencies/resource exhaustion:

---

## 1. HttpClient Instantiation and Lifetime Management
**Issue**  
`ResilientHttpClient` creates a new `HttpClient` instance in `InitializeHttpClient()`, and `Reset()` disposes/recreates it. Frequent instantiation can lead to socket exhaustion under high load, as each `HttpClient` has its own connection pool.

**Recommendation**  
- Use `IHttpClientFactory` (from `Microsoft.Extensions.Http`) to manage connection pooling, lifetime, and DNS rotation.  
- Reuse instances for the same `baseUrl` (e.g., via named/typed clients).

---

## 2. Global ServicePointManager Settings
**Issue**  
`InitializeHttpClient()` modifies `ServicePointManager.DefaultConnectionLimit` and `ConnectionLeaseTimeout`, which are application-wide and may conflict with other components.

**Recommendation**  
- Avoid `ServicePointManager`; configure `HttpClientHandler` properties like `MaxConnectionsPerServer` directly.  
- Apply settings per `HttpClientHandler` instance instead of globally.

---

## 3. Policy Management
**Issue**  
- Policies (retry/circuit breaker) are recreated per client, causing overhead and potential state inconsistency.  
- Nested policies in `SendAsync()` may lead to unintended retry loops.

**Recommendation**  
- Cache policies based on configuration parameters (e.g., retry count/duration).  
- Simplify policy composition to avoid unnecessary nesting.

---

## 4. Thread Safety
**Issue**  
- `SetFaultedRetryParams()` modifies `_faultedRequestPolicy` after initialization, risking thread-safety issues.  
- `ServicePointManager` changes are not thread-safe.

**Recommendation**  
- Make `ResilientHttpClient` immutable after initialization.  
- Remove `SetFaultedRetryParams()` or protect it with locks.

---

## 5. Resource Leaks
**Issue**  
- `HttpResponseMessage` is not always disposed (e.g., after `IsConnectivityIssue` checks).  
- Improper disposal of `HttpClient`/`HttpClientHandler` may leave resources open.

**Recommendation**  
- Always dispose `HttpResponseMessage` using `using` blocks or `await using`.  
- Ensure all code paths release resources (e.g., `StreamContent`, `ByteArrayContent`).

---

## 6. Circuit Breaker Defaults
**Issue**  
Defaults (`exceptionsAllowedBeforeBreaking=5`, `durationOfBreakInSeconds=60`) may be too aggressive for high-throughput systems.

**Recommendation**  
- Use conservative defaults (e.g., `exceptionsAllowedBeforeBreaking=10`, `durationOfBreakInSeconds=30`).  
- Allow configuration via `HttpConfiguration`.

---

## 7. Connection Refresh Logic
**Issue**  
`ForceRefreshConnection` uses `ServicePointManager` to modify `ConnectionLeaseTimeout`, affecting all connections to the endpoint.

**Recommendation**  
Use `HttpClientHandler.PooledConnectionLifetime` instead of `ServicePointManager`.

---

## 8. Factory Design
**Issue**  
`ResilientHttpClientFactory` creates clients without reusing handlers or policies, leading to redundant allocations.

**Recommendation**  
- Use dependency injection to register `ResilientHttpClient` as a typed/named client via `IHttpClientFactory`.  
- Centralize policy management with a shared `PolicyFactory`.

---

## 9. Logging and Diagnostics
**Issue**  
- Logs lack critical details (e.g., policy states, request/response dumps).  
- Sensitive headers (e.g., API keys) are logged without redaction.

**Recommendation**  
- Add debug-level logging for policy transitions (e.g., circuit breaker state changes).  
- Redact sensitive headers using `ApimClientConfiguration.SensitiveLogHeaders`.

---

## 10. Unit Testability
**Issue**  
Tight coupling with `HttpClient` and static `ServicePointManager` hinders testing. Policies are not mockable.

**Recommendation**  
- Abstract `HttpClient` behind an interface (e.g., `IResilientHttpClient`).  
- Use dependency injection for policies.

---

## Critical Fixes Summary
- Replace `HttpClient` with `IHttpClientFactory` for lifetime management.  
- Remove `ServicePointManager` usage in favor of `HttpClientHandler` settings.  
- Cache Polly policies to avoid per-client overhead.  
- Ensure thread safety in configuration methods.  
- Dispose all `HttpResponseMessage` instances promptly.

---

## Conclusion
While the current implementation provides resilience features, it risks socket exhaustion, thread-safety issues, and suboptimal policy management. Refactoring to use `IHttpClientFactory`, caching policies, and eliminating global settings will make it production-ready.