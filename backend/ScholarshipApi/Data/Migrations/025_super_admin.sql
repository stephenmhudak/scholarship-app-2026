-- Super admins have every permission (AuthService grants all Permissions rows to this role, including ones added
-- later), pass every admin check, and can impersonate other users. Only a super admin can create or manage one.
-- There's no way to self-register as one; promote the first with:
--   UPDATE Users SET Role = 'super_admin', SessionVersion = SessionVersion + 1 WHERE Email = '...';
INSERT IGNORE INTO Roles (Id, Name, DisplayName) VALUES
    ('a0000000-0000-0000-0000-000000000007', 'super_admin', 'Super Administrator');
