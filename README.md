# Raven12345

Kho mã nguồn này tập hợp các package Unity do Raven12345 phát triển. Mỗi package
được đặt trong `Assets/Packages` và có thể được dùng độc lập trong một dự án Unity
khác thông qua Unity Package Manager (UPM) với Git URL.

## Raven Package Installer

`Raven Package Installer` là package Editor giúp quản lý danh sách package Git cá
nhân. Sau khi cài, mở **Tools > Raven > Package Installer** để xem trạng thái và
cài các package khác trong catalog, chẳng hạn SpriteForge, AudioForge,
RavenConsole, RavenToolkit, Service Container và Spine Asset Studio. Catalog cũng
có thể được chỉnh sửa để thêm Git URL package riêng của từng dự án.

### Cài đặt bằng Git URL

Trong Unity, mở **Window > Package Manager**, chọn nút **+**, sau đó chọn
**Add package from git URL...** và dán URL sau:

```text
https://github.com/Konosos/Raven12345.git?path=Assets/Packages/PackageInstaller
```

Sau khi Unity tải package, mở **Tools > Raven > Package Installer**. Installer sẽ
tạo hoặc sử dụng `Assets/RavenPackageCatalog.asset`, rồi cho phép người dùng cài
các package khác trực tiếp từ Git URL mà không cần tự nhập từng URL vào Package
Manager.

