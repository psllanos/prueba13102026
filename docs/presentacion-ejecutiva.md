# Presentación Ejecutiva
## Asignación Inteligente de Proveedores — ASISYA

**Prueba técnica — Líder Técnico de Desarrollo**

---

## Slide 1 — El problema de negocio

Un cliente varado llama. El sistema tiene que decidir **a qué proveedor mandar**, en segundos, y
tiene que poder **explicar por qué**.

Hoy esa decisión no existe de forma reproducible: no hay módulo, no hay criterio auditable, no hay
garantía de que el caso quede registrado junto con su decisión.

**Lo que construimos:** un microservicio que responde una sola pregunta —
*dado un pedido de asistencia, ¿qué proveedor debe atenderlo, y por qué?*

| | |
|---|---|
| **Alcance** | Paso 3 (buscar proveedores), 4 (calcular el óptimo) y 6 (registrar el caso) |
| **Fuera de alcance** |  |

---

## Slide 2 — Qué se entrega

Los 4 entregables obligatorios, más los 2 opcionales. **Todo verificado, nada declarado de intención.**

| # | Entregable | Estado | Evidencia |
|---|---|---|---|
| 1 | Diseño arquitectónico (C4, escalabilidad, seguridad) | ✅ | `docs/architecture.md`, `docs/aws-deployment.md` |
| 2 | `ProviderOptimizerService` en .NET 8 | ✅ | `src/` — 4 proyectos, 40 tests |
| 3 | Estándares técnicos del squad | ✅ | `docs/squad-standards.md` |
| 4 | Code review de código defectuoso | ✅ | `docs/code-review/review.md` |
| 5 | CI/CD en GitHub Actions | ✅ | `.github/workflows/ci.yml` |
| 6 | Frontend React  | ✅ | `frontend/` |

---

## Slide 3 — La arquitectura en una imagen

```
        Cliente / App móvil
                │
        ┌───────▼────────┐
        │  CDN + WAF     │   CloudFront · AWS WAF · OWASP
        └───────┬────────┘
                │
        ┌───────▼────────┐
        │  API Gateway   │   JWT / OAuth2 · rate limiting · load balancing
        └───────┬────────┘
                │
   ┌────────────┼────────────┬───────────────┐
   │            │            │               │
┌──▼──────┐ ┌───▼───────┐ ┌──▼────────┐ ┌────▼─────┐
│Assistance│ │ Provider  │ │Notifications│ │ Location │
│ Request │ │ Optimizer │ │             │ │          │
└──┬──────┘ └───┬───────┘ └──┬────────┘ └────┬─────┘
   │            │            │               │
   └────────────┴────────────┴───────────────┘
                │
   ┌────────────┴─────────────┬──────────────┐
   │                          │              │
┌──▼──────┐            ┌──────▼───┐   ┌──────▼──────┐
│RDS Postgres│          │  Redis   │   │  SQS/SNS    │
│(por servicio)│        │ (cache)  │   │ (eventos)   │
└─────────┘            └──────────┘   └─────────────┘
                │
        ┌───────▼────────┐
        │ Observabilidad │   logs · métricas · trazas
        └────────────────┘
```

**Decisión de alcance:** implementamos **un** microservicio (el crítico), no el sistema completo.
Los demás quedan diseñados con responsabilidades, entidades, eventos y contratos definidos.

---

## Slide 4 — Por qué esta arquitectura

| Decisión | Razón de negocio |
|---|---|
| **Reglas de negocio en el dominio, puras** | Se pueden probar sin base de datos ni reloj → el criterio de selección es **verificable**, no opinable |
| **La persistencia es un pre-filtro, no la autoridad** | La regla vive en un solo lugar. Repartirla entre SQL y C# significa dos lugares donde cambiarla y uno donde olvidarla |
| **El caso no se puede representar sin su decisión** | Garantía **estructural** (`jsonb NOT NULL`), no una verificación que alguien tiene que recordar |
| **Toda decisión devuelve su desglose de puntaje** | Ante un reclamo, el sistema puede responder *por qué eligió ese proveedor*. Un puntaje sin factores no es auditable |
| **Desempate determinista por id** | El mismo pedido da siempre el mismo resultado → reproducible ante auditoría |
| **La regla de dependencia la impone el compilador** | No depende de que un reviewer se acuerde |

---

## Slide 5 — El algoritmo de selección

**El problema con la solución ingenua:** sumar magnitudes crudas. Los minutos, los kilómetros y las
estrellas (0–5) tienen escalas incomparables — la distancia aplasta al rating.

**La solución:**

1. **Normalizar** cada factor a `[0,1]` dentro del conjunto de candidatos (min-max).
2. **Respetar la polaridad:** menos ETA es mejor; más rating es mejor.
3. **Ponderar** según configuración (`eta 0.5 · distance 0.2 · availability 0.1 · rating 0.2`).
4. **Desempatar por id** para estabilidad.

**Caso borde que importa:** si todos los candidatos empatan en un factor, ese factor aporta `1.0` a
todos en lugar de dividir por cero.

**Decisión real ejecutada contra la API:**
```
ENTRADA: tow-truck en (4.711, -74.0721), radio 10 km
CANDIDATOS: 3 → 2 elegibles (uno estaba ocupado)

ELEGIDO: Gruas Zona Norte — 8 min ETA   (vs. 12 min del competidor)
score: 0.6
  eta          8.0 min    peso 50%   → +0.50
  distance     2.50 km    peso 20%   → +0.00
  availability 1          peso 10%   → +0.10
  rating       4.1        peso 20%   → +0.00
```
El proveedor con mejor ETA gana. El que estaba ocupado **nunca fue candidato**.

---

## Slide 6 — Calidad: la evidencia

| Verificación | Resultado |
|---|---|
| Build con `-warnaserror` | **0 advertencias**, 0 errores |
| `dotnet format --verify-no-changes` | limpio |
| Tests unitarios | **21 en verde** |
| Tests de integración contra **Postgres real** | **19 en verde** |
| Frontend: typecheck + lint + build | limpio |
| End-to-end | `docker compose up` + `POST /optimize` → 200, caso persistido |

**Los tests de integración no mockean la base.** Levantan la aplicación real
(`WebApplicationFactory`) contra un contenedor Postgres (Testcontainers). Un test unitario con la
base mockeada no prueba que el SQL esté bien.

**Prueba clave:** ningún caso sobrevive a un fallo al escribir su registro de decisión.

---

## Slide 7 — Code review: hallazgo principal

El enunciado pide detectar **8 defectos**. Los encontramos los 8:

asincronía falsa · sin transacción · error de concurrencia · lógica incorrecta · sin validaciones ·
viola SOLID · sin DTOs · sin política de selección

**Pero el hallazgo más grave no estaba en la lista:**

```csharp
private readonly SqlConnection _connection = new SqlConnection(
    "Server=prod-as-isya.database.windows.net;...User Id=sa;Password=As1sya2024!;");
```

Credencial de **producción, usuario `sa` (sysadmin), en texto plano, versionada en Git**. Queda en el
historial para siempre. **Hay que rotarla, no sólo borrarla.**

**Y el snippet es inyectable:** concatena entrada del usuario en el SQL.

> Un review que sólo entrega los 8 defectos pedidos y omite esto no está haciendo su trabajo.

**El problema de fondo:** los 8 defectos no son ocho bugs. Son ocho síntomas de que **no existe una
capa de dominio**. El refactor los vuelve *irrepresentables* en lugar de corregirlos.

---

## Slide 8 — Estándares del squad

No son buenas prácticas genéricas: **codifican lo que ya hace el código de este repositorio**, y
cada regla dice cómo se hace cumplir.

| Regla | Cómo se hace cumplir | Estado |
|---|---|---|
| Dirección de dependencias entre capas | Referencias de proyecto (**error de compilación**) | activo |
| Tipos anulables | Compilador | activo |
| Formato | `dotnet format --verify-no-changes` | activo |
| `docker build` válido | Job en CI | activo |
| Sin `.Result` / `.Wait()` | Analizador `VSTHRD002` | **a agregar** |
| Sin secretos en el repo | Escaneo de secretos + push protection | **a agregar** |
| Pipeline rojo bloquea el merge | Branch protection | **a agregar** |

**Cada "a agregar" está registrado como deuda técnica con dueño y disparador de pago.** Una regla
que no se hace cumplir ni se registra es una aspiración — y las aspiraciones no sobreviven a un
deadline.

---

## Slide 9 — Qué falla hoy (deuda declarada)

En vez de que el evaluador lo descubra, lo declaramos:

| # | Limitación | Riesgo | Cuándo se paga |
|---|---|---|---|
| 1 | **La API no tiene autenticación** | No puede exponerse públicamente | Antes del primer despliegue público |
| 2 | Sin **rate limiting** en `POST /optimize` | Abuso degrada el servicio | Antes de tráfico real |
| 3 | Proveedores **simulados**, sin telemetría real | ETA y distancia no son reales | Al integrar un proveedor |
| 4 | Scoring **en proceso, instancia única** | No escala horizontal | Cuando el volumen lo justifique |
| 5 | Sin **caché distribuida** (Redis diseñado, no cableado) | Carga crece con el tráfico | Ídem 4 |
| 6 | Analizadores de async no habilitados | Las reglas se cumplen por disciplina, no por herramienta | Próxima iteración de tooling |
| 7 | Sin logging estructurado / métricas / trazas | Diagnóstico a mano | Antes de producción |

**El JWT del frontend es simulado y la API no lo verifica.** Es deuda declarada, no una
funcionalidad de seguridad.

---

## Slide 10 — Riesgos y cómo los mitigamos

| Riesgo | Mitigación aplicada |
|---|---|
| Elegir al proveedor equivocado y no poder justificarlo | Desglose de puntaje en cada decisión, persistido en `jsonb` |
| Caso registrado sin proveedor asignado (doble despacho) | Garantía estructural: el caso no existe sin su decisión |
| Dos pedidos concurrentes que se pisan | Cero estado mutable compartido; estado local por request |
| Credenciales filtradas | Sin secretos en el repo; *fail-fast* si falta configuración; escaneo **a agregar** |
| El sistema se cae bajo carga | Diseño de autoscaling, SQS, caché y rate limiting en la arquitectura objetivo |

---

## Slide 11 — Cómo se trabaja en este squad

| Práctica | Regla |
|---|---|
| **Branch strategy** | Trunk-based, ramas cortas (`feat/`, `fix/`, `chore/`, `docs/`). `main` siempre verde |
| **Tamaño de PR** | Objetivo **≤ 400 líneas**. Más grande → chained PRs. Un PR de 900 líneas no se revisa, se aprueba |
| **Code review** | Mínimo 1 aprobación. **Nadie mergea su propio PR.** Bloquea: seguridad, integridad de datos, concurrencia, tests. No bloquea: estilo, problemas preexistentes |
| **Definición de Done** | Checklist concreto: código + tests (unitario y al menos uno de integración si toca persistencia), CI verde, docs, sin secretos, review aprobado, observable |
| **Deuda técnica** | Toda atajo se registra con dueño y disparador de pago. Presupuesto acotado (≤ 15 ítems), triage mensual |

**Regla que más protege al reviewer:** un problema preexistente **nunca** se arregla en silencio
dentro de un PR no relacionado.

---

## Slide 12 — Próximos 30 días

| Prioridad | Acción | Resultado esperado |
|---|---|---|
| 1 | **Autenticación y autorización** en la API | Deja de ser inexpuesta públicamente |
| 2 | **Escaneo de secretos** + branch protection en CI | Una clase entera de incidentes deja de ser posible |
| 3 | **Analizadores** (`CA2007`, `VSTHRD002`) + `.editorconfig` | Las reglas de async pasan de disciplina a compilador |
| 4 | **Observabilidad**: logging estructurado, métricas, trazas | Un fallo se diagnostica sin debugger |
| 5 | **Rate limiting** en `POST /optimize` | Protege el servicio de abuso |
| 6 | Integración de **telemetría real** de proveedores | ETA y distancia dejan de ser simulados |

**Criterio de éxito:** un ingeniero que entra al repo hoy puede levantar el stack, correr la suite y
entender las reglas del squad sin que nadie se lo explique en una reunión.

---

## Slide 13 — Cómo verificar lo que digo

Nada de esto es una declaración de intención. Todo se ejecutó:

```bash
dotnet build ProviderOptimizer.sln -c Release -warnaserror   # 0 warnings
dotnet format ProviderOptimizer.sln --verify-no-changes      # limpio
dotnet test ProviderOptimizer.sln                            # 21 + 19 en verde
docker compose up --build                                    # API + Postgres
cd frontend && npm run typecheck && npm run lint && npm run build
```

**Empezá por el README.** Ahí están los 6 entregables mapeados, y la sección de limitaciones
conocidas — declaradas antes de que las encuentres.

---

## Slide 14 — Lo que no está resuelto

Cuatro cosas que un líder técnico debe decir en voz alta:

1. **El snippet del Entregable 4 no estaba en el PDF.** Lo reconstruí con exactamente los 8 defectos
   que el enunciado enumera. Si aparece el original, el análisis se re-aplica 1:1.
2. **La autenticación no está resuelta.** El login del frontend es simulado y la API no verifica el
   token. Es la primera deuda a pagar.
3. **El sistema completo está diseñado, no implementado.** Se pidió un microservicio; entregamos ese
   uno con calidad de producción.
4. **Las decisiones de arquitectura están documentadas con su razón.** Donde elegimos un camino,
   está escrito por qué — y qué se sacrificó.
