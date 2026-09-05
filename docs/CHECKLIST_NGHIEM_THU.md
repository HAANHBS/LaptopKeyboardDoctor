# Checklist triển khai và nghiệm thu

## A. Thông tin triển khai

| Trường bắt buộc | Giá trị / nơi ghi |
|---|---|
| Tên ứng dụng | Laptop Keyboard Doctor 2026 |
| Phiên bản | 0.3.0 Open Source |
| Ngày tạo | 05/09/2026 UTC |
| Tác giả / đơn vị phát hành | MÁY TÍNH HÀ ANH |
| Địa chỉ | xã Như Thanh, tỉnh Thanh Hoá |
| Kỹ thuật viên nghiệm thu | __________ |
| Tài khoản sử dụng | Không yêu cầu |
| URL mã nguồn | https://github.com/HAANHBS/LaptopKeyboardDoctor |
| Giấy phép | MIT |
| Biến môi trường | Không có |
| Secret / nơi lưu secret | Không có secret |
| Quyền Windows | `asInvoker`; không yêu cầu Administrator mặc định |
| Nơi cài thử | Máy/model: __________; thư mục: __________ |
| Windows build | __________ |
| Keyboard device path đã chọn | __________ |
| Touchpad/Raw Mouse device path đã chọn | __________ |
| Ngày nghiệm thu | __________ |
| Ghi chú lỗi | __________ |

## B. Checklist build

- [ ] Giải nén đầy đủ, không chạy trực tiếp trong ZIP.
- [ ] Có tối thiểu 10 tệp `.cs` trong `src`.
- [ ] Chạy `BUILD_AND_TEST.cmd`.
- [ ] Thấy `SELF TEST: PASS` và `Tests: 12/12`.
- [ ] Thấy `BUILD: PASS` và SHA-256 của EXE.
- [ ] Thấy `SINGLE-FILE DIST: PASS`.
- [ ] Có `dist\LaptopKeyboardDoctor.exe`.
- [ ] `dist` chỉ có đúng một tệp; không có `.dll`, `.config` hoặc `self-test-result.txt`.
- [ ] Properties → Details của EXE có Company `MÁY TÍNH HÀ ANH` và File version `0.3.0.0`.
- [ ] EXE và cửa sổ ứng dụng hiển thị icon Laptop Keyboard Doctor.
- [ ] EXE mở không yêu cầu Internet.
- [ ] Windows Defender/antivirus không chặn; nếu chặn, ghi chính xác tên cảnh báo, không tắt antivirus hàng loạt.

Kết quả build: PASS / FAIL  
SHA-256 EXE: ______________________________  
Ghi chú: ______________________________

## C. Checklist bản portable USB và nguồn mở

- [ ] Chép riêng `LaptopKeyboardDoctor.exe` sang USB sạch rồi mở được trên máy Windows khác.
- [ ] Không cần Internet, file DLL, file config hoặc bộ cài.
- [ ] Tab **Giới thiệu** hiển thị MÁY TÍNH HÀ ANH và địa chỉ xã Như Thanh, tỉnh Thanh Hoá.
- [ ] Repository công khai có `LICENSE`, toàn bộ `src`, script build và workflow Windows.
- [ ] Release GitHub có đúng file `LaptopKeyboardDoctor.exe`.
- [ ] SHA-256 file tải từ Release trùng log workflow.

Kết quả portable/nguồn mở: PASS / FAIL  
Ghi chú: ______________________________

## D. Checklist thu nhận đầu vào

- [ ] Nhấn A một lần: có RAW DOWN và RAW UP cùng device.
- [ ] Hook cũng có DOWN/UP tương ứng.
- [ ] VK và scan code hiển thị; A thường là VK `0x41`, scan phụ thuộc stack/layout.
- [ ] Left/Right Shift, Ctrl, Alt được phân biệt.
- [ ] Mở ứng dụng khác rồi nhấn phím: cửa sổ Keyboard Doctor vẫn nhận Raw Input nền.
- [ ] Gắn bàn phím USB: danh sách có nguồn khác.
- [ ] Chọn keyboard laptop: sự kiện bàn phím USB không đi vào analyzer của nguồn đã chọn.
- [ ] Volume/Media tạo Hook, WM_APPCOMMAND hoặc Raw HID (tùy máy).
- [ ] F13–F24/Copilot nếu có được ghi trong event table, kể cả không có keycap trên sơ đồ.
- [ ] Sự kiện macro/SendInput thử nghiệm được đánh dấu `SOFTWARE_INJECTED` khi Windows cung cấp cờ.
- [ ] Khi tab Bàn phím/Test chuyên sâu đang mở, bấm bốn phím mũi tên không làm đổi tab.
- [ ] Bấm `RESET / ĐO LẠI`: event, alert, màu phím và mô hình người về trạng thái sạch; nguồn/ngưỡng vẫn được giữ.

Kết quả đầu vào: PASS / FAIL  
Device laptop: ______________________________  
Ghi chú: ______________________________

## E. Checklist phát hiện lỗi

- [ ] Test không chạm 30 giây trên bàn phím tốt: không có `IDLE_CONTACT`.
- [ ] Nhấn một phím trong test không chạm: có `IDLE_CONTACT`.
- [ ] Giữ một phím quá ngưỡng: có `STUCK_KEY`.
- [ ] Nhả phím: sơ đồ bỏ trạng thái xanh dương.
- [ ] Chạy từng cặp ma trận: cặp hợp lệ PASS.
- [ ] Cố ý nhấn phím thứ ba: có `PHANTOM_KEY`.
- [ ] Điều chỉnh chatter 10–150 ms hoạt động.
- [ ] Điều chỉnh stuck 500–10000 ms hoạt động.
- [ ] Cảnh báo tô đỏ/cam đúng phím.
- [ ] `SOFTWARE_INJECTED` chỉ ở mức thông tin, không kết luận hỏng phần cứng.
- [ ] Gõ tối thiểu 20 phím: thẻ MÔ HÌNH NGƯỜI chuyển sang “Đã học”.
- [ ] Self-test xác nhận `NON_HUMAN_PULSE` và `EARLY_REPEAT`.
- [ ] Thông tin typematic hiển thị delay/rate đọc từ Windows.

Kết quả detector: PASS / FAIL  
Ghi chú: ______________________________

## F. Checklist touchpad và nút chuột

- [ ] Tab **Touchpad + nút chuột** hiển thị nguồn Raw Mouse.
- [ ] Tháo chuột USB hoặc chọn đúng device path touchpad.
- [ ] Di ngón tay: điểm xanh di chuyển, bộ đếm movement tăng.
- [ ] Di tới đủ bốn góc touchpad thực tế, không có vùng chết rõ rệt.
- [ ] Cuộn dọc/hai ngón: bộ đếm scroll tăng nếu driver phát wheel event.
- [ ] Nhấn trái: trạng thái LEFT chuyển DOWN rồi trở về UP; count tăng đúng một.
- [ ] Nhấn phải: trạng thái RIGHT chuyển DOWN rồi trở về UP; count tăng đúng một.
- [ ] Giữ nút quá ngưỡng trong self-test: có `TOUCHPAD_STUCK`.
- [ ] Double transition nhanh trong self-test: có `TOUCHPAD_CHATTER`.
- [ ] RESET trong tab touchpad xóa toàn bộ số đo và trạng thái nút.

Kết quả touchpad: PASS / FAIL  
Device path: ______________________________  
Ghi chú: ______________________________

## G. Checklist báo cáo và quyền riêng tư

- [ ] Xuất báo cáo tạo TXT, `_events.csv`, `_alerts.csv`.
- [ ] TXT có Windows, thiết bị, ngưỡng và tổng sự kiện.
- [ ] CSV mở đúng Unicode tiếng Việt.
- [ ] CSV chứa time/source/device/control/key/VK/scan/action, mouse flags, delta X/Y, wheel và detail.
- [ ] TXT chứa mô hình dwell/MAD, typematic Windows và thống kê touchpad.
- [ ] Không có nội dung văn bản đã gõ thành câu.
- [ ] Không có clipboard, mật khẩu hoặc dữ liệu mạng.
- [ ] Ngắt Internet, ứng dụng vẫn chạy đầy đủ.
- [ ] Đóng ứng dụng, hook được gỡ và bàn phím hoạt động bình thường.

Kết quả báo cáo: PASS / FAIL  
Đường dẫn báo cáo mẫu: ______________________________  
Ghi chú: ______________________________

## H. Checklist nghiệm thu trên mẫu máy

| Nhóm máy | Model thử | Keyboard path | Touchpad path | Fn/media | Idle/Matrix | Human model | Touchpad L/R | Kết quả/ghi chú |
|---|---|---|---|---|---|---|---|---|
| Laptop cũ PS/2/ACPI | | | | | | | | |
| Laptop USB/HID nội bộ | | | | | | | | |
| Laptop có numpad | | | | | | | | |
| Laptop gaming/NKRO | | | | | | | | |
| Laptop 2024–2026 có Copilot/assistant | | | | | | | | |
| Bàn phím/chuột USB đối chứng | | | | | | | | |

## I. Quy tắc kết luận sửa chữa

- [ ] Lỗi xuất hiện tối thiểu 3 lần trong cùng điều kiện.
- [ ] Đã chọn đúng device laptop, loại sự kiện keyboard USB/Bluetooth.
- [ ] Đã loại `SOFTWARE_INJECTED`, macro, remote-control và accessibility tool.
- [ ] Cảnh báo tốc độ có từ hai/ba bằng chứng lặp theo đúng mã; không kết luận từ một lần gõ nhanh.
- [ ] Đã tháo chuột ngoài hoặc chọn đúng nguồn trước khi kết luận touchpad.
- [ ] Đã thử cold boot và warm test nếu lỗi chập chờn.
- [ ] Đã thử lại sau khi tháo/lắp vệ sinh cáp trong điều kiện an toàn.
- [ ] Có report trước/sau sửa để đối chiếu.
- [ ] Nếu phần mềm không thấy report nhưng máy vẫn có biểu hiện, chuyển sang đo phần cứng/EC/KBC, không kết luận “bàn phím tốt”.

Kết luận cuối: ĐẠT / KHÔNG ĐẠT / CẦN ĐO PHẦN CỨNG  
Người nghiệm thu: __________________  
Ngày giờ: __________________
