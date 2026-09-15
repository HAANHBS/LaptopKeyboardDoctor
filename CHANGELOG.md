# Changelog

## [Unreleased]

### Fixed
- Bổ sung các phím thiếu trên bản đồ bàn phím: `Home`, `End`, `Page Up`, `Page Down`, `Insert`.
- Khóa tab `Luồng sự kiện` khi đang xem để tránh bị nhảy tab do phím điều hướng.
- Chặn `Home`/`End`/`Page Up`/`Page Down` nhầm chuyển tab trong các tab đang focus.
- Tối ưu cập nhật UI chuột/touchpad để giảm lag khi xử lý Raw Mouse.
- Cập nhật tiêu đề phần mềm và icon để dễ nhìn hơn, giữ nguyên mục tiêu chẩn đoán lỗi.

## [0.3.0] - 2026-09-15

### Added
- Mô hình chẩn đoán bàn phím laptop trên Windows bằng Raw Input + Hook + Poll + HID.
- Bản đồ bàn phím trực quan với màu trạng thái Down/Up/cảnh báo.
- Test chuyên sâu: idle contact, matrix pair, touchpad/clickpad và báo cáo xuất file.

### Changed
- Cải thiện sơ đồ tab và cách hiển thị nguồn thiết bị.
- Cập nhật giao diện rõ ràng hơn cho quá trình kiểm tra lỗi tự chạm và phím ma.

### Fixed
- Sửa các tình huống bỏ sót khi xem thiết bị nguồn / kết nối / ngắt kết nối.
- Cải thiện hiển thị trạng thái báo cáo và dữ liệu sự kiện.
