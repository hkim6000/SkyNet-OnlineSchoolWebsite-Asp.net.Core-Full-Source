namespace OnlineSchool.Models
{
    public class SiteData
    {
        public string Brand { get; set; } = string.Empty;
        public string Tagline { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Hours { get; set; } = string.Empty;
        public int RefundDays { get; set; } = 14;
        public int ProgressLimit { get; set; } = 30;
        public bool NoCertificate { get; set; } = true;
        public int MaxVideoMb { get; set; } = 30;
        public List<Topic> Topics { get; set; } = new List<Topic>();
        public List<InstructorInfo> Instructors { get; set; } = new List<InstructorInfo>();
        public List<CourseInfo> Courses { get; set; } = new List<CourseInfo>();
        public List<QuizInfo> Quizzes { get; set; } = new List<QuizInfo>();
        public Dictionary<string, PageDoc> Pages { get; set; } = new Dictionary<string, PageDoc>();
    }

    public class Topic
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Blurb { get; set; } = string.Empty;
    }

    public class InstructorInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
    }

    public class CourseInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Level { get; set; } = "beginner";
        public decimal Price { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Outcomes { get; set; } = string.Empty;
        public string InstructorId { get; set; } = string.Empty;
        public string Cover { get; set; } = string.Empty;
        public bool Published { get; set; } = true;
        public bool Featured { get; set; }
        public List<LessonInfo> Lessons { get; set; } = new List<LessonInfo>();
    }

    public class LessonInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Minutes { get; set; }
        public string Video { get; set; } = string.Empty;
        public string Poster { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public bool Free { get; set; }
    }

    public class QuizInfo
    {
        public string Id { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AfterLesson { get; set; } = string.Empty;
        public int PassMark { get; set; } = 70;
        public List<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    }

    public class QuizQuestion
    {
        public string Text { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new List<string>();
        public int Answer { get; set; }
        public string Why { get; set; } = string.Empty;
    }

    public class Student
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public bool Active { get; set; } = true;
    }

    public class QuizResult
    {
        public string QuizId { get; set; } = string.Empty;
        public int Best { get; set; }
        public int Attempts { get; set; }
        public bool Passed { get; set; }
        public DateTime When { get; set; }
    }

    public class Enrollment
    {
        public string Id { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public DateTime Started { get; set; }
        public string Status { get; set; } = "active";
        public DateTime? LockedOn { get; set; }
        public List<string> Completed { get; set; } = new List<string>();
        public List<QuizResult> Quizzes { get; set; } = new List<QuizResult>();
        public string Last { get; set; } = string.Empty;
    }

    public class RefundLine
    {
        public string Id { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime When { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
    }

    public class Order
    {
        public string Id { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = "pending";
        public DateTime Created { get; set; }
        public DateTime? Paid { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Session { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public List<RefundLine> Refunds { get; set; } = new List<RefundLine>();
    }

    public class RefundRequest
    {
        public string Id { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public bool Eligible { get; set; }
        public int DaysUsed { get; set; }
        public int Progress { get; set; }
        public bool HadCertificate { get; set; }
        public string Status { get; set; } = "open";
        public string Kind { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Note { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
        public DateTime? Decided { get; set; }
        public string RefundId { get; set; } = string.Empty;
    }

    public class CertRecord
    {
        public string Code { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string CourseId { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string EnrollmentId { get; set; } = string.Empty;
        public DateTime Issued { get; set; }
        public bool Revoked { get; set; }
        public DateTime? RevokedOn { get; set; }
    }

    public class ContactMessage
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public bool Read { get; set; }
    }

    public class Staff
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public bool Admin { get; set; }
        public bool Active { get; set; } = true;
        public bool MustChange { get; set; }
        public string ProviderId { get; set; } = string.Empty;
        public bool Own { get; set; }
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
    }

    public class LogEntry
    {
        public DateTime When { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; } = true;
    }

    public class SessionEntry
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
    }

    public class ResetLink
    {
        public string Hash { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
        public bool Used { get; set; }
    }

    public class MediaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public string Kind { get; set; } = "photo";
        public string Name { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public DateTime Added { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class BlockItem
    {
        public string Icon { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class Block
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public bool Hidden { get; set; }
        public string Kicker { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label2 { get; set; } = string.Empty;
        public string Link2 { get; set; } = string.Empty;
        public bool Flip { get; set; }
        public List<BlockItem> Items { get; set; } = new List<BlockItem>();
    }

    public class PageVersion
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public List<Block> Blocks { get; set; } = new List<Block>();
    }

    public class PageDoc
    {
        public List<Block> Blocks { get; set; } = new List<Block>();
        public List<PageVersion> Versions { get; set; } = new List<PageVersion>();
        public DateTime Saved { get; set; }
        public string By { get; set; } = string.Empty;
    }
}
