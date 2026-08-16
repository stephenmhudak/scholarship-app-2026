CREATE TABLE IF NOT EXISTS Roles (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    Name VARCHAR(50) NOT NULL UNIQUE,
    DisplayName VARCHAR(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS Permissions (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    Name VARCHAR(100) NOT NULL UNIQUE,
    DisplayName VARCHAR(150) NOT NULL,
    Description VARCHAR(255) NULL
);

CREATE TABLE IF NOT EXISTS RolePermissions (
    RoleId CHAR(36) NOT NULL,
    PermissionId CHAR(36) NOT NULL,
    PRIMARY KEY (RoleId, PermissionId),
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES Permissions(Id) ON DELETE CASCADE
);

-- Roles
INSERT IGNORE INTO Roles (Id, Name, DisplayName) VALUES
    ('a0000000-0000-0000-0000-000000000001', 'applicant',    'Applicant'),
    ('a0000000-0000-0000-0000-000000000002', 'scorer',       'Scorer'),
    ('a0000000-0000-0000-0000-000000000003', 'app_admin',    'Application Administrator'),
    ('a0000000-0000-0000-0000-000000000004', 'school_admin', 'School Administrator'),
    ('a0000000-0000-0000-0000-000000000005', 'counselor',    'Counselor');

-- Permissions
INSERT IGNORE INTO Permissions (Id, Name, DisplayName, Description) VALUES
    ('b0000000-0000-0000-0000-000000000001', 'view_dashboard',          'View Dashboard',          'Access the applicant dashboard'),
    ('b0000000-0000-0000-0000-000000000002', 'manage_application',      'Manage Application',      'View and submit scholarship applications'),
    ('b0000000-0000-0000-0000-000000000003', 'view_scoring_queue',      'View Scoring Queue',      'Access the scoring queue'),
    ('b0000000-0000-0000-0000-000000000004', 'view_scoring_overview',   'View Scoring Overview',   'View scoring statistics and progress across all applications'),
    ('b0000000-0000-0000-0000-000000000005', 'score_application',       'Score Applications',      'Open and submit scores for applications'),
    ('b0000000-0000-0000-0000-000000000006', 'admin_applications',      'Admin: Applications',     'View and manage all submitted applications'),
    ('b0000000-0000-0000-0000-000000000007', 'admin_settings',          'Admin: Settings',         'Manage system settings, users, and school admin invites'),
    ('b0000000-0000-0000-0000-000000000008', 'admin_scholarship',       'Admin: Scholarship',      'Manage scholarship cycles and questions'),
    ('b0000000-0000-0000-0000-000000000009', 'manage_school',           'Manage School',           'View and edit own school information'),
    ('b0000000-0000-0000-0000-000000000010', 'manage_school_staff',     'Manage School Staff',     'Add, remove, and reset passwords for counselors'),
    ('b0000000-0000-0000-0000-000000000011', 'view_school_data',        'View School Data',        'View school applicant and counselor lists'),
    ('b0000000-0000-0000-0000-000000000012', 'view_counselor_dashboard','View Counselor Dashboard', 'Access the counselor dashboard and applicant overview');

-- Role → Permission assignments
INSERT IGNORE INTO RolePermissions (RoleId, PermissionId) VALUES
    -- applicant
    ('a0000000-0000-0000-0000-000000000001', 'b0000000-0000-0000-0000-000000000001'),
    ('a0000000-0000-0000-0000-000000000001', 'b0000000-0000-0000-0000-000000000002'),
    -- scorer
    ('a0000000-0000-0000-0000-000000000002', 'b0000000-0000-0000-0000-000000000003'),
    ('a0000000-0000-0000-0000-000000000002', 'b0000000-0000-0000-0000-000000000005'),
    ('a0000000-0000-0000-0000-000000000002', 'b0000000-0000-0000-0000-000000000011'),
    -- app_admin
    ('a0000000-0000-0000-0000-000000000003', 'b0000000-0000-0000-0000-000000000004'),
    ('a0000000-0000-0000-0000-000000000003', 'b0000000-0000-0000-0000-000000000005'),
    ('a0000000-0000-0000-0000-000000000003', 'b0000000-0000-0000-0000-000000000006'),
    ('a0000000-0000-0000-0000-000000000003', 'b0000000-0000-0000-0000-000000000007'),
    ('a0000000-0000-0000-0000-000000000003', 'b0000000-0000-0000-0000-000000000008'),
    ('a0000000-0000-0000-0000-000000000003', 'b0000000-0000-0000-0000-000000000011'),
    -- school_admin
    ('a0000000-0000-0000-0000-000000000004', 'b0000000-0000-0000-0000-000000000009'),
    ('a0000000-0000-0000-0000-000000000004', 'b0000000-0000-0000-0000-000000000010'),
    ('a0000000-0000-0000-0000-000000000004', 'b0000000-0000-0000-0000-000000000011'),
    -- counselor
    ('a0000000-0000-0000-0000-000000000005', 'b0000000-0000-0000-0000-000000000011'),
    ('a0000000-0000-0000-0000-000000000005', 'b0000000-0000-0000-0000-000000000012');
