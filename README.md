# .NET Coding Challenge — Inventory Reservations

This is a .NET coding challenge. We assume you are comfortable with .NET Core, ASP.NET Core, and REST APIs.

You will extend a small **inventory reservation** service. The scaffold is intentionally incomplete and **contains subtle defects**. We are as interested in what you *find* in the provided code as in what you *write* yourself.

We would like you to spend no more than **3 hours** on this.

## Ground rules

1. Use .NET 8 and C#
2. The API consumes and returns JSON
3. You do not need to consider authentication, authorization, or HTTPS
4. No persistent storage — use the provided in-memory cache
5. You may add any NuGet package you wish, but be prepared to justify it
6. **Do not disable, weaken, or modify the provided acceptance tests.** You may add tests of your own.

## The domain

The service tracks **products** (each with some available stock) and lets clients **reserve** stock for a short window before either confirming the reservation (finalising a sale) or releasing it (returning the stock). Reservations that are neither confirmed nor released before their expiry time are automatically expired and the stock is returned to the product.

### Entities

**Product**
| Field | Type | Notes |
|--|--|--|
| `Id` | `Guid` | Server-assigned |
| `Name` | `string` | Required, max length 128 |
| `Stock` | `int` | Currently available units. Must never go negative. |

**Reservation**
| Field | Type | Notes |
|--|--|--|
| `Id` | `Guid` | Server-assigned |
| `ProductId` | `Guid` | Required, must refer to an existing product |
| `Quantity` | `int` | Required, must be `>= 1` |
| `CreatedAt` | `DateTime` | UTC, server-assigned |
| `ExpiresAt` | `DateTime` | UTC, server-assigned (`CreatedAt + TTL`) |
| `Status` | `string` | One of `Active`, `Confirmed`, `Released`, `Expired` |

### Endpoints you will need

```
POST   /api/products                       create a product
GET    /api/products/{id}                  get a product (includes current Stock)

POST   /api/reservations                   create a reservation
       body: { productId, quantity, ttlSeconds }
POST   /api/reservations/{id}/confirm      confirm (idempotent)
DELETE /api/reservations/{id}              release (idempotent, returns stock)
GET    /api/reservations/{id}              get a reservation (includes current Status)
```

Some of these are partially implemented in the scaffold. Complete the rest.

### Business rules

1. **No overselling.** The sum of `Active` + `Confirmed` reservation quantities for a product must never exceed the stock that existed when the product was created. Under concurrent load the service must honour this invariant exactly.
2. **Expiry.** A reservation in state `Active` whose `ExpiresAt` is in the past is considered `Expired` and its quantity is returned to the product's `Stock`. The transition happens automatically; clients must observe the correct state and stock on a `GET` issued after expiry has elapsed.
3. **Idempotency.**
   - Confirming an already-`Confirmed` reservation is a no-op and returns 200.
   - Releasing an already-`Released` (or `Expired`) reservation is a no-op and returns 204.
   - Confirming an `Expired` or `Released` reservation must fail with an appropriate error status.
4. **Stock arithmetic.** Releasing or expiring a reservation restores exactly the reserved quantity, no more and no less, regardless of ordering.

### HTTP status codes

Be deliberate — we will be reading the controller. At minimum:

| Situation | Expected |
|--|--|
| Resource created | `201 Created` with `Location` header |
| Resource retrieved | `200 OK` |
| Resource removed / idempotent release | `204 No Content` |
| Body fails schema validation | `400 Bad Request` |
| Unknown id | `404 Not Found` |
| Insufficient stock / illegal state transition | `409 Conflict` |

## Your task

1. **Read the scaffold carefully.** It compiles and a basic test suite passes. It is not, however, correct. The scaffold contains **five defects** — some functional, some architectural, some subtle. Find them. Fix them. Write one sentence per defect in your submission explaining what it was and why your fix is correct.

2. **Implement the missing functionality** needed to satisfy the spec above (confirm, release, expiry, anything else that is stubbed or absent).

3. **Make `ConcurrencyTests` pass.** The provided `tests/dotnet.challenge.tests/ConcurrencyTests.cs` is the acceptance test for rule 1 (no overselling). It is currently failing. You may not modify it.

4. Ensure the rest of the test suite still passes and add any tests of your own that you think are valuable.

## How we will assess your work

- **Correctness under load.** Does `ConcurrencyTests` pass reliably (not just once)?
- **Root-cause thinking.** Did you identify the actual causes of the defects, or patch symptoms? Your per-defect write-up is how we judge this.
- **Production quality.** Readable, SOLID, testable, correct HTTP semantics, disciplined use of `async`/cancellation, no obvious footguns.
- **Restraint.** The scaffold is small on purpose. Don't rewrite it; extend it.

When you're ready to share your work, push to a private repo and add your reviewer as a collaborator.
