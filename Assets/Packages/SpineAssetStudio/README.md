# Spine Asset Studio

Spine Asset Studio là Unity Editor Window dùng để xem trước và chỉnh sửa trực tiếp dữ liệu trong file Spine JSON mà không cần mở Spine Editor. Công cụ tập trung vào quản lý animation event, animation và skin ngay trong Unity.

## Yêu cầu

- Unity project đã cài spine-unity runtime tương thích với phiên bản file export(đã test trên spine 4.2).
- Spine Asset Studio có thể được import trước Spine mà không làm project lỗi compile. Khi chưa có Spine, menu `Spine > Asset Studio` chỉ hiển thị hướng dẫn cài đặt.
- Sau khi import spine-unity, bảo đảm scripting define `SPINE_UNITY` được bật (trong **Project Settings > Player > Scripting Define Symbols** nếu runtime của bạn chưa tự thêm define này). Unity sẽ reload và bật đầy đủ công cụ.
- `SkeletonDataAsset` phải tham chiếu tới file Spine JSON.
- Chỉnh sửa trực tiếp không hỗ trợ file nhị phân `.skel.bytes`.
- Nên quản lý project bằng Git hoặc hệ thống version control trước khi chỉnh dữ liệu gốc.

## Cài đặt từ Git

Trong Unity, mở **Window > Package Manager**, chọn nút **+** rồi chọn **Add package from git URL...**. Dán URL sau:

```
https://github.com/Konosos/Raven12345.git?path=Assets/Packages/SpineAssetStudio#main
```

Unity sẽ lấy đúng thư mục package `Assets/Packages/SpineAssetStudio` từ repository. Máy phát triển cần cài Git và Git phải có trong `PATH`.

Hoặc thêm thủ công vào `Packages/manifest.json` của project đích:

```json
{
  "dependencies": {
    "com.raven12345.spine-asset-studio": "https://github.com/Konosos/Raven12345.git?path=Assets/Packages/SpineAssetStudio#main"
  }
}
```

Sau khi Unity tải package xong, cài `spine-unity` 4.2 tương thích với dữ liệu export. Nếu chưa cài Spine, package vẫn import an toàn và cửa sổ chỉ hiện hướng dẫn cấu hình.

## Mở công cụ

Mở từ menu:

`Spine > Asset Studio`

Hoặc chọn một hay nhiều `SkeletonDataAsset`/Spine JSON trong cửa sổ Project, sau đó dùng:

`Assets > Open in Spine Asset Studio`

## Chọn dữ liệu Spine

Có ba cách chọn dữ liệu:

1. Kéo hoặc chọn `SkeletonDataAsset` vào trường **Skeleton Data**.
2. Chọn asset trong Project rồi nhấn **Use Project Selection**.
3. Dùng menu **Open in Spine Asset Studio** từ Project.

Khi chọn nhiều file, dropdown **Active file** sẽ xuất hiện. Chỉ file active được chỉnh sửa; các file còn lại không bị thay đổi.

## Preview animation

- Chọn animation từ dropdown **Animation**.
- Chọn skin từ dropdown **Preview Skin**.
- Dùng **Play/Pause** để xem animation.
- Kéo thanh thời gian để scrub tới một pose cụ thể.
- Preview chạy trong preview scene nội bộ và không tạo object trong Scene đang mở.

Skin được chọn chỉ ảnh hưởng preview. Thay đổi dropdown Preview Skin không ghi vào JSON.

## Quản lý event

### Thêm event

1. Đưa playhead tới thời điểm mong muốn.
2. Nhấn **Add at Playhead**.
3. Chọn event vừa tạo và chỉnh `Name`, `Int`, `Float`, `String`, `Volume`, `Balance`.

Event definition sẽ được tạo tự động nếu tên chưa tồn tại.

### Chọn và chỉnh event

- Bấm thời gian, tên event hoặc marker trên timeline để chọn.
- Bấm lại event đang chọn để bỏ chọn.
- Event đang chọn hiển thị marker màu cam và mở phần chỉnh sửa chi tiết.
- Kéo marker để thay đổi thời gian trực tiếp.
- Dùng ô tìm kiếm để lọc theo tên.
- Bật sắp xếp **Time** để xem event theo thứ tự thời gian.

### Duplicate và Delete

- **Duplicate** tạo bản sao event ở bước Snap tiếp theo.
- **Delete** xóa event key khỏi animation hiện tại.
- **Undo/Redo** hoàn tác hoặc thực hiện lại thay đổi trong phiên làm việc hiện tại.

## Snap

**Snap interval (seconds)** làm tròn thời gian event theo một bước cố định.

Ví dụ:

- `0.01`: thời gian được làm tròn theo từng 0.01 giây.
- `0.0333`: gần tương đương từng frame ở 30 FPS.
- `0.0167`: gần tương đương từng frame ở 60 FPS.
- `0`: tắt Snap và cho phép đặt thời gian tự do.

Snap được áp dụng khi kéo marker, thêm event và duplicate event.

## Animation Manager

Mở foldout **Animation Manager** để:

- **Rename**: đổi tên animation hiện tại.
- **Duplicate**: sao chép toàn bộ timeline sang animation mới.
- **Delete**: xóa animation sau khi xác nhận.

Khi rename, công cụ cập nhật:

- Mix settings `fromAnimation` và `toAnimation` trên `SkeletonDataAsset`.
- Các `AnimationReferenceAsset` đang tham chiếu cùng SkeletonDataAsset.

Tên mới không được rỗng hoặc trùng với animation đã tồn tại.

## Skin Manager

Mở foldout **Skin Manager** để:

- **Rename**: đổi tên skin hiện tại.
- **Duplicate**: sao chép skin và toàn bộ attachment của skin.
- **Delete**: xóa skin sau khi xác nhận.

Khi rename, công cụ cập nhật linked-skin references, attachment/deform timeline keys và `initialSkinName` trên các Spine component đang được load.

Tên mới không được rỗng hoặc trùng với skin đã tồn tại.

## Lưu dữ liệu

Các thay đổi được ghi trực tiếp vào file Spine JSON và Unity reimport asset ngay lập tức. Không có nút Save riêng.

Undo/Redo của cửa sổ chỉ tồn tại trong phiên và được reset khi:

- Đổi SkeletonDataAsset.
- Đổi active file.
- Đóng hoặc reload Editor Window/domain.

## Lưu ý quan trọng

- Việc re-export từ Spine Editor có thể ghi đè các thay đổi thực hiện trong Unity.
- Không chỉnh cùng một JSON đồng thời trong Spine Editor và Spine Asset Studio.
- Rename animation/skin có thể ảnh hưởng code hoặc asset bên ngoài đang lưu tên dưới dạng chuỗi mà công cụ không phát hiện được.
- Delete animation hoặc skin là thao tác phá hủy. Luôn kiểm tra hộp xác nhận và commit/backup trước khi thực hiện.
- Skin có linked mesh cần giữ tham chiếu hợp lệ. Kiểm tra preview sau khi rename, duplicate hoặc delete.
- Event timeline rỗng không được ghi vào JSON vì Spine runtime 4.2 không xử lý an toàn timeline 0 frame.
- File JSON và spine-unity runtime phải cùng phiên bản tương thích.
- Cảnh báo PMA/Linear Color Space thuộc cấu hình atlas/material của Spine, không phải lỗi dữ liệu event.

## Preview Diagnostics

Nếu preview không hiển thị, nhấn **Log Preview Diagnostics** và kiểm tra Console. Báo cáo bao gồm:

- Trạng thái khởi tạo Spine.
- Số mesh, vertex, submesh và material.
- Bounds của mesh.
- Vị trí và cấu hình camera preview.
- Preview scene đang được sử dụng.

Một preview hợp lệ phải có ít nhất một mesh, submesh và material; bounds không được bằng 0.
