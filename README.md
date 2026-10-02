# KetPsi.Results

High-performance Result pattern library for .NET application and infrastructure layers.

## Design principles

- **Pure carrier** – Result only holds status + payload/errors. It does not own control-flow pipelines.
- **Explicit consumption** – use `IsSuccessful()` / `IsFailed()` / `Match(...)` at the call site.
- **Structured errors** – stable codes, ErrorType (HTTP-mappable), optional metadata.
- **Zero/low-allocation messaging** – custom interpolated-string handler with `@Key`, standard formats, range masks and prefix truncation.

## What was intentionally omitted

| Omitted | Reason |
|---------|--------|
| `IsSuccess` properties on the type | Prefer thin type + extension methods |
| `Map` / `Bind` / `Tap` / `Ensure` | Keep Result a transfer object, not a functional toolkit. Explicit `if` + `Match` is clearer for most teams. |
| Struct-based Result | Inheritance hierarchy + multiple shapes fits records far better; allocation cost is irrelevant in app/infra layers. |

## Handler syntax

```
[@Key:][standardFormat][:mask[:range]][:trunc:N]
```

- `mask` – full mask (`***`)
- `mask:2..` / `mask:..4` / `mask:2..-2` – range mask (Python-style indices)
- `trunc:8` – keep the first 8 characters (remove from the end)

## Quick start

```csharp
var result = await repo.GetAsync(id);

return result.Match(
    order => Results.Ok(order),
    errors => Results.Json(
        ((FailedResult)result).ToProblemDetails(),
        statusCode: ((FailedResult)result).ToStatusCode()));
```

```csharp
string token = "sk-abc123xyz789";
return Result.Authentication.Unauthorized(
    $"Invalid token {token:@token:mask:..4} for user {userId:@uid}");
// Message : "Invalid token ****bc123xyz789 for user 42"
// Metadata: { "token": "sk-abc123xyz789", "uid": 42 }
```
