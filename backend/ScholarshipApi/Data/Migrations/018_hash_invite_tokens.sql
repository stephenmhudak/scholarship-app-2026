-- Invite links are stored as SHA-256 hashes (lowercase hex, matching TokenHash.Sha256), like every other token.
-- Outstanding links keep working.
UPDATE SchoolAdminInvites SET Token = SHA2(Token, 256);
ALTER TABLE SchoolAdminInvites RENAME COLUMN Token TO TokenHash;
