-- The projected name cannot identify typed YAML keys. Keep pre-upgrade
-- cached rows readable but uneditable (NULL identity) until the next scan.
ALTER TABLE properties ADD COLUMN key_identity TEXT;
-- Force the scanner slow path even when source bytes/stat data are unchanged.
UPDATE files SET size_bytes = -1;
