# Laptop Keyboard Doctor - Kiểm tra bàn phím laptop

Ứng dụng Windows native để chẩn đoán lỗi bàn phím và touchpad laptop: tự chạm, thiếu KeyUp, chatter, repeat bất thường, phím ma và tín hiệu media/Fn.

## Tính năng chính

- Bản đồ bàn phím trực quan với trạng thái Down/Up/cảnh báo
- Theo dõi Raw Input / Hook / Poll / HID / Raw Mouse
- Test chuyên sâu: không chạm 30 giây, ma trận phím, touchpad
- Xuất báo cáo dạng TXT/CSV để lưu bằng chứng
- Build offline dạng EXE đơn file, không cần DLL phụ

## Chạy nhanh

1. Tải `LaptopKeyboardDoctor.exe` từ [Releases](https://github.com/HAANHBS/LaptopKeyboardDoctor/releases).
2. Chép EXE vào USB hoặc thư mục bất kỳ.
3. Chạy trên Windows 10/11, không cần quyền Administrator.

```powershell
Get-FileHash .\LaptopKeyboardDoctor.exe -Algorithm SHA256
```

## Build từ mã nguồn

Yêu cầu: Windows 10/11, .NET Framework 4.8.

```text
RUN_LaptopKeyboardDoctor.cmd
```

Hoặc build + self-test:

```text
BUILD_AND_TEST.cmd
```

EXE sinh ra ở:

```text
dist\LaptopKeyboardDoctor.exe
```

## Dùng như thế nào

- Chọn đúng nguồn thiết bị bàn phím/laptop trước khi chạy test chuyên sâu.
- Xem tab `Bàn phím` để kiểm tra phím đang Down/Up và cảnh báo màu.
- Mở tab `Luồng sự kiện` để xem Raw Input/Hook/Poll và xác nhận `DOWN`/`UP`.
- Vào `Test chuyên sâu` để chạy kiểm tra không chạm và ma trận phím.
- Mở tab `Touchpad + nút chuột` để kiểm tra clickpad/touchpad.

## Liên kết

- [CHANGELOG.md](CHANGELOG.md)
- [RELEASE_NOTES.md](RELEASE_NOTES.md)
- [LICENSE](LICENSE)
- GitHub: [github.com/HAANHBS/LaptopKeyboardDoctor](https://github.com/HAANHBS/LaptopKeyboardDoctor)

## Giấy phép

Dự án phát hành theo [MIT License](LICENSE).
