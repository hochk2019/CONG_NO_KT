SET search_path TO congno, public;

INSERT INTO roles (code, name) VALUES
('Admin', 'Admin'),
('Supervisor', 'Supervisor'),
('Accountant', 'Accountant'),
('Viewer', 'Viewer')
ON CONFLICT (code) DO NOTHING;
