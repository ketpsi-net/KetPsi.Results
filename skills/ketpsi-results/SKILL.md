---
name: ketpsi-results
description: How to use the KetPsi.Results .NET library (Result / Result<T>, ErrorResult, ErrorType, domain error factories, interpolated error messages with metadata and masking, HTTP/ProblemDetails mapping, System.Text.Json converters, PagedData). Use when writing or reviewing C# code that references the KetPsi.Results namespace or package, returns Result/Result<T>, or maps Results to HTTP responses or JSON.
---

# KetPsi.Results

A small .NET library (`net8.0`, `net9.0`, `net10.0`) for returning operation outcomes. A `Result` is always exactly one of three states:

| Status | Concrete types | Carries |
|---|---|---|
| `Success` | `SuccessResult`, `SuccessResult<T>` | `Value` (may be `null` if `T` is nullable) |
| `Failed` | `FailedResult`, `FailedResult<T>` | `Errors`: **one or more** `ErrorResult`s, never empty |
| `InProgress` | `InProgressResult`, `InProgressResult<T>` | `Value` = tracking value, not a finished entity |

```csharp
using KetPsi.Results;                 // core types and extensions
using KetPsi.Results.Serialization;   // only for JSON setup
```

## Rules to follow

1. **Use `Result` for expected outcomes; throw exceptions only for bugs.** "Not found", "invalid input", "downstream timed out" become `Result` failures. Never return `null` or a `(bool, string)` tuple for these.
2. **Prefer the domain factories** (`Result.Resource.NotFound(...)` etc.) over `Result.Failed(...)`. They fix the `ErrorType` and a stable error code. Use `Result.Failed(type, code, message)` only when no factory fits.
3. **Error codes are stable, lowercase and dot-separated** (`orders.already_shipped`, `integration.stripe.card_declined`). Clients branch on the code, never on the message.
4. **The library is a carrier, not a pipeline.** It has no `Map`, `Bind`, `Then` or `Tap`. Don't invent them on `Result`; write plain `if (r.IsFailed()) return r.AsFailed<T>();` code.
5. **Use the factories and implicit conversions to create results.** All constructors are internal.

## Creating results

```csharp
// Success / in-progress
Result.Success();                        // SuccessResult
Result.Success(order);                   // SuccessResult<Order>
Result.Success<string?>(null);           // allowed
Result.InProgress();                     // InProgressResult
Result.InProgress(jobId);                // InProgressResult<Guid>

// Implicit conversions (the idiomatic way inside a method returning Result<T>)
public Result<OrderDto> Get(...)
{
    if (order is null) return Result.Resource.NotFound();   // FailedResult -> Result<OrderDto>
    return order.ToDto();                                   // T -> SuccessResult<T>
}
```

> C# does not allow user-defined conversions from interfaces. When `T` is an interface, write `return Result.Success<IFoo>(foo);` instead of `return foo;`. Returning a `FailedResult` still converts implicitly.

### Generic failures

```csharp
Result.Failed("message");                                    // ErrorType.Failure, code "server.internal_error"
Result.Failed(ErrorType.Conflict, "orders.locked", "Order is locked");
Result.Failed(ErrorType.Conflict, "orders.locked", "Order is locked", new Dictionary<string, object?> { ["orderId"] = id });
Result.Failed(ErrorType.NotFound, "orders.missing", $"Order {id:@orderId} not found");   // interpolated -> metadata
Result.Failed(error);  Result.Failed(e1, e2);  Result.Failed(listOfErrors);              // must be non-empty
Result.Failed(err => err                                    // fluent builder; the lambda must return the builder
    .WithErrorType(ErrorType.Failure)
    .WithCode("integration.keycloak.invalid_grant")
    .WithMessage(description)
    .AddMetadata("status_code", 400));                      // AddMetadata overwrites duplicate keys
```

Every overload above also exists as `Result.Failed<T>(...)` and returns `FailedResult<T>`. You rarely need it, because `FailedResult` converts to any `Result<T>`.

`ErrorResult.Create(ErrorType type, string code, string message[, IReadOnlyDictionary<string, object?>? metadata])` builds a single error, for example to collect validation errors in a list. The metadata dictionary is copied.

### Domain factories

Each factory has three overloads: `()` with a default message, `(string message)`, and an interpolated message that captures metadata (see below).

| Factory | ErrorType | Code | HTTP |
|---|---|---|---|
| `Result.Authentication.Unauthorized` | Unauthorized | `auth.unauthorized` | 401 |
| `Result.Authentication.TokenExpired` | Unauthorized | `auth.token_expired` | 401 |
| `Result.Permissions.Forbidden` | Forbidden | `permissions.forbidden` | 403 |
| `Result.Resource.NotFound` | NotFound | `resource.not_found` | 404 |
| `Result.Resource.AlreadyExists` | Conflict | `resource.already_exists` | 409 |
| `Result.Business.RuleViolation` | RuleViolation | `business.rule_violation` | 422 |
| `Result.Business.QuotaExceeded` | QuotaExceeded | `business.quota_exceeded` | 429 |
| `Result.Business.StateConflict` | Conflict | `business.state_conflict` | 409 |
| `Result.Validation.InvalidInput` | Validation | `validation.invalid_input` | 400 |
| `Result.System.InternalError` | Failure | `server.internal_error` | 500 |
| `Result.System.ExecutionCanceled` | Canceled | `system.execution_canceled` | 499 |

### External services: `Result.Service`

Codes are `integration.{serviceName}`, or `integration.{serviceName}.{code}` when a sub-code is given (null or empty sub-codes are ignored).

```csharp
// Unavailable -> ErrorType.DependencyFailure (503): downstream down, timeout, 5xx
Result.Service.Unavailable("keycloak");
Result.Service.Unavailable("keycloak", "timeout");
Result.Service.Unavailable("keycloak", "timeout", "Keycloak did not respond");
Result.Service.Unavailable("keycloak", "timeout", $"No response after {elapsedMs}ms");

// Failure -> ErrorType.Failure (500): the service answered with an error payload
Result.Service.Failure("stripe", "card_declined", message);
Result.Service.Failure(b => b
    .WithServiceName("stripe")
    .WithCode("card_declined")
    .WithMessage($"Card {cardId:@card:mask:..-4} declined")   // merges captured metadata
    .AddMetadata("status_code", 402));                         // same builder shape for Unavailable(b => ...)
```

### Exceptions and combining

```csharp
catch (Exception ex) { return Result.FromException(ex); }
// OperationCanceledException -> ErrorType.Canceled, code "system.execution_canceled".
// Anything else -> the given type (default Failure), code "server.internal_error" unless you pass one.
Result.FromException(ex, ErrorType.DependencyFailure, "db.timeout");
Result.FromException<Order>(ex);

Result combined = Result.Combine(validateName, validateEmail, checkQuota);
// Any failed input (generic or non-generic) -> one FailedResult with all errors in order.
// Otherwise -> Success. InProgress inputs count as non-failed. Null elements throw ArgumentException.
```

`FromException` puts `exception.Message` in the error message. Don't return that message to external clients if it can expose internals.

## Interpolated messages: metadata, formatting, masking

Factories with a message parameter accept an interpolated string. The message is formatted with the invariant culture, and **each hole's raw value is also stored in `ErrorResult.Metadata`**. The key is the hole's expression text (`{orderId}` stores key `orderId`, `{order.Id}` stores key `order.Id`).

```text
{value[,alignment][:[@Key:][standardFormat][:mask[:range]][:trunc:N]]}
```

| Directive | Example | Effect on the message (metadata always keeps the raw value) |
|---|---|---|
| key override | `{id:@orderId}` | metadata key `orderId` |
| .NET format | `{amount:N2}`, `{at:yyyy-MM-dd HH:mm}` | normal formatting; colons inside the format are fine |
| full mask | `{token:mask}` | always `***`, whatever the length |
| range mask | `{t:mask:..4}`, `{t:mask:2..}`, `{t:mask:2..-2}` | `*` over `[start, end)`; negative indices count from the end |
| truncate | `{name:trunc:8}` | keeps the first 8 characters |
| alignment | `{id,8}`, `{id,-8:@k:mask}` | pads the final display text |

Directives apply in this order: format, then truncate, then mask, then alignment. `@Key` must come first.

```csharp
return Result.Authentication.Unauthorized(
    $"Invalid token {token:@token:mask:..4} for user {userId:@uid}");
// Message : "Invalid token ****c123xyz789 for user 42"
// Metadata: { "token": "sk-abc123xyz789", "uid": 42 }
```

Metadata holds **unmasked** secrets, so treat it as server-side data for logs. An interpolated string with no holes is just a plain string and captures nothing.

## Consuming results (extension methods)

```csharp
r.IsSuccessful(); r.IsFailed(); r.IsInProgress();   // or compare r.Status

if (r.TryGetResult(out var value)) { ... }
// true for Success AND InProgress. Check IsSuccessful() first if you need a finished value.
// value may be null when T is nullable.

IReadOnlyList<ErrorResult> errors = r.GetErrors();  // empty unless failed
ErrorResult? first = r.FirstError();
string text = r.GetErrorAsString();                 // "code:message" per line

var output = r.Match(
    onSuccess:    v => ...,
    onFailure:    errors => ...,
    onInProgress: v => ...);                        // optional; if omitted, InProgress goes to onSuccess

// Forward a failure to a different T (keeps every error). Throws InvalidOperationException if r is not failed.
if (tokenResult.IsFailed()) return tokenResult.AsFailed<OrderDto>();
FailedResult f = r.AsFailed();
```

These helpers work even when a `Result<T>` is stored in a variable of type `Result`. In your own code, do **not** check for failure with `r is FailedResult`: `FailedResult<T>` does not derive from `FailedResult`. Use `r.IsFailed()` and `r.GetErrors()`. Pattern matching on the exact concrete type (`r is FailedResult<Order> f`) is fine.

### ErrorResult and ErrorType

- `ErrorResult` exposes `Type`, `Code`, `Message` and `Metadata` (`IReadOnlyDictionary<string, object?>?`, `null` when empty). `ToString()` returns `"code: message"`.
- `ErrorType` values: `Failure`, `DependencyFailure`, `Validation`, `Unauthorized`, `Forbidden`, `NotFound`, `Conflict`, `RuleViolation`, `QuotaExceeded`, `Canceled`.
- **Equality:** results and errors compare by value, including the error lists and the metadata entries. `Result.Resource.NotFound("x") == Result.Resource.NotFound("x")` is `true`. The concrete types must match, though: a `FailedResult` never equals a `FailedResult<T>` with the same errors. Convert first, or compare `GetErrors()`.
- **Immutability:** error lists and metadata passed in are copied. `with { Errors = [] }` throws, and `Status` cannot be changed with `with`.

## HTTP mapping

```csharp
ErrorType.NotFound.ToStatusCode();          // 404
failed.ToStatusCode();                      // from the FIRST error; FailedResult or FailedResult<T>
failed.ToProblemDetails(instance: "/api/orders/99");
// Dictionary: type (= first error's code), title (= ErrorType name), status, detail (= first message),
//             instance, errors [{ Code, Message, Type, Metadata }]
```

Minimal API endpoint:

```csharp
app.MapGet("/orders/{id:guid}", async (Guid id, OrderService svc, CancellationToken ct) =>
{
    var result = await svc.GetOrderAsync(id, ct);
    return result.Match(
        dto => Results.Ok(dto),
        _ =>
        {
            var failed = result.AsFailed();
            return Results.Json(failed.ToProblemDetails(), statusCode: failed.ToStatusCode());
        },
        jobId => Results.Accepted(value: jobId));
});
```

`ToProblemDetails()` includes each error's **metadata**, which can hold unmasked values. For public APIs, either build the response from `Code`, `Message` and `Type`, or keep secrets out of message holes.

## JSON (System.Text.Json)

Results need converters, because their constructors are internal:

```csharp
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web).AddKetPsiResultConverters();   // safe to call twice
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.AddKetPsiResultConverters());

// Source-generated context: copy its options, then add the converters
var sg = new JsonSerializerOptions(AppJsonContext.Default.Options).AddKetPsiResultConverters();
```

Wire format (property names follow the naming policy):

```json
{ "status": "Success", "value": { "id": 1 } }
{ "status": "InProgress", "value": "job-123" }
{ "status": "Failed", "errors": [ { "type": "NotFound", "code": "resource.not_found", "message": "…", "metadata": { "orderId": 42 } } ] }
```

- Deserialize into `Result<T>` or `Result`. Concrete targets like `SuccessResult<T>` also work, but throw `JsonException` when the status doesn't match.
- Success and InProgress payloads of `Result<T>` require `value`. Failed payloads require a non-empty `errors` array. `status` is required.
- Metadata values come back as `JsonElement`, not their original CLR types.

## Paging

```csharp
var page = PagedData<Order>.Create(items, pageNumber: 2, pageSize: 20, totalCount: 95);
// page.Items, PageNumber, PageSize, TotalCount, HasNextPage, HasPreviousPage, TotalPages (= 5)
return Result.Success(page);   // PagedData is a payload, not a Result
```

`Create` throws `ArgumentOutOfRangeException` when `pageNumber < 1`, `pageSize < 1` or `totalCount < 0`, and `ArgumentNullException` for null items.

## Complete service example

```csharp
public async Task<Result<OrderDto>> PlaceOrderAsync(PlaceOrder cmd, CancellationToken ct)
{
    var errors = new List<ErrorResult>();
    if (string.IsNullOrWhiteSpace(cmd.Email))
        errors.Add(ErrorResult.Create(ErrorType.Validation, "validation.email", "Email is required"));
    if (cmd.Quantity <= 0)
        errors.Add(ErrorResult.Create(ErrorType.Validation, "validation.quantity", "Quantity must be positive"));
    if (errors.Count > 0)
        return Result.Failed(errors);

    var stock = await _inventory.ReserveAsync(cmd.Sku, cmd.Quantity, ct);   // Result<Reservation>
    if (stock.IsFailed())
        return stock.AsFailed<OrderDto>();

    if (await _orders.ExistsAsync(cmd.IdempotencyKey, ct))
        return Result.Resource.AlreadyExists($"Order {cmd.IdempotencyKey:@idempotencyKey} already placed");

    try
    {
        var order = await _orders.CreateAsync(cmd, ct);
        return order.ToDto();
    }
    catch (DbException ex)
    {
        return Result.FromException(ex, ErrorType.DependencyFailure, "orders.db_unavailable");
    }
}
```

## Testing results

```csharp
Assert.True(result.IsFailed());
Assert.Equal("resource.not_found", result.FirstError()!.Code);
Assert.Equal(42, result.FirstError()!.Metadata!["orderId"]);
Assert.Equal<Result<OrderDto>>(Result.Resource.NotFound("Order missing"), result);   // value equality; converts to FailedResult<OrderDto>
Assert.Equal(Result.Resource.NotFound("Order missing").Errors, result.GetErrors());   // or compare the error lists
var success = Assert.IsType<SuccessResult<OrderDto>>(result);
```
