# Entregable 4 — Code Review del snippet defectuoso

> **Nota sobre el enunciado.** El PDF de la prueba anuncia *"Se entrega este snippet defectuoso (lo
> puedes pegar en el repositorio)"* y termina la sección en **"Snippet (C#):"** sin incluir el
> código. Verifiqué los streams de contenido, los XObjects y los archivos embebidos del PDF: el
> snippet **no está** en el documento. Para no dejar el entregable vacío, reconstruí un snippet que
> contiene **exactamente los 8 defectos** que el enunciado enumera, y el review va línea por línea
> contra cada uno. Si el snippet original aparece, este análisis se re-aplica 1:1 porque el método
> está derivado de la lista oficial de defectos.
>
> - Snippet revisado: [`defective-snippet.cs`](./defective-snippet.cs)
> - Defectos oficiales: `falta de asincronía real`, `ausencia de transacciones`,
>   `error de concurrencia`, `lógica incorrecta`, `no hay validaciones`, `no cumple SOLID`,
>   `no hay DTOs`, `no hay política de selección`.

---

## 1. Resumen ejecutivo

| # | Defecto exigido | Severidad | Tipo | Línea (snippet) |
|---|-----------------|-----------|------|-----------------|
| 1 | Falta de asincronía real | 🔴 Alta | Escalabilidad | `_connection.OpenAsync().Wait()`, `ExecuteReaderAsync().Result` |
| 2 | Ausencia de transacciones | 🔴 Alta | Integridad de datos | `insert.ExecuteNonQuery()` + `update.ExecuteNonQuery()` |
| 3 | Error de concurrencia | 🔴 Alta | Corrección | `_candidates` estático compartido sin sincronización |
| 4 | Lógica incorrecta | 🔴 Alta | Corrección | `score = Eta*0.5 + Distance*0.2 + Rating*0.3` |
| 5 | No hay validaciones | 🔴 Alta | Robustez / seguridad | cuerpo `dynamic`, sin comprobaciones |
| 6 | No cumple SOLID | 🟠 Media | Diseño | controller instancia `SqlConnection` y contiene todo |
| 7 | No hay DTOs | 🟠 Media | Contrato de API | `return best;` (entidad de persistencia) |
| 8 | No hay política de selección | 🟠 Media | Regla de negocio | pesos mágicos inline |
| — | **Adicional: credenciales en el código** | ⚫ Crítica | Seguridad | cadena de conexión con `Password=...` |

**Veredicto: rechazado.** El cambio no puede fusionarse. Los defectos 1–5 y la fuga de credenciales
son bloqueantes. Hay además **un hallazgo crítico que el enunciado no pedía y que es el más grave de
todos**: la contraseña de producción está *hardcodeada* y versionada (sección 9).

**El problema de fondo no son los ocho defectos aislados.** Son ocho síntomas de la misma causa: no
existe una **capa de dominio** ni un **caso de uso**; toda la lógica —acceso a datos, reglas de
selección, transacción y contrato HTTP— vive en un único método de controller. Por eso el review no
termina en "arreglá estas líneas" sino en la refactorización de la sección 10: si separás dominio,
aplicación y borde, seis de los ocho defectos se vuelven **irrepresentables**, no sólo corregidos.

---

## 2. Defecto 1 — Falta de asincronía real

```csharp
_connection.OpenAsync().Wait();
var reader = command.ExecuteReaderAsync().Result;
```

**Qué está mal.** `OpenAsync()` y `ExecuteReaderAsync()` devuelven `Task`; llamarles `.Wait()` y
`.Result` **bloquea el hilo** en lugar de liberarlo. El método `Optimize` ni siquiera es `async`, así
que la firma miente: promete trabajo asíncrono y hace I/O bloqueante.

**Impacto real.** En ASP.NET Core cada request ocupa un hilo del *thread pool* mientras espera a SQL.
Bajo concurrencia el pool se agota y el servidor deja de responder —el clásico *thread-pool
starvation*—. Es peor que ser sincrónico, porque el hilo queda bloqueado sin poder hacer nada útil.

**Riesgo adicional: *deadlock*.** En contextos con `SynchronizationContext` capturado, `.Result`
puede producir interbloqueo inmediato.

**Corrección.** Propagar `async` de punta a punta y usar `await`:

```csharp
public async Task<IActionResult> OptimizeAsync(
    [FromBody] OptimizeRequestDto request,
    CancellationToken cancellationToken)
{
    await _connection.OpenAsync(cancellationToken);
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    // ...
}
```

Regla de squad que esto justifica: **prohibido `.Result` / `.Wait()` / `.GetAwaiter().GetResult()`**;
lo hace cumplir un analizador (`CA2007`, `VSTHRD002`) en el CI. Ver
[`../squad-standards.md`](../squad-standards.md).

---

## 3. Defecto 2 — Ausencia de transacciones

```csharp
insert.ExecuteNonQuery();   // crea el caso
update.ExecuteNonQuery();   // marca al proveedor como ocupado
```

**Qué está mal.** Son dos escrituras independientes sobre el mismo estado de negocio, sin
`BeginTransaction`. Cada una se confirma por separado.

**Impacto real.** Si la segunda falla (timeout, corte de red, *deadlock* de SQL), queda un caso
creado apuntando a un proveedor que **sigue marcado como disponible**. El sistema cree que asignó
asistencia y el proveedor no lo sabe: se lo puede volver a asignar y se produce un **doble despacho**.
Es un fallo de integridad con consecuencia directa sobre el cliente final que está esperando una grúa.

**Corrección.** Una única transacción que envuelva ambas escrituras:

```csharp
await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
try
{
    await InsertCaseAsync(connection, transaction, caseToCreate, cancellationToken);
    await MarkProviderBusyAsync(connection, transaction, providerId, cancellationToken);
    await transaction.CommitAsync(cancellationToken);
}
catch
{
    await transaction.RollbackAsync(cancellationToken);
    throw;
}
```

El proyecto real ya lo resuelve de forma estructural: la garantía no depende de recordar el
`Commit`, sino de que el caso **no se puede representar sin su decisión**. Ver
`docs/architecture.md` § "Where the rules live" y el test de integración
`tests/ProviderOptimizer.IntegrationTests/DecisionRecordTests.cs`, que prueba que ninguna caso
sobrevive a un fallo del registro de decisión.

---

## 4. Defecto 3 — Error de concurrencia

```csharp
private static readonly List<Provider> _candidates = new List<Provider>();
// ...
_candidates.Clear();
while (reader.Read()) { _candidates.Add(...); }
```

**Qué está mal.** `_candidates` es `static`: **una sola instancia compartida por todos los usuarios y
todos los hilos** del proceso. Los controllers en ASP.NET Core se crean por request, pero un campo
`static` sobrevive a todos los requests. Se hace `Clear()` y luego se agrega en un `while`, sin
ningún lock.

**Impacto real.** Dos solicitudes concurrentes —lo normal, no el caso raro— intercalan `Clear()` y
`Add()`. Resultado: la lista queda con proveedores **mezclados de dos clientes distintos**, y se elige
*el mejor proveedor del otro pedido*. Peor: `List<T>` no es *thread-safe*, así que las escrituras
concurrentes pueden corromper su estructura interna y lanzar excepciones espurias. Es un **estado
global mutable**, el peor antipatrón posible en un servicio.

**Corrección.** El estado de una request vive en la request y no se comparte:

```csharp
public async Task<IActionResult> OptimizeAsync(...)
{
    var candidates = new List<Provider>();   // local, no estático
    // ...
}
```

Como regla de squad: **ningún `static` mutable**; el estado compartido, si existe, va en caché
distribuida (Redis) con clave por request. Ver `docs/architecture.md`.

---

## 5. Defecto 4 — Lógica incorrecta

```csharp
var score = (p.EtaMinutes * 0.5) + (p.DistanceKm * 0.2) + (p.Rating * 0.3);

if (score > bestScore) { bestScore = score; best = p; }
```

Hay **cuatro** errores de lógica encadenados:

1. **Dirección invertida del ETA y la distancia.** Menos ETA es mejor, pero el ETA se *suma*. Un
   proveedor que tarda 120 minutos suma 60 puntos y le gana a uno que tarda 5 minutos (2,5 puntos).
   **El algoritmo selecciona sistemáticamente al peor proveedor.**
2. **`bestScore` inicializado en `0.0`.** El ETA siempre es positivo, así que la primera comparación
   `score > 0.0` funciona por casualidad; pero si algún factor pudiera ser negativo o cero, **nunca
   se seleccionaría nada** y `best` quedaría `null` → `NullReferenceException` en `best.Id`.
3. **`IsAvailable` se lee y se ignora.** Se seleccionan proveedores ocupados. La disponibilidad se
   *consultó* en el `SELECT` y se *capturó* en el objeto, pero no participa del puntaje.
4. **Escalas incomparables.** Se suman magnitudes crudas con unidades distintas (minutos, kilómetros,
   estrellas 0–5). Sin **normalizar**, el factor con números más grandes domina arbitrariamente:
   la distancia (decenas de km) aplasta al rating (0–5).

**Corrección.** Normalizar cada factor al rango `[0,1]` dentro del conjunto de candidatos e invertir
la polaridad de los factores "menos es mejor". La implementación real está en
`src/ProviderOptimizer.Domain/ProviderScoringPolicy.cs`:

```csharp
private static double LowerIsBetter(double value, double lowest, double highest)
    => highest - lowest <= double.Epsilon ? 1d : (highest - value) / (highest - lowest);

private static double HigherIsBetter(double value, double lowest, double highest)
    => highest - lowest <= double.Epsilon ? 1d : (value - lowest) / (highest - lowest);
```

Nótese el caso borde `highest - lowest <= double.Epsilon`: cuando todos los candidatos empatan en un
factor, el factor aporta `1.0` a todos en lugar de dividir por cero. Ese caso borde es exactamente
lo que cubren los tests `tests/ProviderOptimizer.UnitTests/ProviderScoringPolicyTests.cs`.

---

## 6. Defecto 5 — No hay validaciones

```csharp
public Provider Optimize([FromBody] dynamic request)
{
    var serviceType = (string)request.serviceType;
    var latitude = (double)request.latitude;
```

**Qué está mal.** El cuerpo se recibe como `dynamic`, es decir **tipado en runtime y sin modelo**. No
hay ninguna validación:

- `serviceType` puede venir nulo, vacío o con un valor inexistente. Como se interpola directo en el
  SQL, además es **inyección SQL** (sección 9).
- `latitude` / `longitude` pueden venir fuera de rango, nulos o como texto → el *cast* lanza
  `RuntimeBinderException` o `NullReferenceException`, que **no controla** y termina en un 500.
- No hay rango de búsqueda, ni comprobación de que existan proveedores.

**Impacto.** Un cliente malformado o malicioso puede tumbar el endpoint con un 500. Los mensajes de
excepción filtran detalles internos del esquema. Y la inyección SQL es explotable.

**Corrección.** Contrato tipado + validación en el borde + **consulta parametrizada**:

```csharp
public sealed record OptimizeRequestDto(
    [Required, RegularExpression("^(tow-truck|locksmith|battery)$")] string ServiceType,
    [Required, Range(-90, 90)]   double Latitude,
    [Required, Range(-180, 180)] double Longitude,
    [Range(0.5, 100)]            double SearchRadiusKm = 10);

// consulta parametrizada: el valor nunca se concatena al texto SQL
await using var command = new SqlCommand(
    "SELECT ... FROM Providers WHERE ServiceType = @serviceType", connection);
command.Parameters.AddWithValue("@serviceType", serviceType);
```

En el proyecto real la validación es **más fuerte que un atributo**: los valores inválidos *no se
pueden construir*. `GeoPoint.Create` rechaza latitud fuera de `[-90, 90]` y `CustomerLocation`
rechaza coordenadas nulas, así que un objeto inválido **no existe en el sistema** — el
`NullReferenceException` tardío del snippet es irrepresentable. Ver
`src/ProviderOptimizer.Domain/ValueObjects.cs`.

---

## 7. Defecto 6 — No cumple SOLID

**Responsabilidad única (SRP) — la violación central.** Un solo método hace siete cosas: abrir la
conexión, consultar, mapear, puntuar, elegir, escribir dos tablas y serializar la respuesta. Tiene
siete razones para cambiar. Es también un *fat controller*, el antipatrón que la prueba busca.

**Inversión de dependencias (DIP).** El controller instancia `new SqlConnection(...)` directamente:
depende de una implementación concreta y de un *string* de conexión literal. No se puede testear sin
SQL Server real, no se puede sustituir por un doble, y el tipo de base está cableado.

**Abierto/cerrado (OCP).** Agregar un quinto factor de puntaje obliga a **editar** el método. La
regla de negocio está incrustada en el borde HTTP.

**Inyección de dependencias.** En ASP.NET Core el controller debería **recibir** sus colaboradores
por constructor, no construirlos.

**Corrección — la separación en capas.** El mismo caso, ya desacoplado:

```
Controller  →  OptimizeProviderHandler  →  IProviderRepository  (puerto)
                (Application)                IAssistanceCaseRepository
                                             IProviderScoringPolicy
                                             IClock                  (Infraestructura)
```

El controller queda reducido a traducir HTTP ↔ dominio:

```csharp
private static async Task<IResult> HandleAsync(
    OptimizeRequest? request,
    OptimizeProviderHandler handler,
    SearchOptions options,
    CancellationToken cancellationToken)
{
    if (request is null)
        return Results.BadRequest(new ProblemPayload("invalid_request", "A request body is required."));

    Domain.AssistanceRequest domainRequest;
    try { domainRequest = PayloadMapper.ToDomain(request, options); }
    catch (ArgumentException exception)
        return Results.BadRequest(new ProblemPayload("invalid_request", exception.Message));

    var outcome = await handler.HandleAsync(domainRequest, cancellationToken);
    if (!outcome.HasSelection)
        return Results.Conflict(new ProblemPayload("no_provider_available", "..."));

    return Results.Ok(PayloadMapper.ToPayload(outcome));
}
```

Esto es código real del repo: `src/ProviderOptimizer.Api/Endpoints/OptimizeEndpoint.cs`. El análisis
no es teórico, está aplicado en el entregable 2. La estructura completa de capas y la regla de
dependencia dirigida hacia adentro están en `docs/architecture.md`.

---

## 8. Defecto 7 — No hay DTOs

```csharp
return best;   // devuelve la entidad de persistencia Provider
```

**Qué está mal.** Se serializa directamente la **entidad de persistencia** como respuesta HTTP. La
entidad se usó para leer SQL Server y ahora se expone al cliente con la misma forma; el contrato de
la API *es* el esquema de la base.

**Impacto real.** Cualquier cambio de columna (renombrar, agregar campos internos) **rompe a todos
los clientes móviles**. Se filtran campos internos (`IsAvailable`, marcadores de negocio) y, si mañana
se agrega una columna sensible, sale sola al público. Además, sin DTO de entrada no hay validación
posible (defecto 5).

**Corrección.** DTOs explícitos y mapeo en el borde:

```csharp
public sealed record OptimizeRequestDto(string ServiceType, double Latitude, double Longitude, double? SearchRadiusKm);

public sealed record OptimizeResponseDto(
    ProviderDto       Provider,
    ScoreBreakdownDto Breakdown,
    Guid              CaseId,
    DateTimeOffset    CreatedAt,
    IReadOnlyList<ScoreFactorDto> DecisionFactors);
```

En el proyecto real: `src/ProviderOptimizer.Api/Contracts/Payloads.cs` define los DTOs y
`src/ProviderOptimizer.Api/Endpoints/PayloadMapper.cs` es el único lugar donde se traduce
dominio ↔ contrato. Los DTOs son `record` inmutables y el contrato de error es un `ProblemPayload`
uniforme. Documentado en `docs/architecture.md`.

---

## 9. Hallazgo adicional (no pedido) — Credenciales versionadas e inyección SQL

> Este defecto **no está en la lista del enunciado** y es el más grave del snippet. Un review que lo
> omita no estaría haciendo su trabajo.

```csharp
private readonly SqlConnection _connection = new SqlConnection(
    "Server=prod-as-isya.database.windows.net;Database=Assistance;User Id=sa;Password=As1sya2024!;");
```

**Crítico.** Hay una credencial de **producción, con usuario `sa` (sysadmin)**, en texto plano dentro
del código fuente. Está versionada en Git: queda en el historial **para siempre**, es visible para
cualquiera con acceso al repo y sigue viva aunque se borre la línea después (hay que **rotar** la
contraseña, no sólo borrarla). Es el hallazgo que en un review real frena el merge de inmediato y
dispara un incidente de seguridad.

**Inyección SQL.** El mismo snippet concatena entrada del usuario en el SQL:

```csharp
"... WHERE ServiceType = '" + serviceType + "'"
"INSERT INTO AssistanceCases (...) VALUES ('" + serviceType + "', " + best.Id + ", ...)"
```

`serviceType` viene del cuerpo `dynamic`: **el snippet es inyectable**. Un `'; DROP TABLE ... --` o un
`UNION SELECT` exfiltra datos.

**Corrección (OWASP A03:2021 Injection, A07:2021 Auth Failures).**

1. **Revocar y rotar** la credencial expuesta ya. Tratar el secreto como comprometido.
2. El usuario `sa` nunca debe usarse por la aplicación: usuario dedicado con privilegios mínimos.
3. Secretos **fuera del repositorio**: AWS Secrets Manager / variables de entorno. El repo real ya
   lo hace: `Program.cs` **falla al arrancar** si no hay cadena de conexión, en lugar de traer un
   default inseguro.
4. Consultas **siempre parametrizadas**.

---

## 10. Refactor propuesto

El refactor no es "corregir líneas": es **mover cada responsabilidad a la capa que le corresponde**.
Así, seis de los ocho defectos dejan de ser posibles.

```csharp
// ── 1. El borde HTTP: sólo traduce. No sabe SQL, no sabe puntuar. ────────────────────────────
[HttpPost("optimize")]
public async Task<IActionResult> OptimizeAsync(
    [FromBody] OptimizeRequestDto request,          // DTO tipado     → arregla defecto 7
    CancellationToken cancellationToken)            // cancelación real
{
    var domainRequest = PayloadMapper.ToDomain(request);   // valida → arregla defecto 5
    var outcome = await _handler.HandleAsync(domainRequest, cancellationToken);  // await → defecto 1

    return outcome.HasSelection
        ? Ok(PayloadMapper.ToPayload(outcome))                        // DTO de salida → defecto 7
        : Conflict(new ProblemPayload("no_provider_available", "..."));  // 409 explícito
}

// ── 2. Application: orquesta el caso de uso y posee el límite transaccional. ──────────────────
public sealed class OptimizeProviderHandler(
    IProviderRepository        providers,       // puerto  → arregla defecto 6 (DIP)
    IAssistanceCaseRepository  cases,
    IProviderScoringPolicy     scoring,         // política inyectada → arregla defecto 8
    IClock                     clock,
    ScoringWeights             weights)
{
    public async Task<OptimizeOutcome> HandleAsync(
        AssistanceRequest request, CancellationToken cancellationToken = default)
    {
        var origin = request.Location.Coordinates;

        var fetched = await providers
            .FindByServiceTypeAsync(request.ServiceType, cancellationToken)   // await real → defecto 1
            .ConfigureAwait(false);

        var candidates = ProviderEligibility.Candidates(       // estado LOCAL → arregla defecto 3
            fetched, request.ServiceType, origin, request.SearchRadiusKm);

        var selection = scoring.Select(candidates, origin, weights);   // política pura → defecto 4 y 8
        if (!selection.HasSelection) return OptimizeOutcome.NoProviderAvailable();

        var assistanceCase = AssistanceCase.Create(
            Guid.NewGuid(), request.ServiceType, request.Location,
            selection.Selected!.Id, selection.Breakdown!, clock.UtcNow);

        await cases.SaveAsync(assistanceCase, cancellationToken)   // atómico → arregla defecto 2
             .ConfigureAwait(false);

        return OptimizeOutcome.Selected(selection.Selected!, selection.Breakdown!, assistanceCase);
    }
}

// ── 3. Domain: puro, sin I/O, sin reloj, sin aleatoriedad. ───────────────────────────────────
public sealed class ProviderScoringPolicy : IProviderScoringPolicy
{
    public SelectionResult Select(
        IReadOnlyCollection<Provider> candidates, GeoPoint origin, ScoringWeights weights)
    {
        if (candidates.Count == 0) return SelectionResult.None();

        // Normaliza cada factor a [0,1] y respeta la polaridad → arregla defecto 4
        // (menos ETA es mejor, más rating es mejor, disponibilidad como flag)
        // Desempate determinista por Id → resultado estable y reproducible
        // ...
    }
}
```

### Mapa defecto → corrección → evidencia

| Defecto | Cómo se corrige | Evidencia en el repo |
|---|---|---|
| 1. Asincronía falsa | `async` de punta a punta + `await`; prohibido `.Result`/`.Wait()` | `OptimizeProviderHandler.HandleAsync` |
| 2. Sin transacción | unidad de trabajo atómica; garantía estructural del esquema | `DecisionRecordTests.cs` |
| 3. Concurrencia | estado local por request; cero `static` mutable | `ProviderEligibility.Candidates` |
| 4. Lógica incorrecta | normalización min-max + polaridad + desempate por Id | `ProviderScoringPolicy.cs` |
| 5. Sin validaciones | DTO tipado + validación en el borde + SQL parametrizado | `ValueObjects.cs`, `PayloadMapper.cs` |
| 6. Viola SOLID | puertos/adaptadores, DI por constructor, capas | `docs/architecture.md`, `Program.cs` |
| 7. Sin DTOs | DTOs de entrada/salida + mapper; dominio nunca sale a la API | `Contracts/Payloads.cs` |
| 8. Sin política | pesos inyectados y política de dominio aislada | `ScoringWeights.cs` |
| + credenciales e inyección SQL | rotar secreto, Secrets Manager, consultas parametrizadas | `Program.cs` (falla si falta config) |

---

## 11. Mejores prácticas propuestas

1. **Prohibir `.Result`/`.Wait()`/`GetAwaiter().GetResult()`** por analizador (`VSTHRD002`, `CA2007`)
   y romper el build si aparecen.
2. **Cero `static` mutable.** Estado compartido sólo en caché distribuida, con clave por request.
3. **Validar en el borde y hacer irrepresentables los estados inválidos.** Preferir constructores
   privados que rechazan datos malos antes que atributos que se pueden olvidar.
4. **Nunca concatenar SQL.** Consultas parametrizadas, sin excepción.
5. **Secretos sólo desde el entorno**, con *fail-fast* al arrancar si falta configuración.
6. **Ningún tipo de persistencia cruza el límite HTTP.** Mapeo explícito en ambos sentidos.
7. **Reglas de negocio en el dominio, no en el controller.** Son puras, testeables y desplegables sin
   infraestructura.
8. **Toda la lógica de decisión produce un `ScoreBreakdown`.** Un puntaje sin sus factores no es
   auditable; el sistema debe poder explicar *por qué* eligió a ese proveedor.
9. **Desempate determinista** para que el mismo request dé siempre el mismo resultado.
10. **Tests que prueben la garantía, no la implementación.** El suite debe poder mostrar que ninguna
    caso existe sin su decisión, y que un pedido sin proveedores devuelve 409 sin escribir nada.

---

## 12. Checklist del reviewer

- [x] Defecto 1 — falta de asincronía real → localizado y corregido
- [x] Defecto 2 — ausencia de transacciones → localizado y corregido
- [x] Defecto 3 — error de concurrencia → localizado y corregido
- [x] Defecto 4 — lógica incorrecta → localizado y corregido
- [x] Defecto 5 — no hay validaciones → localizado y corregido
- [x] Defecto 6 — no cumple SOLID → localizado y corregido
- [x] Defecto 7 — no hay DTOs → localizado y corregido
- [x] Defecto 8 — no hay política de selección → localizado y corregido
- [x] Vulnerabilidad adicional: credenciales en código + inyección SQL → reportada y corregida
- [x] Refactor propuesto con código real
- [x] Mejores prácticas documentadas
