SET search_path TO congno, public;

UPDATE permissions
SET name = CASE code
    WHEN 'customer.view' THEN 'Xem khách hàng'
    WHEN 'customer.edit.all' THEN 'Sửa mọi khách hàng'
    WHEN 'customer.edit.owned' THEN 'Sửa khách hàng phụ trách'
    WHEN 'customer.edit.unassigned' THEN 'Sửa khách hàng chưa phân công'
    WHEN 'customer.assignment.manage' THEN 'Quản lý phân công khách hàng'
    WHEN 'import.upload' THEN 'Tải tệp nhập liệu'
    WHEN 'import.history' THEN 'Xem lịch sử nhập liệu'
    WHEN 'import.commit.invoice' THEN 'Ghi nhận nhập hóa đơn'
    WHEN 'import.commit.advance' THEN 'Ghi nhận nhập trả hộ'
    WHEN 'import.commit.receipt' THEN 'Ghi nhận nhập thu tiền'
    WHEN 'import.rollback' THEN 'Hoàn tác đợt nhập'
    WHEN 'advance.manage' THEN 'Quản lý trả hộ'
    WHEN 'receipt.approve' THEN 'Duyệt thu tiền'
    WHEN 'period.lock.manage' THEN 'Quản lý khóa kỳ'
    WHEN 'reports.view' THEN 'Xem báo cáo'
    WHEN 'invoice.manage' THEN 'Quản lý hóa đơn'
    WHEN 'audit.view' THEN 'Xem nhật ký hệ thống'
    WHEN 'admin.health.view' THEN 'Xem sức khỏe hệ thống'
    WHEN 'risk.view' THEN 'Xem cảnh báo rủi ro'
    WHEN 'risk.manage' THEN 'Quản lý cảnh báo rủi ro'
    WHEN 'backup.manage' THEN 'Quản lý sao lưu'
    WHEN 'backup.restore' THEN 'Khôi phục bản sao lưu'
    WHEN 'admin.manage' THEN 'Quản trị hệ thống'
    ELSE name
END
WHERE code IN (
    'customer.view',
    'customer.edit.all',
    'customer.edit.owned',
    'customer.edit.unassigned',
    'customer.assignment.manage',
    'import.upload',
    'import.history',
    'import.commit.invoice',
    'import.commit.advance',
    'import.commit.receipt',
    'import.rollback',
    'advance.manage',
    'receipt.approve',
    'period.lock.manage',
    'reports.view',
    'invoice.manage',
    'audit.view',
    'admin.health.view',
    'risk.view',
    'risk.manage',
    'backup.manage',
    'backup.restore',
    'admin.manage'
);
