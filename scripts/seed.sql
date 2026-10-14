-- Datos de ejemplo para desarrollo: proveedores repartidos por toda Bogotá.
--
-- Esto NO es una migración. Está deliberadamente fuera de
-- src/ProviderOptimizer.Infrastructure/Migrations porque los datos de demo nunca deben formar parte
-- del historial de esquema que aplica producción.
--
-- Ejecutar después de `docker compose up`:
--   docker compose cp scripts/seed.sql postgres:/tmp/seed.sql
--   docker compose exec -T postgres psql -U postgres -d provider_optimizer -f /tmp/seed.sql
--
-- COBERTURA GEOGRÁFICA: los proveedores se reparten por las cinco localidades principales de
-- Bogotá (Centro, Norte, Sur, Occidente y Occidente lejano) en lugar de amontonarse en el centro.
-- Sembrar sólo alrededor de (4.711, -74.0721) hacía que cualquier usuario que no viviera ahí viera
-- "no hay proveedores disponibles" con el radio por defecto, y pareciera un defecto del sistema
-- cuando en realidad el motor había decidido bien.
--
-- Los ids son FIJOS y legibles a propósito, no `gen_random_uuid()`. Con un uuid aleatorio, correr
-- este archivo dos veces inserta duplicados: `ON CONFLICT (id)` nunca se activa porque cada
-- ejecución genera ids distintos. Con ids fijos el script es idempotente.
--
-- Se usa DO UPDATE en lugar de DO NOTHING para que re-ejecutar el archivo también refresque los
-- valores si se edita este script durante el desarrollo.

INSERT INTO providers (id, name, service_type, latitude, longitude, eta_minutes, rating, is_available) VALUES
  -- Centro (Chapinero / Centro Internacional)
  ('11111111-1111-4111-8111-111111111111', 'Grúas El Rápido',      'tow-truck', 4.6480, -74.0620, 12, 4.6, true),
  ('22222222-2222-4222-8222-222222222222', 'Grúas Zona Centro',    'tow-truck', 4.6560, -74.0580,  8, 4.1, true),
  ('33333333-3333-4333-8333-333333333333', 'Grúas Ocupadas SA',    'tow-truck', 4.6400, -74.0680,  5, 5.0, false),
  ('44444444-4444-4444-8444-444444444444', 'Cerrajería 24/7',      'locksmith', 4.6530, -74.0660, 15, 4.8, true),
  ('55555555-5555-4555-8555-555555555555', 'Baterías Express',     'battery',   4.6620, -74.0540, 20, 4.3, true),

  -- Norte (Usaquén / Suba)
  ('10000000-0000-4000-8000-000000000001', 'Grúas Norte 24h',      'tow-truck', 4.7010, -74.0310, 14, 4.5, true),
  ('10000000-0000-4000-8000-000000000002', 'Cerrajería Suba',      'locksmith', 4.7410, -74.0840, 18, 4.2, true),
  ('10000000-0000-4000-8000-000000000003', 'Baterías Usaquén',     'battery',   4.7100, -74.0350, 22, 4.4, true),

  -- Sur (Kennedy / Bosa / Ciudad Bolívar) - cubre la zona donde falla el radio por defecto
  ('20000000-0000-4000-8000-000000000001', 'Grúas Kennedy',        'tow-truck', 4.6280, -74.1490, 10, 4.7, true),
  ('20000000-0000-4000-8000-000000000002', 'Grúas Bosa Sur',       'tow-truck', 4.6080, -74.1770,  9, 4.3, true),
  ('20000000-0000-4000-8000-000000000003', 'Cerrajería Kennedy',   'locksmith', 4.6240, -74.1520, 13, 4.6, true),
  ('20000000-0000-4000-8000-000000000004', 'Batería Móvil Sur',    'battery',   4.6180, -74.1600, 16, 3.9, true),
  ('20000000-0000-4000-8000-000000000005', 'Grúas Ciudad Bolívar', 'tow-truck', 4.5720, -74.1420, 21, 4.0, true),

  -- Occidente (Fontibón / Engativá)
  ('30000000-0000-4000-8000-000000000001', 'Grúas Fontibón',       'tow-truck', 4.6730, -74.1420, 11, 4.4, true),
  ('30000000-0000-4000-8000-000000000002', 'Cerrajería Engativá',  'locksmith', 4.7010, -74.1120, 17, 4.5, true),
  ('30000000-0000-4000-8000-000000000003', 'Baterías Fontibón',    'battery',   4.6690, -74.1480, 19, 4.1, true),

  -- Occidente lejano (Madrid / Funza) - para probar el límite del radio
  ('40000000-0000-4000-8000-000000000001', 'Grúas Sabana Occidente', 'tow-truck', 4.7300, -74.2600, 35, 4.2, true)
ON CONFLICT (id) DO UPDATE SET
  name         = EXCLUDED.name,
  service_type = EXCLUDED.service_type,
  latitude     = EXCLUDED.latitude,
  longitude    = EXCLUDED.longitude,
  eta_minutes  = EXCLUDED.eta_minutes,
  rating       = EXCLUDED.rating,
  is_available = EXCLUDED.is_available;

-- Limpia los proveedores de una versión anterior del seed, para que re-ejecutar no deje duplicados
-- de nombres que ya cambiaron de id.
DELETE FROM providers
WHERE id NOT IN (
  '11111111-1111-4111-8111-111111111111',
  '22222222-2222-4222-8222-222222222222',
  '33333333-3333-4333-8333-333333333333',
  '44444444-4444-4444-8444-444444444444',
  '55555555-5555-4555-8555-555555555555',
  '10000000-0000-4000-8000-000000000001',
  '10000000-0000-4000-8000-000000000002',
  '10000000-0000-4000-8000-000000000003',
  '20000000-0000-4000-8000-000000000001',
  '20000000-0000-4000-8000-000000000002',
  '20000000-0000-4000-8000-000000000003',
  '20000000-0000-4000-8000-000000000004',
  '20000000-0000-4000-8000-000000000005',
  '30000000-0000-4000-8000-000000000001',
  '30000000-0000-4000-8000-000000000002',
  '30000000-0000-4000-8000-000000000003',
  '40000000-0000-4000-8000-000000000001'
)
-- Sólo borra los que no tienen casos asociados, para no violar la clave foránea.
AND NOT EXISTS (
  SELECT 1 FROM assistance_cases c WHERE c.provider_id = providers.id
);
