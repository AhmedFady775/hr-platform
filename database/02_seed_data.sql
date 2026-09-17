-- Seed ~10 leave requests referencing real employee ids from https://dummyjson.com/users (ids 1-10).
-- EmployeeId is intentionally not a local FK -- see README for why.

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM [LeaveRequests])
BEGIN
    INSERT INTO [LeaveRequests] ([EmployeeId], [StartDate], [EndDate], [Type], [Status], [CreatedAt], [ReviewerNote])
    VALUES
        (1,  '2026-01-05', '2026-01-09', 0, 1, '2025-12-20T09:00:00', N'Approved - holiday coverage confirmed'),
        (2,  '2026-01-12', '2026-01-12', 1, 1, '2026-01-11T08:15:00', N'Approved - single sick day'),
        (3,  '2026-02-01', '2026-02-05', 0, 0, '2026-01-15T14:30:00', NULL),
        (4,  '2026-02-10', '2026-02-11', 2, 2, '2026-01-20T10:00:00', N'Rejected - insufficient notice for unpaid leave'),
        (5,  '2026-02-20', '2026-02-27', 0, 0, '2026-02-01T11:45:00', NULL),
        (6,  '2026-03-03', '2026-03-04', 1, 0, '2026-02-25T16:20:00', NULL),
        (7,  '2026-03-15', '2026-03-19', 0, 1, '2026-02-28T09:30:00', N'Approved'),
        (8,  '2026-03-22', '2026-03-22', 1, 1, '2026-03-21T07:50:00', N'Approved - doctor note on file'),
        (9,  '2026-04-01', '2026-04-10', 2, 0, '2026-03-10T13:00:00', NULL),
        (10, '2026-04-15', '2026-04-16', 0, 2, '2026-03-25T15:10:00', N'Rejected - conflicts with quarter close');
END;
GO
