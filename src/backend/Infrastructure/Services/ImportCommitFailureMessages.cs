using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CongNoGolden.Infrastructure.Services;

internal static class ImportCommitFailureMessages
{
    internal static string Build(DbUpdateException exception, string batchType)
    {
        var postgresException = exception.InnerException as PostgresException;
        var constraintMessage = TryBuildConstraintMessage(postgresException?.ConstraintName, batchType);
        if (constraintMessage is not null)
        {
            return constraintMessage;
        }

        var documentLabel = GetDocumentLabel(batchType);
        return $"Không thể ghi dữ liệu import {documentLabel}. Kế toán cần kiểm tra MST khách hàng, tên khách hàng và thử import lại. Nếu lỗi còn tiếp diễn, liên hệ quản trị hệ thống.";
    }

    internal static string? TryBuildConstraintMessage(string? constraintName, string batchType)
    {
        if (string.IsNullOrWhiteSpace(constraintName))
        {
            return null;
        }

        if (constraintName is not (
            "invoices_customer_tax_code_fkey" or
            "advances_customer_tax_code_fkey" or
            "receipts_customer_tax_code_fkey"))
        {
            return null;
        }

        var documentLabel = GetDocumentLabel(batchType);
        return $"Không thể ghi dữ liệu import {documentLabel} vì hệ thống chưa tạo hoặc liên kết được khách hàng theo MST khách hàng trên file. Kế toán cần kiểm tra MST khách hàng, tên khách hàng; nếu khách hàng chưa có trong danh mục thì tạo/cập nhật khách hàng rồi import lại.";
    }

    private static string GetDocumentLabel(string batchType)
    {
        return batchType switch
        {
            "INVOICE" => "hóa đơn",
            "ADVANCE" => "khoản trả hộ",
            "RECEIPT" => "phiếu thu",
            _ => "dữ liệu"
        };
    }
}
