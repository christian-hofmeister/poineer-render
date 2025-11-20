-- V1__create_poi_table.sql
-- Basic schema for POIs in Berlin

CREATE TABLE IF NOT EXISTS poi (
    id              INTEGER PRIMARY KEY,
    osm_id          TEXT NOT NULL,
    name            TEXT,
    amenity         TEXT,
    latitude        REAL NOT NULL,
    longitude       REAL NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_poi_amenity ON poi (amenity);
CREATE INDEX IF NOT EXISTS idx_poi_lat_lon ON poi (latitude, longitude);
CREATE INDEX IF NOT EXISTS idx_poi_osm_id ON poi (osm_id);