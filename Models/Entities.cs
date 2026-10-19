using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DwaarixVMS.Models;

public static class Rx
{
    public const string Mobile = @"^[6-9]\d{9}$";
    public const string Name = @"^[A-Za-z][A-Za-z .'\-]{1,99}$";
    public const string Password = @"^(?=.*[A-Za-z])(?=.*\d).{8,64}$";
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Watchman = "Watchman";
    public const string Resident = "Resident";
    public const string Staff = "Admin,Watchman";
    public static readonly string[] All = { Admin, Watchman, Resident };
}

public static class VisitStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string CheckedIn = "CheckedIn";
    public const string CheckedOut = "CheckedOut";
    public const string Cancelled = "Cancelled";
    public static readonly string[] All = { Pending, Approved, Rejected, CheckedIn, CheckedOut, Cancelled };
}

public static class Lists
{
    public static readonly string[] Purposes = { "Guest", "Service", "Delivery", "Interview", "Maintenance", "Cab / Taxi", "Other" };
    public static readonly string[] Companies = { "Amazon", "Flipkart", "Swiggy", "Zomato", "Blinkit", "BigBasket", "Courier", "Other" };
    public static readonly string[] PreCategories = { "Guest", "Electrician", "Plumber", "Housekeeping", "Delivery Partner", "Cab / Taxi", "Cook", "Other" };
    public static readonly string[] EmergencyTypes = { "Medical", "Fire", "Security threat", "Lift / Power failure", "Other" };
}

public class AppUser
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    [Required, StringLength(50)] public string Username { get; set; } = "";
    [Required, StringLength(200)] public string PasswordHash { get; set; } = "";
    [Required, StringLength(20)] public string Role { get; set; } = Roles.Watchman;
    [StringLength(15)] public string? Mobile { get; set; }
    public int? ResidentId { get; set; }
    public Resident? Resident { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Resident
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the flat number")]
    [StringLength(10, ErrorMessage = "Flat number is too long")]
    [RegularExpression(@"^[A-Za-z0-9\-]+$", ErrorMessage = "Use letters, numbers and hyphen only, e.g. A-501")]
    [Display(Name = "Flat")]
    public string FlatNo { get; set; } = "";

    [Required(ErrorMessage = "Enter the resident's name")]
    [StringLength(100)]
    [RegularExpression(Rx.Name, ErrorMessage = "Name can have letters, spaces and . ' - only")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Enter a mobile number")]
    [RegularExpression(Rx.Mobile, ErrorMessage = "Enter a valid 10-digit mobile number")]
    public string Mobile { get; set; } = "";

    [EmailAddress(ErrorMessage = "Enter a valid email address"), StringLength(100)]
    public string? Email { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [NotMapped] public string Label => $"{FlatNo} - {FullName}";
}

public class Visitor
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string VisitorCode { get; set; } = "";
    [Required, StringLength(100)] public string VisitorName { get; set; } = "";
    [Required, StringLength(15)] public string Mobile { get; set; } = "";
    [Required, StringLength(40)] public string Purpose { get; set; } = "";
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    public DateTime ExpectedTime { get; set; }
    [StringLength(250)] public string? Remarks { get; set; }
    [StringLength(200)] public string? PhotoPath { get; set; }
    [Required, StringLength(20)] public string Status { get; set; } = VisitStatus.Pending;
    public DateTime? InTime { get; set; }
    public DateTime? OutTime { get; set; }
    public DateTime ValidTill { get; set; }
    public int? PreApprovalId { get; set; }
    public PreApproval? PreApproval { get; set; }
    public int CreatedById { get; set; }
    public AppUser CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class PreApproval
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    [Required, StringLength(100)] public string VisitorName { get; set; } = "";
    [Required, StringLength(15)] public string Mobile { get; set; } = "";
    [Required, StringLength(40)] public string Category { get; set; } = "";
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTill { get; set; }
    public bool IsRecurring { get; set; }
    [Required, StringLength(20)] public string Status { get; set; } = "Active";
    [Required, StringLength(6)] public string AccessCode { get; set; } = "";
    public int CreatedById { get; set; }
    public AppUser CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Delivery
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string PersonName { get; set; } = "";
    [Required, StringLength(15)] public string Mobile { get; set; } = "";
    [Required, StringLength(40)] public string Company { get; set; } = "";
    public int ResidentId { get; set; }
    public Resident Resident { get; set; } = null!;
    [StringLength(250)] public string? Remarks { get; set; }
    public DateTime ArrivedAt { get; set; } = DateTime.Now;
    public DateTime? CollectedAt { get; set; }
    public int CreatedById { get; set; }
    public AppUser CreatedBy { get; set; } = null!;
}

public class Emergency
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string Type { get; set; } = "";
    [Required, StringLength(250)] public string Message { get; set; } = "";
    public int RaisedById { get; set; }
    public AppUser RaisedBy { get; set; } = null!;
    public DateTime RaisedAt { get; set; } = DateTime.Now;
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    [Required, StringLength(100)] public string Title { get; set; } = "";
    [Required, StringLength(300)] public string Message { get; set; } = "";
    [Required, StringLength(30)] public string Type { get; set; } = "info";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
