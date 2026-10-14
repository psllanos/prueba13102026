# Optimizador de Proveedores — Prueba Técnica ASISYA

Solución del módulo **Asignación Inteligente de Proveedores** para un sistema de asistencias
vehiculares (grúa, cerrajería, batería).

Cuando un cliente pide asistencia, el sistema decide **qué proveedor debe atenderlo, y por qué**, y
después registra el caso.

---

## Qué hace

```
POST /optimize                → elige el proveedor óptimo, guarda el caso y su decisión
GET  /providers/available     → lista los proveedores habilitados para un servicio y una ubicación
```

La selección es **determinista** sobre ETA, distancia, disponibilidad y rating. Cada factor se
normaliza entre los candidatos y se pondera, y la respuesta incluye el **desglose completo del
puntaje** para que la decisión sea auditable y no un número suelto. Los empates se resuelven por id.

Cubre los pasos **3, 4 y 6** del flujo del enunciado: buscar proveedores, calcular el óptimo y
registrar el caso.

---

## Cómo levantarlo

### Requisitos

.NET 8 SDK · Docker Desktop · (opcional) Node 22+ para el frontend.

### Stack completo

```bash
docker compose up --build
```

Levanta Postgres (con healthcheck) y la API, aplicando las migraciones al arrancar. La API espera a
que Postgres acepte conexiones: aplica migraciones al inicio y no reintenta.

```bash
# Cargar proveedores de ejemplo. Esta forma funciona en TODAS las shells, incluido cmd.exe:
# copia el archivo DENTRO del contenedor y lo ejecuta con el flag -f de psql, así que no usa
# redirección, ni $(...), ni heredoc.
docker compose cp scripts/seed.sql postgres:/tmp/seed.sql
docker compose exec -T postgres psql -U postgres -d provider_optimizer -f /tmp/seed.sql

curl -s "http://localhost:8080/providers/available?serviceType=tow-truck&latitude=4.711&longitude=-74.0721&radiusKm=10"

curl -s -X POST http://localhost:8080/optimize \
  -H "Content-Type: application/json" \
  -d '{"serviceType":"tow-truck","location":{"latitude":4.711,"longitude":-74.0721},"searchRadiusKm":10}'
```

<details>
<summary><strong>Windows: qué constructos funcionan y cuáles fallan en silencio</strong></summary>

El problema con `cmd.exe` es más peligroso que un error de sintaxis. Este comando **parece**
funcionar pero no hace nada:

```cmd
docker compose exec -T postgres psql -U postgres -d provider_optimizer < scripts/seed.sql
```

`<` **no** es una redirección de entrada en `cmd.exe`. cmd le pasa `"<"` y `"scripts/seed.sql"` a
`psql` como argumentos literales, y `psql` responde:

```
psql: warning: extra command-line argument "<" ignored
psql: warning: extra command-line argument "scripts/seed.sql" ignored
```

...y sale **con éxito sin cargar nada**. No hay código de error que detectar; la siguiente llamada a
la API simplemente devuelve una lista vacía.

| Constructo | `cmd.exe` | PowerShell | bash / Git Bash | Linux |
|---|---|---|---|---|
| Redirección `< file` | ❌ ignorada en silencio | ❌ | ✅ | ✅ |
| Heredoc `<<'SQL'` | ❌ error de sintaxis | ❌ | ✅ | ✅ |
| Sustitución `$(...)` | ❌ | ✅ | ✅ | ✅ |
| `psql -f` dentro del contenedor | ✅ | ✅ | ✅ | ✅ |

La forma con `docker compose cp` + `-f` es la única que funciona en todos lados, y por eso es la
instrucción principal.

**Si estás en bash o Git Bash**, el equivalente corto también sirve:

```bash
docker compose exec -T postgres psql -U postgres -d provider_optimizer < scripts/seed.sql
```

</details>

### Sin Docker

```bash
dotnet build ProviderOptimizer.sln -c Release
dotnet run --project src/ProviderOptimizer.Api
```

Requiere `ConnectionStrings__OptimizerDb`. La aplicación **falla al arrancar** si no está, en lugar
de caer en un valor por defecto inseguro.

### Frontend

```bash
cd frontend
npm install
npm run dev     # http://localhost:5173, hace proxy de /api hacia la API en :8080
```

Login con JWT simulado (cualquier usuario y contraseña no vacíos), mapa con la ubicación del usuario
y el radio de búsqueda, tabla de proveedores con ETA, y el desglose del puntaje del proveedor
elegido.

---

## Verificación

Todo lo de abajo se ejecutó. Nada está afirmado desde la intención.

| Verificación | Comando | Resultado |
|---|---|---|
| Build (los warnings son errores) | `dotnet build ProviderOptimizer.sln -c Release -warnaserror` | 0 warnings, 0 errores |
| Formato | `dotnet format ProviderOptimizer.sln --verify-no-changes` | limpio |
| Tests unitarios | `dotnet test tests/ProviderOptimizer.UnitTests` | **21 en verde** |
| Tests de integración (Postgres real) | `dotnet test tests/ProviderOptimizer.IntegrationTests` | **19 en verde** |
| Frontend: tipos | `npm run typecheck` | limpio |
| Frontend: lint | `npm run lint` | limpio |
| Frontend: build | `npm run build` | compila |
| End-to-end | `docker compose up` + `POST /optimize` real | 200, caso persistido |

La suite de integración levanta la aplicación real (`WebApplicationFactory`) contra un **Postgres
real** (Testcontainers). No mockea la base: un test unitario con la base mockeada no prueba que el
SQL esté bien.

---

## Arquitectura

```
ProviderOptimizer.Api              minimal API, DTOs, composition root, migraciones al arrancar
        │
ProviderOptimizer.Infrastructure   EF Core, Npgsql, repositorios (implementan los puertos)
        │
ProviderOptimizer.Application      casos de uso y puertos
        │
ProviderOptimizer.Domain           entidades, value objects, reglas de scoring y elegibilidad
```

**La regla de dependencia apunta hacia adentro.** `Domain` no referencia nada; la regla la impone el
compilador vía referencias de proyecto, así que una violación rompe el build en lugar de depender de
que un reviewer se acuerde.

Tres decisiones sostienen el diseño:

| Decisión | Por qué |
|---|---|
| Elegibilidad y scoring son **código de dominio puro**, no SQL | Cada regla se prueba con tests unitarios, sin base de datos y sin reloj |
| El repositorio es un **pre-filtro grueso**, no la autoridad | El filtrado SQL no se puede testear; un test de contrato prueba que el pre-filtro coincide con la regla de dominio |
| Un caso **no se puede representar sin su decisión** (`breakdown jsonb NOT NULL`) | La garantía es estructural, no una verificación que alguien tiene que recordar |

---

## Estructura

```
src/            Proyectos de Clean Architecture (Domain, Application, Infrastructure, Api)
tests/          Suite unitaria y de integración
frontend/       Cliente React (TypeScript, Vite) con mapa y tabla de proveedores
scripts/        Datos de ejemplo para desarrollo
openspec/       Cambio guiado por especificación: proposal, design, specs, tasks
.github/        Pipeline de CI
```

---

## Limitaciones conocidas

Declaradas de frente, en lugar de dejar que las descubras.

- **La API no tiene autenticación ni autorización.** No debe exponerse públicamente hasta que la
  tenga. El JWT del frontend es un placeholder simulado que la API no verifica.
- **Sin rate limiting** en `POST /optimize`, que hace una consulta saliente por llamada.
- **Los proveedores son simulados** — el ETA y la distancia no son telemetría real.
- **El scoring corre en proceso sobre una sola instancia**; todavía no hay caché distribuida.
- **Sin logging estructurado, métricas ni trazas.**
- `dotnet format` corre en CI, pero los analizadores que fuerzan las reglas de async
  (`VSTHRD002`, `CA2007`) todavía no están habilitados.
