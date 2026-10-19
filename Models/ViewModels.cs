using System.ComponentModel.DataAnnotations;

namespace DwaarixVMS.Models;

public class LoginVM
{
    [Required(ErrorMessage = "Enter your username")] public string Username { get; set; } = "";
    [Required(ErrorMessage = "Enter your password"), DataType(DataType.Password)] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
}

public class ChangePasswordVM
{
    [Required(ErrorMessage = "Enter your current password"), DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Enter a new password"), DataType(DataType.Password), Display(Name = "New password")]
    [RegularExpression(Rx.Password, ErrorMessage = "Use 8 or more characters with at least one letter and one number")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Repeat the new password"), DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match")]
    public string ConfirmPassword { get; set; } = "";
}

public class VisitorCreateVM : IValidatableObject
{
    [Required(ErrorMessage = "Enter the visitor's name"), StringLength(100)]
    [RegularExpression(Rx.Name, ErrorMessage = "Name can have letters, spaces and . ' - only")]
    [Display(Name = "Visitor name")]
    public string VisitorName { get; set; } = "";

    [Required(ErrorMessage = "Enter a mobile number")]
    [RegularExpression(Rx.Mobile, ErrorMessage = "Enter a valid 10-digit mobile number")]
    [Display(Name = "Mobile number")]
    public string Mobile { get; set; } = "";

    [Required(ErrorMessage = "Select a purpose")] public string Purpose { get; set; } = "";

    [Required(ErrorMessage = "Select the flat or resident")]
    [Range(1, int.MaxValue, ErrorMessage = "Select the flat or resident")]
    [Display(Name = "Visiting flat / member")]
    public int? ResidentId { get; set; }

    [Required(ErrorMessage = "Enter the expected time")]
    [Display(Name = "Expected time")]
    public DateTime ExpectedTime { get; set; } = DateTime.Now;

    [StringLength(250, ErrorMessage = "Remarks can be up to 250 characters")]
    public string? Remarks { get; set; }

    public string? PhotoData { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!string.IsNullOrEmpty(Purpose) && !Lists.Purposes.Contains(Purpose))
            yield return new ValidationResult("Select a purpose from the list", new[] { nameof(Purpose) });
        if (ExpectedTime < DateTime.Now.AddDays(-1) || ExpectedTime > DateTime.Now.AddDays(30))
            yield return new ValidationResult("Choose a time within the next 30 days", new[] { nameof(ExpectedTime) });
    }
}

public class PreApprovalVM : IValidatableObject
{
    [Required(ErrorMessage = "Enter the visitor's name"), StringLength(100)]
    [RegularExpression(Rx.Name, ErrorMessage = "Name can have letters, spaces and . ' - only")]
    [Display(Name = "Visitor name")]
    public string VisitorName { get; set; } = "";

    [Required(ErrorMessage = "Enter a mobile number")]
    [RegularExpression(Rx.Mobile, ErrorMessage = "Enter a valid 10-digit mobile number")]
    [Display(Name = "Mobile number")]
    public string Mobile { get; set; } = "";

    [Required(ErrorMessage = "Select a category")] public string Category { get; set; } = "";

    [Display(Name = "Resident")]
    public int? ResidentId { get; set; }

    [Required(ErrorMessage = "Enter the start time"), Display(Name = "Valid from")]
    public DateTime ValidFrom { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Enter the end time"), Display(Name = "Valid till")]
    public DateTime ValidTill { get; set; } = DateTime.Now.AddDays(1);

    [Display(Name = "Allow multiple entries until expiry")]
    public bool IsRecurring { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!string.IsNullOrEmpty(Category) && !Lists.PreCategories.Contains(Category))
            yield return new ValidationResult("Select a category from the list", new[] { nameof(Category) });
        if (ValidTill <= ValidFrom)
            yield return new ValidationResult("End time must be after the start time", new[] { nameof(ValidTill) });
        if (ValidTill < DateTime.Now)
            yield return new ValidationResult("End time is already in the past", new[] { nameof(ValidTill) });
        if (ValidTill > ValidFrom.AddDays(365))
            yield return new ValidationResult("A pre-approval can last up to 365 days", new[] { nameof(ValidTill) });
    }
}

public class DeliveryVM : IValidatableObject
{
    [Required(ErrorMessage = "Enter the delivery person's name"), StringLength(100)]
    [RegularExpression(Rx.Name, ErrorMessage = "Name can have letters, spaces and . ' - only")]
    [Display(Name = "Delivery person name")]
    public string PersonName { get; set; } = "";

    [Required(ErrorMessage = "Enter a mobile number")]
    [RegularExpression(Rx.Mobile, ErrorMessage = "Enter a valid 10-digit mobile number")]
    [Display(Name = "Mobile number")]
    public string Mobile { get; set; } = "";

    [Required(ErrorMessage = "Select the company")] public string Company { get; set; } = "";

    [Required(ErrorMessage = "Select the flat or resident")]
    [Range(1, int.MaxValue, ErrorMessage = "Select the flat or resident")]
    [Display(Name = "Delivering to")]
    public int? ResidentId { get; set; }

    [StringLength(250, ErrorMessage = "Remarks can be up to 250 characters")]
    public string? Remarks { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!string.IsNullOrEmpty(Company) && !Lists.Companies.Contains(Company))
            yield return new ValidationResult("Select a company from the list", new[] { nameof(Company) });
    }
}

public class EmergencyVM : IValidatableObject
{
    [Required(ErrorMessage = "Select the type of emergency")] public string Type { get; set; } = "";

    [Required(ErrorMessage = "Describe what is happening"), StringLength(250, MinimumLength = 5, ErrorMessage = "Use between 5 and 250 characters")]
    public string Message { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!string.IsNullOrEmpty(Type) && !Lists.EmergencyTypes.Contains(Type))
            yield return new ValidationResult("Select a type from the list", new[] { nameof(Type) });
    }
}

public class UserVM : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the full name"), StringLength(100)]
    [RegularExpression(Rx.Name, ErrorMessage = "Name can have letters, spaces and . ' - only")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Enter a username"), StringLength(50, MinimumLength = 3, ErrorMessage = "Use 3 to 50 characters")]
    [RegularExpression(@"^[A-Za-z0-9._]+$", ErrorMessage = "Use letters, numbers, dot and underscore only")]
    public string Username { get; set; } = "";

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Select a role")] public string Role { get; set; } = Roles.Watchman;

    [RegularExpression(Rx.Mobile, ErrorMessage = "Enter a valid 10-digit mobile number")]
    public string? Mobile { get; set; }

    [Display(Name = "Linked resident")]
    public int? ResidentId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!Roles.All.Contains(Role))
            yield return new ValidationResult("Select a valid role", new[] { nameof(Role) });
        if (Id == 0 && string.IsNullOrWhiteSpace(Password))
            yield return new ValidationResult("Set a password for the new user", new[] { nameof(Password) });
        if (!string.IsNullOrWhiteSpace(Password) && !System.Text.RegularExpressions.Regex.IsMatch(Password, Rx.Password))
            yield return new ValidationResult("Use 8 or more characters with at least one letter and one number", new[] { nameof(Password) });
        if (Role == Roles.Resident && ResidentId == null)
            yield return new ValidationResult("Link this login to a resident", new[] { nameof(ResidentId) });
    }
}

public class VisitorListVM
{
    public List<Visitor> Items { get; set; } = new();
    public string? Q { get; set; }
    public string? Status { get; set; }
    public DateTime? Date { get; set; }
}

public class DashboardVM
{
    public int Inside { get; set; }
    public int TodayCount { get; set; }
    public int Pending { get; set; }
    public int ActivePre { get; set; }
    public int Uncollected { get; set; }
    public int OpenEmergencies { get; set; }
    public List<Visitor> PendingList { get; set; } = new();
    public List<Visitor> InsideList { get; set; } = new();
}

public class ReportVM
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<Visitor> Rows { get; set; } = new();
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public Dictionary<string, int> ByPurpose { get; set; } = new();
    public Dictionary<DateTime, int> ByDay { get; set; } = new();
}
