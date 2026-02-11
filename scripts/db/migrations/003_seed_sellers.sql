SET search_path TO congno, public;

INSERT INTO sellers (seller_tax_code, name, address) VALUES
('2300328765', 'CONG TY TNHH TIEP VAN HOANG KIM', NULL),
('2301098313', 'CONG TY TNHH GIAI PHAP DAU TU HOANG MINH',
 'Phong 405 Toa nha Trung Thanh so 10 Nguyen Dang Dao, Phuong Tien Ninh Ve, TP Bac Ninh, Viet Nam')
ON CONFLICT (seller_tax_code) DO NOTHING;
