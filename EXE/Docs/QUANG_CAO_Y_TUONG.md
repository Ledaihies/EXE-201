# Ý tưởng đặt quảng cáo – Nguồn Việt

Dưới đây là các vị trí và hình thức quảng cáo phù hợp với trang đặc sản, có thể triển khai dần.

---

## 1. Banner ngang (trên đầu trang)

- **Vị trí:** Ngay dưới topbar (dưới "Giao hàng toàn quốc"), trên logo "Nguồn Việt".
- **Hình thức:** Một dải banner 728×90 hoặc 970×90 (full width trên mobile có thể 320×50).
- **Ưu điểm:** Nhìn thấy ngay khi vào trang, phù hợp quảng cáo thương hiệu hoặc khuyến mãi lớn.
- **Lưu ý:** Không nên quá cao để tránh đẩy nội dung chính xuống.

---

## 2. Banner trong Hero (cạnh ô tìm kiếm)

- **Vị trí:** Bên phải hero, thay hoặc xen kẽ carousel ảnh đặc sản hiện tại.
- **Hình thức:** Một khung 300×250 hoặc 336×280 (Medium Rectangle), có thể xoay nhiều banner.
- **Ưu điểm:** Gần ô tìm kiếm, người dùng đang có ý định mua sắm.
- **Gợi ý:** Ưu tiên quảng cáo đối tác (ví dụ: đối tác vận chuyển, ví điện tử).

---

## 3. Banner giữa hai block nội dung

- **Vị trí:** Giữa "Danh mục đặc sản" và "Đặc sản quanh bạn" (phần bản đồ), hoặc giữa "Đặc sản quanh bạn" và "Sản phẩm nổi bật".
- **Hình thức:** Banner ngang 728×90 hoặc 970×90, có thể dùng responsive (trên mobile 320×50).
- **Ưu điểm:** Tách giữa hai nội dung, ít gây khó chịu, dễ đàm phán với đối tác.

---

## 4. Quảng cáo xen sản phẩm (Native / Sponsored)

- **Vị trí:** Trong danh sách sản phẩm (trang Đặc sản, hoặc block "Sản phẩm nổi bật").
- **Hình thức:** Một số ô trong grid là "Sản phẩm được tài trợ" hoặc "Gợi ý đối tác", cùng format thẻ sản phẩm (ảnh, tên, giá) nhưng có nhãn nhỏ "Quảng cáo" hoặc "Tài trợ".
- **Ưu điểm:** Hòa với nội dung, tăng khả năng click, phù hợp đặc sản/ẩm thực cùng ngành.

---

## 5. Sidebar (khi có layout 2 cột)

- **Vị trí:** Cột phải trên trang Danh sách sản phẩm (Product/Index) khi màn hình đủ rộng.
- **Hình thức:** 300×250, 300×600 (Half Page), hoặc nhiều khung nhỏ xếp dọc.
- **Ưu điểm:** Chuẩn quảng cáo display, dễ tích hợp Google AdSense hoặc đối tác.

---

## 6. Footer / Trên footer

- **Vị trí:** Một dải ngang ngay trên footer màu tối (trên "Nguồn Việt" trong footer).
- **Hình thức:** Banner ngang 728×90 hoặc full-width với nền nhạt, có thể là "Đối tác" hoặc "Khuyến mãi đối tác".
- **Ưu điểm:** Không cản trở nội dung chính, phù hợp đối tác dài hạn.

---

## 7. Pop-up / Modal (dùng cẩn thận)

- **Vị trí:** Modal xuất hiện lần đầu vào trang hoặc khi thoát (exit-intent).
- **Hình thức:** Ưu đãi đăng ký nhận tin, mã giảm giá, hoặc một banner đối tác.
- **Lưu ý:** Chỉ một lần/phiên, có nút đóng rõ ràng, tránh làm phiền trên mobile.

---

## 8. Trong Chatbox "Hỗ trợ nhanh"

- **Vị trí:** Một dòng nhỏ trong khung chat hoặc dưới ô nhập: "Được tài trợ bởi …" hoặc link đối tác.
- **Hình thức:** Text link hoặc logo nhỏ.
- **Ưu điểm:** Tận dụng vùng đã có, ít chiếm chỗ.

---

## Thứ tự triển khai gợi ý

1. **Banner giữa nội dung** (mục 3) – dễ thêm, ít ảnh hưởng layout.
2. **Banner trong Hero** (mục 2) hoặc **xen sản phẩm** (mục 4) – tăng doanh thu khi đã có traffic.
3. **Banner trên đầu** (mục 1) hoặc **trên footer** (mục 6) – khi đã có đối tác rõ ràng.
4. **Sidebar** (mục 5) – khi có layout 2 cột cho trang sản phẩm.
5. **Pop-up / Chat** (mục 7, 8) – sau khi đã tối ưu trải nghiệm.

---

## Kỹ thuật gợi ý

- Dùng **partial view** (ví dụ `_AdSlotBanner.cshtml`) nhận tham số: vị trí (top, mid, footer), kích thước, có thể bật/tắt theo cấu hình.
- Lưu **mã quảng cáo** (HTML/script hoặc ID slot) trong **appsettings** hoặc database để đổi nhanh không cần deploy.
- Trên mobile: ưu tiên banner ngang 320×50 hoặc native xen sản phẩm, tránh pop-up che toàn màn hình.
