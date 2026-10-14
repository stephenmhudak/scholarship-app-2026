-- School admins (principals) also do a counselor's job: the counselor dashboard of their school's applicants and
-- reminders. They already had view_school_data and submit_references.
INSERT IGNORE INTO RolePermissions (RoleId, PermissionId) VALUES
    ('a0000000-0000-0000-0000-000000000004', 'b0000000-0000-0000-0000-000000000012');

-- Permissions are carried in the session token, so sign school admins out once to pick up the new one
UPDATE Users SET SessionVersion = SessionVersion + 1 WHERE Role = 'school_admin';
