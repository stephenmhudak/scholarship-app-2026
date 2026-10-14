-- References are submitted from a signed-in account: the new referee role, or a counselor or school admin.
-- Applicants, scorers and admins can't, so nobody scores a reference they wrote.
INSERT IGNORE INTO Roles (Id, Name, DisplayName) VALUES
    ('a0000000-0000-0000-0000-000000000006', 'referee', 'Referee');

INSERT IGNORE INTO Permissions (Id, Name, DisplayName, Description) VALUES
    ('b0000000-0000-0000-0000-000000000013', 'submit_references', 'Submit References', 'Submit references through a reference code and see the references you submitted');

INSERT IGNORE INTO RolePermissions (RoleId, PermissionId) VALUES
    ('a0000000-0000-0000-0000-000000000006', 'b0000000-0000-0000-0000-000000000013'),
    ('a0000000-0000-0000-0000-000000000004', 'b0000000-0000-0000-0000-000000000013'),
    ('a0000000-0000-0000-0000-000000000005', 'b0000000-0000-0000-0000-000000000013');

-- The account that submitted the reference (null for references submitted before accounts were required)
ALTER TABLE ReferenceRequests ADD COLUMN SubmittedById CHAR(36) NULL;
ALTER TABLE ReferenceRequests ADD CONSTRAINT FK_RefRequests_SubmittedBy FOREIGN KEY (SubmittedById) REFERENCES Users(Id) ON DELETE SET NULL;

-- Who uploaded each file, so a submission can only use files its own account uploaded
ALTER TABLE ReferenceFiles ADD COLUMN UploadedById CHAR(36) NULL;
ALTER TABLE ReferenceFiles ADD CONSTRAINT FK_RefFiles_UploadedBy FOREIGN KEY (UploadedById) REFERENCES Users(Id) ON DELETE SET NULL;
