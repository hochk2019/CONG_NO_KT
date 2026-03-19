namespace CongNoGolden.Infrastructure.Security;

public static class AppPermissions
{
    public const string CustomerView = "customer.view";
    public const string CustomerEditAll = "customer.edit.all";
    public const string CustomerEditOwned = "customer.edit.owned";
    public const string CustomerEditUnassigned = "customer.edit.unassigned";
    public const string CustomerAssignmentManage = "customer.assignment.manage";

    public const string ImportUpload = "import.upload";
    public const string ImportHistory = "import.history";
    public const string ImportCommitInvoice = "import.commit.invoice";
    public const string ImportCommitAdvance = "import.commit.advance";
    public const string ImportCommitReceipt = "import.commit.receipt";
    public const string ImportRollback = "import.rollback";

    public const string AdvanceManage = "advance.manage";
    public const string ReceiptApprove = "receipt.approve";
    public const string AdminManage = "admin.manage";
}
