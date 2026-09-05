# Laptop Keyboard Doctor 2026

Phiên bản: **0.3.0** — bản nguồn mở MIT, có icon nhúng, nhận diện tác giả và build portable **một tệp EXE duy nhất** để chép vào USB.

**Tác giả / đơn vị phát hành:** MÁY TÍNH HÀ ANH  
**Địa chỉ:** xã Như Thanh, tỉnh Thanh Hoá  
**Mã nguồn:** [github.com/HAANHBS/LaptopKeyboardDoctor](https://github.com/HAANHBS/LaptopKeyboardDoctor)

Ứng dụng chẩn đoán bàn phím laptop chạy native trên Windows, tập trung vào các lỗi khó thấy bằng trang test trình duyệt: tự chạm, mất KeyUp, chatter/double key, lặp bất thường, phím ma do chạm ma trận và tín hiệu phím media/Fn đời mới.

## Chạy bản portable từ USB

1. Tải `LaptopKeyboardDoctor.exe` trong mục [Releases](https://github.com/HAANHBS/LaptopKeyboardDoctor/releases).
2. Chép đúng tệp EXE đó vào USB; không cần chép DLL, config hay thư mục phụ.
3. Trên Windows 10/11, nhấp đúp EXE để chạy. Ứng dụng hoạt động ngoại tuyến và mặc định không cần quyền Administrator.

SHA-256 của từng bản phát hành được GitHub Actions in trong log build. Có thể kiểm tra bằng PowerShell:

```powershell
Get-FileHash .\LaptopKeyboardDoctor.exe -Algorithm SHA256
```

## Build từ mã nguồn trên Windows

Yêu cầu: Windows 10/11, .NET Framework 4.8 (thường có sẵn), tài khoản người dùng bình thường. Không cần Internet và không cần cài thư viện.

1. Giải nén toàn bộ gói vào một thư mục ngắn, ví dụ `D:\KeyboardDoctor`.
2. Nhấp đúp `RUN_LaptopKeyboardDoctor.cmd`.
3. Script biên dịch ứng dụng và chạy 12 self-test. Khi PASS, cửa sổ ứng dụng tự mở.
4. Nhấn vài phím và xem mục **Nguồn**. Nếu có nhiều bàn phím, chọn đúng thiết bị laptop trước khi chạy test chuyên sâu.

Muốn chỉ build và kiểm tra, chạy `BUILD_AND_TEST.cmd`. EXE sinh tại `dist\LaptopKeyboardDoctor.exe`. Build sẽ báo lỗi nếu `dist` có bất kỳ tệp thứ hai nào.

Icon được tạo trong `obj` lúc build rồi nhúng trực tiếp vào EXE. Manifest, metadata phiên bản và thông tin tác giả cũng được nhúng; self-test dùng tệp tạm và tự xóa sau khi chạy.

## Vì sao mạnh hơn test web

| Lớp đo | Dữ liệu thu được | Giá trị chẩn đoán |
|---|---|---|
| Raw Input | thiết bị nguồn, scan code, E0/E1, Make/Break | phân biệt bàn phím laptop với bàn phím USB; thấy mã thấp hơn lớp DOM của trình duyệt |
| Low-level hook | VK, scan code, Down/Up, cờ `INJECTED` | đối chiếu toàn hệ thống và tách sự kiện phần mềm chèn khỏi nghi lỗi phần cứng |
| Poll trạng thái | bit Down hiện thời của Windows, quét 20 ms | phát hiện trạng thái Down không khớp luồng Raw Input hoặc thiếu KeyUp |
| Raw HID Consumer | gói dữ liệu từ Usage Page Consumer | lưu dấu vết media/assistant/phím đời mới ngay cả khi chưa có tên VK chuẩn |
| Raw Mouse | nguồn thiết bị, di chuyển, cuộn, nút trái/phải/giữa | kiểm tra touchpad, clickpad và tách nguồn với chuột USB khi Windows cung cấp device path |
| Mô hình thời gian người | dwell time, median/MAD, burst, nhịp đều, repeat delay/rate | cảnh báo xung/chùm/nhịp không giống thao tác tự nhiên thay vì chỉ dựa vào một ngưỡng cứng |
| Guided tests | phiên không chạm 30 giây và cặp ma trận | biến “phím tự chạm” và “phím thứ ba tự xuất hiện” thành bằng chứng lặp lại được |

Ứng dụng chỉ quan sát, không dùng `BlockInput`, không dùng `RIDEV_NOLEGACY`, không nuốt hook và không khóa tổ hợp Windows. Hook luôn gọi `CallNextHookEx`.

## Quy trình test chuẩn tại bàn sửa chữa

### A. Test nhanh 2–3 phút

- Chọn đúng thiết bị nguồn.
- Nhấn/nhả từng phím, xem màu: xanh lá là đã nhận đủ; xanh dương là đang Down; đỏ/cam là có cảnh báo.
- Mở **Luồng sự kiện**, xác nhận mỗi lần nhấn có `RAW KEYBOARD DOWN` và khi nhả có `RAW KEYBOARD UP`.
- Giữ từng Shift/Ctrl/Alt và thử hàng chữ, số, F1–F12.
- Thử Fn cùng âm lượng/độ sáng/đèn bàn phím.

### B. Dò tự chạm

- Vào **Test chuyên sâu** → **Bắt đầu 30 giây**.
- Không chạm bàn phím. Nên tắt phần mềm macro và không dùng bàn phím USB đã chọn.
- Có `IDLE_CONTACT` hoặc `IDLE_HID_CONTACT`: nghi tiếp điểm/cáp/mạch phát tín hiệu ngoài ý muốn.

### C. Dò chạm ma trận

- Chạy lần lượt 20 cặp được gợi ý.
- Mỗi bước chỉ giữ đúng hai phím trên màn hình.
- Nếu xuất hiện phím thứ ba, ứng dụng ghi `PHANTOM_KEY`, scan code và thiết bị nguồn.
- Lặp lại cặp lỗi ít nhất ba lần; thử ấn nhẹ palmrest/lắc nhẹ cáp trong điều kiện an toàn để tìm lỗi chập chờn.

### D. Dò lỗi theo nhiệt/áp lực

- Chạy một phiên khi máy nguội và một phiên sau khi tải máy 15–30 phút.
- Lặp test khi pin/sạc thay đổi nếu khách mô tả lỗi phụ thuộc nguồn.
- Xuất báo cáo ngay khi lỗi xuất hiện. Ứng dụng tạo một tệp TXT và hai tệp CSV (events, alerts).

### E. Test touchpad và nút trái/phải

- Mở tab **Touchpad + nút chuột**, chỉ di ngón tay trên touchpad để nhận diện nguồn.
- Nếu đang gắn chuột USB, chọn device path vừa xuất hiện khi di touchpad.
- Di lần lượt bốn góc; cuộn dọc/ngang; bấm riêng trái, phải và click giữa nếu máy hỗ trợ.
- Trạng thái phải trở về UP sau khi nhả. `TOUCHPAD_CHATTER` hoặc `TOUCHPAD_STUCK` phải được kiểm tra lặp lại.
- Dùng **RESET / ĐO LẠI** trên thanh trên cùng hoặc ngay trong tab touchpad để bắt đầu phiên sạch.

## Mã cảnh báo

| Mã | Ý nghĩa | Cách xác nhận |
|---|---|---|
| `IDLE_CONTACT` | Raw Keyboard phát phím trong lúc không chạm | lặp 3 phiên, chọn đúng thiết bị, loại bàn phím ngoài |
| `IDLE_HID_CONTACT` | Consumer HID phát gói khi không chạm | thử media/Fn, kiểm tra hotkey service và cáp |
| `CHATTER` | Down mới xuất hiện rất sát sau Up | tăng/giảm ngưỡng; gõ đơn chậm để loại double-tap chủ ý |
| `STUCK_KEY` | trạng thái Down vượt ngưỡng | xác nhận không giữ chủ ý; xem repeat count và KeyUp |
| `REPEAT_STORM` | số lần lặp trong 1 giây quá cao | so với typematic bình thường và thời gian giữ |
| `PHANTOM_KEY` | cặp đang giữ phát sinh phím thứ ba | lặp cùng cặp; đây là bằng chứng mạnh của chạm ma trận |
| `RAW_MISSING_KEYUP` | Poll đã Up nhưng Raw chưa Up | lặp lại, xem driver/EC/cáp và tải hệ thống |
| `POLL_ONLY_DOWN` | Windows báo Down nhưng Raw chưa có sự kiện gần đó | đối chiếu Hook, macro, phần mềm điều khiển và quyền tiến trình |
| `SOFTWARE_INJECTED` | hook thấy cờ sự kiện do phần mềm chèn | không kết luận hỏng bàn phím; kiểm tra macro/remote/accessibility |
| `NON_HUMAN_PULSE` | nhiều xung Down–Up ngắn hơn ngưỡng sinh học bảo thủ | kiểm tra không chạm và lặp lại cùng phím; cảnh báo mạnh khi có từ hai xung |
| `HUMAN_TIMING_OUTLIER` | dwell time lặp lại lệch khỏi median/MAD của người đang test | chờ mô hình học đủ 20 mẫu, sau đó lặp lại trên cùng phím |
| `MACHINE_RHYTHM` | ít nhất sáu chu kỳ gần như đều tuyệt đối | loại phần mềm macro rồi kiểm tra cáp/matrix/EC |
| `NON_HUMAN_BURST` | từ bốn phím khác nhau phát trong khoảng 8 ms | loại thao tác tì bàn tay; chạy test ma trận để xác nhận |
| `EARLY_REPEAT` | repeat xuất hiện trước độ trễ bàn phím Windows | so với thông số typematic hiển thị trong Thiết lập |
| `TOUCHPAD_CHATTER` | nút touchpad tái nhấn quá sát lần nhả | bấm đơn chậm, lặp lại và so với chuột ngoài |
| `TOUCHPAD_STUCK` | nút touchpad giữ Down quá ngưỡng | xác nhận đã nhả tay, kiểm tra clickpad/cáp |

## Giới hạn phải hiểu đúng

- `Fn` trên nhiều laptop được EC/firmware xử lý và không phát mã độc lập lên Windows. Ứng dụng chỉ xác nhận Fn qua tác dụng kết hợp hoặc gói HID/WM_APPCOMMAND mà firmware phát ra.
- `Ctrl+Alt+Del` là Secure Attention Sequence; ứng dụng người dùng không được phép ghi đầy đủ như phím thường.
- Nếu mạch lỗi nhưng controller không phát bất kỳ report nào, không phần mềm nào nhìn thấy. Khi đó phải đo cáp/đường matrix, thử keyboard khác hoặc kiểm tra EC/KBC.
- Cảnh báo `STUCK_KEY` có thể đúng với thao tác cố ý giữ phím. Kết luận sửa chữa phải dựa vào chế độ không chạm, khả năng lặp lại và thiết bị nguồn.
- Precision Touchpad có thể được Windows hợp nhất thành nguồn `SYSTEM`; trường hợp đó ứng dụng đo được chuyển động/click nhưng không luôn phân biệt được với chuột ngoài. Nên tháo chuột USB khi nghiệm thu.
- Phiên bản này tối ưu cho Windows 10/11. “Laptop đời cũ” nghĩa là bàn phím PS/2/ACPI cũ vẫn đi qua keyboard stack của Windows; máy còn Windows 7 cần cài .NET Framework 4.8 và tự nghiệm thu riêng.

## Tự kiểm tra

`Build.ps1` chạy 12 test thuật toán sau mỗi lần biên dịch:

1. nhấn/nhả bình thường không báo lỗi;
2. chatter được phát hiện;
3. giữ Down quá ngưỡng được phát hiện;
4. sự kiện trong phiên không chạm được phát hiện;
5. phím thứ ba trong bài test ma trận được phát hiện;
6. cặp ma trận hợp lệ được PASS.
7. xung Down–Up không giống người được phát hiện;
8. repeat sớm hơn cấu hình Windows được phát hiện;
9. mô hình dwell time hiệu chuẩn sau 20 mẫu;
10. click touchpad bình thường được ghi đúng;
11. chatter nút touchpad được phát hiện;
12. nút touchpad bị giữ được phát hiện.

Trước khi chạy trên Windows, có thể kiểm tra cấu trúc gói bằng:

```text
python tests\verify_package.py
```

## Quyền riêng tư

Ứng dụng không kết nối mạng, không ghi ký tự thành câu, không đọc clipboard và không gửi dữ liệu. Báo cáo chỉ chứa thời gian, tên/mã phím, trạng thái, thiết bị nguồn và bằng chứng chẩn đoán. Người dùng chủ động chọn nơi lưu báo cáo.

## Giấy phép nguồn mở

Copyright © 2026 MÁY TÍNH HÀ ANH. Dự án được phát hành theo [MIT License](LICENSE). Giấy phép cho phép sử dụng, sao chép, sửa đổi và phân phối lại mã nguồn với điều kiện giữ thông báo bản quyền và giấy phép.

Pull request và báo lỗi có bằng chứng (model máy, Windows build, device path, report đã ẩn thông tin riêng tư) đều được hoan nghênh.

## Tệp chính

- `RUN_LaptopKeyboardDoctor.cmd`: build nếu cần rồi mở ứng dụng.
- `BUILD_AND_TEST.cmd`: build và chạy self-test, không mở giao diện.
- `Build.ps1`: quy trình build offline bằng compiler .NET Framework của Windows.
- `LICENSE`: giấy phép nguồn mở MIT.
- `assets\app-icon.svg`: bản vector gốc của icon được nhúng.
- `.github\workflows\build-windows.yml`: build/self-test thật trên Windows và tạo Release khi đánh tag.
- `src\`: mã nguồn C# WinForms + Win32 Raw Input/hook.
- `tests\verify_package.py`: kiểm tra cấu trúc/an toàn gói đa nền tảng.
- `docs\`: nghiên cứu kỹ thuật và checklist nghiệm thu.
