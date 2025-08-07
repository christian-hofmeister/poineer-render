-- Initial schema for POIneer regional SQLite database
CREATE TABLE IF NOT EXISTS poi (
    id TEXT PRIMARY KEY,
    name TEXT,
    lat REAL NOT NULL,
    lon REAL NOT NULL,
    type TEXT,
    subtype TEXT,
    tags TEXT
);

-- Optional: Index for faster location-based queries (e.g. for nearby POIs)
CREATE INDEX IF NOT EXISTS idx_poi_location ON poi (lat, lon);