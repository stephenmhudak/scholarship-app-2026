-- Guided page tours (frontend/src/tours): the last time each user went through each tour, and which version of it.
-- TourId is "<role>:<page>"; Version is a hash of the tour's steps and the page's components, so a changed page shows
-- its tour again. Outcome is how the last run ended: completed (reached the last step) or dismissed (closed early).
CREATE TABLE IF NOT EXISTS UserTours (
    UserId      CHAR(36)     NOT NULL,
    TourId      VARCHAR(100) NOT NULL,
    Version     VARCHAR(64)  NOT NULL,
    Outcome     VARCHAR(20)  NOT NULL,
    LastSeenAt  DATETIME     NOT NULL,
    CompletedAt DATETIME     NULL,
    TimesSeen   INT          NOT NULL DEFAULT 1,
    PRIMARY KEY (UserId, TourId),
    CONSTRAINT FK_UserTours_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
