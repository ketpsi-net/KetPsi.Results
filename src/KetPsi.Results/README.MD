# KetPsi.Results

[![NuGet](https://img.shields.io/nuget/v/KetPsi.Results.svg)](https://www.nuget.org/packages/KetPsi.Results/)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-purple.svg?style=flat-square)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)


**KetPsi.Results** is a lightweight .NET library for representing operation outcomes as **always-valid states**: success, structured failure, or in-progress.

It is meant for **application and infrastructure layers**—places where methods need to return a clear result to the caller without ambiguous `null`s, boolean flags on DTOs, or ad-hoc error strings. The type is a **carrier**: it holds status plus a value or errors. It does not own business pipelines (`Map` / `Bind` / railway composition stay in your code).

Typical uses:

- Application services returning `Result<T>` to APIs or other services  
- HTTP / gRPC clients mapping external failures into stable codes and types  
- Endpoints mapping `ErrorType` to HTTP status codes and Problem Details  
- Logging with safe messages while keeping raw diagnostic metadata  

**Target frameworks:** `net8.0`, `net9.0`

---

## What problem it solves

Without a shared outcome type, teams invent local conventions:

```csharp
Order? order = await repo.GetAsync(id);           // null = not found?
if (response.Success && response.Data != null)    // which fields matter?
return (false, "something went wrong");           // no code, no type, no metadata
```

Callers cannot tell from the type alone what is safe to read. KetPsi.Results makes the outcome itself the model:

| Status | Guarantee |
|--------|-----------|
| **Success** | Payload is the result (`Result<T>.Value`). No error list. |
| **Failed** | One or more `ErrorResult`s. No success payload to misuse. |
| **InProgress** | Tracking / intermediate value only—not a finished entity. |

A failure is never “just a string.” Each error has:

- **ErrorType** — category for policy and HTTP (Validation, NotFound, Unauthorized, DependencyFailure, …)  
- **Code** — stable machine id (`resource.not_found`, `integration.keycloak.invalid_grant`)  
- **Message** — human-readable text  
- **Metadata** — optional raw values for diagnostics (ids, status codes, tokens)

Exceptions are still appropriate for **bugs and contract violations**. Expected business and integration outcomes use `Result`.

---

## Quick start

```csharp
public async Task<Result<OrderDto>> GetOrderAsync(Guid id, CancellationToken ct)
{
    var order = await _repo.GetAsync(id, ct);
    if (order is null)
        return Result.Resource.NotFound($"Order {id:@orderId} was not found");

    if (!order.IsActive)
        return Result.Business.RuleViolation("Order is inactive");

    return order.ToDto();   // implicit Result<OrderDto>
}
```

At the API edge:

```csharp
var result = await _orders.GetOrderAsync(id, ct);

return result.Match(
    dto => Results.Ok(dto),
    errors =>
    {
        var failed = result.AsFailed();
        return Results.Json(
            failed.ToProblemDetails(),
            statusCode: failed.ToStatusCode());
    });
```

---

## Creating results

### Success and in-progress

```csharp
Result.Success();
Result.Success(order);
Result.Success<string?>(null);          // null reference payloads are allowed

Result.InProgress();
Result.InProgress(trackingId);
```

### Failure

```csharp
// Generic internal failure
Result.Failed("something went wrong");
Result.Failed<Order>("something went wrong");

// Explicit type + code
Result.Failed(ErrorType.NotFound, "res.missing", "Item not found");
Result.Failed<Order>(ErrorType.NotFound, "res.missing", "Item not found");

// From ErrorResult instance(s)
Result.Failed(error);
Result.Failed(error1, error2);
Result.Failed(errorList);

// Interpolated message → message + auto metadata
int id = 42;
Result.Failed(ErrorType.NotFound, "res.missing", $"Item {id:@itemId} not found");
```

### From exception / combine

```csharp
Result.FromException(ex);
Result.FromException(ex, ErrorType.DependencyFailure, "db.timeout");
Result.FromException<Order>(ex);

// OperationCanceledException → ErrorType.Canceled by default

Result.Combine(validate, checkQuota, ensureUnique);
// Any failure → single FailedResult with all errors; else Success
```

---

## Domain factories

Prefer factories over raw `Failed(...)` when a standard code and type already exist.

| Group | Examples | ErrorType (typical) |
|-------|----------|---------------------|
| **Authentication** | `Unauthorized`, `TokenExpired` | Unauthorized |
| **Permissions** | `Forbidden` | Forbidden |
| **Resource** | `NotFound`, `AlreadyExists` | NotFound, Conflict |
| **Business** | `RuleViolation`, `QuotaExceeded`, `StateConflict` | RuleViolation, QuotaExceeded, Conflict |
| **Validation** | `InvalidInput` | Validation |
| **System** | `InternalError`, `ExecutionCanceled` | Failure, Canceled |
| **Service** | `Unavailable`, `Failure` | DependencyFailure, Failure |

```csharp
Result.Authentication.Unauthorized();
Result.Authentication.Unauthorized("Token expired");
Result.Authentication.Unauthorized($"User {userId:@uid} is not authenticated");

Result.Resource.NotFound($"Order {id:@orderId} was not found");
Result.Validation.InvalidInput("Email is required");
Result.System.InternalError("Unexpected empty payload");
```

### Integrations — `Result.Service`

Builds codes `integration.{serviceName}` or `integration.{serviceName}.{code}`:

```csharp
Result.Service.Unavailable("keycloak");
Result.Service.Unavailable("keycloak", "timeout");
Result.Service.Unavailable("keycloak", "timeout", "Keycloak did not respond");

Result.Service.Failure("keycloak", "invalid_grant", description);
Result.Service.Failure("keycloak", errorCode, $"Provider error: {detail:@detail}");
```

| Method | Use when | ErrorType |
|--------|----------|-----------|
| `Unavailable` | Downstream down / timeout / 5xx availability | DependencyFailure → 503 |
| `Failure` | Service responded with an error payload | Failure → 500 (or map higher up) |

---

## Inspecting results

```csharp
if (result.IsSuccessful()) { }
if (result.IsFailed()) { }
if (result.IsInProgress()) { }

if (result.TryGetResult(out var value))
{
    // Success or InProgress value
}

var errors = result.GetErrors();     // empty list if not failed
var first  = result.FirstError();
var text   = result.GetErrorAsString();
```

### Match

```csharp
return result.Match(
    onSuccess: order => Results.Ok(order),
    onFailure: errors => Results.BadRequest(errors),
    onInProgress: id => Results.Accepted(id));   // optional; defaults to onSuccess path for value
```

### Forwarding a failure to another `T`

Non-generic `FailedResult` converts implicitly to any `Result<T>`. For an existing typed failure:

```csharp
if (token.IsFailed())
    return token.AsFailed<OrderDto>();   // misuse on success → throws (bug)
```

---

## Error message handler

Factories that take a message support an interpolated-string handler. It builds the display string and **automatically stores raw values in metadata**.

**Syntax** (colon-separated):

```text
[@Key:][standardFormat][:mask[:range]][:trunc:N]
```

| Feature | Example | Result |
|---------|---------|--------|
| Auto key | `{orderId}` | Metadata key `orderId` |
| Override key | `{orderId:@id}` | Metadata key `id` |
| Format | `{amount:N2}`, `{dt:yyyy-MM-dd HH:mm}` | Standard .NET formatting (colons preserved) |
| Full mask | `{token:mask}` | Message shows `***`; metadata keeps raw token |
| Range mask | `{token:mask:..4}` | Mask indices `[0, 4)` |
| | `{token:mask:2..}` | Mask from index 2 to end |
| | `{token:mask:2..-2}` | Mask from 2 to second-to-last |
| Truncate | `{name:trunc:8}` | Keep first 8 characters |
| | `{name:trunc:0}` | Empty string |

```csharp
string token = "sk-abc123xyz789";
return Result.Authentication.Unauthorized(
    $"Invalid token {token:@token:mask:..4} for user {userId:@uid}");

// Message : "Invalid token ****c123xyz789 for user 42"  (example masking)
// Metadata: { "token": "sk-abc123xyz789", "uid": 42 }
```

Use metadata server-side for structured logs; return or display the message where redaction matters.

---

## HTTP mapping

```csharp
failed.ToStatusCode();                 // from FirstError.Type
ErrorType.NotFound.ToStatusCode();     // 404

failed.ToProblemDetails();
failed.ToProblemDetails(instance: "/api/orders/99");
```

Example Problem Details shape:

```json
{
  "type": "resource.not_found",
  "title": "NotFound",
  "status": 404,
  "detail": "Order 99 was not found",
  "instance": "/api/orders/99",
  "errors": [ { "code", "message", "type", "metadata" } ]
}
```

| ErrorType | Status |
|-----------|--------|
| Failure | 500 |
| DependencyFailure | 503 |
| Validation | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |
| RuleViolation | 422 |
| QuotaExceeded | 429 |
| Canceled | 499 |

---

## JSON serialization (System.Text.Json)

`Result` instances are not plain POCOs (internal construction). Register converters once:

```csharp
using KetPsi.Results.Serialization;

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
}.AddKetPsiResultConverters();

string json = JsonSerializer.Serialize(result, options);
Result<Order> restored = JsonSerializer.Deserialize<Result<Order>>(json, options)!;
```

Wire format:

```json
{ "status": "Success", "value": { "id": 1 } }
{ "status": "InProgress", "value": "job-123" }
{
  "status": "Failed",
  "errors": [
    {
      "type": "NotFound",
      "code": "resource.not_found",
      "message": "Order was not found",
      "metadata": { "orderId": "…" }
    }
  ]
}
```

Nested `value` uses `JsonTypeInfo` from the options resolver when source generation is configured.

ASP.NET Core:

```csharp
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.AddKetPsiResultConverters());
```

---

## Paged data

`PagedData<T>` is a data envelope, not a Result. Return it inside `Result<PagedData<T>>`:

```csharp
var page = PagedData<Order>.Create(items, pageNumber: 2, pageSize: 20, totalCount: 95);
return Result.Success(page);

// page.HasNextPage, page.HasPreviousPage
```

---

## End-to-end examples

### External identity provider

```csharp
if (response.IsSuccessStatusCode)
{
    if (responseData is null)
        return Result.System.InternalError("Empty token payload from identity provider");
    return Result.Success(responseData);
}

if (responseData?.Error == "invalid_grant")
    return Result.Authentication.Unauthorized(
        responseData.ErrorDescription ?? "Invalid grant");

if (responseData is not null)
    return Result.Service.Failure(
        "keycloak",
        responseData.Error,
        responseData.ErrorDescription ?? "Identity provider error");

return Result.Service.Unavailable("keycloak", ((int)response.StatusCode).ToString());
```

### Aggregating validation errors

```csharp
var errors = new List<ErrorResult>();
if (string.IsNullOrWhiteSpace(email))
    errors.Add(ErrorResult.Create(ErrorType.Validation, "validation.email", "Email is required"));
if (age < 18)
    errors.Add(ErrorResult.Create(ErrorType.Validation, "validation.age", "Must be 18+"));

if (errors.Count > 0)
    return Result.Failed(errors);

return Result.Success();
```

### Chaining application steps

```csharp
var tokenResult = await _idp.GetTokenAsync(ct);
if (tokenResult.IsFailed())
    return tokenResult.AsFailed<OrderDto>();

if (!tokenResult.TryGetResult(out var token) || token is null)
    return Result.System.InternalError("Token outcome had no value");

var order = await _orders.FindAsync(id, ct);
if (order is null)
    return Result.Resource.NotFound($"Order {id:@orderId} was not found");

return order.ToDto();
```

---

## Design choices (short)

| Choice | Why |
|--------|-----|
| Always-valid states | Callers never interpret mixed or partial objects |
| Extensions for checks / HTTP | Keep the core type thin |
| No Map/Bind/Tap on Result | Composition belongs in application code |
| Handler + metadata | One expression for safe message and full diagnostics |
| Domain + Service factories | Shared codes and types across services |
| Throw on API misuse | e.g. `AsFailed` on success is a bug, not a domain error |

---

## Installation

```xml
<ItemGroup>
  <ProjectReference Include="..\KetPsi.Results\src\KetPsi.Results.csproj" />
</ItemGroup>
```

```bash
dotnet add package KetPsi.Results
```

```bash
dotnet test
```
