-- Execute with psql -v expected_db=<your local DB name> -f 01_stage_fixed_baseline.sql
-- Stages local data only: IsActive=false, IsProductionApproved=false.
\set ON_ERROR_STOP on
SELECT set_config('app.aed_expected_db', :'expected_db', false);
BEGIN;
DO $guard$
BEGIN
    IF current_database() <> current_setting('app.aed_expected_db') THEN
        RAISE EXCEPTION 'Wrong database: expected %, connected %',
            current_setting('app.aed_expected_db'), current_database();
    END IF;
    IF EXISTS (
        SELECT 1 FROM "ScenarioConfigs"
        WHERE "ScenarioConfigId" = 'FIXED_BASELINE_V1'
          AND "ScenarioConfigVersion" = 'FIXED_BASELINE_V1'
    ) THEN
        RAISE EXCEPTION 'Fixed baseline already exists: inspect it instead of overwriting';
    END IF;
    IF EXISTS (
        SELECT 1 FROM "ScenarioContentDefinitions"
        WHERE "UnityCompatibilityVersion" = '0.1.0'
          AND "ContentWhitelistVersion" = 'M2-WHITELIST-1'
          AND ("ContentType", "ContentId") IN (
            ('Map', 'M2-MAP-1'), ('Monster', 'STALKER'),
            ('ObjectiveSpawnSet', 'DEFAULT_OBJECTIVES'),
            ('RouteModifier', 'DEFAULT_ROUTE')
          )
    ) THEN
        RAISE EXCEPTION 'One or more content definitions already exist: inspect before seeding';
    END IF;
END
$guard$;

INSERT INTO "ScenarioContentDefinitions"
    ("Id", "ContentType", "ContentId", "ContentWhitelistVersion",
     "UnityCompatibilityVersion", "IsActive", "IsProductionApproved",
     "Provenance", "CreatedAtUtc", "UpdatedAtUtc")
SELECT gen_random_uuid(), seed."ContentType", seed."ContentId",
       'M2-WHITELIST-1', '0.1.0', false, false,
       'LOCAL_AED_V2_SOURCE_FIXEDDIRECTOR_20261008', now(), now()
FROM (VALUES
    ('Map', 'M2-MAP-1'),
    ('Monster', 'STALKER'),
    ('ObjectiveSpawnSet', 'DEFAULT_OBJECTIVES'),
    ('RouteModifier', 'DEFAULT_ROUTE')
) AS seed("ContentType", "ContentId");

INSERT INTO "ScenarioConfigs"
    ("ScenarioConfigId", "ScenarioConfigVersion", "SchemaVersion", "PolicyVersion",
     "ConfigSource", "MapId", "MonsterType", "ObjectiveSpawnSetId",
     "SupportItemBudget", "DetectionFillRate", "DetectionDecayRate", "ChaseSpeed",
     "SearchDuration", "RouteModifier", "EscapeDoorTimerSeconds",
     "FallbackConfigId", "FallbackConfigVersion", "ContentWhitelistVersion",
     "UnityCompatibilityVersion", "IsFixedFallback", "IsActive", "IsProductionApproved",
     "Provenance", "CreatedAtUtc", "UpdatedAtUtc")
VALUES
    ('FIXED_BASELINE_V1', 'FIXED_BASELINE_V1', '1.1', 'AED_SCENARIO_POLICY_V1_1',
     'Fixed', 'M2-MAP-1', 'STALKER', 'DEFAULT_OBJECTIVES',
     0, 0.5, 1.0, 9.0, 5.0, 'DEFAULT_ROUTE', 45.0,
     'FIXED_BASELINE_V1', 'FIXED_BASELINE_V1', 'M2-WHITELIST-1',
     '0.1.0', true, false, false,
     'LOCAL_AED_V2_SOURCE_FIXEDDIRECTOR_20261008', now(), now());
COMMIT;

SELECT "ScenarioConfigId", "ScenarioConfigVersion", "UnityCompatibilityVersion",
       "IsActive", "IsProductionApproved", "IsFixedFallback"
FROM "ScenarioConfigs"
WHERE "ScenarioConfigId" = 'FIXED_BASELINE_V1'
  AND "ScenarioConfigVersion" = 'FIXED_BASELINE_V1';
